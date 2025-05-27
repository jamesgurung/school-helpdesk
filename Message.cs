using System.Text.Json.Serialization;

namespace SchoolHelpdesk;

public class Message
{
  public DateTime Timestamp { get; set; }
  public string AuthorName { get; set; }
  public bool IsEmployee { get; set; }
  public string Content { get; set; }
  public bool IsPrivate { get; set; }
  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public List<Attachment> Attachments { get; set; }
  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public string OriginalEmail { get; set; }
}

public class Attachment
{
  public string FileName { get; set; }
  public string Url { get; set; }
}