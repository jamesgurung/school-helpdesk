using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Security.Cryptography;
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

  public static async Task<List<Message>> GetMessagesAsync(string ticketId)
  {
    ArgumentNullException.ThrowIfNull(ticketId);
    var blobClient = messagesClient.GetBlobClient($"{ticketId}.json");
    var content = await blobClient.DownloadContentAsync();
    var messages = JsonSerializer.Deserialize<List<Message>>(content.Value.Content.ToString(), JsonSerializerOptions.Web);
    foreach (var attachment in messages.Where(o => o.Attachments is not null).SelectMany(o => o.Attachments))
    {
      attachment.Url = GetAttachmentSasUrl(attachment.Url);
    }
    return messages;
  }

  public static async Task InsertMessageAsync(string ticketId, Message message)
  {
    var blobClient = messagesClient.GetBlobClient($"{ticketId}.json");
    var json = JsonSerializer.Serialize(new[] { message }, JsonSerializerOptions.Web);
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
    await blobClient.UploadAsync(stream);
  }

  public static async Task AppendMessageAsync(string ticketId, Message message)
  {
    var blobClient = messagesClient.GetBlobClient($"{ticketId}.json");
    var existingContent = await blobClient.DownloadContentAsync();
    var existingMessages = JsonSerializer.Deserialize<List<Message>>(existingContent.Value.Content.ToString(), JsonSerializerOptions.Web) ?? [];
    existingMessages.Add(message);
    var json = JsonSerializer.Serialize(existingMessages, JsonSerializerOptions.Web);
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
    await blobClient.UploadAsync(stream, true);
  }

  public static string GetAttachmentSasUrl(string blobName)
  {
    var builder = new BlobSasBuilder
    {
      BlobContainerName = "attachments",
      BlobName = blobName,
      Resource = "b",
      StartsOn = DateTime.UtcNow.AddMinutes(-2),
      ExpiresOn = DateTime.UtcNow.AddDays(1),
      Protocol = SasProtocol.Https
    };
    builder.SetPermissions(BlobSasPermissions.Read);
    return $"{attachmentsClient.Uri}/{blobName}?{builder.ToSasQueryParameters(sharedKeyCredential)}";
  }

  public static async Task<string> UploadAttachmentAsync(Stream fileStream, string fileName)
  {
    var fileId = Guid.NewGuid().ToString();
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
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
    await blobClient.UploadAsync(stream, true);
  }

  public static async Task LoadConfigAsync()
  {
    var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture) { Encoding = Encoding.UTF8 };

    List<CsvStudent> students;
    var studentsBlob = configClient.GetBlobClient("students.csv");
    using (var stream = await studentsBlob.OpenReadAsync())
    using (var reader = new StreamReader(stream))
    using (var csv = new CsvReader(reader, csvConfig))
    {
      students = csv.GetRecords<CsvStudent>()
        .Where(s => !string.IsNullOrWhiteSpace(s.ParentEmailAddress))
        .OrderBy(s => s.ParentLastName).ThenBy(s => s.ParentFirstName).ThenBy(s => s.TutorGroup).ThenBy(s => s.LastName).ThenBy(s => s.FirstName).ToList();
    }

    List<CsvStaff> staff;
    var staffBlob = configClient.GetBlobClient("staff.csv");
    using (var stream = await staffBlob.OpenReadAsync())
    using (var reader = new StreamReader(stream))
    using (var csv = new CsvReader(reader, csvConfig))
    {
      staff = csv.GetRecords<CsvStaff>().Where(s => !string.IsNullOrWhiteSpace(s.Email)).OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ThenBy(s => s.Email).ToList();
    }

    School.Instance.StaffByEmail = staff.ToDictionary(s => s.Email, s => new Staff { Email = s.Email, Name = $"{s.Title} {s.FirstName[0]} {s.LastName}" }, StringComparer.OrdinalIgnoreCase);

    var studentsByParent = students
      .Select(s => new
      {
        s.FirstName,
        s.LastName,
        s.TutorGroup,
        s.ParentEmailAddress,
        s.Relationship,
        ParentName = $"{s.ParentTitle} {(string.IsNullOrEmpty(s.ParentFirstName) ? string.Empty : $"{s.ParentFirstName[0]} ")}{s.ParentLastName}".Trim()
      }).GroupBy(s => $"{s.ParentEmailAddress}:{s.ParentName}", StringComparer.OrdinalIgnoreCase);

    var parents = studentsByParent.Select(g =>
      {
        var first = g.First();
        var name = string.IsNullOrWhiteSpace(first.ParentName) ? "Parent/Carer" : first.ParentName;
        return new Parent
        {
          Email = first.ParentEmailAddress,
          Name = name,
          Children = g.Select(s => new Student { FirstName = s.FirstName, LastName = s.LastName, TutorGroup = s.TutorGroup, ParentRelationship = s.Relationship }).ToList()
        };
      });

    School.Instance.ParentsByEmail = parents.ToLookup(o => o.Email, StringComparer.OrdinalIgnoreCase);

    var users = new
    {
      Parents = parents,
      Staff = School.Instance.StaffByEmail.Values
    };
    School.Instance.UsersJson = JsonSerializer.Serialize(users, JsonSerializerOptions.Web);
    School.Instance.UsersJsonHash = ComputeHash(School.Instance.UsersJson);

    var htmlTemplate = await configClient.GetBlobClient("template.html").DownloadContentAsync();
    School.Instance.HtmlEmailTemplate = htmlTemplate.Value.Content.ToString().ReplaceLineEndings("\n");

    var textTemplate = await configClient.GetBlobClient("template.txt").DownloadContentAsync();
    School.Instance.TextEmailTemplate = textTemplate.Value.Content.ToString().ReplaceLineEndings("\n");
  }

  private static string ComputeHash(string input)
  {
    var bytes = Encoding.UTF8.GetBytes(input);
    var hashBytes = SHA256.HashData(bytes);
    return Convert.ToHexString(hashBytes).ToLowerInvariant();
  }
}