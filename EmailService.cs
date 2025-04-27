using PostmarkDotNet;
using PostmarkDotNet.Webhooks;

namespace SchoolHelpdesk;

public static class EmailService
{
  private static string _serverToken;
  private static string _authKey;

  public static async Task ProcessInboundAsync(PostmarkInboundWebhookMessage message, string authKey)
  {
    if (authKey != _authKey || message is null) return;

    if (!School.Instance.StudentsByParentEmail.Contains(message.From))
    {
      var spamHeader = message.Headers.FirstOrDefault(h => h.Name == "X-Spam-Status")?.Value;
      if (spamHeader?.StartsWith("yes", StringComparison.OrdinalIgnoreCase) ?? false) return;

      await Send(School.Instance.AdminUsers[0], GetReplySubject(message.Subject), "Not found: " + message.From, EmailTag.Unknown);
      return;
    }
    await Send(School.Instance.AdminUsers[0], GetReplySubject(message.Subject), "Found: " + message.From, EmailTag.Parent);
  }

  public static async Task Send(string to, string subject, string markdownBody, string tag)
  {
    var client = new PostmarkClient(_serverToken);
    var message = new PostmarkMessage
    {
      To = to,
      Subject = subject,
      HtmlBody = ComposeHtmlEmail(markdownBody),
      TextBody = ComposeTextEmail(markdownBody),
      From = School.Instance.HelpdeskEmail,
      Tag = tag,
      MessageStream = "outbound",
      TrackOpens = false,
      TrackLinks = LinkTrackingOptions.None
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
    if (string.IsNullOrEmpty(postmarkServerToken) || string.IsNullOrEmpty(postmarkInboundAuthKey))
    {
      throw new InvalidOperationException("Postmark server token and inbound auth key must be provided.");
    }
    _serverToken = postmarkServerToken;
    _authKey = postmarkInboundAuthKey;
  }
}

public class EmailTag
{
  public const string Unknown = "Unknown";
  public const string Parent = "Parent";
  public const string Staff = "Staff";
}
