using Company.Product.Worker.Accessors.Http;
using Company.Product.Worker.Accessors.Notifications;
using Company.Product.Worker.Engines;
using Company.Product.Worker.Managers;
using Company.Product.Worker.Managers.Exceptions;
using Company.Product.Worker.Managers.Models;
using NSubstitute;

namespace Company.Product.Worker.Tests.Managers;

public class OrderNotificationManagerTests
{
    private readonly IOrdersApiAccessor _ordersApi = Substitute.For<IOrdersApiAccessor>();
    private readonly INotificationAccessor _notifications = Substitute.For<INotificationAccessor>();
    private readonly OrderNotificationManager _manager;

    public OrderNotificationManagerTests()
    {
        _manager = new OrderNotificationManager(_ordersApi, new NotificationEngine(), _notifications);
    }

    [Fact]
    public async Task Sends_notification_for_known_order()
    {
        var order = new OrderDto(Guid.NewGuid(), "Contoso", 2, 522.47m, DateTimeOffset.UnixEpoch);
        _ordersApi.FindAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        await _manager.NotifyOrderPlacedAsync(order.Id, CancellationToken.None);

        await _notifications.Received(1).SendAsync(
            Arg.Is<NotificationDto>(n => n.Body.Contains("522.47", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unknown_order_throws_not_found()
    {
        _ordersApi.FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((OrderDto?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _manager.NotifyOrderPlacedAsync(Guid.NewGuid(), CancellationToken.None));

        await _notifications.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }
}
