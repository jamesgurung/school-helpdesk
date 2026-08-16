using PostmarkDotNet.Webhooks;

namespace SchoolHelpdesk;

public static class EmailVerificationService
{
  public static bool Validate(PostmarkInboundWebhookMessage message)
  {
    ArgumentNullException.ThrowIfNull(message);

    var spfHeader = GetHeader(message, "received-spf")?.TrimStart();
    if (string.IsNullOrEmpty(spfHeader) || !spfHeader.StartsWith("pass", StringComparison.OrdinalIgnoreCase) || (spfHeader.Length > 4 && !char.IsWhiteSpace(spfHeader[4]) && spfHeader[4] != '('))
      return false;

    if (GetHeader(message, "x-spam-status")?.StartsWith("yes", StringComparison.OrdinalIgnoreCase) ?? false)
      return false;

    var gatewayAuthentication = GetGatewayAuthentication(GetHeader(message, "authentication-results"), message.From);
    var spamAssassinAuthentication = GetSpamAssassinAuthentication(GetHeader(message, "x-spam-tests"));
    return gatewayAuthentication == AuthenticationResult.Pass || spamAssassinAuthentication == AuthenticationResult.Pass;
  }

  private static string GetHeader(PostmarkInboundWebhookMessage message, string name)
  {
    return message.Headers?.FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
  }

  private static AuthenticationResult GetGatewayAuthentication(string header, string from)
  {
    if (string.IsNullOrWhiteSpace(header)) return AuthenticationResult.Missing;

    var at = from?.LastIndexOf('@', StringComparison.Ordinal) ?? -1;
    var fromDomain = at >= 0 && at < from.Length - 1 ? from[(at + 1)..] : null;
    if (!TryGetAuthenticationClauses(header, out var clauses)) return AuthenticationResult.Fail;

    var isMicrosoft = string.Equals(School.Instance.ForwardingProvider, "Microsoft", StringComparison.OrdinalIgnoreCase);
    var foundResult = false;
    foreach (var clause in clauses)
    {
      if (!TryGetMethodResult(clause, out var method, out var result, out var propertiesStart)) continue;
      var passed = result.Equals("pass", StringComparison.OrdinalIgnoreCase);

      if (isMicrosoft)
      {
        if (!method.Equals("compauth", StringComparison.OrdinalIgnoreCase) &&
          !method.Equals("dmarc", StringComparison.OrdinalIgnoreCase))
        {
          continue;
        }

        foundResult = true;
        if (passed) return AuthenticationResult.Pass;
        continue;
      }

      if (method.Equals("dmarc", StringComparison.OrdinalIgnoreCase))
      {
        foundResult = true;
        if (passed) return AuthenticationResult.Pass;
        continue;
      }

      if (method.Equals("spf", StringComparison.OrdinalIgnoreCase))
      {
        foundResult = true;
        if (passed && fromDomain is not null &&
          IsMatchingDomain(GetPropertyDomain(clause, propertiesStart, "smtp.mailfrom"), fromDomain))
        {
          return AuthenticationResult.Pass;
        }
        continue;
      }

      if (!method.Equals("dkim", StringComparison.OrdinalIgnoreCase)) continue;
      foundResult = true;
      if (!passed || fromDomain is null) continue;
      if (IsMatchingDomain(GetPropertyDomain(clause, propertiesStart, "header.d"), fromDomain) ||
        IsMatchingDomain(GetPropertyDomain(clause, propertiesStart, "header.i"), fromDomain))
      {
        return AuthenticationResult.Pass;
      }
    }

    return foundResult ? AuthenticationResult.Fail : AuthenticationResult.Missing;
  }

  private static bool IsMatchingDomain(string domain, string fromDomain)
  {
    return domain?.Equals(fromDomain, StringComparison.OrdinalIgnoreCase) ?? false;
  }

  private static bool TryGetAuthenticationClauses(string header, out List<string> clauses)
  {
    clauses = [];
    var clauseStart = 0;
    var commentDepth = 0;
    var quoted = false;
    for (var i = 0; i < header.Length; i++)
    {
      var character = header[i];
      if ((quoted || commentDepth > 0) && character == '\\')
      {
        i++;
        continue;
      }
      if (quoted)
      {
        if (character == '"') quoted = false;
        continue;
      }
      if (commentDepth > 0)
      {
        if (character == '(') commentDepth++;
        else if (character == ')') commentDepth--;
        continue;
      }
      if (character == '"')
      {
        quoted = true;
      }
      else if (character == '(')
      {
        commentDepth = 1;
      }
      else if (character == ')')
      {
        return false;
      }
      else if (character == ';')
      {
        if (clauseStart >= 0) clauses.Add(header[clauseStart..i]);
        clauseStart = i + 1;
      }
    }

    if (quoted || commentDepth > 0) return false;
    if (clauseStart >= 0) clauses.Add(header[clauseStart..]);
    return true;
  }

