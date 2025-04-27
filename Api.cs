namespace SchoolHelpdesk;

public static class Api
{
  public static void MapApiPaths(this WebApplication app)
  {
    var group = app.MapGroup("/api").ValidateAntiforgery();


  }
}