namespace Company.Product.Api.Managers.Models;

/// <summary>Whether current stock can fulfill an order.</summary>
/// <param name="OrderId">Identifier of the order.</param>
/// <param name="CanFulfill">True when every line is available.</param>
/// <param name="Lines">Availability of each line, in line-number order.</param>
public sealed record OrderAvailabilityDto(
    Guid OrderId,
    bool CanFulfill,
    IReadOnlyList<OrderLineAvailabilityDto> Lines);

/// <summary>Whether current stock covers one order line.</summary>
/// <param name="LineNumber">1-based position of the line on the order.</param>
/// <param name="Sku">Stock keeping unit.</param>
/// <param name="Ordered">Units ordered on this line.</param>
/// <param name="Available">Units left for this line after earlier lines with the same SKU.</param>
/// <param name="IsAvailable">True when <paramref name="Available"/> covers <paramref name="Ordered"/>.</param>
public sealed record OrderLineAvailabilityDto(
    int LineNumber,
    string Sku,
    int Ordered,
    int Available,
    bool IsAvailable);
