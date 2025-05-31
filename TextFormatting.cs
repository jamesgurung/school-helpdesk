using Ganss.Xss;
using System.Net;

namespace SchoolHelpdesk;

public static class TextFormatting
{
  public static string CleanText(string text)
  {
    return string.IsNullOrWhiteSpace(text) ? string.Empty : WebUtility.HtmlEncode(text).Replace("\r", "");
  }

  public static EmailBody ParseEmailBody(string strippedTextReply, string textBody, string htmlBody)
  {
    var sanitizer = new HtmlSanitizer();
    sanitizer.AllowedTags.Remove("img");
    var sanitizedHtml = sanitizer.Sanitize(htmlBody);

    if (string.IsNullOrEmpty(strippedTextReply))
    {
      strippedTextReply = textBody;
    }

    return new EmailBody
    {
      SanitizedHtml = sanitizedHtml,
      MessageText = strippedTextReply
    };
  }
}

public class EmailBody
{
  public string SanitizedHtml { get; set; }
  public string MessageText { get; set; }
}