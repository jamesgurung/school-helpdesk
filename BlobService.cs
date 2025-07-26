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

  public static async Task<List<Message>> GetMessagesAsync(int ticketId)
  {
    var blobClient = messagesClient.GetBlobClient($"{ticketId.ToRowKey()}.json");
    var content = await blobClient.DownloadContentAsync();
    var messages = JsonSerializer.Deserialize<List<Message>>(content.Value.Content.ToString(), JsonSerializerOptions.Web);
    foreach (var attachment in messages.Where(o => o.Attachments is not null).SelectMany(o => o.Attachments))
    {
      attachment.Url = GetAttachmentSasUrl(attachment.Url);
    }
    return messages;
  }

  public static async Task CreateConversationAsync(int ticketId, params List<Message> messages)
  {
    var blobClient = messagesClient.GetBlobClient($"{ticketId.ToRowKey()}.json");
    var json = JsonSerializer.Serialize(messages, JsonSerializerOptions.Web);
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
    await blobClient.UploadAsync(stream);
  }

  public static async Task AppendMessagesAsync(int ticketId, params List<Message> messages)
  {
    var blobClient = messagesClient.GetBlobClient($"{ticketId.ToRowKey()}.json");
    var existingContent = await blobClient.DownloadContentAsync();
    var existingMessages = JsonSerializer.Deserialize<List<Message>>(existingContent.Value.Content.ToString(), JsonSerializerOptions.Web) ?? [];
    existingMessages.AddRange(messages);
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
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
    await blobClient.UploadAsync(stream, true);
  }

  public static async Task LoadConfigAsync()
  {
    var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture) { Encoding = Encoding.UTF8 };

    var students = new List<CsvStudent>();
    var studentsBlob = configClient.GetBlobClient("students.csv");
    using (var stream = await studentsBlob.OpenReadAsync())
    using (var reader = new StreamReader(stream))
    using (var csv = new CsvReader(reader, csvConfig))
    {
      await foreach (var student in csv.GetRecordsAsync<CsvStudent>())
      {
        if (string.IsNullOrWhiteSpace(student.ParentEmailAddress)) continue;
        students.Add(student);
      }
      students = students.OrderBy(s => s.ParentLastName).ThenBy(s => s.ParentFirstName).ThenBy(s => s.TutorGroup).ThenBy(s => s.LastName).ThenBy(s => s.FirstName).ToList();
    }

    var staff = new List<CsvStaff>();
    var staffBlob = configClient.GetBlobClient("staff.csv");
    using (var stream = await staffBlob.OpenReadAsync())
    using (var reader = new StreamReader(stream))
    using (var csv = new CsvReader(reader, csvConfig))
    {
      await foreach (var person in csv.GetRecordsAsync<CsvStaff>())
      {
        if (string.IsNullOrWhiteSpace(person.Email)) continue;
        staff.Add(person);
      }
      staff = staff.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ThenBy(s => s.Email).ToList();
    }

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

    School.Instance.Holidays = [];
    var holidaysBlob = configClient.GetBlobClient("holidays.csv");
    using (var stream = await holidaysBlob.OpenReadAsync())
    using (var reader = new StreamReader(stream))
    using (var csv = new CsvReader(reader, csvConfig))
    {
      await foreach (var holiday in csv.GetRecordsAsync<Holiday>())
      {
        School.Instance.Holidays.Add(holiday);
      }
    }
  }

  private static string ComputeHash(string input)
  {
    var bytes = Encoding.UTF8.GetBytes(input);
    var hashBytes = SHA256.HashData(bytes);
    return Convert.ToHexString(hashBytes).ToLowerInvariant();
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