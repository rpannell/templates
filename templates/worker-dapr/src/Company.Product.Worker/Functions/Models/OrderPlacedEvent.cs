namespace Company.Product.Worker.Functions.Models;

/// <summary>The <c>order-placed</c> event: an order was created. The contract with publishers.</summary>
/// <param name="OrderId">Identifier of the new order.</param>
public sealed record OrderPlacedEvent(Guid OrderId);
