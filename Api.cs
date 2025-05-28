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

    group.MapPost("/tickets", [Authorize(Roles = AuthConstants.Manager)] async (NewTicketEntity ticket, HttpContext context) =>
    {
      if (ticket is null || string.IsNullOrWhiteSpace(ticket.PartitionKey))
      {
        return Results.BadRequest("Ticket data is required.");
      }

      var user = School.Instance.StaffByEmail[context.User.Identity.Name].Name;
      var ticketEntity = ticket as TicketEntity;
      var message = ticket?.Message?.Trim();
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
      if (!context.User.IsInRole(AuthConstants.Manager) && !await TableService.TicketExistsAsync(context.User.Identity.Name, id))
      {
        return Results.Forbid();
      }
      var messages = await BlobService.GetMessagesAsync(id);
      return Results.Ok(messages);
    });

    group.MapPut("/tickets/{id}/assignee", async (string id, [FromBody] ChangeAssigneePayload payload, HttpContext context) =>
    {
      if (!context.User.IsInRole(AuthConstants.Manager))
        return Results.Forbid();

      if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(payload?.AssigneeEmail))
        return Results.BadRequest("Ticket ID and assignee email are required.");

      if (string.IsNullOrWhiteSpace(payload?.NewAssigneeEmail))
        return Results.BadRequest("New assignee email is required.");

      if (string.Equals(payload.AssigneeEmail, payload.NewAssigneeEmail, StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest("New assignee email cannot be the same as the current assignee email.");

      if (!School.Instance.StaffByEmail.TryGetValue(payload.NewAssigneeEmail, out var staff))
        return Results.BadRequest("New assignee email does not match any staff member.");

      await TableService.ReassignTicketAsync(payload.AssigneeEmail, id, payload.NewAssigneeEmail, staff.Name);
      return Results.NoContent();
    });

    group.MapPut("/tickets/{id}/student", async (string id, [FromBody] ChangeStudentPayload payload, HttpContext context) =>
    {
      if (!context.User.IsInRole(AuthConstants.Manager))
        return Results.Forbid();

      if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(payload?.AssigneeEmail))
        return Results.BadRequest("Ticket ID and assignee email are required.");

      if (string.IsNullOrWhiteSpace(payload?.StudentFirst) || string.IsNullOrWhiteSpace(payload?.StudentLast))
        return Results.BadRequest("Student information is required.");

      var entity = await TableService.GetTicketAsync(payload.AssigneeEmail, id);
      var parents = School.Instance.ParentsByEmail[entity.ParentEmail];
      if (!parents.Any())
        return Results.BadRequest("Parent associated with this ticket does not exist.");

      var child = parents.SelectMany(o => o.Children).FirstOrDefault(c => string.Equals(c.FirstName, payload.StudentFirst, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(c.LastName, payload.StudentLast, StringComparison.OrdinalIgnoreCase));
      if (child is null)
        return Results.BadRequest("Student does not match any child of the parent associated with this ticket.");

      await TableService.ChangeTicketStudentAsync(entity, child);
      return Results.NoContent();
    });

    group.MapPut("/tickets/{id}/parent", async (string id, [FromBody] ChangeParentPayload payload, HttpContext context) =>
    {
      if (!context.User.IsInRole(AuthConstants.Manager))
        return Results.Forbid();

      if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(payload?.AssigneeEmail))
        return Results.BadRequest("Ticket ID and assignee email are required.");

      if (string.IsNullOrWhiteSpace(payload?.NewParentName))
        return Results.BadRequest("New parent information is required.");

      var entity = await TableService.GetTicketAsync(payload.AssigneeEmail, id);
      var newParent = School.Instance.ParentsByEmail[entity.ParentEmail].FirstOrDefault(p => p.Name == payload.NewParentName);

      if (newParent is null)
        return Results.BadRequest("New parent does not exist.");

      var currentStudent = newParent.Children.FirstOrDefault(c =>
        string.Equals(c.FirstName, entity.StudentFirstName, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(c.LastName, entity.StudentLastName, StringComparison.OrdinalIgnoreCase));

      if (currentStudent is null)
        return Results.BadRequest("Current student is not associated with the new parent.");

      await TableService.ChangeTicketParentAsync(entity, newParent, currentStudent.ParentRelationship);
      return Results.NoContent();
    });


    group.MapPut("/tickets/{id}/status", async (string id, [FromBody] ChangeStatusPayload payload, HttpContext context) =>
    {
      if (string.IsNullOrWhiteSpace(id))
        return Results.BadRequest("Ticket ID is required.");

      if (!context.User.IsInRole(AuthConstants.Manager) && !await TableService.TicketExistsAsync(context.User.Identity.Name, id))
        return Results.Forbid();

      await TableService.CloseTicketAsync(payload.AssigneeEmail, id, payload.IsClosed);
      return Results.NoContent();
    });

    group.MapPut("/tickets/{id}/title", async (string id, [FromBody] ChangeTitlePayload payload, HttpContext context) =>
    {
      if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(payload?.NewTitle))
        return Results.BadRequest("Ticket ID and title are required.");

      if (!context.User.IsInRole(AuthConstants.Manager))
        return Results.Forbid();

      await TableService.RenameTicketAsync(payload.AssigneeEmail, id, payload.NewTitle);
      return Results.NoContent();
    });

    group.MapPost("/tickets/{id}/message", async (string id, [FromBody] NewMessagePayload payload, HttpContext context) =>
    {
      if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(payload?.Content))
        return Results.BadRequest("Ticket ID and message content are required.");

      if (!context.User.IsInRole(AuthConstants.Manager) && !await TableService.TicketExistsAsync(context.User.Identity.Name, id))
        return Results.Forbid();

      var user = School.Instance.StaffByEmail[context.User.Identity.Name].Name;
      var message = new Message
      {
        AuthorName = user,
        IsEmployee = true,
        Timestamp = DateTime.UtcNow,
        Content = payload.Content,
        IsPrivate = payload.IsPrivate
      };
      await BlobService.AppendMessageAsync(id, message);
      await TableService.UpdateLastParentMessageDateAsync(payload.AssigneeEmail, id, null);
      return Results.NoContent();
    });
  }
}