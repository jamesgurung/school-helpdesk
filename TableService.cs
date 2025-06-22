using Azure;
using Azure.Data.Tables;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace SchoolHelpdesk;

public static class TableService
{
  private static TableClient ticketsClient;
  private static TableClient commentsClient;
  private static int latestTicketId = -1;

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

  public static async Task<TicketEntity> GetTicketAsync(string assigneeEmail, int id)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    var ticket = await ticketsClient.GetEntityAsync<TicketEntity>(assigneeEmail, id.ToRowKey());
    return ticket.Value;
  }

  public static async Task<TicketEntity> GetTicketAsync(int id)
  {
    var tickets = await ticketsClient.QueryAsync<TicketEntity>(o => o.RowKey == id.ToRowKey()).ToListAsync();
    return tickets.Count == 1 ? tickets[0] : null;
  }

  public static async Task<List<TicketEntity>> GetAllTicketsAsync()
  {
    var tickets = await ticketsClient.QueryAsync<TicketEntity>().ToListAsync();
    return tickets.OrderByDescending(t => t.Timestamp).ToList();
  }

  public static async Task<List<TicketEntity>> GetTicketsByAssigneeAsync(string assigneeEmail)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    var tickets = await ticketsClient.QueryAsync<TicketEntity>(o => o.PartitionKey == assigneeEmail).ToListAsync();
    return tickets.OrderByDescending(t => t.Timestamp).ToList();
  }

  public static async Task<bool> TicketExistsAsync(string assigneeEmail, int id)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    var ticket = await ticketsClient.GetEntityIfExistsAsync<TicketEntity>(assigneeEmail, id.ToRowKey(), select: ["RowKey"]);
    return ticket.HasValue;
  }

  public static async Task<int> CreateTicketAsync(TicketEntity ticket)
  {
    ArgumentNullException.ThrowIfNull(ticket);
    if (latestTicketId < 0) throw new InvalidOperationException("Latest ticket ID not initialized.");

    var id = Interlocked.Increment(ref latestTicketId);
    ticket.Created = DateTime.UtcNow;
    ticket.WaitingSince = ticket.Created;
    ticket.RowKey = id.ToRowKey();
    await ticketsClient.AddEntityAsync(ticket);
    return id;
  }

  public static async Task<TicketEntity> ReassignTicketAsync(string assigneeEmail, int id, string newAssigneeEmail, string newAssigneeName)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    ArgumentNullException.ThrowIfNull(newAssigneeEmail);

    var rowKey = id.ToRowKey();
    var ticket = await ticketsClient.GetEntityAsync<TicketEntity>(assigneeEmail, rowKey);
    ticket.Value.PartitionKey = newAssigneeEmail;
    ticket.Value.AssigneeName = newAssigneeName;
    await ticketsClient.AddEntityAsync(ticket.Value);
    await ticketsClient.DeleteEntityAsync(assigneeEmail, rowKey);
    return ticket.Value;
  }

  public static async Task RenameTicketAsync(string assigneeEmail, int id, string newTitle)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    ArgumentNullException.ThrowIfNull(newTitle);
    var ticket = await ticketsClient.GetEntityAsync<TicketEntity>(assigneeEmail, id.ToRowKey());
    ticket.Value.Title = newTitle;
    await ticketsClient.UpdateEntityAsync(ticket.Value, ETag.All, TableUpdateMode.Replace);
  }

  public static async Task CloseTicketAsync(string assigneeEmail, int id, bool isClosed)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    var ticket = await ticketsClient.GetEntityAsync<TicketEntity>(assigneeEmail, id.ToRowKey());
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
    ticket.ParentRelationship = student.ParentRelationship;
    await ticketsClient.UpdateEntityAsync(ticket, ETag.All, TableUpdateMode.Replace);
  }

  public static async Task ChangeTicketParentAsync(TicketEntity ticket, Parent parent, string relationship)
  {
    ArgumentNullException.ThrowIfNull(ticket);
    ArgumentNullException.ThrowIfNull(parent);
    ticket.ParentName = parent.Name;
    ticket.ParentEmail = parent.Email;
    ticket.ParentPhone = parent.Phone;
    ticket.ParentRelationship = relationship;
    await ticketsClient.UpdateEntityAsync(ticket, ETag.All, TableUpdateMode.Replace);
  }

  public static async Task ClearLastParentMessageDateAsync(string assigneeEmail, int id)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    var ticket = await ticketsClient.GetEntityAsync<TicketEntity>(assigneeEmail, id.ToRowKey());
    ticket.Value.WaitingSince = null;
    await ticketsClient.UpdateEntityAsync(ticket.Value, ETag.All, TableUpdateMode.Replace);
  }

  public static async Task SetLastParentMessageDateAsync(TicketEntity ticket)
  {
    ArgumentNullException.ThrowIfNull(ticket);
    if (ticket.WaitingSince is not null && !ticket.IsClosed) return;
    ticket.WaitingSince ??= DateTime.UtcNow;
    ticket.IsClosed = false;
    await ticketsClient.UpdateEntityAsync(ticket, ETag.All, TableUpdateMode.Replace);
  }

  public static async Task LoadLatestTicketIdAsync()
  {
    var tickets = await ticketsClient.QueryAsync<TicketEntity>(select: ["RowKey"]).ToListAsync();
    latestTicketId = tickets.Count == 0 ? 0 : tickets.Max(t => int.TryParse(t.RowKey, out var id) ? id : 0);
  }

  public static string ToRowKey(this int id)
  {
    return id.ToString("D6");
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
  public string ParentName { get; set; }
  public string ParentEmail { get; set; }
  public string ParentPhone { get; set; }
  public string ParentRelationship { get; set; }
}

public class NewTicketEntity : TicketEntity
{
  [IgnoreDataMember]
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