namespace Company.Product.Api.Models;

/// <summary>Whether current stock can fulfill an order.</summary>
/// <param name="OrderId">Identifier of the order.</param>
/// <param name="CanFulfill">True when every line is available.</param>
/// <param name="Lines">Availability of each line, in line-number order.</param>
public sealed record OrderAvailability(
    Guid OrderId,
    bool CanFulfill,
    IReadOnlyList<OrderLineAvailability> Lines);

/// <summary>Whether current stock covers one order line.</summary>
/// <param name="LineNumber">1-based position of the line on the order.</param>
/// <param name="Sku">Stock keeping unit.</param>
/// <param name="Ordered">Units ordered on this line.</param>
/// <param name="Available">Units left for this line after earlier lines with the same SKU.</param>
/// <param name="IsAvailable">True when available units cover the ordered units.</param>
public sealed record OrderLineAvailability(
    int LineNumber,
    string Sku,
    int Ordered,
    int Available,
    bool IsAvailable);
