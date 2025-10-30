using Azure;
using Azure.Data.Tables;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace SchoolHelpdesk;

public static class TableService
{
  private static TableClient ticketsClient;
  private static int latestTicketId = -1;

  public static void Configure(string connectionString)
  {
    ticketsClient = new TableServiceClient(connectionString).GetTableClient("tickets");
  }

  public static async Task WarmUpAsync()
  {
    var nonExistentKey = "warmup";
    await ticketsClient.QueryAsync<TableEntity>(o => o.PartitionKey == nonExistentKey).ToListAsync();
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

  public static async Task<List<TicketEntity>> GetAllTicketsAsync(DateTime? after = null)
  {
    var tickets = after is null
      ? await ticketsClient.QueryAsync<TicketEntity>().ToListAsync()
      : await ticketsClient.QueryAsync<TicketEntity>(o => o.LastUpdated > after.Value || !o.IsClosed).ToListAsync();
    return tickets.OrderByDescending(t => t.LastUpdated).ToList();
  }

  public static async Task<List<TicketEntity>> GetTicketsByAssigneeAsync(string assigneeEmail, DateTime? after = null)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    var tickets = after is null
      ? await ticketsClient.QueryAsync<TicketEntity>(o => o.PartitionKey == assigneeEmail).ToListAsync()
      : await ticketsClient.QueryAsync<TicketEntity>(o => o.PartitionKey == assigneeEmail && (o.LastUpdated > after.Value || !o.IsClosed)).ToListAsync();
    return tickets.OrderByDescending(t => t.LastUpdated).ToList();
  }

