using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text;

namespace SchoolHelpdesk;

public static class BlobService
{
  public static void Configure(string connectionString, string accountName, string accountKey)
  {
    var blobClient = new BlobServiceClient(connectionString);
    attachmentsClient = blobClient.GetBlobContainerClient("attachments");
    configClient = blobClient.GetBlobContainerClient("config");
    sharedKeyCredential = new StorageSharedKeyCredential(accountName, accountKey);
  }

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

    var studentsBlob = configClient.GetBlobClient("students.csv");
    using (var stream = await studentsBlob.OpenReadAsync())
    using (var reader = new StreamReader(stream))
    using (var csv = new CsvReader(reader, csvConfig))
    {
      var records = csv.GetRecords<Student>().ToList();
      School.Instance.Students = records;
    }

    var staffBlob = configClient.GetBlobClient("staff.csv");
    using (var stream = await staffBlob.OpenReadAsync())
    using (var reader = new StreamReader(stream))
    using (var csv = new CsvReader(reader, csvConfig))
    {
      var records = csv.GetRecords<Staff>().ToList();
      School.Instance.Staff = records;
    }
    School.Instance.StudentsByParentEmail = School.Instance.Students.ToLookup(s => s.ParentEmailAddress, s => s, StringComparer.OrdinalIgnoreCase);
    School.Instance.StaffByEmail = School.Instance.Staff.ToDictionary(s => s.Email, s => s, StringComparer.OrdinalIgnoreCase);

    var templateBlob = configClient.GetBlobClient("template.html");
    var templateResponse = await templateBlob.DownloadContentAsync();
    School.Instance.EmailTemplate = templateResponse.Value.Content.ToString();
  }
}