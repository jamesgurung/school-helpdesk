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