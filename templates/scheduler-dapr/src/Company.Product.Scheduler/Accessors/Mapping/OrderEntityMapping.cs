using Company.Product.Scheduler.Accessors.Models;
using Company.Product.Scheduler.Managers.Models;

namespace Company.Product.Scheduler.Accessors.Mapping;

/// <summary>Orders API JSON shapes to Dtos. Entities are created and consumed only inside Accessors.</summary>
internal static class OrderEntityMapping
{
    /// <summary>Maps an API order to the scheduler's Dto.</summary>
    /// <param name="order">The API order.</param>
    /// <returns>The order Dto.</returns>
    public static OrderDto ToDto(this OrderEntity order) =>
        new(order.Id, order.CustomerName, order.Lines.Count, order.Total, order.CreatedAt);
}
