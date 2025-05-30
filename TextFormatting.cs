using Ganss.Xss;

namespace SchoolHelpdesk;

public static class TextFormatting
{
  public static EmailBody ParseEmailBody(string originalHtml)
  {
    var sanitizer = new HtmlSanitizer();
    sanitizer.AllowedTags.Remove("img");
    var sanitizedHtml = sanitizer.Sanitize(originalHtml);

    var messageText = ""; // ExtractLatestMessageInPlainText(sanitizedHtml);

    return new EmailBody
    {
      SanitizedHtml = sanitizedHtml,
      MessageText = messageText
    };
  }
}

public class EmailBody
{
  public string SanitizedHtml { get; set; }
  public string MessageText { get; set; }
}