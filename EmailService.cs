using PostmarkDotNet;
using PostmarkDotNet.Model;
using PostmarkDotNet.Webhooks;

namespace SchoolHelpdesk;

public static class EmailService
{
  private static string _serverToken;
  private static string _authKey;

  public static async Task ProcessInboundAsync(PostmarkInboundWebhookMessage message, string authKey)
  {
    if (authKey != _authKey || message is null) return;
    var messageId = message.GetHeader("message-id");
    var replySubject = GetReplySubject(message.Subject);

    if (!School.Instance.StudentsByParentEmail.Contains(message.From))
    {
      var spamHeader = message.Headers.FirstOrDefault(o => o.Name == "x-spam-status")?.Value;
      if (spamHeader?.StartsWith("yes", StringComparison.OrdinalIgnoreCase) ?? false) return;

      await SendAsync(message.From, replySubject,
        "Email address not recognised.",
        "This mailbox is only for use by parents and carers of current students, and we do not have your email address in our records.\n\nIf you have an enquiry, please contact reception.",
        EmailTag.Unknown, messageId);

      return;
    }

    return;
  }

  public static async Task SendAsync(string to, string subject, string heading, string body, string tag, string threadId = null)
  {
    var client = new PostmarkClient(_serverToken);
    var message = new PostmarkMessage
    {
      To = to,
      Subject = subject,
      HtmlBody = ComposeHtmlEmail(heading, body),
      TextBody = ComposeTextEmail(heading, body),
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

  public static string ComposeHtmlEmail(string heading, string body)
  {
    var mainBody = string.IsNullOrEmpty(heading) ? body : $"<h2 class=\"email-header\">{heading}</h2>\n{body}";
    var html = School.Instance.EmailTemplate.Replace("{{BODY}}", Markdown.ToHtml(mainBody));
    return PreMailer.Net.PreMailer.MoveCssInline(html).Html;
  }

  public static string ComposeTextEmail(string heading, string body)
  {
    return string.IsNullOrEmpty(heading) ? body : $"{heading.ToUpperInvariant()}\n\n{body}";
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