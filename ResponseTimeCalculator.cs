namespace SchoolHelpdesk;

public static class ResponseTimeCalculator
{
  private static readonly TimeSpan WorkingDayStart = new(8, 30, 0);
  private static readonly TimeSpan WorkingDayEnd = new(16, 30, 0);

  public static (int? First, int? Average) Calculate(IReadOnlyList<Message> messages, string assigneeName)
  {
    ArgumentNullException.ThrowIfNull(messages);
    if (messages.Count < 2) return (null, null);

    if (messages[0].IsEmployee && string.Equals(messages[0].AuthorName, assigneeName, StringComparison.OrdinalIgnoreCase)) return (null, null);

    var ticketOpened = messages[0].Timestamp;
    var firstResponse = messages.Skip(1).FirstOrDefault(m => m.IsEmployee && (!m.IsPrivate || string.Equals(m.Content, "#close", StringComparison.OrdinalIgnoreCase)));
    var firstResponseTime = firstResponse is null ? (int?)null : WorkingSecondsBetween(ticketOpened, firstResponse.Timestamp);

    if (string.IsNullOrWhiteSpace(assigneeName)) return (firstResponseTime, null);
    const string assignCommand = "#assign ";
    var responseTimes = new List<int>();
    var isAssigned = false;
    DateTime? waitingSince = null;

    foreach (var message in messages)
    {
      if (message.Content?.StartsWith(assignCommand, StringComparison.OrdinalIgnoreCase) ?? false)
      {
        var assignedName = message.Content[assignCommand.Length..];
        isAssigned = string.Equals(assignedName, assigneeName, StringComparison.OrdinalIgnoreCase);
        waitingSince = isAssigned ? message.Timestamp : null;
        continue;
      }
      if (!isAssigned) continue;
      if (!message.IsEmployee)
      {
        waitingSince ??= message.Timestamp;
        continue;
      }
      if (!string.Equals(message.AuthorName, assigneeName, StringComparison.OrdinalIgnoreCase) || waitingSince is null) continue;
      responseTimes.Add(WorkingSecondsBetween(waitingSince.Value, message.Timestamp));
      waitingSince = null;
    }

    return (firstResponseTime, responseTimes.Count == 0 ? null : (int?)responseTimes.Average());
  }

  private static int WorkingSecondsBetween(DateTime start, DateTime end)
  {
    if (end <= start) return 0;

    var holidays = School.Instance.Holidays;
    var seconds = 0;
    for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
    {
      if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

      var date = DateOnly.FromDateTime(day);
      if (holidays?.Any(h => h.Start <= date && h.End >= date) ?? false) continue;

      var workingStart = day + WorkingDayStart;
      var workingEnd = day + WorkingDayEnd;
      var intervalStart = start > workingStart ? start : workingStart;
      var intervalEnd = end < workingEnd ? end : workingEnd;
      if (intervalEnd > intervalStart) seconds += (int)(intervalEnd - intervalStart).TotalSeconds;
    }
    return seconds;
  }
}
