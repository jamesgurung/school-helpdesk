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
  private static readonly HashSet<string> validFileExtensions =
    new([".pdf", ".docx", ".doc", ".odt", ".xlsx", ".xls", ".pptx", ".ppt", ".csv", ".txt", ".png", ".jpg", ".jpeg", ".webp", ".heic"], StringComparer.OrdinalIgnoreCase);

  private static readonly char[] invalidFileNameChars = Path.GetInvalidFileNameChars();

  public static bool ValidateAttachment(string fileName, long contentLength, out string error)
  {
    if (contentLength == 0) { error = "Attachment cannot be empty."; return false; }
    if (contentLength > 10 * 1024 * 1024) { error = "Attachment size exceeds the limit of 10 MB."; return false; }
    if (!validFileExtensions.Contains(Path.GetExtension(fileName))) { error = "Invalid file type."; return false; }
    if (string.IsNullOrWhiteSpace(fileName)) { error = "Attachment file name cannot be empty."; return false; }
    if (fileName.Length > 100) { error = "Attachment file name is too long."; return false; }
    if (fileName.IndexOfAny(invalidFileNameChars) >= 0) { error = "Attachment file name contains invalid characters."; return false; }
    if (fileName.Contains("..", StringComparison.Ordinal) || fileName.Contains('/', StringComparison.Ordinal)) { error = "Attachment file name cannot contain relative paths."; return false; }
    error = null;
    return true;
  }

  public string FileName { get; set; }
  public string Url { get; set; }
}