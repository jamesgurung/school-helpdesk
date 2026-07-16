using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SchoolHelpdesk.Pages;

public class TicketsModel : PageModel
{
  public bool IsManager { get; private set; }
  public bool IsDispatcher { get; private set; }
  public DateTime RecentTicketsAfter { get; private set; }
  public List<TicketEntity> Tickets { get; private set; }

  public async Task OnGetAsync()
  {
    IsManager = User.IsInRole(AuthConstants.Manager);
    IsDispatcher = User.IsInRole(AuthConstants.Dispatcher);
    RecentTicketsAfter = DateTime.Today.AddDays(IsManager ? -14 : -90);
    Tickets = IsManager
      ? await TableService.GetAllTicketsAsync(RecentTicketsAfter)
      : await TableService.GetTicketsByAssigneeAsync(User.Identity.Name, RecentTicketsAfter);
  }
}
