using Company.Product.Api.Engines;
using Company.Product.Api.Engines.Options;
using Company.Product.Api.Managers.Models;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Company.Product.Api.Tests.Engines;

public class OrderPricingEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Price_calculates_line_totals_and_subtotal()
    {
        var engine = CreateEngine();
        var order = new CreateOrderDto("Contoso", [
            new CreateOrderLineDto("A-1", 2, 10.00m),
            new CreateOrderLineDto("B-2", 3, 1.50m),
        ]);

        var priced = engine.Price(Guid.NewGuid(), order, Now);

        Assert.Equal(new[] { 1, 2 }, priced.Lines.Select(l => l.LineNumber));
        Assert.Equal(new[] { 20.00m, 4.50m }, priced.Lines.Select(l => l.LineTotal));
        Assert.Equal(24.50m, priced.Subtotal);
        Assert.Equal(0m, priced.Discount);
        Assert.Equal(24.50m, priced.Total);
        Assert.Equal(Now, priced.CreatedAt);
    }

    [Fact]
    public void Price_applies_discount_at_threshold()
    {
        var engine = CreateEngine(threshold: 500m, percent: 5m);
        var order = new CreateOrderDto("Contoso", [new CreateOrderLineDto("A-1", 1, 500.00m)]);

        var priced = engine.Price(Guid.NewGuid(), order, Now);

        Assert.Equal(25.00m, priced.Discount);
        Assert.Equal(475.00m, priced.Total);
    }

    [Fact]
    public void Price_skips_discount_below_threshold()
    {
        var engine = CreateEngine(threshold: 500m, percent: 5m);
        var order = new CreateOrderDto("Contoso", [new CreateOrderLineDto("A-1", 1, 499.99m)]);

        var priced = engine.Price(Guid.NewGuid(), order, Now);

        Assert.Equal(0m, priced.Discount);
        Assert.Equal(499.99m, priced.Total);
    }

    [Fact]
    public void Price_trims_customer_and_sku()
    {
        var engine = CreateEngine();
        var order = new CreateOrderDto("  Contoso ", [new CreateOrderLineDto(" A-1 ", 1, 1m)]);

        var priced = engine.Price(Guid.NewGuid(), order, Now);

        Assert.Equal("Contoso", priced.CustomerName);
        Assert.Equal("A-1", priced.Lines[0].Sku);
    }

    private static OrderPricingEngine CreateEngine(decimal threshold = 500m, decimal percent = 5m)
    {
        var options = Substitute.For<IOptionsMonitor<PricingOptions>>();
        options.CurrentValue.Returns(new PricingOptions { DiscountThreshold = threshold, DiscountPercent = percent });

        return new OrderPricingEngine(options);
    }
}
