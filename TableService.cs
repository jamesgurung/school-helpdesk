using Azure;
using Azure.Data.Tables;
using System.Runtime.Serialization;
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

public class Ticket : ITableEntity
{
  [JsonPropertyName("open")]
  public string PartitionKey { get; set; }
  [JsonPropertyName("id")]
  public string RowKey { get; set; }
  [JsonIgnore]
  public DateTimeOffset? Timestamp { get; set; }
  public ETag ETag { get; set; }

  public string Title { get; set; }
  public DateTime CreatedDate { get; set; }
  public DateTime UpdatedDate { get; set; }
  public DateTime DueDate { get; set; }
  public string StudentFirstName { get; set; }
  public string StudentLastName { get; set; }
  public string TutorGroup { get; set; }
  public string AssigneeEmail { get; set; }
  public string AssigneeTitle { get; set; }
  public string AssigneeFirstName { get; set; }
  public string AssigneeLastName { get; set; }
  public string ParentEmail { get; set; }
  public string ParentFirstName { get; set; }
  public string ParentLastName { get; set; }
  public string ParentRelationship { get; set; }
  public string ThreadId { get; set; }
}

public class Comment : ITableEntity
{
  [JsonIgnore]
  public string PartitionKey { get; set; }
  [JsonPropertyName("id")]
  public string RowKey { get; set; }
  [JsonIgnore]
  public DateTimeOffset? Timestamp { get; set; }
  public ETag ETag { get; set; }

  public string AuthorEmail { get; set; }
  public string AuthorTitle { get; set; }
  public string AuthorFirstName { get; set; }
  public string AuthorLastName { get; set; }
  public string Content { get; set; }
  public DateTime Date { get; set; }

  [JsonIgnore]
  public string Attachments { get; set; }

  [IgnoreDataMember, JsonPropertyName("attachments")]
  public IList<Attachment> AttachmentsList =>
    Attachments?.Split(';').Select(o => o.Split(',')).Select(o => new Attachment
    {
      FileName = o[0],
      Url = BlobService.GetAttachmentSasUrl(o[1])
    }).ToList();
}

public class Attachment
{
  public string FileName { get; set; }
  public string Url { get; set; }
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