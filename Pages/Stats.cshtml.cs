using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SchoolHelpdesk.Pages;

public class StatsModel : PageModel
{
  public List<WeeklyStats> Weeks { get; private set; } = [];
  public List<ParentStats> Parents { get; private set; } = [];
  public List<AssigneeStats> Assignees { get; private set; } = [];

  public async Task OnGetAsync()
  {
    var holidays = School.Instance.Holidays ?? [];
    var now = DateTime.UtcNow;
    var currentWeekStartUtc = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
    var currentWeekStart = DateOnly.FromDateTime(currentWeekStartUtc);
    var threeMonthsAgo = currentWeekStartUtc.AddDays(-13 * 7);
    var sixMonthsAgo = currentWeekStartUtc.AddDays(-26 * 7);
    var tickets = (await TableService.GetAllTicketsAsync(sixMonthsAgo.AddSeconds(-1))).Where(o => o.TimeToFirstResponse is not null).ToList();

    Weeks = tickets
      .Where(o => o.IsClosed && o.LastUpdated >= sixMonthsAgo)
      .GroupBy(o => DateOnly.FromDateTime(o.LastUpdated.Date.AddDays(-(((int)o.LastUpdated.DayOfWeek + 6) % 7))))
      .OrderBy(g => g.Key)
      .Select(g => new WeeklyStats(
        g.Key,
        g.Key == currentWeekStart,
        Enumerable.Range(0, 5).Select(g.Key.AddDays).All(day => holidays.Any(h => h.Start <= day && h.End >= day)),
        g.Count(),
        g.Median(t => t.TimeToFirstResponse.Value)))
      .ToList();

    Parents = tickets
      .Where(o => o.LastUpdated >= threeMonthsAgo)
      .GroupBy(o => o.ParentEmail)
      .Select(g => new ParentStats(
        string.Join(", ", g.GroupBy(o => o.ParentName).OrderByDescending(gg => gg.Count()).ThenBy(gg => gg.Key)
          .Select(gg => gg.Key).Where(o => !string.IsNullOrWhiteSpace(o))),
        string.Join(", ", g.GroupBy(o => $"{o.StudentFirstName} {o.StudentLastName}").OrderByDescending(gg => gg.Count()).ThenBy(gg => gg.Key)
          .Select(gg => gg.Key).Where(o => !string.IsNullOrWhiteSpace(o))),
        g.Count()))
      .OrderByDescending(o => o.Count)
      .Take(10)
      .ToList();

    Assignees = tickets
      .Where(o => o.LastUpdated >= threeMonthsAgo && o.AssigneeName is not null)
      .GroupBy(o => o.AssigneeName)
      .Select(g => new AssigneeStats(g.Key, g.Count(), (int?)g.Average(t => t.AverageAssigneeResponseTime)))
      .Where(o => o.Count >= 3)
      .OrderByDescending(o => o.Count)
      .ToList();
  }
}

public record WeeklyStats(DateOnly WeekStart, bool IsCurrentWeek, bool IsHolidayWeek, int TicketsClosed, int? AverageTimeToFirstResponse);
public record ParentStats(string ParentName, string StudentName, int Count);
public record AssigneeStats(string AssigneeName, int Count, int? AverageResponseTime);
