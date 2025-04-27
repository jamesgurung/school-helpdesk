using PostmarkDotNet;
using PostmarkDotNet.Webhooks;

namespace SchoolHelpdesk;

public static class EmailService
{
  private static string _serverToken;
  private static string _authKey;

  public static async Task ProcessInboundAsync(PostmarkInboundWebhookMessage message, string authKey)
  {
    if (authKey != _authKey) return;

    if (!School.Instance.StudentsByParentEmail.Contains(message.From))
    {
      var spamHeader = message.Headers.FirstOrDefault(h => h.Name == "X-Spam-Status")?.Value;
      if (spamHeader?.StartsWith("yes", StringComparison.OrdinalIgnoreCase) ?? false) return;

      await Send(School.Instance.AdminUsers[0], GetReplySubject(message.Subject), "Not found: " + message.From);
      return;
    }
    await Send(School.Instance.AdminUsers[0], GetReplySubject(message.Subject), "Found: " + message.From);
  }

  public static async Task Send(string to, string subject, string markdownBody)
  {
    var client = new PostmarkClient(_serverToken);
    var message = new PostmarkMessage
    {
      To = to,
      Subject = subject,
      HtmlBody = ComposeHtmlEmail(markdownBody),
      TextBody = ComposeTextEmail(markdownBody),
      From = School.Instance.AppWebsite,
      Tag = "Helpdesk"
    };
    await client.SendMessageAsync(message);
  }

  public static string ComposeHtmlEmail(string markdownBody)
  {
    var css = "";
    var html = $"<html><body><style>{css}</style>{Markdown.ToHtml(markdownBody)}</body></html>";
    return PreMailer.Net.PreMailer.MoveCssInline(html).Html;
  }

  public static string ComposeTextEmail(string markdownBody)
  {
    return markdownBody;
  }

  public static string GetReplySubject(string messageSubject)
  {
    return messageSubject.StartsWith("Re: ", StringComparison.OrdinalIgnoreCase) ? messageSubject : "Re: " + messageSubject;
  }

  public static void Configure(string postmarkServerToken, string postmarkInboundAuthKey)
  {
    _serverToken = postmarkServerToken;
    _authKey = postmarkInboundAuthKey;
  }
}
