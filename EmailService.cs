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

    var spamHeader = message.Headers.FirstOrDefault(o => string.Equals(o.Name, "x-spam-status", StringComparison.OrdinalIgnoreCase))?.Value;
    if (spamHeader?.StartsWith("yes", StringComparison.OrdinalIgnoreCase) ?? false) return;

    var spfHeader = message.Headers.FirstOrDefault(o => string.Equals(o.Name, "received-spf", StringComparison.OrdinalIgnoreCase))?.Value;
    if (spfHeader?.StartsWith("fail", StringComparison.OrdinalIgnoreCase) ?? false) return;

    var textBody = string.IsNullOrWhiteSpace(message.TextBody) ? null : message.TextBody.Trim();
    var htmlBody = string.IsNullOrWhiteSpace(message.HtmlBody) ? null : message.HtmlBody.Trim();
    var strippedTextReply = string.IsNullOrWhiteSpace(message.StrippedTextReply) ? null : message.StrippedTextReply.Trim();
    if (textBody is null && htmlBody is null) return;

    var messageId = message.GetHeader("message-id");
    var parents = School.Instance.ParentsByEmail[message.From].ToList();

    if (parents.Count == 0)
    {
      // Unknown sender
      if (!(spfHeader?.StartsWith("pass", StringComparison.OrdinalIgnoreCase) ?? false)) return;
      var isStaff = School.Instance.StaffByEmail.ContainsKey(message.From);
      await SendRejectionEmailAsync(message.From, message.Subject, messageId, isStaff ? RejectionReason.StaffSender : RejectionReason.UnknownSender);
      return;
    }

    var parentEmail = parents[0].Email;

    var ticketNumberMatch = TicketNumberRegex().Match(message.Subject);
    if (ticketNumberMatch.Success && int.TryParse(ticketNumberMatch.Groups[1].Value, out var ticketNumber))
    {
      var ticket = await TableService.GetTicketAsync(ticketNumber);
      if (ticket is not null && string.Equals(ticket.ParentEmail, parentEmail, StringComparison.OrdinalIgnoreCase))
      {
        // Existing ticket for this parent
        var parentName = ticket.ParentName ?? "Parent/Carer";
        var body = TextFormatting.ParseEmailBody(textBody, htmlBody, strippedTextReply, true);
        var attachments = await UploadAttachmentsAsync(message.Attachments);
        var messages = new List<Message>(2);
        var now = DateTime.UtcNow;
        if (ticket.IsClosed)
        {
          messages.Add(new()
          {
            AuthorName = parentName,
            IsEmployee = false,
            IsPrivate = true,
            Timestamp = now,
            Content = "#reopen"
          });
        }
        messages.Add(new()
        {
          AuthorName = parentName,
          IsEmployee = false,
          IsPrivate = false,
          Timestamp = now,
          Content = body.MessageText,
          OriginalEmail = body.SanitizedHtml,
          Attachments = attachments
        });
        await BlobService.AppendMessagesAsync(ticketNumber, messages);
        await TableService.UpdateForNewParentMessageAsync(ticket, now);
        if (ticket.PartitionKey != "unassigned" && School.Instance.StaffByEmail.TryGetValue(ticket.PartitionKey, out var assignee))
        {
          await SendTicketUpdateEmailAsync(ticketNumber, ticket, assignee, TicketUpdateAction.Updated);
        }
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
      var parentName = parents.Count == 1 ? parents[0].Name : null;
      var students = parents.SelectMany(o => o.Children).DistinctBy(o => (o.FirstName, o.LastName)).ToList();
      var student = students.Count == 1 ? students[0] : null;
      var body = TextFormatting.ParseEmailBody(textBody, htmlBody, strippedTextReply, false);
      var subject = string.IsNullOrWhiteSpace(message.Subject) ? "Enquiry" : message.Subject.Trim();
      if (subject.StartsWith("RE: ", StringComparison.OrdinalIgnoreCase) || subject.StartsWith("FW: ", StringComparison.OrdinalIgnoreCase))
      {
        subject = subject[3..].Trim();
      }
      if (subject.Length > 40) subject = subject[..37] + "...";
      var now = DateTime.UtcNow;

      var ticket = new TicketEntity
      {
        PartitionKey = "unassigned",
        AssigneeName = null,
        Title = subject,
        ParentName = parentName,
        ParentEmail = parentEmail,
        ParentPhone = parentName is null ? null : parents[0].Phone,
        ParentRelationship = parentName is null ? null : student?.ParentRelationship,
        Created = now,
        LastUpdated = now,
        WaitingSince = now,
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
          AuthorName = parentName ?? "Parent/Carer",
          IsEmployee = false,
          IsPrivate = false,
          Timestamp = DateTime.UtcNow,
          Content = body.MessageText,
          OriginalEmail = body.SanitizedHtml,
          Attachments = attachments
        }
      };

      await BlobService.CreateConversationAsync(id, messages);
      await SendTicketCreatedEmailAsync(parentEmail, id, ticket.Title, null, null);

      if (School.Instance.NotifyFirstManager && School.Instance.StaffByEmail.TryGetValue(School.Instance.Managers?[0], out var manager))
      {
        await SendTicketUpdateEmailAsync(id, ticket, manager, TicketUpdateAction.NotifyNew);
      }
    }
  }

  public static async Task SendTicketCreatedEmailAsync(string parentEmail, int id, string title, string createdBy, string parentSalutation)
  {
    var subject = $"[Ticket #{id}] {title}";
    var body = createdBy is null || parentSalutation is null
      ? ("Thank you for contacting us. We've received your enquiry and created a ticket.\n\n" +
        "Our team will review your message and get back to you shortly.\n\n" +
        "Best wishes\n\n" + School.Instance.Name)
      : ($"Dear {parentSalutation}\n\n" +
        $"Thank you for contacting us. {createdBy} has created a helpdesk ticket for you.\n\n" +
        "Our team will review your enquiry and get back to you shortly.\n\n" +
        "Best wishes\n\n" + School.Instance.Name);
    await SendAsync(parentEmail, subject, body, EmailTag.Parent);
  }

  public static async Task SendTicketUpdateEmailAsync(int id, TicketEntity ticket, Staff staff, TicketUpdateAction action)
  {
    ArgumentNullException.ThrowIfNull(ticket);
    ArgumentNullException.ThrowIfNull(staff);

    var (heading, intro, outro) = action switch
    {
      TicketUpdateAction.Assigned => (
        "New Ticket",
        "You have received a new enquiry through our helpdesk:",
        $"When you have a moment, please sign in to the <a href=\"https://{School.Instance.AppWebsite}\">helpdesk portal</a> to review and respond."
      ),
      TicketUpdateAction.Unassigned => (
        "Ticket Reassigned",
        "The following enquiry has been transferred to another member of staff:",
        "No further action is required on your part."
      ),
      TicketUpdateAction.Reminder => (
        "Ticket Reminder",
        "This is a gentle reminder about the open helpdesk enquiry below:",
        $"When you have a moment, please sign in to the <a href=\"https://{School.Instance.AppWebsite}\">helpdesk portal</a> to review and respond."
      ),
      TicketUpdateAction.NotifyNew => (
        "Ticket Received",
        "A new helpdesk enquiry has been received by email:",
        $"Please assign a member of staff on the <a href=\"https://{School.Instance.AppWebsite}\">helpdesk portal</a>."
      ),
      TicketUpdateAction.Updated => (
        "Response Received",
        "You have received a new reply to this enquiry:",
        $"When you have a moment, please sign in to the <a href=\"https://{School.Instance.AppWebsite}\">helpdesk portal</a> to review and respond."
      ),
      _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };

    var studentName = ticket.StudentFirstName is null ? null : $"{ticket.StudentFirstName} {ticket.StudentLastName} {ticket.TutorGroup}";
    var subject = $"{heading}{(studentName is null ? string.Empty : $" - {studentName}")}";
    var body = $"Hi {staff.FirstName}\n\n{intro}\n\n" +
      $"<b>Ticket #{id}</b>\n" +
      $"<b>{ticket.Title}</b>\n" +
      $"<b>From {ticket.ParentName ?? ticket.ParentEmail}{(studentName is null ? string.Empty : $" (re. {studentName})")}</b>\n\n" +
      $"{outro}\n\nBest wishes\n\n{School.Instance.Name}";
    await SendAsync(staff.Email, subject, body, EmailTag.Staff);
  }

  public static async Task SendParentReplyAsync(int id, TicketEntity ticket, Message message, List<PostmarkMessageAttachment> attachments)
  {
    ArgumentNullException.ThrowIfNull(ticket);
    ArgumentNullException.ThrowIfNull(message);
    ArgumentNullException.ThrowIfNull(attachments);
    var subject = $"[Ticket #{id}] {ticket.Title}";
    var body = $"<b>Your enquiry received a response from {message.AuthorName}:</b>\n\n{TextFormatting.CleanText(message.Content)}";
    await SendAsync(ticket.ParentEmail, subject, body, EmailTag.Parent, null, attachments);
  }

  private static async Task SendAsync(string to, string subject, string body, string tag, string threadId = null, List<PostmarkMessageAttachment> attachments = null)
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
      Attachments = attachments,
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

  private static string ComposeHtmlEmail(string body)
  {
    body = body.Replace("\n", "<br>", StringComparison.OrdinalIgnoreCase).Trim();
    var html = School.Instance.HtmlEmailTemplate.Replace("{{BODY}}", body, StringComparison.OrdinalIgnoreCase);
    return PreMailer.Net.PreMailer.MoveCssInline(html, true, stripIdAndClassAttributes: true, removeComments: true).Html;
  }

  private static string ComposeTextEmail(string body)
  {
    body = body.Replace("<b>", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("</b>", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
    return School.Instance.TextEmailTemplate.Replace("{{BODY}}", body, StringComparison.OrdinalIgnoreCase);
  }

  private static string GetHeader(this PostmarkInboundWebhookMessage message, string name)
  {
    return message.Headers.FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
  }

  private static async Task SendRejectionEmailAsync(string to, string subject, string messageId, RejectionReason reason)
  {
    var replySubject = subject.StartsWith("Re: ", StringComparison.OrdinalIgnoreCase) ? subject : ("Re: " + subject);
    var body = "Sorry, your email could not be delivered. " + reason switch
    {
      RejectionReason.UnknownSender =>
        "This mailbox is only for use by parents and carers of current students, and we do not have your email address as a primary contact in our records.\n\n" +
        "If you have an enquiry, please contact reception.",
      RejectionReason.StaffSender =>
        "Email replies from staff are not supported.\n\n" +
        $"Please sign in to the <a href=\"https://{School.Instance.AppWebsite}\">helpdesk portal</a> to respond to a ticket.",
      RejectionReason.UnknownTicket =>
        "The ticket number you provided does not exist or is not associated with your email address.\n\n" +
        "Please send a new email to open a fresh ticket.",
      _ =>
        throw new NotImplementedException()
    };
    await SendAsync(to, replySubject, body, EmailTag.Unknown, messageId);
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

public static class EmailTag
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

public enum TicketUpdateAction
{
  Assigned,
  Unassigned,
  Reminder,
  NotifyNew,
  Updated
}