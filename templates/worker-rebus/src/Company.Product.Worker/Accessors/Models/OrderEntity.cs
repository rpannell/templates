namespace Company.Product.Worker.Accessors.Models;

/// <summary>An order as the orders API returns it (JSON shape). Never leaves the Accessors namespace.</summary>
/// <param name="Id">Order identifier.</param>
/// <param name="CustomerName">Customer name.</param>
/// <param name="Lines">Order lines.</param>
/// <param name="Subtotal">Sum of line totals.</param>
/// <param name="Discount">Discount applied.</param>
/// <param name="Total">Amount due.</param>
/// <param name="CreatedAt">Creation time (UTC).</param>
public sealed record OrderEntity(
    Guid Id,
    string CustomerName,
    IReadOnlyList<OrderLineEntity> Lines,
    decimal Subtotal,
    decimal Discount,
    decimal Total,
    DateTimeOffset CreatedAt);

/// <summary>An order line as the orders API returns it.</summary>
/// <param name="LineNumber">1-based line position.</param>
/// <param name="Sku">Stock keeping unit.</param>
/// <param name="Quantity">Units.</param>
/// <param name="UnitPrice">Price per unit.</param>
/// <param name="LineTotal">Quantity times price.</param>
public sealed record OrderLineEntity(int LineNumber, string Sku, int Quantity, decimal UnitPrice, decimal LineTotal);
