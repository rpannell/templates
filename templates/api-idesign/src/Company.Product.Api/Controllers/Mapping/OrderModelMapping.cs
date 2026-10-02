using Company.Product.Api.Managers.Models;
using Company.Product.Api.Models;

namespace Company.Product.Api.Controllers.Mapping;

/// <summary>API model &lt;-&gt; Dto mapping. API models never go below the Controllers.</summary>
internal static class OrderModelMapping
{
    /// <summary>Maps a create-order request to its Dto.</summary>
    /// <param name="request">The request body.</param>
    /// <returns>The Dto passed to the Manager.</returns>
    public static CreateOrderDto ToDto(this CreateOrderRequest request) => new(
        request.CustomerName,
        (request.Lines ?? [])
            .Select(line => new CreateOrderLineDto(line.Sku, line.Quantity, line.UnitPrice))
            .ToList());

    /// <summary>Maps an order Dto to the API model.</summary>
    /// <param name="order">The order Dto.</param>
    /// <returns>The order as returned by the API.</returns>
    public static Order ToModel(this OrderDto order) => new(
        order.Id,
        order.CustomerName,
        order.Lines
            .Select(line => new OrderLine(
                line.LineNumber,
                line.Sku,
                line.Quantity,
                line.UnitPrice,
                line.LineTotal))
            .ToList(),
        order.Subtotal,
        order.Discount,
        order.Total,
        order.CreatedAt);

    /// <summary>Maps an availability Dto to the API model.</summary>
    /// <param name="availability">The availability Dto.</param>
    /// <returns>The availability as returned by the API.</returns>
    public static OrderAvailability ToModel(this OrderAvailabilityDto availability) => new(
        availability.OrderId,
        availability.CanFulfill,
        availability.Lines
            .Select(line => new OrderLineAvailability(
                line.LineNumber,
                line.Sku,
                line.Ordered,
                line.Available,
                line.IsAvailable))
            .ToList());
}
