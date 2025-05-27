using Azure;
using Azure.Data.Tables;
using System.Text.Json.Serialization;

namespace SchoolHelpdesk;

public static class TableService
{
  private static TableClient ticketsClient;
  private static TableClient commentsClient;

  public static void Configure(string connectionString)
  {
    ticketsClient = new TableServiceClient(connectionString).GetTableClient("tickets");
    commentsClient = new TableServiceClient(connectionString).GetTableClient("comments");
  }

  public static async Task WarmUpAsync()
  {
    var nonExistentKey = "warmup";
    await ticketsClient.QueryAsync<TableEntity>(o => o.PartitionKey == nonExistentKey).ToListAsync();
    await commentsClient.QueryAsync<TableEntity>(o => o.PartitionKey == nonExistentKey).ToListAsync();
  }

  public static async Task<TicketEntity> GetTicketAsync(string assigneeEmail, string id)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    ArgumentNullException.ThrowIfNull(id);
    var ticket = await ticketsClient.GetEntityAsync<TicketEntity>(assigneeEmail, id);
    return ticket.Value;
  }

  public static async Task<List<TicketEntity>> GetAllTicketsAsync()
  {
    var tickets = await ticketsClient.QueryAsync<TicketEntity>().ToListAsync();
    return tickets.OrderByDescending(t => t.Created).ToList();
  }

  public static async Task<List<TicketEntity>> GetTicketsByAssigneeAsync(string assigneeEmail)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    var tickets = await ticketsClient.QueryAsync<TicketEntity>(o => o.PartitionKey == assigneeEmail).ToListAsync();
    return tickets.OrderByDescending(t => t.Created).ToList();
  }

  public static async Task<bool> TicketExistsAsync(string assigneeEmail, string id)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    ArgumentNullException.ThrowIfNull(id);
    var ticket = await ticketsClient.GetEntityIfExistsAsync<TicketEntity>(assigneeEmail, id, select: ["RowKey"]);
    return ticket.HasValue;
  }

  public static async Task<TicketEntity> InsertTicketAsync(TicketEntity ticket)
  {
    ArgumentNullException.ThrowIfNull(ticket);
    ticket.Created = DateTime.UtcNow;
    ticket.WaitingSince = ticket.Created;
    ticket.RowKey = Guid.NewGuid().ToString("N");
    await ticketsClient.AddEntityAsync(ticket);
    return ticket;
  }

  public static async Task ReassignTicketAsync(string assigneeEmail, string id, string newAssigneeEmail, string newAssigneeName)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    ArgumentNullException.ThrowIfNull(id);
    ArgumentNullException.ThrowIfNull(newAssigneeEmail);

    var ticket = await ticketsClient.GetEntityAsync<TicketEntity>(assigneeEmail, id);
    ticket.Value.PartitionKey = newAssigneeEmail;
    ticket.Value.AssigneeName = newAssigneeName;
    await ticketsClient.AddEntityAsync(ticket.Value);
    await ticketsClient.DeleteEntityAsync(assigneeEmail, id);
  }

  public static async Task RenameTicketAsync(string assigneeEmail, string id, string newTitle)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    ArgumentNullException.ThrowIfNull(id);
    ArgumentNullException.ThrowIfNull(newTitle);
    var ticket = await ticketsClient.GetEntityAsync<TicketEntity>(assigneeEmail, id);
    ticket.Value.Title = newTitle;
    await ticketsClient.UpdateEntityAsync(ticket.Value, ETag.All, TableUpdateMode.Replace);
  }

  public static async Task CloseTicketAsync(string assigneeEmail, string id, bool isClosed)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    ArgumentNullException.ThrowIfNull(id);
    var ticket = await ticketsClient.GetEntityAsync<TicketEntity>(assigneeEmail, id);
    ticket.Value.IsClosed = isClosed;
    await ticketsClient.UpdateEntityAsync(ticket.Value, ETag.All, TableUpdateMode.Replace);
  }

  public static async Task ChangeTicketStudentAsync(TicketEntity ticket, Student student)
  {
    ArgumentNullException.ThrowIfNull(ticket);
    ArgumentNullException.ThrowIfNull(student);
    ticket.StudentFirstName = student.FirstName;
    ticket.StudentLastName = student.LastName;
    ticket.TutorGroup = student.TutorGroup;
    await ticketsClient.UpdateEntityAsync(ticket, ETag.All, TableUpdateMode.Replace);
  }

  internal static async Task UpdateLastParentMessageDateAsync(string assigneeEmail, string id, DateTime? timestamp)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    ArgumentNullException.ThrowIfNull(id);
    var ticket = await ticketsClient.GetEntityAsync<TicketEntity>(assigneeEmail, id);
    ticket.Value.WaitingSince = timestamp;
    await ticketsClient.UpdateEntityAsync(ticket.Value, ETag.All, TableUpdateMode.Replace);
  }
}

public class TicketEntity : ITableEntity
{
  [JsonPropertyName("assigneeEmail")]
  public string PartitionKey { get; set; }
  [JsonPropertyName("id")]
  public string RowKey { get; set; }
  [JsonIgnore]
  public DateTimeOffset? Timestamp { get; set; }
  [JsonIgnore]
  public ETag ETag { get; set; }

  public bool IsClosed { get; set; }
  public string Title { get; set; }
  public DateTime Created { get; set; }
  public DateTime? WaitingSince { get; set; }
  public string StudentFirstName { get; set; }
  public string StudentLastName { get; set; }
  public string TutorGroup { get; set; }
  public string AssigneeName { get; set; }
  public string ParentEmail { get; set; }
  public string ParentName { get; set; }
  public string ParentRelationship { get; set; }
  [JsonIgnore]
  public string ThreadId { get; set; }
}

public class NewTicketEntity : TicketEntity
{
  public string Message { get; set; }
}

public static class QueryExtensions
{
  public static async Task<List<T>> ToListAsync<T>(this AsyncPageable<T> query)
  {
    ArgumentNullException.ThrowIfNull(query);
    var list = new List<T>();
    await foreach (var item in query)
    {
      list.Add(item);
    }
    return list;
  }
}