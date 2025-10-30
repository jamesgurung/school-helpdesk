using Ganss.Xss;
using ReverseMarkdown;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SchoolHelpdesk;

public static partial class TextFormatting
{
  private static readonly Converter _markdownConverter = new(new()
  {
    CleanupUnnecessarySpaces = true,
    RemoveComments = true,
    SmartHrefHandling = true,
    UnknownTags = Config.UnknownTagsOption.Bypass
  });

  public static string CleanText(string text)
  {
    return string.IsNullOrWhiteSpace(text) ? string.Empty : WebUtility.HtmlEncode(text).Replace("\r", "", StringComparison.Ordinal).Trim();
  }

  public static EmailBody ParseEmailBody(string textBody, string htmlBody, string strippedTextReply, bool extractReply)
  {
    var body = new EmailBody();
    var sanitizer = new HtmlSanitizer();
    sanitizer.AllowedTags.Remove("img");
    body.SanitizedHtml = sanitizer.Sanitize(htmlBody ?? textBody).Trim();

    if (extractReply && strippedTextReply is not null)
    {
      body.MessageText = strippedTextReply;
    }
    else
    {
      body.MessageText = textBody ?? _markdownConverter.Convert(body.SanitizedHtml);
      if (extractReply) body.MessageText = ExtractReply(body.MessageText);
      body.MessageText = RemoveEmptyLines(body.MessageText);
    }

    body.MessageText = body.MessageText.Trim().TrimStart('#');
    return body;
  }

  public static string ExtractReply(string messageBody)
  {
    if (string.IsNullOrEmpty(messageBody)) return string.Empty;
    var normalized = messageBody.Replace("\r", string.Empty, StringComparison.Ordinal);
    var span = normalized.AsSpan();
    var delimiterRegex = ReplyDelimiterRegex();
    var helpdeskEmail = $"{School.Instance.Name} <{School.Instance.HelpdeskEmail}>";
    var sb = new StringBuilder();
    var pos = 0;
    while (pos < span.Length)
    {
      var nextLf = span[pos..].IndexOf('\n');
      var line = nextLf < 0 ? span[pos..] : span.Slice(pos, nextLf);
      pos = nextLf < 0 ? span.Length : pos + nextLf + 1;
      var trimmed = line.Trim();
      if (trimmed.Length > 0)
      {
        var lineText = trimmed.ToString().Replace("*", string.Empty, StringComparison.Ordinal);
        if (delimiterRegex.IsMatch(lineText)) break;
        if (lineText.Contains(helpdeskEmail, StringComparison.OrdinalIgnoreCase)) break;
      }
      sb.Append(line);
      if (nextLf >= 0) sb.Append('\n');
    }
    return sb.ToString().Trim();
  }

  public static string RemoveEmptyLines(string text)
  {
    if (string.IsNullOrWhiteSpace(text)) return string.Empty;
    var lines = text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
    var builder = new StringBuilder();
    var blankCount = 0;
    foreach (var lineText in lines)
    {
      var trimmed = lineText.TrimEnd();
      if (string.IsNullOrWhiteSpace(trimmed))
      {
        if (blankCount++ == 0) builder.AppendLine();
      }
      else
      {
        blankCount = 0;
        builder.AppendLine(trimmed);
      }
    }
    return builder.ToString().Trim();
  }

  [GeneratedRegex(@"^(?:On\s.+\swrote:|>+|--\s*$|__\s*$|Sent from(?:\s+[^\s]+){1,4}\s*$|From:\s.*|[-=_—]{2,}\s*Original Message\s*[-=_—]{2,}|[-=_—]{2,}\s*Forwarded by.*[-=_—]{2,}|Your enquiry received a response from .+:)$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
  private static partial Regex ReplyDelimiterRegex();
}

public class EmailBody
{
  public string SanitizedHtml { get; set; }
  public string MessageText { get; set; }
}