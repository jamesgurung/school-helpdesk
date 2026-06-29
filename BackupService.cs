using Azure.Data.Tables;
using Azure.Storage.Blobs;
using CsvHelper;
using System.Globalization;
using System.IO.Compression;

namespace SchoolHelpdesk;

public static class BackupService
{
  private static BlobServiceClient blobServiceClient;
  private static TableServiceClient tableServiceClient;

  public static void Configure(string connectionString)
  {
    blobServiceClient = new BlobServiceClient(connectionString);
    tableServiceClient = new TableServiceClient(connectionString);
  }

  public static async Task<string> CreateBackupAsync(CancellationToken ct)
  {
    var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.zip");

    try
    {
      await using var fileStream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, true);
      using var zip = new ZipArchive(fileStream, ZipArchiveMode.Create);

      zip.CreateEntry("blobs/");
      zip.CreateEntry("tables/");

      await AddBlobsAsync(zip, ct);
      await AddTablesAsync(zip, ct);

      return path;
    }
    catch
    {
      File.Delete(path);
      throw;
    }
  }

  private static async Task AddBlobsAsync(ZipArchive zip, CancellationToken ct)
  {
    await foreach (var container in blobServiceClient.GetBlobContainersAsync(cancellationToken: ct))
    {
      var containerClient = blobServiceClient.GetBlobContainerClient(container.Name);
      zip.CreateEntry($"blobs/{container.Name}/");

      await foreach (var blob in containerClient.GetBlobsAsync(cancellationToken: ct))
      {
        if (container.Name == "config" && blob.Name == "keys.xml") continue;

        var safeBlobName = Uri.EscapeDataString(blob.Name).Replace("..", "%2E%2E");
        var entry = zip.CreateEntry($"blobs/{container.Name}/{safeBlobName}", CompressionLevel.SmallestSize);
        await using var entryStream = entry.Open();
        await containerClient.GetBlobClient(blob.Name).DownloadToAsync(entryStream, ct);
      }
    }
  }

  private static async Task AddTablesAsync(ZipArchive zip, CancellationToken ct)
  {
    await foreach (var table in tableServiceClient.QueryAsync(cancellationToken: ct))
    {
      var tableClient = tableServiceClient.GetTableClient(table.Name);
      var columns = new SortedSet<string>(StringComparer.Ordinal);
      var types = new Dictionary<string, string>(StringComparer.Ordinal);

      await foreach (var entity in tableClient.QueryAsync<TableEntity>(cancellationToken: ct))
      {
        foreach (var property in entity.Where(p => !IsSystemProperty(p.Key)))
        {
          columns.Add(property.Key);
          if (property.Value is not null && !types.ContainsKey(property.Key))
            types[property.Key] = GetTypeName(property.Value);
        }
      }

      var entry = zip.CreateEntry($"tables/{table.Name}.csv", CompressionLevel.SmallestSize);
      await using var entryStream = entry.Open();
      await using var writer = new StreamWriter(entryStream);
      await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

      WriteHeaders(csv, columns);
      await csv.NextRecordAsync();

      var isFirstRow = true;
      await foreach (var entity in tableClient.QueryAsync<TableEntity>(cancellationToken: ct))
      {
        csv.WriteField(entity.PartitionKey);
        csv.WriteField(entity.RowKey);
        csv.WriteField(entity.Timestamp.HasValue ? FormatValue(entity.Timestamp.Value) : string.Empty);

        foreach (var column in columns)
        {
          entity.TryGetValue(column, out var value);
          csv.WriteField(FormatValue(value));
          csv.WriteField(isFirstRow && types.TryGetValue(column, out var type) ? type : string.Empty);
        }

        await csv.NextRecordAsync();
        isFirstRow = false;
      }
    }
  }

  private static void WriteHeaders(CsvWriter csv, IEnumerable<string> columns)
  {
    csv.WriteField("PartitionKey");
    csv.WriteField("RowKey");
    csv.WriteField("Timestamp");

    foreach (var column in columns)
    {
      csv.WriteField(column);
      csv.WriteField($"{column}@type");
    }
  }

  private static bool IsSystemProperty(string name)
  {
    return string.Equals(name, "PartitionKey", StringComparison.Ordinal)
    || string.Equals(name, "RowKey", StringComparison.Ordinal)
    || string.Equals(name, "Timestamp", StringComparison.Ordinal)
    || string.Equals(name, "odata.etag", StringComparison.OrdinalIgnoreCase);
  }

  private static string GetTypeName(object value)
  {
    return value switch
    {
      string => "String",
      int => "Int32",
      long => "Int64",
      double => "Double",
      bool => "Boolean",
      DateTime => "DateTime",
      DateTimeOffset => "DateTime",
      Guid => "Guid",
      byte[] => "Binary",
      _ => "String"
    };
  }

  private static string FormatValue(object value)
  {
    return value switch
    {
      null => string.Empty,
      DateTime date => date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
      DateTimeOffset date => date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
      byte[] bytes => Convert.ToBase64String(bytes),
      double number => number.ToString("R", CultureInfo.InvariantCulture),
      IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
      _ => value.ToString()
    };
  }
}
