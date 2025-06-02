using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using System.Text.Json;

namespace SchoolHelpdesk;

public static class QueueService
{
  private static QueueClient emailsClient;

  public static void Configure(string connectionString)
  {
    var queueServiceClient = new QueueServiceClient(connectionString);
    emailsClient = queueServiceClient.GetQueueClient("emails");
  }

  public static async Task ProcessPendingEmailsAsync()
  {
    QueueMessage[] messages;
    do
    {
      var response = await emailsClient.ReceiveMessagesAsync(32, TimeSpan.FromMinutes(5));
      messages = response.Value;
      foreach (var msg in messages)
      {
        var pendingEmail = JsonSerializer.Deserialize<PendingEmail>(msg.MessageText, JsonSerializerOptions.Web);
        await SendTicketUpdateEmailAsync(pendingEmail);
        await emailsClient.DeleteMessageAsync(msg.MessageId, msg.PopReceipt);
      }
    }
    while (messages.Length > 0);
  }

  private static async Task SendTicketUpdateEmailAsync(PendingEmail email)
  {
    if (!Enum.TryParse<TicketUpdateAction>(email.TicketUpdateAction, true, out var action))
      throw new InvalidOperationException($"Invalid ticket update action: {email.TicketUpdateAction}");

    var ticket = new TicketEntity
    {
      Title = email.Title,
      ParentName = email.ParentName,
      ParentEmail = email.ParentEmail,
      StudentFirstName = email.StudentFirstName,
      StudentLastName = email.StudentLastName,
      TutorGroup = email.TutorGroup
    };

    var staff = new Staff
    {
      FirstName = email.StaffFirstName,
      Email = email.StaffEmail
    };

    await EmailService.SendTicketUpdateEmailAsync(email.Id, ticket, staff, action);
  }
}

public class PendingEmail
{
  public int Id { get; set; }
  public string Title { get; set; }
  public string ParentName { get; set; }
  public string ParentEmail { get; set; }
  public string StudentFirstName { get; set; }
  public string StudentLastName { get; set; }
  public string TutorGroup { get; set; }
  public string StaffFirstName { get; set; }
  public string StaffEmail { get; set; }
  public string TicketUpdateAction { get; set; }
}