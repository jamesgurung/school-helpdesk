using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SchoolHelpdesk;

public static class BlobService
{
  public static void Configure(string connectionString, string accountName, string accountKey)
  {
    var blobClient = new BlobServiceClient(connectionString);
    messagesClient = blobClient.GetBlobContainerClient("messages");
    attachmentsClient = blobClient.GetBlobContainerClient("attachments");
    configClient = blobClient.GetBlobContainerClient("config");
    sharedKeyCredential = new StorageSharedKeyCredential(accountName, accountKey);
  }

  private static BlobContainerClient messagesClient;
  private static BlobContainerClient attachmentsClient;
  private static BlobContainerClient configClient;
  private static StorageSharedKeyCredential sharedKeyCredential;

  public static async Task<List<Message>> GetMessagesAsync(int ticketId)
  {
    var blobClient = messagesClient.GetBlobClient($"{ticketId.ToRowKey()}.json");
    var content = await blobClient.DownloadContentAsync();
    var messages = content.Value.Content.ToObjectFromJson<List<Message>>(JsonSerializerOptions.Web);
    foreach (var attachment in messages.Where(o => o.Attachments is not null).SelectMany(o => o.Attachments))
    {
      attachment.Url = GetAttachmentSasUrl(attachment.Url, attachment.FileName);
    }
    return messages;
  }

  public static async Task CreateConversationAsync(int ticketId, params List<Message> messages)
  {
    var blobClient = messagesClient.GetBlobClient($"{ticketId.ToRowKey()}.json");
    await blobClient.UploadAsync(BinaryData.FromObjectAsJson(messages, JsonSerializerOptions.Web));
  }

  public static async Task<List<Message>> AppendMessagesAsync(int ticketId, params List<Message> newMessages)
  {
    var blobClient = messagesClient.GetBlobClient($"{ticketId.ToRowKey()}.json");
    var existingContent = await blobClient.DownloadContentAsync();
    var messages = existingContent.Value.Content.ToObjectFromJson<List<Message>>(JsonSerializerOptions.Web) ?? [];
    messages.AddRange(newMessages);
    await blobClient.UploadAsync(BinaryData.FromObjectAsJson(messages, JsonSerializerOptions.Web), true);
    return messages;
  }

  public static string GetAttachmentSasUrl(string blobName, string fileName)
  {
    var builder = new BlobSasBuilder
    {
      BlobContainerName = "attachments",
      BlobName = blobName,
      Resource = "b",
      StartsOn = DateTime.UtcNow.AddMinutes(-2),
      ExpiresOn = DateTime.UtcNow.AddDays(1),
      Protocol = SasProtocol.Https,
      ContentDisposition = $"inline; filename=\"{fileName}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}"
    };
    builder.SetPermissions(BlobSasPermissions.Read);
    return $"{attachmentsClient.Uri}/{blobName}?{builder.ToSasQueryParameters(sharedKeyCredential)}";
  }

  public static async Task<string> UploadAttachmentAsync(Stream fileStream, string fileName)
  {
    var fileId = Guid.NewGuid().ToString("N");
    var extension = Path.GetExtension(fileName);
    var blobName = $"{fileId}{extension}";
    var blobClient = attachmentsClient.GetBlobClient(blobName);
    await blobClient.UploadAsync(fileStream, true);
    return blobName;
  }

  public static async Task UpdateDataFileAsync(string fileName, string content)
  {
    ArgumentNullException.ThrowIfNull(fileName);
    ArgumentNullException.ThrowIfNull(content);
    var blobClient = configClient.GetBlobClient(fileName);
    await blobClient.UploadAsync(BinaryData.FromString(content), true);
  }

  public static async Task LoadConfigAsync()
  {
    var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture) { Encoding = Encoding.UTF8 };

    var students = (await ReadCsvAsync<CsvStudent>("students.csv", csvConfig, s => !string.IsNullOrWhiteSpace(s.ParentEmailAddress)))
      .OrderBy(s => s.ParentLastName).ThenBy(s => s.ParentFirstName).ThenBy(s => s.TutorGroup).ThenBy(s => s.LastName).ThenBy(s => s.FirstName).ToList();
    var staff = (await ReadCsvAsync<CsvStaff>("staff.csv", csvConfig, s => !string.IsNullOrWhiteSpace(s.Email)))
      .OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ThenBy(s => s.Email).ToList();

