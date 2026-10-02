using System.Text.Json;
using Company.Product.Worker.Managers.Exceptions;
using Company.Product.Worker.Managers.Models;

namespace Company.Product.Worker.Engines;

/// <summary>Reads "object created" notifications from any object store.</summary>
public interface IObjectNotificationEngine
{
    /// <summary>
    /// Extracts the created objects from a storage notification. The format is detected automatically:
    /// Amazon S3 and MinIO (<c>Records[]</c>), Azure Event Grid <c>Microsoft.Storage.BlobCreated</c> (Event Grid or
    /// CloudEvents schema, single or array), and Google Cloud Storage Pub/Sub notifications (object resource).
    /// Events that aren't "object created" are ignored.
    /// </summary>
    /// <param name="notification">The notification body as delivered by the Dapr binding.</param>
    /// <returns>The created objects. Empty when the notification only holds other event types.</returns>
    /// <exception cref="BusinessValidationException">The body isn't JSON or isn't a known notification format.</exception>
    IReadOnlyList<StoredObjectDto> ParseCreatedObjects(string notification);
}

/// <summary>Pure notification parsing. No I/O and no cloud SDKs.</summary>
public sealed class ObjectNotificationEngine : IObjectNotificationEngine
{
    /// <summary>Event Grid event type for a new or overwritten blob.</summary>
    public const string BlobCreatedEventType = "Microsoft.Storage.BlobCreated";

    /// <inheritdoc />
    public IReadOnlyList<StoredObjectDto> ParseCreatedObjects(string notification)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(notification);
        }
        catch (JsonException ex)
        {
            throw Invalid("The notification is not valid JSON: " + ex.Message);
        }

        using (document)
        {
            try
            {
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Records", out var records))
                {
                    return ParseS3(records);
                }

                if (root.ValueKind == JsonValueKind.Array)
                {
                    return root.EnumerateArray().SelectMany<JsonElement, StoredObjectDto>(ParseEventGrid).ToList();
                }

                if (root.ValueKind == JsonValueKind.Object && IsEventGrid(root))
                {
                    return ParseEventGrid(root);
                }

                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("bucket", out _) && root.TryGetProperty("name", out _))
                {
                    return [ParseGcs(root)];
                }
            }
            catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or UriFormatException)
            {
                throw Invalid("The notification is missing a required field: " + ex.Message);
            }
        }

        throw Invalid("Unrecognized notification format. Expected Amazon S3 / MinIO, Azure Event Grid or Google Cloud Storage.");
    }

    /// <summary>Amazon S3 and MinIO: <c>{"Records":[{"eventName":"ObjectCreated:Put","s3":{"bucket":{"name":...},"object":{"key":...,"size":...}}}]}</c>.</summary>
    /// <param name="records">The <c>Records</c> array.</param>
    /// <returns>The created objects.</returns>
    private static List<StoredObjectDto> ParseS3(JsonElement records)
    {
        var created = new List<StoredObjectDto>();

        foreach (var record in records.EnumerateArray())
        {
            // AWS sends "ObjectCreated:Put", MinIO sends "s3:ObjectCreated:Put".
            var eventName = GetString(record, "eventName") ?? string.Empty;
            if (!eventName.Contains("ObjectCreated", StringComparison.Ordinal))
            {
                continue;
            }

            var s3 = record.GetProperty("s3");
            var bucket = s3.GetProperty("bucket").GetProperty("name").GetString() ?? string.Empty;
            var item = s3.GetProperty("object");

            // S3 keys are URL-encoded, with '+' for spaces.
            var key = Uri.UnescapeDataString((item.GetProperty("key").GetString() ?? string.Empty).Replace('+', ' '));
            long? size = item.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var bytes) ? bytes : null;

            created.Add(new StoredObjectDto(bucket, key, size));
        }

        return created;
    }

    /// <summary>True when the element looks like an Event Grid event (Event Grid or CloudEvents schema).</summary>
    /// <param name="element">A JSON object.</param>
    /// <returns><see langword="true"/> for an Event Grid-style event.</returns>
    private static bool IsEventGrid(JsonElement element) =>
        element.TryGetProperty("eventType", out _)
        || (element.TryGetProperty("type", out _) && element.TryGetProperty("data", out _));

    /// <summary>Azure Event Grid <c>Microsoft.Storage.BlobCreated</c>: the blob URL is <c>https://account.blob.core.windows.net/container/path</c>.</summary>
    /// <param name="element">One event.</param>
    /// <returns>The created blob, or nothing for other event types.</returns>
    private static List<StoredObjectDto> ParseEventGrid(JsonElement element)
    {
        var eventType = GetString(element, "eventType") ?? GetString(element, "type");
        if (!string.Equals(eventType, BlobCreatedEventType, StringComparison.Ordinal))
        {
            return [];
        }

        var data = element.GetProperty("data");
        var url = new Uri(data.GetProperty("url").GetString() ?? string.Empty);
        var segments = url.AbsolutePath.TrimStart('/').Split('/', 2);
        var container = segments[0];
        var key = segments.Length > 1 ? Uri.UnescapeDataString(segments[1]) : string.Empty;
        long? size = data.TryGetProperty("contentLength", out var sizeElement) && sizeElement.TryGetInt64(out var bytes) ? bytes : null;

        return [new StoredObjectDto(container, key, size)];
    }

    /// <summary>Google Cloud Storage Pub/Sub notification: the message data is the object resource (<c>bucket</c>, <c>name</c>, <c>size</c> as a string).</summary>
    /// <param name="element">The object resource.</param>
    /// <returns>The created object.</returns>
    private static StoredObjectDto ParseGcs(JsonElement element)
    {
        var bucket = GetString(element, "bucket") ?? string.Empty;
        var name = GetString(element, "name") ?? string.Empty;
        long? size = long.TryParse(GetString(element, "size"), out var bytes) ? bytes : null;

        return new StoredObjectDto(bucket, name, size);
    }

    /// <summary>Reads a string property, or <see langword="null"/> when it's missing or not a string.</summary>
    /// <param name="element">A JSON object.</param>
    /// <param name="name">Property name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Creates the exception for a notification that can't be used. Retrying won't fix it.</summary>
    /// <param name="message">What is wrong.</param>
    /// <returns>The exception to throw.</returns>
    private static BusinessValidationException Invalid(string message) =>
        new(new Dictionary<string, string[]> { ["notification"] = [message] });
}
