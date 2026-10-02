using Company.Product.Worker.Managers;
using Company.Product.Worker.Utilities.Options;
using Microsoft.Extensions.Options;

namespace Company.Product.Worker.Functions;

/// <summary>
/// FILE function (drop folder): polls <c>FileProcessing:DropFolder:Path</c>/incoming and imports each file. It works
/// with any folder the container can mount (volume, NFS, SMB, Azure Files, an SFTP landing folder), so it doesn't
/// need Dapr. Several replicas can share the folder safely, because each file is claimed with an atomic move.
/// </summary>
/// <param name="manager">File import use case.</param>
/// <param name="options">Drop folder settings (read on every poll, so changes apply without a restart).</param>
/// <param name="logger">Logger.</param>
public sealed class DropFolderFunction(
    IFileImportManager manager,
    IOptionsMonitor<DropFolderOptions> options,
    ILogger<DropFolderFunction> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = options.CurrentValue;

            if (settings.Enabled)
            {
                try
                {
                    // Drain everything that's ready, then wait for the next poll.
                    while (await manager.ImportNextDropFileAsync(stoppingToken))
                    {
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Drop folder import failed. The file stays in incoming/ and is retried on the next poll");
                }
            }

            await Task.Delay(settings.PollInterval, stoppingToken);
        }
    }
}
