using Company.Product.Scheduler.Engines;
using Company.Product.Scheduler.Managers.Models;

namespace Company.Product.Scheduler.Tests.Engines;

public class SalesSummaryEngineTests
{
    private static readonly DateTimeOffset To = new(2026, 1, 16, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset From = To.AddDays(-1);

    private readonly SalesSummaryEngine _engine = new();

    [Fact]
    public void Totals_only_orders_inside_the_window()
    {
        var orders = new List<OrderDto>
        {
            Order(10m, From),               // included (start is inclusive)
            Order(20.01m, To.AddHours(-1)), // included
            Order(99m, To),                 // excluded (end is exclusive)
            Order(99m, From.AddTicks(-1)),  // excluded
        };

        var summary = _engine.Summarize(orders, From, To);

        Assert.Equal(2, summary.OrderCount);
        Assert.Equal(30.01m, summary.Revenue);
        Assert.Equal(15.01m, summary.AverageOrderValue);
    }

    [Fact]
    public void Empty_window_has_zero_average()
    {
        var summary = _engine.Summarize([], From, To);

        Assert.Equal(0, summary.OrderCount);
        Assert.Equal(0m, summary.AverageOrderValue);
    }

    private static OrderDto Order(decimal total, DateTimeOffset createdAt) =>
        new(Guid.NewGuid(), "Contoso", 1, total, createdAt);
}
