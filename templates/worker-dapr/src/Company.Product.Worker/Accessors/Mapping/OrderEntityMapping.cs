using Company.Product.Worker.Accessors.Models;
using Company.Product.Worker.Managers.Models;

namespace Company.Product.Worker.Accessors.Mapping;

/// <summary>Dto &lt;-&gt; orders API JSON shapes. Entities are created and consumed only inside Accessors.</summary>
internal static class OrderEntityMapping
{
    /// <summary>Maps an imported order to the API's create request.</summary>
    /// <param name="order">The imported order.</param>
    /// <returns>The request body.</returns>
    public static CreateOrderRequestEntity ToRequestEntity(this ImportOrderDto order) => new(
        order.CustomerName,
        order.Lines.Select(line => new CreateOrderLineRequestEntity(line.Sku, line.Quantity, line.UnitPrice)).ToList());

    /// <summary>Maps an API order to the worker's Dto.</summary>
    /// <param name="order">The API order.</param>
    /// <returns>The order Dto.</returns>
    public static OrderDto ToDto(this OrderEntity order) =>
        new(order.Id, order.CustomerName, order.Lines.Count, order.Total, order.CreatedAt);
}
