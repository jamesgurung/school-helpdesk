using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostmarkDotNet.Webhooks;

namespace SchoolHelpdesk;

public static class Api
{
  public static void MapApiPaths(this WebApplication app)
  {
    var group = app.MapGroup("/api").ValidateAntiforgery();

    app.MapPost("/inbound", [AllowAnonymous] async ([FromBody] PostmarkInboundWebhookMessage message, [FromQuery] string auth) =>
    {
      await EmailService.ProcessInboundAsync(message, auth);
      return Results.Ok();
    });
  }
}