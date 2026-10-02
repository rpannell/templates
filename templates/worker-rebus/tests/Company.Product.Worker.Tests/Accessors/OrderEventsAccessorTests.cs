using Company.Product.Worker.Accessors.Exceptions;
using Company.Product.Worker.Accessors.Messaging;
using Company.Product.Worker.Accessors.Models;
using Company.Product.Worker.Managers.Models;
using NSubstitute;
using Rebus.Bus;

namespace Company.Product.Worker.Tests.Accessors;

public class OrderEventsAccessorTests
{
    private static readonly OrderDto Order = new(Guid.NewGuid(), "Contoso", 1, 10m, DateTimeOffset.UnixEpoch);

    private readonly IBus _bus = Substitute.For<IBus>();

    [Fact]
    public async Task Publishes_the_wire_shape_of_the_event()
    {
        await new OrderEventsAccessor(_bus).PublishOrderImportedAsync(Order, CancellationToken.None);

        await _bus.Received(1).Publish(
            Arg.Is<object>(m => m is OrderImportedEntity
                && ((OrderImportedEntity)m).OrderId == Order.Id
                && ((OrderImportedEntity)m).CustomerName == "Contoso"
                && ((OrderImportedEntity)m).Total == 10m),
            Arg.Any<IDictionary<string, string>>());
    }

    [Fact]
    public async Task Bus_failure_is_transient()
    {
        _bus.Publish(Arg.Any<object>(), Arg.Any<IDictionary<string, string>>())
            .Returns(Task.FromException(new InvalidOperationException("broker unavailable")));

        await Assert.ThrowsAsync<ResourceAccessException>(
            () => new OrderEventsAccessor(_bus).PublishOrderImportedAsync(Order, CancellationToken.None));
    }
}
