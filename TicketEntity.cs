using Azure;
using Azure.Data.Tables;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace SchoolHelpdesk;

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
  public int? TimeToFirstResponse { get; set; }
  public int? AverageAssigneeResponseTime { get; set; }
}

public class NewTicketEntity : TicketEntity
{
  [IgnoreDataMember]
  public string Message { get; set; }
}

public record TicketCacheItem(string AssigneeEmail, DateTime LastUpdated);
