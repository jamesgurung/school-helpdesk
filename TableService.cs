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

  public static async Task<List<TicketEntity>> GetAllTicketsAsync()
  {
    return await ticketsClient.QueryAsync<TicketEntity>().ToListAsync();
  }

  public static async Task<List<TicketEntity>> GetTicketsByAssigneeAsync(string assigneeEmail)
  {
    ArgumentNullException.ThrowIfNull(assigneeEmail);
    return await ticketsClient.QueryAsync<TicketEntity>(o => o.PartitionKey == assigneeEmail).ToListAsync();
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
    ticket.CreatedDate = DateTime.UtcNow;
    ticket.UpdatedDate = ticket.CreatedDate;
    ticket.RowKey = Guid.NewGuid().ToString("N");
    await ticketsClient.AddEntityAsync(ticket);
    return ticket;
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

  [JsonPropertyName("closed")]
  public bool IsClosed { get; set; }
  [JsonPropertyName("title")]
  public string Title { get; set; }
  [JsonPropertyName("created")]
  public DateTime CreatedDate { get; set; }
  [JsonPropertyName("updated")]
  public DateTime UpdatedDate { get; set; }
  [JsonPropertyName("studentFirstName")]
  public string StudentFirstName { get; set; }
  [JsonPropertyName("studentLastName")]
  public string StudentLastName { get; set; }
  [JsonPropertyName("tutorGroup")]
  public string TutorGroup { get; set; }
  [JsonPropertyName("assigneeName")]
  public string AssigneeName { get; set; }
  [JsonPropertyName("parentEmail")]
  public string ParentEmail { get; set; }
  [JsonPropertyName("parentName")]
  public string ParentName { get; set; }
  [JsonPropertyName("parentRelationship")]
  public string ParentRelationship { get; set; }
  [JsonIgnore]
  public string ThreadId { get; set; }
}

public class NewTicketEntity : TicketEntity
{
  [JsonPropertyName("message")]
  public string InitialMessage { get; set; }
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