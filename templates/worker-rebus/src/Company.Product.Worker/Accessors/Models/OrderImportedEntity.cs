namespace Company.Product.Worker.Accessors.Models;

/// <summary>
/// The <c>OrderImported</c> event as it goes on the bus: a contract with subscribers, so treat changes like an API
/// change. Never leaves the Accessors namespace.
/// </summary>
/// <param name="OrderId">Identifier of the created order.</param>
/// <param name="CustomerName">Customer name.</param>
/// <param name="Total">Amount due.</param>
public sealed record OrderImportedEntity(Guid OrderId, string CustomerName, decimal Total);