  public static async Task<bool> TicketExistsAsync(string assigneeEmail, int id)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    if (LastUpdatedCache.TryGetValue(id, out var cacheItem) && cacheItem.AssigneeEmail == assigneeEmail)
    {
      return true;
    }
    var ticket = await ticketsClient.GetEntityIfExistsAsync<TicketEntity>(assigneeEmail, id.ToRowKey(), select: ["RowKey"]);
    return ticket.HasValue;
  }

  public static async Task<int> CreateTicketAsync(TicketEntity ticket)
  {
    ArgumentNullException.ThrowIfNull(ticket);
    if (latestTicketId < 0) throw new InvalidOperationException("Latest ticket ID not initialized.");

    var id = Interlocked.Increment(ref latestTicketId);
    ticket.Created = DateTime.UtcNow;
    ticket.LastUpdated = ticket.Created;
    ticket.WaitingSince = ticket.Created;
    ticket.RowKey = id.ToRowKey();
    var response = await ticketsClient.AddEntityAsync(ticket);
    ticket.ETag = response.Headers.ETag.Value;
    SetLastUpdatedCache(id, ticket.PartitionKey, ticket.LastUpdated);
    return id;
  }

  public static async Task<TicketEntity> ReassignTicketAsync(string assigneeEmail, int id, string newAssigneeEmail, string newAssigneeName)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    ArgumentNullException.ThrowIfNull(newAssigneeEmail);
    if (assigneeEmail == newAssigneeEmail) throw new ArgumentException("New assignee email must be different from current assignee email.");

    var rowKey = id.ToRowKey();
    var ticket = await ticketsClient.GetEntityAsync<TicketEntity>(assigneeEmail, rowKey);
    ticket.Value.PartitionKey = newAssigneeEmail;
    ticket.Value.AssigneeName = newAssigneeName;
    ticket.Value.LastUpdated = DateTime.UtcNow;
    var response = await ticketsClient.AddEntityAsync(ticket.Value);
    ticket.Value.ETag = response.Headers.ETag.Value;
    await ticketsClient.DeleteEntityAsync(assigneeEmail, rowKey);
    SetLastUpdatedCache(id, newAssigneeEmail, ticket.Value.LastUpdated);
    return ticket.Value;
  }

  public static async Task RenameTicketAsync(string assigneeEmail, int id, string newTitle)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    ArgumentNullException.ThrowIfNull(newTitle);
    var ticket = await ticketsClient.GetEntityAsync<TicketEntity>(assigneeEmail, id.ToRowKey());
    ticket.Value.Title = newTitle;
    await ticketsClient.UpdateEntityAsync(ticket.Value, ticket.Value.ETag, TableUpdateMode.Replace);
  }

  public static async Task<DateTime?> CloseTicketAsync(string assigneeEmail, int id, bool isClosed)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    var ticket = await ticketsClient.GetEntityAsync<TicketEntity>(assigneeEmail, id.ToRowKey());
    if (ticket.Value.IsClosed == isClosed) return null;
    ticket.Value.IsClosed = isClosed;
    ticket.Value.LastUpdated = DateTime.UtcNow;
    await ticketsClient.UpdateEntityAsync(ticket.Value, ticket.Value.ETag, TableUpdateMode.Replace);
    SetLastUpdatedCache(id, assigneeEmail, ticket.Value.LastUpdated);
    return ticket.Value.LastUpdated;
  }

  public static async Task ChangeTicketStudentAsync(TicketEntity ticket, Student student)
  {
    ArgumentNullException.ThrowIfNull(ticket);
    ticket.StudentFirstName = student?.FirstName;
    ticket.StudentLastName = student?.LastName;
    ticket.TutorGroup = student?.TutorGroup;
    ticket.ParentRelationship = student?.ParentRelationship;
    var response = await ticketsClient.UpdateEntityAsync(ticket, ticket.ETag, TableUpdateMode.Replace);
    ticket.ETag = response.Headers.ETag.Value;
  }

  public static async Task ChangeTicketParentAsync(TicketEntity ticket, Parent parent, string relationship)
  {
    ArgumentNullException.ThrowIfNull(ticket);
    ArgumentNullException.ThrowIfNull(parent);
    ticket.ParentName = parent.Name;
    ticket.ParentEmail = parent.Email;
    ticket.ParentPhone = parent.Phone;
    ticket.ParentRelationship = relationship;
    var response = await ticketsClient.UpdateEntityAsync(ticket, ticket.ETag, TableUpdateMode.Replace);
    ticket.ETag = response.Headers.ETag.Value;
  }

  public static async Task SetLastUpdatedAsync(TicketEntity ticket, DateTime lastUpdated, bool cancelWaiting)
  {
    ArgumentNullException.ThrowIfNull(ticket);
    ticket.LastUpdated = lastUpdated;
    if (cancelWaiting) ticket.WaitingSince = null;
    var response = await ticketsClient.UpdateEntityAsync(ticket, ticket.ETag, TableUpdateMode.Replace);
    ticket.ETag = response.Headers.ETag.Value;
    SetLastUpdatedCache(int.Parse(ticket.RowKey, CultureInfo.InvariantCulture), ticket.PartitionKey, lastUpdated);
  }

  public static async Task UpdateForNewParentMessageAsync(TicketEntity ticket, DateTime lastUpdated)
  {
    ArgumentNullException.ThrowIfNull(ticket);
    ticket.LastUpdated = lastUpdated;
    ticket.WaitingSince ??= lastUpdated;
    ticket.IsClosed = false;
    var response = await ticketsClient.UpdateEntityAsync(ticket, ticket.ETag, TableUpdateMode.Replace);
    ticket.ETag = response.Headers.ETag.Value;
    SetLastUpdatedCache(int.Parse(ticket.RowKey, CultureInfo.InvariantCulture), ticket.PartitionKey, lastUpdated);
  }

  public static async Task LoadLatestTicketIdAsync()
  {
    var tickets = await ticketsClient.QueryAsync<TicketEntity>(select: ["RowKey"]).ToListAsync();
    latestTicketId = tickets.Count == 0 ? 0 : tickets.Max(t => int.TryParse(t.RowKey, out var id) ? id : 0);
  }

  public static string ToRowKey(this int id)
  {
    return id.ToString("D6", CultureInfo.InvariantCulture);
  }

  private static readonly Dictionary<int, TicketCacheItem> LastUpdatedCache = [];

  public static async Task<DateTime?> GetTicketCacheItemAsync(int ticketId, string user)
  {
    if (!LastUpdatedCache.TryGetValue(ticketId, out var cacheItem))
    {
      var tickets = await ticketsClient.QueryAsync<TicketEntity>(o => o.RowKey == ticketId.ToRowKey(), select: ["LastUpdated"]).ToListAsync();
      if (tickets.Count == 1)
      {
        cacheItem = new(tickets[0].PartitionKey, tickets[0].LastUpdated);
        LastUpdatedCache[ticketId] = cacheItem;
      }
      else
      {
        return null;
      }
    }
    return user is null || user == cacheItem.AssigneeEmail ? cacheItem.LastUpdated : null;
  }

  public static void SetLastUpdatedCache(int ticketId, string assignee, DateTime lastUpdated)
  {
    LastUpdatedCache[ticketId] = new(assignee, lastUpdated);
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
  public DateTime LastUpdated { get; set; }
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

public record TicketCacheItem(string AssigneeEmail, DateTime LastUpdated);

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