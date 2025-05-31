using PostmarkDotNet;
using PostmarkDotNet.Model;
using PostmarkDotNet.Webhooks;
using System.Text.RegularExpressions;

namespace SchoolHelpdesk;

public static partial class EmailService
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

    var parents = School.Instance.ParentsByEmail[message.From].ToList();

    if (parents.Count == 0)
    {
      // Unknown sender
      var spamHeader = message.Headers.FirstOrDefault(o => o.Name == "x-spam-status")?.Value;
      if (spamHeader?.StartsWith("yes", StringComparison.OrdinalIgnoreCase) ?? false) return;
      var isStaff = School.Instance.StaffByEmail.ContainsKey(message.From);
      await SendRejectionEmailAsync(message.From, message.Subject, messageId, isStaff ? RejectionReason.StaffSender : RejectionReason.UnknownSender);
      return;
    }

    var parentEmail = parents[0].Email;
    var body = TextFormatting.ParseEmailBody(message.StrippedTextReply, message.TextBody, message.HtmlBody);

    var ticketNumberMatch = TicketNumberRegex().Match(message.Subject);
    if (ticketNumberMatch.Success && int.TryParse(ticketNumberMatch.Groups[1].Value, out var ticketNumber))
    {
      var ticket = await TableService.GetTicketAsync(ticketNumber);
      if (ticket is not null && string.Equals(ticket.ParentEmail, parentEmail, StringComparison.OrdinalIgnoreCase))
      {
        // Existing ticket for this parent
        var parentName = ticket.ParentName ?? string.Join(" / ", parents.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase));
        var attachments = await UploadAttachmentsAsync(message.Attachments);
        var messages = new List<Message>()
        {
          new()
          {
            AuthorName = parentName,
            IsEmployee = false,
            IsPrivate = false,
            Timestamp = DateTime.UtcNow,
            Content = body.MessageText,
            OriginalEmail = body.SanitizedHtml,
            Attachments = attachments
          }
        };
        if (ticket.IsClosed)
        {
          messages.Add(new()
          {
            AuthorName = parentName,
            IsEmployee = false,
            IsPrivate = false,
            Timestamp = DateTime.UtcNow,
            Content = "#reopen"
          });
        }
        await BlobService.AppendMessagesAsync(ticketNumber, messages);
        await TableService.SetLastParentMessageDateAsync(ticket);
      }
      else
      {
        // Ticket not associated with this parent
        await SendRejectionEmailAsync(parentEmail, message.Subject, messageId, RejectionReason.UnknownTicket);
      }
    }
    else
    {
      // New ticket
      var parentNames = string.Join(" / ", parents.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase));
      var parentName = parents.Count == 1 ? parents[0].Name : null;
      var students = parents.SelectMany(o => o.Children).DistinctBy(o => (o.FirstName, o.LastName)).ToList();
      var student = students.Count == 1 ? students[0] : null;

      var ticket = new TicketEntity
      {
        PartitionKey = "unassigned",
        AssigneeName = null,
        Title = message.Subject,
        ParentEmail = parentEmail,
        ParentName = parentName,
        ParentRelationship = parentName is null ? null : student?.ParentRelationship,
        Created = DateTime.UtcNow,
        WaitingSince = DateTime.UtcNow,
        StudentFirstName = student?.FirstName,
        StudentLastName = student?.LastName,
        TutorGroup = student?.TutorGroup,
        IsClosed = false
      };

      var id = await TableService.CreateTicketAsync(ticket);
      var attachments = await UploadAttachmentsAsync(message.Attachments);

      var messages = new List<Message>
      {
        new()
        {
          AuthorName = parentNames,
          IsEmployee = false,
          IsPrivate = false,
          Timestamp = DateTime.UtcNow,
          Content = body.MessageText,
          OriginalEmail = body.SanitizedHtml,
          Attachments = attachments
        }
      };

      await BlobService.AppendMessagesAsync(id, messages);
      await SendTicketCreatedEmailAsync(parentEmail, parentNames, id, ticket.Title);
    }
  }

  public static async Task SendAsync(string to, string subject, string body, string tag, string threadId = null)
  {
    var client = new PostmarkClient(_serverToken);
    var message = new PostmarkMessage
    {
      To = _debugEmail ?? to,
      Subject = subject,
      HtmlBody = ComposeHtmlEmail(body),
      TextBody = ComposeTextEmail(body),
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

  public static string ComposeHtmlEmail(string body)
  {
    var html = School.Instance.HtmlEmailTemplate.Replace("{{BODY}}", body.Replace("\n", "<br>"), StringComparison.OrdinalIgnoreCase);
    return PreMailer.Net.PreMailer.MoveCssInline(html, true, stripIdAndClassAttributes: true, removeComments: true).Html;
  }

  public static string ComposeTextEmail(string body)
  {
    return School.Instance.TextEmailTemplate.Replace("{{BODY}}", body, StringComparison.OrdinalIgnoreCase);
  }

  private static string GetHeader(this PostmarkInboundWebhookMessage message, string name)
  {
    return message.Headers.FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
  }

  private static async Task SendTicketCreatedEmailAsync(string parentEmail, string parentName, int id, string title)
  {
    var subject = $"[Ticket #{id}] {title}";
    var to = $"\"{parentName.Replace("\"", string.Empty)}\" <{parentEmail}>";
    var body = $"Dear {parentName}\n\n" +
      "Thank you for contacting us. We've received your enquiry and created a ticket.\n\n" +
      "Our team will review your message and get back to you shortly.\n\n" +
      "Best wishes\n\n" + School.Instance.Name;
    await SendAsync(to, subject, body, EmailTag.Parent);
  }

  private static async Task SendRejectionEmailAsync(string to, string subject, string messageId, RejectionReason reason)
  {
    var replySubject = subject.StartsWith("Re: ", StringComparison.OrdinalIgnoreCase) ? subject : ("Re: " + subject);
    var body = "Sorry, your email could not be delivered. " + reason switch
    {
      RejectionReason.UnknownSender =>
        "This mailbox is only for use by parents and carers of current students, and we do not have your email address in our records.\n\n" +
        "If you have an enquiry, please contact reception.",
      RejectionReason.StaffSender =>
        "Email replies from staff are not supported.\n\n" +
        $"Please sign in to our <a href=\"{School.Instance.AppWebsite}\">helpdesk portal</a> to respond to a ticket.",
      RejectionReason.UnknownTicket =>
        "The ticket number you provided does not exist or is not associated with your email address.\n\n" +
        "Please send a new email to open a fresh ticket.",
      _ =>
        throw new NotImplementedException()
    };
    await SendAsync(to, replySubject, body, messageId, EmailTag.Unknown);
  }

  private static async Task<List<Attachment>> UploadAttachmentsAsync(List<PostmarkDotNet.Attachment> inboundAttachments)
  {
    if (inboundAttachments is null || inboundAttachments.Count == 0) return null;
    var attachments = new List<Attachment>();
    foreach (var attachment in inboundAttachments)
    {
      if (!Attachment.ValidateAttachment(attachment.Name, attachment.ContentLength, out _)) continue;
      using var stream = new MemoryStream(Convert.FromBase64String(attachment.Content));
      var blobName = await BlobService.UploadAttachmentAsync(stream, attachment.Name);
      attachments.Add(new() { FileName = attachment.Name, Url = blobName });
    }
    return attachments.Count == 0 ? null : attachments;
  }

  [GeneratedRegex(@"\[Ticket\s*#(\d+)\]")]
  private static partial Regex TicketNumberRegex();
}

public class EmailTag
{
  public const string Unknown = "Unknown";
  public const string Parent = "Parent";
  public const string Staff = "Staff";
}

public enum RejectionReason
{
  UnknownSender,
  StaffSender,
  UnknownTicket
}