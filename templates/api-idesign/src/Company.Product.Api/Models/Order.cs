namespace Company.Product.Api.Models;

/// <summary>An order as returned by the API.</summary>
/// <param name="Id">Unique order identifier.</param>
/// <param name="CustomerName">Name of the customer who placed the order.</param>
/// <param name="Lines">Order lines, in line-number order.</param>
/// <param name="Subtotal">Sum of all line totals before discount.</param>
/// <param name="Discount">Discount applied to the subtotal.</param>
/// <param name="Total">Amount due: subtotal minus discount.</param>
/// <param name="CreatedAt">When the order was created (UTC).</param>
public sealed record Order(
    Guid Id,
    string CustomerName,
    IReadOnlyList<OrderLine> Lines,
    decimal Subtotal,
    decimal Discount,
    decimal Total,
    DateTimeOffset CreatedAt);

/// <summary>A single line on an order.</summary>
/// <param name="LineNumber">1-based position of the line on the order.</param>
/// <param name="Sku">Stock keeping unit of the product.</param>
/// <param name="Quantity">Number of units ordered.</param>
/// <param name="UnitPrice">Price per unit.</param>
/// <param name="LineTotal">Quantity times unit price, rounded to cents.</param>
public sealed record OrderLine(
    int LineNumber,
    string Sku,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);
