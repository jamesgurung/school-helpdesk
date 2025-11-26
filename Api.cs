using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostmarkDotNet;
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

    group.MapGet("/users", [Authorize(Roles = AuthConstants.Manager)] () => Results.Content(School.Instance.UsersJson, "application/json"));

    group.MapGet("/refresh", [Authorize(Roles = AuthConstants.Administrator)] async () =>
    {
      await BlobService.LoadConfigAsync();
    });

    group.MapPost("/tickets", [Authorize(Roles = AuthConstants.Manager)] async (NewTicketEntity ticket, HttpContext context) =>
    {
      if (ticket is null)
        return Results.BadRequest("Ticket data is required.");

      if (string.IsNullOrWhiteSpace(ticket.PartitionKey) || string.IsNullOrWhiteSpace(ticket.AssigneeName))
        return Results.BadRequest("Assignee email and name are required.");

      if (!School.Instance.StaffByEmail.TryGetValue(ticket.PartitionKey, out var assignee))
        return Results.BadRequest("Assignee email does not match any staff member.");

      if (string.IsNullOrWhiteSpace(ticket.ParentEmail) || string.IsNullOrWhiteSpace(ticket.ParentName) || string.IsNullOrWhiteSpace(ticket.ParentRelationship))
        return Results.BadRequest("Parent email, name, and relationship are required.");

      if (string.IsNullOrWhiteSpace(ticket.StudentFirstName) || string.IsNullOrWhiteSpace(ticket.StudentLastName) || string.IsNullOrWhiteSpace(ticket.TutorGroup))
        return Results.BadRequest("Student first name, last name, and tutor group are required.");

      if (string.IsNullOrEmpty(ticket.Title) || ticket.Title.Length > 40)
        return Results.BadRequest("Ticket title is required and must be 40 characters or less.");

      if (ticket.IsClosed)
        return Results.BadRequest("Cannot create a closed ticket.");

      var message = ticket?.Message?.Trim();

      if (string.IsNullOrWhiteSpace(message))
        return Results.BadRequest("Initial message is required.");

      if (message.StartsWith('#'))
        return Results.BadRequest("Message content cannot start with a hash (#).");

      var parent = School.Instance.ParentsByEmail[ticket.ParentEmail].FirstOrDefault(p => p.Name == ticket.ParentName);

      if (parent is null)
        return Results.BadRequest("Parent does not exist.");

      var hasChild = parent.Children.Any(c => string.Equals(c.FirstName, ticket.StudentFirstName, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(c.LastName, ticket.StudentLastName, StringComparison.OrdinalIgnoreCase) && string.Equals(c.TutorGroup, ticket.TutorGroup, StringComparison.OrdinalIgnoreCase));

      if (!hasChild)
        return Results.BadRequest("Student does not match any child of the parent.");

      var user = School.Instance.StaffByEmail[context.User.Identity.Name].Name;
      var ticketEntity = ticket as TicketEntity;

      var id = await TableService.CreateTicketAsync(ticketEntity);
      var tasks = new List<Task>
      {
        BlobService.CreateConversationAsync(id, new Message { AuthorName = user, IsEmployee = true, Timestamp = ticketEntity.LastUpdated, Content = message },
          new Message { AuthorName = user, IsEmployee = true, IsPrivate = true, Timestamp = ticketEntity.LastUpdated, Content = $"#assign {assignee.Name}" }),
        EmailService.SendTicketCreatedEmailAsync(parent.Email, id, ticket.Title, user, GetSalutation(parent.Name))
      };
      if (ticket.PartitionKey != context.User.Identity.Name)
      {
        tasks.Add(EmailService.SendTicketUpdateEmailAsync(id, ticketEntity, assignee, TicketUpdateAction.Assigned));
      }
      await Task.WhenAll(tasks);
      return Results.Created((string)null, ticketEntity);
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

    group.MapPut("/tickets/{id:int}/assignee", [Authorize(Roles = AuthConstants.Manager)] async (int id, [FromBody] ChangeAssigneePayload payload, HttpContext context) =>
    {
      if (string.IsNullOrWhiteSpace(payload?.AssigneeEmail))
        return Results.BadRequest("Assignee email is required.");

      var newAssigneeEmail = payload.NewAssigneeEmail?.Trim().ToLowerInvariant();
      if (string.IsNullOrEmpty(newAssigneeEmail))
        return Results.BadRequest("New assignee email is required.");

      if (string.Equals(payload.AssigneeEmail, newAssigneeEmail, StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest("New assignee email cannot be the same as the current assignee email.");

      if (!School.Instance.StaffByEmail.TryGetValue(newAssigneeEmail, out var staff))
        return Results.BadRequest("New assignee email does not match any staff member.");

      var ticket = await TableService.ReassignTicketAsync(payload.AssigneeEmail, id, newAssigneeEmail, staff.Name);

      var currentUser = School.Instance.StaffByEmail[context.User.Identity.Name].Name;
      await BlobService.AppendMessagesAsync(id, new Message
      {
        AuthorName = currentUser,
        IsEmployee = true,
        Timestamp = ticket.LastUpdated,
        IsPrivate = true,
        Content = $"#assign {staff.Name}"
      });

      if (payload.NewAssigneeEmail != context.User.Identity.Name)
      {
        await EmailService.SendTicketUpdateEmailAsync(id, ticket, staff, TicketUpdateAction.Assigned);
      }

      if (payload.AssigneeEmail != "unassigned" && payload.AssigneeEmail != context.User.Identity.Name && !ticket.IsClosed &&
        School.Instance.StaffByEmail.TryGetValue(payload.AssigneeEmail, out var oldAssignee))
      {
        await EmailService.SendTicketUpdateEmailAsync(id, ticket, oldAssignee, TicketUpdateAction.Unassigned);
      }

      return Results.Ok(ticket.LastUpdated);
    });

    group.MapPut("/tickets/{id:int}/student", [Authorize(Roles = AuthConstants.Manager)] async (int id, [FromBody] ChangeStudentPayload payload, HttpContext context) =>
    {
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

    group.MapPut("/tickets/{id:int}/parent", [Authorize(Roles = AuthConstants.Manager)] async (int id, [FromBody] ChangeParentPayload payload, HttpContext context) =>
    {
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
        {
          await TableService.ChangeTicketStudentAsync(entity, null);
        }
      }

      await TableService.ChangeTicketParentAsync(entity, newParent, currentStudent?.ParentRelationship);
      return Results.NoContent();
    });


    group.MapPut("/tickets/{id:int}/status", async (int id, [FromBody] ChangeStatusPayload payload, HttpContext context) =>
    {
      if (!context.User.IsInRole(AuthConstants.Manager) && !await TableService.TicketExistsAsync(context.User.Identity.Name, id))
        return Results.Forbid();

      var updated = await TableService.CloseTicketAsync(payload.AssigneeEmail, id, payload.IsClosed);
      if (updated is null) return Results.NoContent();

      var currentUser = School.Instance.StaffByEmail[context.User.Identity.Name].Name;
      await BlobService.AppendMessagesAsync(id, new Message
      {
        AuthorName = currentUser,
        IsEmployee = true,
        Timestamp = updated.Value,
        IsPrivate = true,
        Content = payload.IsClosed ? "#close" : "#reopen"
      });

      return Results.Ok(updated.Value);
    });

    group.MapPut("/tickets/{id:int}/title", [Authorize(Roles = AuthConstants.Manager)] async (int id, [FromBody] ChangeTitlePayload payload, HttpContext context) =>
    {
      if (string.IsNullOrWhiteSpace(payload?.NewTitle))
        return Results.BadRequest("Ticket title is required.");

      if (payload.NewTitle.Length > 40)
        return Results.BadRequest("Ticket title must be 40 characters or less.");

      await TableService.RenameTicketAsync(payload.AssigneeEmail, id, payload.NewTitle);
      return Results.NoContent();
    });

    group.MapPost("/tickets/{id:int}/message", async (int id, HttpContext context) =>
    {
      if (!context.Request.HasFormContentType)
        return Results.BadRequest("Request must be a form.");

      var form = await context.Request.ReadFormAsync();

      if (!bool.TryParse(form["isPrivate"].ToString(), out var isPrivate))
        return Results.BadRequest("Must specify whether the message is private.");

      var assigneeEmail = form["assigneeEmail"].ToString();
      if (string.IsNullOrWhiteSpace(assigneeEmail))
        return Results.BadRequest("Assignee email is required.");

      var content = form["content"].ToString();
      if (string.IsNullOrWhiteSpace(content))
        return Results.BadRequest("Message content is required.");

      if (content.StartsWith('#'))
        return Results.BadRequest("Message content cannot start with a hash (#).");

      foreach (var file in form.Files)
      {
        if (!Attachment.ValidateAttachment(file.FileName, file.Length, out var errorMessage)) return Results.BadRequest(errorMessage);
      }

      if (!context.User.IsInRole(AuthConstants.Manager) && assigneeEmail != context.User.Identity.Name)
        return Results.Forbid();

      var ticket = await TableService.GetTicketAsync(assigneeEmail, id);

      if (ticket.StudentFirstName is null || ticket.ParentName is null || ticket.AssigneeName is null)
        return Results.BadRequest("Ticket must have a student, parent, and assignee before adding a message.");

      var attachments = new List<Attachment>(form.Files.Count);
      var postmarkAttachments = new List<PostmarkMessageAttachment>(form.Files.Count);
      foreach (var file in form.Files)
      {
        using var stream = file.OpenReadStream();
        var blobName = await BlobService.UploadAttachmentAsync(stream, file.FileName);
        attachments.Add(new()
        {
          FileName = file.FileName,
          Url = blobName
        });
        if (isPrivate) continue;
        stream.Position = 0;
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        var bytes = memoryStream.ToArray();
        postmarkAttachments.Add(new()
        {
          Name = file.FileName,
          Content = Convert.ToBase64String(bytes),
          ContentType = file.ContentType
        });
      }

      var user = School.Instance.StaffByEmail[context.User.Identity.Name].Name;
      if (!isPrivate)
      {
        content = $"Dear {GetSalutation(ticket.ParentName)}\n\n{content}\n\nBest wishes\n\n{GetSalutation(user)}";
      }

      var now = DateTime.UtcNow;
      var tasks = new List<Task>(3);
      var message = new Message
      {
        AuthorName = user,
        IsEmployee = true,
        Timestamp = now,
        Content = content,
        IsPrivate = isPrivate,
        Attachments = attachments.Count > 0 ? attachments : null
      };
      tasks.Add(BlobService.AppendMessagesAsync(id, message));
      tasks.Add(TableService.SetLastUpdatedAsync(ticket, now, !isPrivate));

      if (!isPrivate)
      {
        tasks.Add(EmailService.SendParentReplyAsync(id, ticket, message, postmarkAttachments));
      }

      await Task.WhenAll(tasks);

      if (message.Attachments is not null)
      {
        foreach (var attachment in message.Attachments)
        {
          attachment.Url = BlobService.GetAttachmentSasUrl(attachment.Url);
        }
      }
      return Results.Ok(message);
    });

    group.MapPost("/tickets/{id:int}/suggest", async (int id, [FromBody] AIPayload payload, HttpContext context) =>
    {
      if (string.IsNullOrWhiteSpace(payload?.Guidance))
        return Results.BadRequest("Response guidance is required.");

      if (!context.User.IsInRole(AuthConstants.Manager) && context.User.Identity.Name != payload.AssigneeEmail)
        return Results.Forbid();

      var entity = await TableService.GetTicketAsync(payload.AssigneeEmail, id);
      var studentName = entity?.StudentFirstName;
      if (string.IsNullOrEmpty(studentName))
        return Results.BadRequest("Ticket must be associated with a student.");

      var messages = await BlobService.GetMessagesAsync(id);
      if (messages is null || messages.Count == 0)
        return Results.BadRequest("Ticket must have at least one message.");

      var suggestedReply = await AIService.GenerateReplyAsync(studentName, messages, payload.Guidance, entity.RowKey);
      return Results.Ok(suggestedReply);
    });

    group.MapGet("/tickets/{id:int}/lastupdated", async (int id, HttpContext context) =>
    {
      var user = context.User.IsInRole(AuthConstants.Manager) ? null : context.User.Identity.Name;
      var lastUpdated = await TableService.GetTicketCacheItemAsync(id, user);
      if (lastUpdated is null) return Results.NotFound();
      return Results.Ok(lastUpdated.Value);
    });

    app.MapPut("/api/users", [AllowAnonymous] async (HttpContext context, [FromHeader(Name = "X-Api-Key")] string auth) =>
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

  private static string GetSalutation(string addressee)
  {
    if (string.IsNullOrEmpty(addressee)) return "Parent/Carer";
    var tokens = addressee.Split(' ', 3);
    return tokens.Length == 3 && tokens[1].Length == 1 ? $"{tokens[0]} {tokens[2]}" : (tokens.Length >= 2 ? addressee : "Parent/Carer");
  }
}