using Company.Product.Api.Engines;
using Company.Product.Api.Managers.Models;

namespace Company.Product.Api.Tests.Engines;

public class OrderFulfillmentEngineTests
{
    private readonly OrderFulfillmentEngine _engine = new();

    [Fact]
    public void CheckAvailability_fulfills_when_stock_covers_every_line()
    {
        var order = Order(("A-1", 2), ("B-2", 1));

        var result = _engine.CheckAvailability(order, [new StockLevelDto("A-1", 5), new StockLevelDto("B-2", 1)]);

        Assert.True(result.CanFulfill);
        Assert.All(result.Lines, line => Assert.True(line.IsAvailable));
    }

    [Fact]
    public void CheckAvailability_flags_short_line()
    {
        var order = Order(("A-1", 3));

        var result = _engine.CheckAvailability(order, [new StockLevelDto("A-1", 2)]);

        Assert.False(result.CanFulfill);
        Assert.False(result.Lines[0].IsAvailable);
        Assert.Equal(2, result.Lines[0].Available);
    }

    [Fact]
    public void CheckAvailability_treats_unknown_sku_as_zero()
    {
        var order = Order(("A-1", 1));

        var result = _engine.CheckAvailability(order, []);

        Assert.False(result.CanFulfill);
        Assert.Equal(0, result.Lines[0].Available);
    }

    [Fact]
    public void CheckAvailability_shares_stock_between_lines_with_same_sku()
    {
        var order = Order(("A-1", 3), ("A-1", 3));

        var result = _engine.CheckAvailability(order, [new StockLevelDto("a-1", 4)]);

        Assert.True(result.Lines[0].IsAvailable);
        Assert.False(result.Lines[1].IsAvailable);
        Assert.Equal(1, result.Lines[1].Available);
    }

    private static OrderDto Order(params (string Sku, int Quantity)[] lines) => new()
    {
        Id = Guid.NewGuid(),
        CustomerName = "Contoso",
        Lines = lines.Select((line, index) => new OrderLineDto(index + 1, line.Sku, line.Quantity, 1m, line.Quantity)).ToList(),
        Subtotal = 0m,
        Discount = 0m,
        Total = 0m,
        CreatedAt = DateTimeOffset.UnixEpoch,
    };
}
