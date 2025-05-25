using System.Text.Json.Serialization;

namespace SchoolHelpdesk;

public class Message
{
  [JsonPropertyName("timestamp")]
  public DateTime Timestamp { get; set; }
  [JsonPropertyName("authorName")]
  public string AuthorName { get; set; }
  [JsonPropertyName("isEmployee")]
  public bool IsEmployee { get; set; }
  [JsonPropertyName("content")]
  public string Content { get; set; }
  [JsonPropertyName("attachments")]
  public List<Attachment> Attachments { get; set; }
}

public class Attachment
{
  [JsonPropertyName("fileName")]
  public string FileName { get; set; }
  [JsonPropertyName("url")]
  public string Url { get; set; }
}