  private static bool TryGetMethodResult(string clause, out string method, out string result, out int propertiesStart)
  {
    method = null;
    result = null;
    propertiesStart = 0;
    var index = 0;
    if (!SkipCommentsAndWhitespace(clause, ref index)) return false;
    var tokenStart = index;
    while (index < clause.Length && IsTokenCharacter(clause[index])) index++;
    if (tokenStart == index) return false;
    method = clause[tokenStart..index];
    if (!SkipCommentsAndWhitespace(clause, ref index) || index >= clause.Length || clause[index++] != '=') return false;
    if (!SkipCommentsAndWhitespace(clause, ref index)) return false;
    tokenStart = index;
    while (index < clause.Length && IsTokenCharacter(clause[index])) index++;
    if (tokenStart == index) return false;
    result = clause[tokenStart..index];
    propertiesStart = index;
    return true;
  }

  private static string GetPropertyDomain(string clause, int index, string propertyName)
  {
    while (index < clause.Length)
    {
      if (!SkipCommentsAndWhitespace(clause, ref index)) return null;
      var tokenStart = index;
      while (index < clause.Length && (IsTokenCharacter(clause[index]) || clause[index] == '.')) index++;
      if (tokenStart == index)
      {
        index++;
        continue;
      }
      var property = clause[tokenStart..index];
      if (!SkipCommentsAndWhitespace(clause, ref index) || index >= clause.Length || clause[index++] != '=') continue;
      if (!SkipCommentsAndWhitespace(clause, ref index)) return null;

      var valueStart = index;
      var quoted = false;
      for (; index < clause.Length; index++)
      {
        var character = clause[index];
        if (quoted && character == '\\')
        {
          index++;
          continue;
        }
        if (character == '"') quoted = !quoted;
        else if (!quoted && (char.IsWhiteSpace(character) || character == '(')) break;
      }
      if (quoted || !property.Equals(propertyName, StringComparison.OrdinalIgnoreCase)) continue;

      var value = clause[valueStart..index].Trim('<', '>');
      var atIndex = -1;
      quoted = false;
      for (var i = 0; i < value.Length; i++)
      {
        if (quoted && value[i] == '\\')
        {
          i++;
          continue;
        }
        if (value[i] == '"') quoted = !quoted;
        else if (!quoted && value[i] == '@') atIndex = i;
      }

      var domain = value[(atIndex + 1)..];
      return domain.Length > 0 && !domain.Any(o => char.IsWhiteSpace(o) || o is '"' or '(' or ')' or ';' or '@' or '<' or '>') ? domain : null;
    }

    return null;
  }

  private static bool SkipCommentsAndWhitespace(string value, ref int index)
  {
    while (index < value.Length)
    {
      if (char.IsWhiteSpace(value[index]))
      {
        index++;
        continue;
      }
      if (value[index] != '(') return true;

      var commentDepth = 1;
      index++;
      while (index < value.Length && commentDepth > 0)
      {
        if (value[index] == '\\')
        {
          index += 2;
        }
        else if (value[index] == '(')
        {
          commentDepth++;
          index++;
        }
        else if (value[index] == ')')
        {
          commentDepth--;
          index++;
        }
        else
        {
          index++;
        }
      }
      if (commentDepth > 0) return false;
    }
    return true;
  }

  private static bool IsTokenCharacter(char character)
  {
    return char.IsAsciiLetterOrDigit(character) || character is '-' or '_';
  }

  private static AuthenticationResult GetSpamAssassinAuthentication(string header)
  {
    if (string.IsNullOrWhiteSpace(header)) return AuthenticationResult.Missing;
    var tests = header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return tests.Length == 0
      ? AuthenticationResult.Missing
      : tests.Contains("DKIM_VALID_AU", StringComparer.Ordinal) ? AuthenticationResult.Pass : AuthenticationResult.Fail;
  }

  private enum AuthenticationResult
  {
    Missing,
    Fail,
    Pass
  }
}
