namespace Company.Product.Worker.Accessors.Exceptions;

/// <summary>
/// A backing resource (the orders API, the message broker) failed. It's transient, so the handler lets it
/// propagate and Rebus redelivers the message, up to <c>Messaging:MaxDeliveryAttempts</c> times.
/// </summary>
/// <param name="message">What the Accessor was trying to do.</param>
/// <param name="innerException">The underlying failure.</param>
public sealed class ResourceAccessException(string message, Exception innerException)
    : Exception(message, innerException);
