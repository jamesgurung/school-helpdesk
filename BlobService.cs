using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Html;
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

  public static string GetAttachmentSasUrl(string id)
  {
    var builder = new BlobSasBuilder
    {
      BlobContainerName = "attachments",
      BlobName = id,
      Resource = "b",
      StartsOn = DateTime.UtcNow.AddMinutes(-2),
      ExpiresOn = DateTime.UtcNow.AddHours(1),
      Protocol = SasProtocol.Https
    };
    builder.SetPermissions(BlobSasPermissions.Read);
    return builder.ToSasQueryParameters(sharedKeyCredential).ToString();
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
      students = csv.GetRecords<CsvStudent>().ToList();
    }

    List<CsvStaff> staff;
    var staffBlob = configClient.GetBlobClient("staff.csv");
    using (var stream = await staffBlob.OpenReadAsync())
    using (var reader = new StreamReader(stream))
    using (var csv = new CsvReader(reader, csvConfig))
    {
      staff = csv.GetRecords<CsvStaff>().ToList();
    }

    School.Instance.StaffByEmail = staff.ToDictionary(s => s.Email, s => new Staff { Email = s.Email, Name = $"{s.Title} {s.FirstName[0]} {s.LastName}" }, StringComparer.OrdinalIgnoreCase);

    School.Instance.ParentsByEmail = students
      .GroupBy(s => s.ParentEmailAddress, StringComparer.OrdinalIgnoreCase)
      .Select(g =>
      {
        var parentNames = g.Select(s => $"{s.ParentTitle} {s.ParentFirstName} {s.ParentLastName}").Distinct().ToList();
        var name = parentNames.Count > 1 ? string.Join(" and ", parentNames) : parentNames.FirstOrDefault() ?? "Parent/Carer";
        var parentRelationships = g.Select(s => s.Relationship).Distinct().ToList();
        var relationship = parentRelationships.Count > 1 ? "Parents" : parentRelationships.FirstOrDefault() ?? "Parent/Carer";
        return new Parent
        {
          Email = g.Key,
          Name = name,
          Relationship = relationship,
          Children = g.Select(s => new Student { FirstName = s.FirstName, LastName = s.LastName, TutorGroup = s.TutorGroup }).ToList()
        };
      }).ToDictionary(o => o.Email);

    School.Instance.ParentsJson = new HtmlString(JsonSerializer.Serialize(School.Instance.ParentsByEmail.Values, JsonSerializerOptions.Web));
    School.Instance.StaffJson = new HtmlString(JsonSerializer.Serialize(School.Instance.StaffByEmail.Values, JsonSerializerOptions.Web));

    var htmlTemplate = await configClient.GetBlobClient("template.html").DownloadContentAsync();
    School.Instance.HtmlEmailTemplate = htmlTemplate.Value.Content.ToString().ReplaceLineEndings("\n");

    var textTemplate = await configClient.GetBlobClient("template.txt").DownloadContentAsync();
    School.Instance.TextEmailTemplate = textTemplate.Value.Content.ToString().ReplaceLineEndings("\n");
  }
}