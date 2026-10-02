namespace Company.Product.Worker.Utilities.Options;

/// <summary>Object storage settings, bound from <c>FileProcessing:ObjectStorage</c>.</summary>
public sealed class ObjectStorageOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "FileProcessing:ObjectStorage";

    /// <summary>Name of the Dapr output binding that reads objects (S3, Azure Blob, GCS, local storage...).</summary>
    public string BindingName { get; set; } = "object-storage";

    /// <summary>
    /// Metadata key the binding's <c>get</c> operation uses for the object name: <c>key</c> for S3 and GCS,
    /// <c>blobName</c> for Azure Blob, <c>fileName</c> for Dapr local storage.
    /// </summary>
    public string KeyMetadataName { get; set; } = "key";
}
