﻿using Microsoft.AspNetCore.Authorization;
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

      var id = await TableService.CreateTicketAsync(ticketEntity);
      await BlobService.CreateConversationAsync(id, new Message { AuthorName = user, IsEmployee = true, Timestamp = DateTime.UtcNow, Content = message },
        new Message { AuthorName = user, IsEmployee = true, IsPrivate = true, Timestamp = DateTime.UtcNow, Content = $"#assign {ticketEntity.AssigneeName}" });

      return Results.Created((string)null, ticketEntity.RowKey);
    });

    group.MapGet("/tickets/{id:int}", async (int id, HttpContext context) =>
    {
      if (!context.User.IsInRole(AuthConstants.Manager) && !await TableService.TicketExistsAsync(context.User.Identity.Name, id))
      {
        return Results.Forbid();
      }
      var messages = await BlobService.GetMessagesAsync(id);
      return Results.Ok(messages);
    });

    group.MapPut("/tickets/{id:int}/assignee", async (int id, [FromBody] ChangeAssigneePayload payload, HttpContext context) =>
    {
      if (!context.User.IsInRole(AuthConstants.Manager))
        return Results.Forbid();

      if (string.IsNullOrWhiteSpace(payload?.AssigneeEmail))
        return Results.BadRequest("Assignee email is required.");

      var newAssigneeEmail = payload.NewAssigneeEmail?.Trim().ToLowerInvariant();
      if (string.IsNullOrEmpty(newAssigneeEmail))
        return Results.BadRequest("New assignee email is required.");

      if (string.Equals(payload.AssigneeEmail, newAssigneeEmail, StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest("New assignee email cannot be the same as the current assignee email.");

      if (!School.Instance.StaffByEmail.TryGetValue(newAssigneeEmail, out var staff))
        return Results.BadRequest("New assignee email does not match any staff member.");

      await TableService.ReassignTicketAsync(payload.AssigneeEmail, id, newAssigneeEmail, staff.Name);

      var currentUser = School.Instance.StaffByEmail[context.User.Identity.Name].Name;
      await BlobService.AppendMessagesAsync(id, new Message
      {
        AuthorName = currentUser,
        IsEmployee = true,
        Timestamp = DateTime.UtcNow,
        IsPrivate = true,
        Content = $"#assign {staff.Name}"
      });

      return Results.NoContent();
    });

    group.MapPut("/tickets/{id:int}/student", async (int id, [FromBody] ChangeStudentPayload payload, HttpContext context) =>
    {
      if (!context.User.IsInRole(AuthConstants.Manager))
        return Results.Forbid();

      if (string.IsNullOrWhiteSpace(payload?.AssigneeEmail))
        return Results.BadRequest("Assignee email is required.");

      if (string.IsNullOrWhiteSpace(payload?.StudentFirst) || string.IsNullOrWhiteSpace(payload?.StudentLast))
        return Results.BadRequest("Student information is required.");

      var entity = await TableService.GetTicketAsync(payload.AssigneeEmail, id);
      var parent = School.Instance.ParentsByEmail[entity.ParentEmail].FirstOrDefault(o => o.Name == entity.ParentName);
      if (parent is null)
        return Results.BadRequest("Parent associated with this ticket does not exist.");

      var child = parent.Children.FirstOrDefault(c => string.Equals(c.FirstName, payload.StudentFirst, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(c.LastName, payload.StudentLast, StringComparison.OrdinalIgnoreCase));
      if (child is null)
        return Results.BadRequest("Student does not match any child of the parent associated with this ticket.");

      await TableService.ChangeTicketStudentAsync(entity, child);
      return Results.NoContent();
    });

    group.MapPut("/tickets/{id:int}/parent", async (int id, [FromBody] ChangeParentPayload payload, HttpContext context) =>
    {
      if (!context.User.IsInRole(AuthConstants.Manager))
        return Results.Forbid();

      if (string.IsNullOrWhiteSpace(payload?.AssigneeEmail))
        return Results.BadRequest("Assignee email is required.");

      if (string.IsNullOrWhiteSpace(payload?.NewParentName))
        return Results.BadRequest("New parent information is required.");

      var entity = await TableService.GetTicketAsync(payload.AssigneeEmail, id);
      var newParent = School.Instance.ParentsByEmail[entity.ParentEmail].FirstOrDefault(p => p.Name == payload.NewParentName);

      if (newParent is null)
        return Results.BadRequest("New parent does not exist.");

      Student currentStudent = null;
      if (entity.StudentFirstName is not null && entity.StudentLastName is not null)
      {
        currentStudent = newParent.Children.FirstOrDefault(c =>
          string.Equals(c.FirstName, entity.StudentFirstName, StringComparison.OrdinalIgnoreCase) &&
          string.Equals(c.LastName, entity.StudentLastName, StringComparison.OrdinalIgnoreCase));

        if (currentStudent is null)
          return Results.BadRequest("Current student is not associated with the new parent.");
      }

      await TableService.ChangeTicketParentAsync(entity, newParent, currentStudent?.ParentRelationship);
      return Results.NoContent();
    });


    group.MapPut("/tickets/{id:int}/status", async (int id, [FromBody] ChangeStatusPayload payload, HttpContext context) =>
    {
      if (!context.User.IsInRole(AuthConstants.Manager) && !await TableService.TicketExistsAsync(context.User.Identity.Name, id))
        return Results.Forbid();

      await TableService.CloseTicketAsync(payload.AssigneeEmail, id, payload.IsClosed);

      var currentUser = School.Instance.StaffByEmail[context.User.Identity.Name].Name;
      await BlobService.AppendMessagesAsync(id, new Message
      {
        AuthorName = currentUser,
        IsEmployee = true,
        Timestamp = DateTime.UtcNow,
        IsPrivate = true,
        Content = payload.IsClosed ? "#close" : "#reopen"
      });

      return Results.NoContent();
    });

    group.MapPut("/tickets/{id:int}/title", async (int id, [FromBody] ChangeTitlePayload payload, HttpContext context) =>
    {
      if (string.IsNullOrWhiteSpace(payload?.NewTitle))
        return Results.BadRequest("Ticket title is required.");

      if (!context.User.IsInRole(AuthConstants.Manager))
        return Results.Forbid();

      await TableService.RenameTicketAsync(payload.AssigneeEmail, id, payload.NewTitle);
      return Results.NoContent();
    });

    group.MapPost("/tickets/{id:int}/message", async (int id, HttpContext context) =>
    {
      if (!context.User.IsInRole(AuthConstants.Manager) && !await TableService.TicketExistsAsync(context.User.Identity.Name, id))
        return Results.Forbid();

      if (!context.Request.HasFormContentType)
        return Results.BadRequest("Request must be a form.");

      var form = await context.Request.ReadFormAsync();

      if (!bool.TryParse(form["isPrivate"].ToString(), out var isPrivate))
        return Results.BadRequest("Must specify whether the message is private.");

      var assigneeEmail = form["assigneeEmail"].ToString();
      if (string.IsNullOrWhiteSpace(assigneeEmail))
        return Results.BadRequest("Assignee email is required.");

      var content = TextFormatting.CleanText(form["content"].ToString());
      if (string.IsNullOrWhiteSpace(content))
        return Results.BadRequest("Message content is required.");

      if (content.StartsWith('#'))
        return Results.BadRequest("Message content cannot start with a hash (#).");

      foreach (var file in form.Files)
      {
        if (!Attachment.ValidateAttachment(file.Name, file.Length, out var errorMessage)) return Results.BadRequest(errorMessage);
      }

      var attachments = new List<Attachment>();
      foreach (var file in form.Files)
      {
        using var stream = file.OpenReadStream();
        var blobName = await BlobService.UploadAttachmentAsync(stream, file.FileName);
        attachments.Add(new Attachment
        {
          FileName = file.FileName,
          Url = blobName
        });
      }

      var user = School.Instance.StaffByEmail[context.User.Identity.Name].Name;
      var message = new Message
      {
        AuthorName = user,
        IsEmployee = true,
        Timestamp = DateTime.UtcNow,
        Content = content,
        IsPrivate = isPrivate,
        Attachments = attachments.Count > 0 ? attachments : null
      };
      await BlobService.AppendMessagesAsync(id, message);

      if (!isPrivate)
      {
        await TableService.ClearLastParentMessageDateAsync(assigneeEmail, id);
      }

      if (message.Attachments is not null)
      {
        foreach (var attachment in message.Attachments)
        {
          attachment.Url = BlobService.GetAttachmentSasUrl(attachment.Url);
        }
      }
      return Results.Ok(message);
    });

    app.MapPut("/api/people", [AllowAnonymous] async (HttpContext context, [FromHeader(Name = "X-Api-Key")] string auth) =>
    {
      if (string.IsNullOrEmpty(School.Instance.SyncApiKey)) return Results.Conflict("An sync API key is not configured.");
      if (auth != School.Instance.SyncApiKey) return Results.Unauthorized();

      var formFiles = context.Request.Form.Files;
      if (formFiles.Count != 2) return Results.BadRequest();
      if (formFiles.Any(o => o.Length == 0)) return Results.BadRequest();
      var staffFile = formFiles.SingleOrDefault(o => o.Name == "staff");
      var studentsFile = formFiles.SingleOrDefault(o => o.Name == "students");
      if (staffFile is null || studentsFile is null) return Results.BadRequest();

      using (var staffStream = staffFile.OpenReadStream())
      {
        using var staffReader = new StreamReader(staffStream);
        var staffCsv = await staffReader.ReadToEndAsync();
        await BlobService.UpdateDataFileAsync("staff.csv", staffCsv);
      }

      using (var studentsStream = studentsFile.OpenReadStream())
      {
        using var studentsReader = new StreamReader(studentsStream);
        var studentsCsv = await studentsReader.ReadToEndAsync();
        await BlobService.UpdateDataFileAsync("students.csv", studentsCsv);
      }

      await BlobService.LoadConfigAsync();
      return Results.NoContent();
    });
  }
}