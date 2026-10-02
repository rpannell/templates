namespace Company.Product.Scheduler.Managers.Models;

/// <summary>Sales totals for a time window.</summary>
/// <param name="From">Start of the window (inclusive).</param>
/// <param name="To">End of the window (exclusive).</param>
/// <param name="OrderCount">Orders created in the window.</param>
/// <param name="Revenue">Sum of order totals.</param>
/// <param name="AverageOrderValue">Revenue divided by order count, rounded to cents (0 when there are no orders).</param>
public sealed record SalesSummaryDto(DateTimeOffset From, DateTimeOffset To, int OrderCount, decimal Revenue, decimal AverageOrderValue);
