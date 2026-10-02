namespace Company.Product.Worker.Utilities.Options;

/// <summary>
/// Drop-folder file trigger settings, bound from <c>FileProcessing:DropFolder</c>. The folder gets
/// <c>incoming/</c>, <c>processing/</c>, <c>processed/</c> and <c>failed/</c> subfolders.
/// </summary>
public sealed class DropFolderOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "FileProcessing:DropFolder";

    /// <summary>Turns the drop-folder trigger on. Off by default, so only deployments with a mounted folder poll.</summary>
    public bool Enabled { get; set; }

    /// <summary>Root folder: a mounted volume, network share or SFTP landing folder.</summary>
    public string Path { get; set; } = "/data/drop";

    /// <summary>How often to look for new files once the folder is empty.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Files younger than this are skipped, so a file still being copied is never picked up half-written.</summary>
    public TimeSpan MinimumFileAge { get; set; } = TimeSpan.FromSeconds(5);
}
