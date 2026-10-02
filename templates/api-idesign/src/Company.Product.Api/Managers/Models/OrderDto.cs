namespace Company.Product.Api.Managers.Models;

/// <summary>Internal representation of a priced order, passed between Managers, Engines and Accessors.</summary>
public sealed record OrderDto
{
    /// <summary>Unique order identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Name of the customer who placed the order.</summary>
    public required string CustomerName { get; init; }

    /// <summary>Order lines, in line-number order.</summary>
    public required IReadOnlyList<OrderLineDto> Lines { get; init; }

    /// <summary>Sum of all line totals before discount.</summary>
    public required decimal Subtotal { get; init; }

    /// <summary>Discount applied to the subtotal.</summary>
    public required decimal Discount { get; init; }

    /// <summary>Amount due: subtotal minus discount.</summary>
    public required decimal Total { get; init; }

    /// <summary>When the order was created (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Internal representation of a priced order line.</summary>
/// <param name="LineNumber">1-based position of the line on the order.</param>
/// <param name="Sku">Stock keeping unit of the product.</param>
/// <param name="Quantity">Number of units ordered.</param>
/// <param name="UnitPrice">Price per unit.</param>
/// <param name="LineTotal">Quantity times unit price, rounded to cents.</param>
public sealed record OrderLineDto(
    int LineNumber,
    string Sku,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);
