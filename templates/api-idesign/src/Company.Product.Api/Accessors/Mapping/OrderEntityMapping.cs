using Company.Product.Api.Accessors.Models;
using Company.Product.Api.Managers.Models;

namespace Company.Product.Api.Accessors.Mapping;

/// <summary>Dto &lt;-&gt; Entity mapping. Entities are created and consumed only inside Accessors.</summary>
internal static class OrderEntityMapping
{
    /// <summary>Maps an order Dto to its table rows.</summary>
    /// <param name="order">The order to map.</param>
    /// <returns>The order entity with its line entities.</returns>
    public static OrderEntity ToEntity(this OrderDto order) => new()
    {
        Id = order.Id,
        CustomerName = order.CustomerName,
        Subtotal = order.Subtotal,
        Discount = order.Discount,
        Total = order.Total,
        CreatedAt = order.CreatedAt,
        Lines = order.Lines
            .Select(line => new OrderLineEntity
            {
                OrderId = order.Id,
                LineNumber = line.LineNumber,
                Sku = line.Sku,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                LineTotal = line.LineTotal,
            })
            .ToList(),
    };

    /// <summary>Maps order rows to a Dto.</summary>
    /// <param name="order">The order entity, with lines loaded.</param>
    /// <returns>The order Dto, lines in line-number order.</returns>
    public static OrderDto ToDto(this OrderEntity order) => new()
    {
        Id = order.Id,
        CustomerName = order.CustomerName,
        Subtotal = order.Subtotal,
        Discount = order.Discount,
        Total = order.Total,
        CreatedAt = order.CreatedAt,
        Lines = order.Lines
            .OrderBy(line => line.LineNumber)
            .Select(line => new OrderLineDto(
                line.LineNumber,
                line.Sku,
                line.Quantity,
                line.UnitPrice,
                line.LineTotal))
            .ToList(),
    };
}
