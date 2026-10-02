using Company.Product.Scheduler.Managers.Models;

namespace Company.Product.Scheduler.Engines;

/// <summary>Sales calculations.</summary>
public interface ISalesSummaryEngine
{
    /// <summary>Totals the orders created in a time window.</summary>
    /// <param name="orders">Orders to consider. Orders outside the window are ignored.</param>
    /// <param name="from">Window start (inclusive).</param>
    /// <param name="to">Window end (exclusive).</param>
    /// <returns>Count, revenue and average order value for the window.</returns>
    SalesSummaryDto Summarize(IReadOnlyList<OrderDto> orders, DateTimeOffset from, DateTimeOffset to);
}

/// <summary>Pure sales calculations. No I/O.</summary>
public sealed class SalesSummaryEngine : ISalesSummaryEngine
{
    /// <inheritdoc />
    public SalesSummaryDto Summarize(IReadOnlyList<OrderDto> orders, DateTimeOffset from, DateTimeOffset to)
    {
        var inWindow = orders.Where(order => order.CreatedAt >= from && order.CreatedAt < to).ToList();
        var revenue = inWindow.Sum(order => order.Total);
        var average = inWindow.Count == 0
            ? 0m
            : Math.Round(revenue / inWindow.Count, 2, MidpointRounding.AwayFromZero);

        return new SalesSummaryDto(from, to, inWindow.Count, revenue, average);
    }
}
