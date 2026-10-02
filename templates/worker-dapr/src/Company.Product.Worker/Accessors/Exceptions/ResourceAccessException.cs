namespace Company.Product.Worker.Accessors.Exceptions;

/// <summary>
/// A backing resource (the API, object storage, the drop folder, Dapr) failed. It's transient, so the
/// message is retried by Dapr and a drop-folder file goes back to <c>incoming/</c>.
/// </summary>
/// <param name="message">What the Accessor was trying to do.</param>
/// <param name="innerException">The underlying failure.</param>
public sealed class ResourceAccessException(string message, Exception innerException)
    : Exception(message, innerException);
