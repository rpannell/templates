using Company.Product.Worker.Accessors.Exceptions;
using Company.Product.Worker.Managers.Models;
using Company.Product.Worker.Utilities.Options;
using Microsoft.Extensions.Options;

namespace Company.Product.Worker.Accessors.Files;

/// <summary>A drop folder: files land in <c>incoming/</c> and end up in <c>processed/</c> or <c>failed/</c>.</summary>
public interface IDropFolderAccessor
{
    /// <summary>
    /// Claims the next file by moving it from <c>incoming/</c> to <c>processing/</c>. A move is atomic, so when
    /// several worker instances share the folder, each file is claimed by exactly one of them.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The claimed file, or <see langword="null"/> when there's nothing ready.</returns>
    /// <exception cref="ResourceAccessException">The folder can't be read.</exception>
    Task<DropFileDto?> ClaimNextAsync(CancellationToken cancellationToken);

    /// <summary>Moves a claimed file to <c>processed/</c>.</summary>
    /// <param name="file">The claimed file.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the file is moved.</returns>
    Task CompleteAsync(DropFileDto file, CancellationToken cancellationToken);

    /// <summary>Moves a claimed file to <c>failed/</c> and writes the reason next to it (<c>.error.txt</c>).</summary>
    /// <param name="file">The claimed file.</param>
    /// <param name="reason">Why it was rejected.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the file is moved.</returns>
    Task FailAsync(DropFileDto file, string reason, CancellationToken cancellationToken);

    /// <summary>Puts a claimed file back in <c>incoming/</c> so it's retried (after a transient failure).</summary>
    /// <param name="file">The claimed file.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the file is moved.</returns>
    Task ReleaseAsync(DropFileDto file, CancellationToken cancellationToken);
}

/// <summary>File-system drop folder: a local path, mounted volume, network share (NFS, SMB, Azure Files) or SFTP landing folder.</summary>
/// <param name="options">Folder path and file-age settings.</param>
/// <param name="timeProvider">Source of the current time.</param>
public sealed class DropFolderAccessor(IOptionsMonitor<DropFolderOptions> options, TimeProvider timeProvider) : IDropFolderAccessor
{
    /// <summary>Where new files are dropped.</summary>
    public const string IncomingFolder = "incoming";

    /// <summary>Files claimed by a worker instance.</summary>
    public const string ProcessingFolder = "processing";

    /// <summary>Successfully processed files.</summary>
    public const string ProcessedFolder = "processed";

    /// <summary>Rejected files, each with an <c>.error.txt</c>.</summary>
    public const string FailedFolder = "failed";

    /// <inheritdoc />
    public async Task<DropFileDto?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;

        try
        {
            var incoming = EnsureFolder(settings.Path, IncomingFolder);
            var processing = EnsureFolder(settings.Path, ProcessingFolder);
            var cutoff = timeProvider.GetUtcNow().UtcDateTime - settings.MinimumFileAge;

            foreach (var path in Directory.EnumerateFiles(incoming).Order(StringComparer.Ordinal))
            {
                var name = Path.GetFileName(path);
                if (IsIgnored(name) || File.GetLastWriteTimeUtc(path) > cutoff)
                {
                    continue;
                }

                var claimed = Path.Combine(processing, name);
                try
                {
                    File.Move(path, claimed, overwrite: false);
                }
                catch (IOException)
                {
                    // Another instance claimed it first, a writer still holds it, or a same-named file is in processing.
                    continue;
                }

                var content = await File.ReadAllTextAsync(claimed, cancellationToken);
                return new DropFileDto(name, claimed, content);
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ResourceAccessException($"Could not read the drop folder '{settings.Path}'.", ex);
        }
    }

    /// <inheritdoc />
    public Task CompleteAsync(DropFileDto file, CancellationToken cancellationToken)
    {
        MoveTo(file, ProcessedFolder);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task FailAsync(DropFileDto file, string reason, CancellationToken cancellationToken)
    {
        var target = MoveTo(file, FailedFolder);
        await File.WriteAllTextAsync(target + ".error.txt", reason, cancellationToken);
    }

    /// <inheritdoc />
    public Task ReleaseAsync(DropFileDto file, CancellationToken cancellationToken)
    {
        var incoming = EnsureFolder(options.CurrentValue.Path, IncomingFolder);
        File.Move(file.ClaimedPath, Path.Combine(incoming, file.Name), overwrite: false);
        return Task.CompletedTask;
    }

    /// <summary>Moves a claimed file into a result folder, prefixed with a UTC timestamp so names never clash.</summary>
    /// <param name="file">The claimed file.</param>
    /// <param name="folder">Result folder name.</param>
    /// <returns>The new path.</returns>
    private string MoveTo(DropFileDto file, string folder)
    {
        var destination = EnsureFolder(options.CurrentValue.Path, folder);
        var stamp = timeProvider.GetUtcNow().ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var target = Path.Combine(destination, $"{stamp}-{file.Name}");
        File.Move(file.ClaimedPath, target, overwrite: false);
        return target;
    }

    /// <summary>Creates a subfolder of the drop folder if needed.</summary>
    /// <param name="root">Drop folder root.</param>
    /// <param name="name">Subfolder name.</param>
    /// <returns>The subfolder path.</returns>
    private static string EnsureFolder(string root, string name) =>
        Directory.CreateDirectory(Path.Combine(root, name)).FullName;

    /// <summary>
    /// Skips hidden files and files still being uploaded under a temporary name. Writers should upload as
    /// <c>name.tmp</c> or <c>name.partial</c> and rename when done.
    /// </summary>
    /// <param name="name">File name.</param>
    /// <returns><see langword="true"/> to leave the file alone.</returns>
    private static bool IsIgnored(string name) =>
        name.StartsWith('.')
        || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".partial", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".filepart", StringComparison.OrdinalIgnoreCase);
}
