using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostmarkDotNet.Webhooks;

namespace SchoolHelpdesk;

public static class Api
{
  public static void MapApiPaths(this WebApplication app)
  {
    app.MapPost("/inbound", [AllowAnonymous] async ([FromBody] PostmarkInboundWebhookMessage message, [FromQuery] string auth) =>
    {
      await EmailService.ProcessInboundAsync(message, auth);
      return Results.Ok();
    });

    var group = app.MapGroup("/api").ValidateAntiforgery().RequireAuthorization();

    group.MapGet("/users", () => Results.Content(School.Instance.UsersJson, "application/json"));

    group.MapGet("/refresh", [Authorize(Roles = AuthConstants.Administrator)] async () =>
    {
      await BlobService.LoadConfigAsync();
    });

    group.MapPost("/tickets", async (NewTicketEntity ticket, HttpContext context) =>
    {
      if (ticket is null || string.IsNullOrWhiteSpace(ticket.PartitionKey))
      {
        return Results.BadRequest("Ticket data is required.");
      }

      var user = School.Instance.StaffByEmail[context.User.Identity.Name].Name;
      var ticketEntity = ticket as TicketEntity;
      var message = ticket?.InitialMessage?.Trim();
      if (string.IsNullOrWhiteSpace(message))
      {
        return Results.BadRequest("Initial message is required.");
      }

      await TableService.InsertTicketAsync(ticketEntity);
      await BlobService.InsertMessageAsync(ticket.RowKey, new Message
      {
        AuthorName = user,
        IsEmployee = true,
        Timestamp = DateTime.UtcNow,
        Content = message
      });

      return Results.Created((string)null, ticketEntity.RowKey);
    });

    group.MapGet("/tickets/{id}", async (string id, HttpContext context) =>
    {
      if (string.IsNullOrWhiteSpace(id))
      {
        return Results.BadRequest("Ticket ID is required.");
      }
      if (!context.User.IsInRole(AuthConstants.Administrator) && !await TableService.TicketExistsAsync(context.User.Identity.Name, id))
      {
        return Results.Forbid();
      }
      var messages = await BlobService.GetMessagesAsync(id);
      return Results.Ok(messages);
    });
  }
}