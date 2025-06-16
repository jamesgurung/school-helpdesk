using System.Runtime.InteropServices;

namespace SchoolHelpdesk;

public class ReminderService(ILogger<ReminderService> logger) : BackgroundService
{
  private readonly TimeZoneInfo ukTimeZone = TimeZoneInfo.FindSystemTimeZoneById(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "GMT Standard Time" : "Europe/London");

  protected override async Task ExecuteAsync(CancellationToken cancellationToken)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      var utcNow = DateTime.UtcNow;
      var ukNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, ukTimeZone);
      var ukNext = new DateTime(ukNow.Year, ukNow.Month, ukNow.Day, 7, 30, 0);
      if (ukNow >= ukNext) ukNext = ukNext.AddDays(1);
      if (ukNext.DayOfWeek == DayOfWeek.Saturday) ukNext = ukNext.AddDays(2);
      else if (ukNext.DayOfWeek == DayOfWeek.Sunday) ukNext = ukNext.AddDays(1);
      var utcNext = TimeZoneInfo.ConvertTimeToUtc(ukNext, ukTimeZone);
      var delay = utcNext - utcNow;
      if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
      await Task.Delay(delay, cancellationToken);
      try
      {
        await SendRemindersAsync();
        logger.LogInformation("Sent reminder emails");
      }
      catch
      {
        logger.LogError("Error sending reminder emails");
      }
      try
      {
        await QueueService.ProcessPendingEmailsAsync();
        logger.LogInformation("Processed pending emails from queue");
      }
      catch
      {
        logger.LogError("Error processing pending emails from queue");
      }
    }
  }

  private static async Task SendRemindersAsync()
  {
    var allTickets = await TableService.GetAllTicketsAsync();
    var now = DateTime.UtcNow;
    var openTickets = allTickets.Where(t => !t.IsClosed && (t.WaitingSince is null || now - t.WaitingSince > TimeSpan.FromHours(16))).ToList();

    foreach (var ticket in openTickets)
    {
      var id = int.Parse(ticket.RowKey);
      if (!School.Instance.StaffByEmail.TryGetValue(ticket.PartitionKey, out var staff)) continue;
      await EmailService.SendTicketUpdateEmailAsync(id, ticket, staff, TicketUpdateAction.Reminder);
    }
  }
}