using PostmarkDotNet;
using PostmarkDotNet.Model;
using PostmarkDotNet.Webhooks;

namespace SchoolHelpdesk;

public static class EmailService
{
  private static string _serverToken;
  private static string _authKey;
  private static string _debugEmail;

  public static void Configure(string postmarkServerToken, string postmarkInboundAuthKey, string debugEmail)
  {
    if (string.IsNullOrEmpty(postmarkServerToken) || string.IsNullOrEmpty(postmarkInboundAuthKey))
    {
      throw new InvalidOperationException("Postmark server token and inbound auth key must be provided.");
    }
#if DEBUG
    if (string.IsNullOrEmpty(debugEmail))
    {
      throw new InvalidOperationException("Debug email must be provided.");
    }
#endif
    _serverToken = postmarkServerToken;
    _authKey = postmarkInboundAuthKey;
    _debugEmail = debugEmail;
  }

  public static async Task ProcessInboundAsync(PostmarkInboundWebhookMessage message, string authKey)
  {
    if (authKey != _authKey || message is null) return;
    var messageId = message.GetHeader("message-id");
    var replySubject = GetReplySubject(message.Subject);

    var parents = School.Instance.ParentsByEmail[message.From];
    if (!parents.Any())
    {
      var spamHeader = message.Headers.FirstOrDefault(o => o.Name == "x-spam-status")?.Value;
      if (spamHeader?.StartsWith("yes", StringComparison.OrdinalIgnoreCase) ?? false) return;

      await SendAsync(message.From, replySubject,
        "Sorry, your email could not be delivered. This mailbox is only for use by parents and carers of current students, and we do not have your email address in our records.\n\n" +
        "If you have an enquiry, please contact reception.",
        null, EmailTag.Unknown, messageId);

      return;
    }

    return;
  }

  public static async Task SendAsync(string to, string subject, string body, string signature, string tag, string threadId = null)
  {
    var client = new PostmarkClient(_serverToken);
    var message = new PostmarkMessage
    {
      To = _debugEmail ?? to,
      Subject = subject,
      HtmlBody = ComposeHtmlEmail(body, signature),
      TextBody = ComposeTextEmail(body, signature),
      From = $"\"{School.Instance.Name}\" <{School.Instance.HelpdeskEmail}>",
      Tag = tag,
      MessageStream = "outbound",
      TrackOpens = false,
      TrackLinks = LinkTrackingOptions.None
    };
    if (!string.IsNullOrEmpty(threadId))
    {
      message.Headers.Add(new MailHeader("In-Reply-To", threadId));
      message.Headers.Add(new MailHeader("References", threadId));
    }
    await client.SendMessageAsync(message);
  }

  public static string ComposeHtmlEmail(string body, string senderName)
  {
    var html = School.Instance.HtmlEmailTemplate.Replace("{{BODY}}", body, StringComparison.OrdinalIgnoreCase);

    return PreMailer.Net.PreMailer.MoveCssInline(html, true, stripIdAndClassAttributes: true, removeComments: true).Html;
  }

  public static string ComposeTextEmail(string body, string senderName)
  {
    return School.Instance.TextEmailTemplate.Replace("{{BODY}}", body, StringComparison.OrdinalIgnoreCase);
  }

  public static string GetReplySubject(string messageSubject)
  {
    return messageSubject.StartsWith("Re: ", StringComparison.OrdinalIgnoreCase) ? messageSubject : "Re: " + messageSubject;
  }

  private static string GetHeader(this PostmarkInboundWebhookMessage message, string name)
  {
    return message.Headers.FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
  }
}

public class EmailTag
{
  public const string Unknown = "Unknown";
  public const string Parent = "Parent";
  public const string Staff = "Staff";
}