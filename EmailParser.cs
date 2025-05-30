using System.Text;
using System.Text.RegularExpressions;

namespace SchoolHelpdesk;

public static partial class EmailParser
{
  public static string GetLatestMessage(string messageBody)
  {
    if (string.IsNullOrWhiteSpace(messageBody)) return string.Empty;

    var text = messageBody.Replace("\r\n", "\n");
    text = MultiLineQuoteHeader.Replace(text, m => m.Value.Replace('\n', ' '));
    text = MultiSpace.Replace(text, " ");
    text = LineSeparator.Replace(text, m => m.Value + "\n", 1);

    var rawLines = text.Split('\n');
    var types = new LineType[rawLines.Length];
    var signatureSeen = false;

    for (var i = 0; i < rawLines.Length; i++)
    {
      var line = rawLines[i];
      if (!signatureSeen && (Quoted.IsMatch(line) || QuoteHeader.IsMatch(line)))
      {
        types[i] = LineType.Quoted;
      }
      else if (!signatureSeen && SignatureDelimiter.IsMatch(line))
      {
        types[i] = LineType.Signature;
        signatureSeen = true;
      }
      else
      {
        types[i] = signatureSeen ? LineType.Signature : LineType.Content;
      }
    }

    var visible = new bool[rawLines.Length];
    var sawContent = false;
    for (var i = rawLines.Length - 1; i >= 0; i--)
    {
      var line = rawLines[i];
      switch (types[i])
      {
        case LineType.Content when !string.IsNullOrWhiteSpace(line):
          visible[i] = true;
          sawContent = true;
          break;
        case LineType.Quoted when sawContent:
          visible[i] = true;
          break;
        case LineType.Quoted:
        case LineType.Signature:
        case LineType.Content:
        default:
          visible[i] = false;
          break;
      }
    }

    var sb = new StringBuilder();
    for (var i = 0; i < rawLines.Length; i++)
    {
      if (visible[i]) sb.Append(rawLines[i]).Append('\n');
    }

    return sb.ToString().Trim();
  }

  private enum LineType { Quoted, Signature, Content }

  private static readonly Regex MultiLineQuoteHeader = MultiLineQuoteHeaderRegex();
  private static readonly Regex LineSeparator = LineSeparatorRegex();
  private static readonly Regex SignatureDelimiter = SignatureDelimeterRegex();
  private static readonly Regex QuoteHeader = QuoteHeaderRegex();
  private static readonly Regex Quoted = QuotedRegex();
  private static readonly Regex MultiSpace = MultiSpaceRegex();

  [GeneratedRegex("(?!On.*On\\s.+?wrote:)(On\\s(.+?)wrote:)", RegexOptions.Compiled | RegexOptions.Singleline)]
  private static partial Regex MultiLineQuoteHeaderRegex();

  [GeneratedRegex("([^\\n])((?=\\n_{7}_+)|(?=\\n-{7}-+))$", RegexOptions.Compiled)]
  private static partial Regex LineSeparatorRegex();

  [GeneratedRegex("(?m)(--\\s*$|__\\s*$|—\\s*$|\\w-$)|(^Sent from my (\\w+\\s*){1,3}$)|(From:.*)", RegexOptions.Compiled)]
  private static partial Regex SignatureDelimeterRegex();

  [GeneratedRegex("^On.*wrote:$", RegexOptions.Compiled)]
  private static partial Regex QuoteHeaderRegex();

  [GeneratedRegex("^>+", RegexOptions.Compiled)]
  private static partial Regex QuotedRegex();

  [GeneratedRegex(" {2,}")]
  private static partial Regex MultiSpaceRegex();
}