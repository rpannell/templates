using Company.Product.Api.Accessors;
using Company.Product.Api.Accessors.Http;
using Company.Product.Api.Engines;
using Company.Product.Api.Managers;
using Company.Product.Api.Managers.Exceptions;
using Company.Product.Api.Managers.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Company.Product.Api.Tests.Managers;

public class OrderManagerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly IOrderValidationEngine _validation = Substitute.For<IOrderValidationEngine>();
    private readonly IOrderPricingEngine _pricing = Substitute.For<IOrderPricingEngine>();
    private readonly IOrderFulfillmentEngine _fulfillment = Substitute.For<IOrderFulfillmentEngine>();
    private readonly IOrderAccessor _accessor = Substitute.For<IOrderAccessor>();
    private readonly IInventoryApiAccessor _inventory = Substitute.For<IInventoryApiAccessor>();
    private readonly TimeProvider _time = Substitute.For<TimeProvider>();
    private readonly OrderManager _manager;

    public OrderManagerTests()
    {
        _time.GetUtcNow().Returns(Now);
        _manager = new OrderManager(
            _validation, _pricing, _fulfillment, _accessor, _inventory, _time, NullLogger<OrderManager>.Instance);
    }

    [Fact]
    public async Task CreateAsync_validates_prices_and_saves()
    {
        var request = new CreateOrderDto("Contoso", [new CreateOrderLineDto("A-1", 1, 10m)]);
        var priced = PricedOrder();
        _pricing.Price(Arg.Any<Guid>(), request, Now).Returns(priced);

        var result = await _manager.CreateAsync(request, CancellationToken.None);

        Assert.Same(priced, result);
        _validation.Received(1).Validate(request);
        await _accessor.Received(1).SaveAsync(priced, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_does_not_save_when_validation_fails()
    {
        var request = new CreateOrderDto("", []);
        _validation
            .When(v => v.Validate(request))
            .Do(_ => throw new BusinessValidationException(new Dictionary<string, string[]>()));

        await Assert.ThrowsAsync<BusinessValidationException>(
            () => _manager.CreateAsync(request, CancellationToken.None));

        await _accessor.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Fact]
    public async Task GetAsync_throws_not_found_when_missing()
    {
        var id = Guid.NewGuid();
        _accessor.FindAsync(id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<OrderDto?>(null));

        var exception = await Assert.ThrowsAsync<OrderNotFoundException>(
            () => _manager.GetAsync(id, CancellationToken.None));

        Assert.Equal(id, exception.OrderId);
    }

    [Fact]
    public async Task GetAvailabilityAsync_asks_inventory_for_distinct_skus_and_checks_stock()
    {
        var order = PricedOrder() with
        {
            Lines = [new OrderLineDto(1, "A-1", 1, 10m, 10m), new OrderLineDto(2, "a-1", 2, 10m, 20m)],
        };
        IReadOnlyList<StockLevelDto> stock = [new StockLevelDto("A-1", 5)];
        var expected = new OrderAvailabilityDto(order.Id, true, []);

        _accessor.FindAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<OrderDto?>(order));
        _inventory.GetStockLevelsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(stock);
        _fulfillment.CheckAvailability(order, stock).Returns(expected);

        var result = await _manager.GetAvailabilityAsync(order.Id, CancellationToken.None);

        Assert.Same(expected, result);
        await _inventory.Received(1).GetStockLevelsAsync(
            Arg.Is<IReadOnlyCollection<string>>(skus => skus.Count == 1),
            Arg.Any<CancellationToken>());
    }

    private static OrderDto PricedOrder() => new()
    {
        Id = Guid.NewGuid(),
        CustomerName = "Contoso",
        Lines = [new OrderLineDto(1, "A-1", 1, 10m, 10m)],
        Subtotal = 10m,
        Discount = 0m,
        Total = 10m,
        CreatedAt = Now,
    };
}