    School.Instance.StaffByEmail = staff.ToDictionary(s => s.Email, s => new Staff
    {
      Email = s.Email,
      Name = $"{s.Title} {s.FirstName[0]} {s.LastName}",
      FirstName = s.FirstName
    }, StringComparer.OrdinalIgnoreCase);

    var studentsByParent = students
      .Select(s => new
      {
        s.FirstName,
        s.LastName,
        s.TutorGroup,
        ParentEmailAddress = s.ParentEmailAddress.ToLowerInvariant(),
        ParentPhoneNumber = FormatPhoneNumber(s.ParentPhoneNumber),
        s.Relationship,
        ParentName = $"{s.ParentTitle} {(string.IsNullOrEmpty(s.ParentFirstName) ? string.Empty : $"{s.ParentFirstName[0]} ")}{s.ParentLastName}".Trim()
      }).GroupBy(s => $"{s.ParentEmailAddress}:{s.ParentName}", StringComparer.OrdinalIgnoreCase);

    var parents = studentsByParent.Select(g =>
      {
        var first = g.First();
        var name = string.IsNullOrWhiteSpace(first.ParentName) ? "Parent/Carer" : first.ParentName;
        return new Parent
        {
          Name = name,
          Email = first.ParentEmailAddress,
          Phone = first.ParentPhoneNumber,
          Children = g.Select(s => new Student { FirstName = s.FirstName, LastName = s.LastName, TutorGroup = s.TutorGroup, ParentRelationship = s.Relationship }).ToList()
        };
      }).ToList();

    School.Instance.ParentsByEmail = parents.ToLookup(o => o.Email, StringComparer.OrdinalIgnoreCase);

    var users = new
    {
      Parents = parents,
      Staff = School.Instance.StaffByEmail.Values
    };
    School.Instance.UsersJson = JsonSerializer.Serialize(users, JsonSerializerOptions.Web);

    var htmlTemplate = await configClient.GetBlobClient("template.html").DownloadContentAsync();
    School.Instance.HtmlEmailTemplate = htmlTemplate.Value.Content.ToString().ReplaceLineEndings("\n");

    var textTemplate = await configClient.GetBlobClient("template.txt").DownloadContentAsync();
    School.Instance.TextEmailTemplate = textTemplate.Value.Content.ToString().ReplaceLineEndings("\n");

    School.Instance.Holidays = await ReadCsvAsync<Holiday>("holidays.csv", csvConfig);

    var blocklistBlob = configClient.GetBlobClient("blocklist.txt");
    try
    {
      var content = await blocklistBlob.DownloadContentAsync();
      var lines = content.Value.Content.ToString().ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
      School.Instance.BlockedEmails = new HashSet<string>(lines.Where(l => l.Contains('@', StringComparison.Ordinal)), StringComparer.OrdinalIgnoreCase);
      School.Instance.BlockedDomains = new HashSet<string>(lines.Where(l => !l.Contains('@', StringComparison.Ordinal)), StringComparer.OrdinalIgnoreCase);
    }
    catch (RequestFailedException ex) when (ex.Status == 404)
    {
      School.Instance.BlockedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      School.Instance.BlockedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
  }

  public static async Task AddToBlocklistAsync(string entry)
  {
    var blobClient = configClient.GetBlobClient("blocklist.txt");
    var response = await blobClient.DownloadContentAsync();
    var array = response.Value.Content.ToString().ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var hashset = new HashSet<string>(array, StringComparer.OrdinalIgnoreCase);
    hashset.Add(entry);
    var content = string.Join("\n", hashset);
    await blobClient.UploadAsync(BinaryData.FromString(content), true);
  }

  private static async Task<List<T>> ReadCsvAsync<T>(string blobName, CsvConfiguration config, Func<T, bool> predicate = null)
  {
    using var stream = await configClient.GetBlobClient(blobName).OpenReadAsync();
    using var reader = new StreamReader(stream);
    using var csv = new CsvReader(reader, config);
    var records = new List<T>();
    await foreach (var record in csv.GetRecordsAsync<T>())
    {
      if (predicate?.Invoke(record) ?? true) records.Add(record);
    }
    return records;
  }

  private static string FormatPhoneNumber(string phoneNumber)
  {
    if (string.IsNullOrWhiteSpace(phoneNumber)) return null;
    var digits = new string(phoneNumber.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
    if (digits.StartsWith("+44", StringComparison.Ordinal))
    {
      digits = digits[3..];
      if (digits.Length > 0 && digits[0] != '0') digits = "0" + digits;
    }
    return digits.Length != 11 ? digits : $"{digits[..5]} {digits[5..8]} {digits[8..11]}";
  }
}
