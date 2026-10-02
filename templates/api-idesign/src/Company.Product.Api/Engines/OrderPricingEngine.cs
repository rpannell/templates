using Company.Product.Api.Engines.Options;
using Company.Product.Api.Managers.Models;
using Microsoft.Extensions.Options;

namespace Company.Product.Api.Engines;

/// <summary>Order pricing calculations.</summary>
public interface IOrderPricingEngine
{
    /// <summary>Calculates line totals, discount and total for a validated order.</summary>
    /// <param name="orderId">Identifier to give the priced order.</param>
    /// <param name="order">A validated order.</param>
    /// <param name="createdAt">Creation time to stamp on the order.</param>
    /// <returns>The priced order, with line numbers assigned and names trimmed.</returns>
    OrderDto Price(Guid orderId, CreateOrderDto order, DateTimeOffset createdAt);
}

/// <summary>Pure pricing calculations. No I/O.</summary>
/// <param name="options">Pricing configuration, read on every call so changes apply without a restart.</param>
public sealed class OrderPricingEngine(IOptionsMonitor<PricingOptions> options) : IOrderPricingEngine
{
    /// <inheritdoc />
    public OrderDto Price(Guid orderId, CreateOrderDto order, DateTimeOffset createdAt)
    {
        var pricing = options.CurrentValue;

        var lines = order.Lines
            .Select((line, index) => new OrderLineDto(
                LineNumber: index + 1,
                Sku: line.Sku.Trim(),
                Quantity: line.Quantity,
                UnitPrice: line.UnitPrice,
                LineTotal: RoundMoney(line.Quantity * line.UnitPrice)))
            .ToList();

        var subtotal = lines.Sum(line => line.LineTotal);

        var discount = subtotal >= pricing.DiscountThreshold
            ? RoundMoney(subtotal * pricing.DiscountPercent / 100m)
            : 0m;

        return new OrderDto
        {
            Id = orderId,
            CustomerName = order.CustomerName.Trim(),
            Lines = lines,
            Subtotal = subtotal,
            Discount = discount,
            Total = subtotal - discount,
            CreatedAt = createdAt,
        };
    }

    /// <summary>Rounds to cents, with midpoints rounded away from zero.</summary>
    /// <param name="value">Amount to round.</param>
    /// <returns>The rounded amount.</returns>
    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
