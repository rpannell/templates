namespace Company.Product.Worker.Handlers.Models;

/// <summary>
/// Command: create this order through the orders API. Sent point-to-point to this worker's input queue.
/// </summary>
/// <param name="Reference">The sender's own order reference, used in logs and error messages.</param>
/// <param name="CustomerName">Customer placing the order.</param>
/// <param name="Lines">Order lines.</param>
public sealed record ImportOrderCommand(string Reference, string CustomerName, IReadOnlyList<ImportOrderLine> Lines);

/// <summary>A line of an <see cref="ImportOrderCommand"/>.</summary>
/// <param name="Sku">Stock keeping unit.</param>
/// <param name="Quantity">Units ordered.</param>
/// <param name="UnitPrice">Price per unit.</param>
public sealed record ImportOrderLine(string Sku, int Quantity, decimal UnitPrice);
