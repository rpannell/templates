namespace Company.Product.Worker.Managers.Models;

/// <summary>A drop-folder file this worker instance has claimed (moved to <c>processing/</c>).</summary>
/// <param name="Name">Original file name.</param>
/// <param name="ClaimedPath">Full path of the file in <c>processing/</c>.</param>
/// <param name="Content">File content (UTF-8 text).</param>
public sealed record DropFileDto(string Name, string ClaimedPath, string Content);
