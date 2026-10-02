namespace Company.Product.Scheduler.Accessors.Exceptions;

/// <summary>
/// A backing resource (the orders API) failed. The job run is recorded as failed, and the next scheduled run tries
/// again.
/// </summary>
/// <param name="message">What the Accessor was trying to do.</param>
/// <param name="innerException">The underlying failure.</param>
public sealed class ResourceAccessException(string message, Exception innerException)
    : Exception(message, innerException);
