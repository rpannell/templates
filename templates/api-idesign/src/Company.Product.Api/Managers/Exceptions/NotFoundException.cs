namespace Company.Product.Api.Managers.Exceptions;

/// <summary>A requested resource does not exist. Mapped to HTTP 404.</summary>
/// <param name="message">Description of what was not found.</param>
public class NotFoundException(string message) : Exception(message);

/// <summary>The requested order does not exist.</summary>
/// <param name="orderId">Identifier of the missing order.</param>
public sealed class OrderNotFoundException(Guid orderId)
    : NotFoundException($"Order '{orderId}' was not found.")
{
    /// <summary>Identifier of the missing order.</summary>
    public Guid OrderId { get; } = orderId;
}
