using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

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
  }
}