namespace Company.Product.Api.Accessors.Exceptions;

/// <summary>A backing resource (database, external API, cache) failed. Mapped to HTTP 503.</summary>
/// <param name="message">What the Accessor was trying to do.</param>
/// <param name="innerException">The underlying infrastructure failure.</param>
public sealed class ResourceAccessException(string message, Exception innerException)
    : Exception(message, innerException);
