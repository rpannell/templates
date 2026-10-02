using Company.Product.Api.Managers.Models;

namespace Company.Product.Api.Engines;

/// <summary>Fulfillment rules for orders.</summary>
public interface IOrderFulfillmentEngine
{
    /// <summary>Decides whether stock covers each line of an order.</summary>
    /// <param name="order">The order to check.</param>
    /// <param name="stockLevels">Units on hand per SKU. SKUs that are missing count as zero.</param>
    /// <returns>Per-line availability and whether the whole order can be fulfilled.</returns>
    OrderAvailabilityDto CheckAvailability(OrderDto order, IReadOnlyList<StockLevelDto> stockLevels);
}

/// <summary>Pure fulfillment rules. No I/O.</summary>
public sealed class OrderFulfillmentEngine : IOrderFulfillmentEngine
{
    /// <inheritdoc />
    public OrderAvailabilityDto CheckAvailability(OrderDto order, IReadOnlyList<StockLevelDto> stockLevels)
    {
        // Lines that share a SKU draw from the same stock, in line order.
        var remaining = stockLevels
            .GroupBy(stock => stock.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(stock => stock.Available), StringComparer.OrdinalIgnoreCase);

        var lines = new List<OrderLineAvailabilityDto>(order.Lines.Count);

        foreach (var line in order.Lines.OrderBy(line => line.LineNumber))
        {
            var available = remaining.GetValueOrDefault(line.Sku);
            var isAvailable = available >= line.Quantity;

            lines.Add(new OrderLineAvailabilityDto(line.LineNumber, line.Sku, line.Quantity, available, isAvailable));
            remaining[line.Sku] = Math.Max(available - line.Quantity, 0);
        }

        return new OrderAvailabilityDto(order.Id, lines.TrueForAll(line => line.IsAvailable), lines);
    }
}
