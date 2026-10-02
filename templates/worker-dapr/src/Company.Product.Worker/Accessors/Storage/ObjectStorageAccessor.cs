using System.Text;
using Company.Product.Worker.Accessors.Exceptions;
using Company.Product.Worker.Utilities.Options;
using Dapr;
using Dapr.Client;
using Microsoft.Extensions.Options;

namespace Company.Product.Worker.Accessors.Storage;

/// <summary>Object storage (S3, Azure Blob, GCS, MinIO, local files), reached through a Dapr output binding.</summary>
public interface IObjectStorageAccessor
{
    /// <summary>Reads an object as UTF-8 text.</summary>
    /// <param name="key">Object key or blob name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The object content.</returns>
    /// <exception cref="ResourceAccessException">The binding call failed (missing object, no access, sidecar down).</exception>
    Task<string> ReadTextAsync(string key, CancellationToken cancellationToken);
}

/// <summary>
/// Reads objects with the binding's <c>get</c> operation. Which store it reads from is decided by the Dapr component
/// named in <see cref="ObjectStorageOptions.BindingName"/>, so there are no cloud SDKs here.
/// </summary>
/// <param name="daprClient">Dapr sidecar client.</param>
/// <param name="options">Binding name and key metadata name.</param>
public sealed class ObjectStorageAccessor(DaprClient daprClient, IOptionsMonitor<ObjectStorageOptions> options) : IObjectStorageAccessor
{
    /// <inheritdoc />
    public async Task<string> ReadTextAsync(string key, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        var request = new BindingRequest(settings.BindingName, "get");
        request.Metadata[settings.KeyMetadataName] = key;

        try
        {
            var response = await daprClient.InvokeBindingAsync(request, cancellationToken);
            return Encoding.UTF8.GetString(response.Data.Span);
        }
        catch (DaprException ex)
        {
            throw new ResourceAccessException($"Could not read '{key}' through the '{settings.BindingName}' binding.", ex);
        }
    }
}
