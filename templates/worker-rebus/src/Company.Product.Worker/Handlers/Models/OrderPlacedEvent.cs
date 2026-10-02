namespace Company.Product.Worker.Handlers.Models;

/// <summary>
/// An order was created. The contract with publishers. Rebus identifies a message by its .NET type name, so the
/// publisher must use the same type (see the README, "Message contracts").
/// </summary>
/// <param name="OrderId">Identifier of the new order.</param>
public sealed record OrderPlacedEvent(Guid OrderId);
