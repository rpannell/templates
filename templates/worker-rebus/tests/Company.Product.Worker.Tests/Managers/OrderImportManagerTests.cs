using Company.Product.Worker.Accessors.Http;
using Company.Product.Worker.Accessors.Messaging;
using Company.Product.Worker.Managers;
using Company.Product.Worker.Managers.Exceptions;
using Company.Product.Worker.Managers.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Company.Product.Worker.Tests.Managers;

public class OrderImportManagerTests
{
    private static readonly ImportOrderDto Order = new("A-1", "Contoso", [new ImportOrderLineDto("WIDGET-1", 2, 5m)]);

    private readonly IOrdersApiAccessor _ordersApi = Substitute.For<IOrdersApiAccessor>();
    private readonly IOrderEventsAccessor _events = Substitute.For<IOrderEventsAccessor>();
    private readonly OrderImportManager _manager;

    public OrderImportManagerTests()
    {
        _manager = new OrderImportManager(_ordersApi, _events, NullLogger<OrderImportManager>.Instance);
    }

    [Fact]
    public async Task Creates_the_order_then_publishes_the_event()
    {
        var created = new OrderDto(Guid.NewGuid(), "Contoso", 1, 10m, DateTimeOffset.UnixEpoch);
        _ordersApi.CreateAsync(Order, Arg.Any<CancellationToken>()).Returns(created);

        await _manager.ImportAsync(Order, CancellationToken.None);

        await _events.Received(1).PublishOrderImportedAsync(created, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejected_order_publishes_nothing()
    {
        _ordersApi.CreateAsync(Order, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<OrderDto>(new BusinessValidationException(
                new Dictionary<string, string[]> { ["A-1"] = ["rejected"] })));

        await Assert.ThrowsAsync<BusinessValidationException>(() => _manager.ImportAsync(Order, CancellationToken.None));

        await _events.DidNotReceiveWithAnyArgs().PublishOrderImportedAsync(default!, default);
    }
}
