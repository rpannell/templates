using System.Diagnostics.Metrics;
using Company.Product.Worker.Accessors.Files;
using Company.Product.Worker.Accessors.Http;
using Company.Product.Worker.Accessors.Storage;
using Company.Product.Worker.Engines;
using Company.Product.Worker.Managers.Exceptions;
using Company.Product.Worker.Managers.Models;
using Company.Product.Worker.Utilities.Telemetry;

namespace Company.Product.Worker.Managers;

/// <summary>Imports order files, whether they arrive in object storage or in a drop folder.</summary>
public interface IFileImportManager
{
    /// <summary>
    /// Handles an object storage "file created" notification: reads each created object and imports it.
    /// </summary>
    /// <param name="notification">The notification body (S3/MinIO, Azure Event Grid or GCS format).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One result per imported file.</returns>
    /// <exception cref="BusinessValidationException">The notification or a file is invalid. Retrying won't help.</exception>
    Task<IReadOnlyList<FileImportResultDto>> ImportFromNotificationAsync(string notification, CancellationToken cancellationToken);

    /// <summary>
    /// Claims and imports the next file in the drop folder. A valid file goes to <c>processed/</c>, an invalid one to
    /// <c>failed/</c>. After a transient failure the file goes back to <c>incoming/</c> and the exception is rethrown.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> if a file was handled, <see langword="false"/> when the folder had nothing ready.</returns>
    Task<bool> ImportNextDropFileAsync(CancellationToken cancellationToken);
}

/// <summary>
/// File import use case: parse (Engine), then create each order through the orders API (Accessor).
/// Sources differ only in how the file is fetched and where it ends up.
/// </summary>
/// <param name="notificationEngine">Reads object storage notifications.</param>
/// <param name="csvEngine">Parses and validates order files.</param>
/// <param name="objectStorage">Reads objects.</param>
/// <param name="dropFolder">Claims and moves drop-folder files.</param>
/// <param name="ordersApi">Creates orders.</param>
/// <param name="logger">Logger.</param>
public sealed class FileImportManager(
    IObjectNotificationEngine notificationEngine,
    ICsvOrderImportEngine csvEngine,
    IObjectStorageAccessor objectStorage,
    IDropFolderAccessor dropFolder,
    IOrdersApiAccessor ordersApi,
    ILogger<FileImportManager> logger) : IFileImportManager
{
    private static readonly Counter<long> FilesImported = ApplicationTelemetry.Meter.CreateCounter<long>(
        "files.imported", unit: "{file}", description: "Files handled, by source and outcome.");

    private static readonly Counter<long> OrdersImported = ApplicationTelemetry.Meter.CreateCounter<long>(
        "orders.imported", unit: "{order}", description: "Orders created from imported files.");

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileImportResultDto>> ImportFromNotificationAsync(string notification, CancellationToken cancellationToken)
    {
        var createdObjects = notificationEngine.ParseCreatedObjects(notification);
        var results = new List<FileImportResultDto>(createdObjects.Count);

        foreach (var stored in createdObjects)
        {
            var content = await objectStorage.ReadTextAsync(stored.Key, cancellationToken);
            results.Add(await ImportAsync(stored.Key, content, "object-storage", cancellationToken));
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<bool> ImportNextDropFileAsync(CancellationToken cancellationToken)
    {
        var file = await dropFolder.ClaimNextAsync(cancellationToken);
        if (file is null)
        {
            return false;
        }

        try
        {
            await ImportAsync(file.Name, file.Content, "drop-folder", cancellationToken);
            await dropFolder.CompleteAsync(file, cancellationToken);
        }
        catch (BusinessValidationException ex)
        {
            await dropFolder.FailAsync(file, ex.Message, cancellationToken);
        }
        catch
        {
            // Transient (API down, cancelled): put the file back so the next poll retries it.
            await dropFolder.ReleaseAsync(file, CancellationToken.None);
            throw;
        }

        return true;
    }

    /// <summary>Parses a file and creates its orders.</summary>
    /// <param name="fileName">File name or object key, for logs.</param>
    /// <param name="content">File content.</param>
    /// <param name="source">Trigger that delivered the file, for logs and metrics.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The import result.</returns>
    private async Task<FileImportResultDto> ImportAsync(string fileName, string content, string source, CancellationToken cancellationToken)
    {
        IReadOnlyList<ImportOrderDto> orders;
        try
        {
            orders = csvEngine.Parse(content);
        }
        catch (BusinessValidationException ex)
        {
            FilesImported.Add(1, new KeyValuePair<string, object?>("source", source), new KeyValuePair<string, object?>("outcome", "rejected"));
            logger.LogWarning("Rejected {FileName} from {Source}: {Reason}", fileName, source, ex.Message);
            throw;
        }

        var created = 0;
        foreach (var order in orders)
        {
            await ordersApi.CreateAsync(order, cancellationToken);
            created++;
        }

        FilesImported.Add(1, new KeyValuePair<string, object?>("source", source), new KeyValuePair<string, object?>("outcome", "imported"));
        OrdersImported.Add(created, new KeyValuePair<string, object?>("source", source));
        logger.LogInformation("Imported {OrderCount} order(s) from {FileName} ({Source})", created, fileName, source);

        return new FileImportResultDto(fileName, created);
    }
}
