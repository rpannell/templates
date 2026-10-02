using Company.Product.Scheduler.Accessors.Http;
using Company.Product.Scheduler.Accessors.Notifications;
using Company.Product.Scheduler.Engines;
using Company.Product.Scheduler.Managers;
using Company.Product.Scheduler.Managers.Models;
using NSubstitute;

namespace Company.Product.Scheduler.Tests.Managers;

public class SalesSummaryManagerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 16, 6, 0, 0, TimeSpan.Zero);

    private readonly IOrdersApiAccessor _ordersApi = Substitute.For<IOrdersApiAccessor>();
    private readonly INotificationAccessor _notifications = Substitute.For<INotificationAccessor>();
    private readonly SalesSummaryManager _manager;

    public SalesSummaryManagerTests()
    {
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);

        _manager = new SalesSummaryManager(_ordersApi, new SalesSummaryEngine(), new NotificationEngine(), _notifications, time);
    }

    [Fact]
    public async Task Summarizes_the_last_window_and_sends_it()
    {
        IReadOnlyList<OrderDto> orders =
        [
            new(Guid.NewGuid(), "Contoso", 1, 100m, Now.AddHours(-2)),
            new(Guid.NewGuid(), "Fabrikam", 1, 50m, Now.AddDays(-2)),   // outside the 1-day window
        ];
        _ordersApi.ListRecentAsync(SalesSummaryManager.MaxOrders, Arg.Any<CancellationToken>()).Returns(orders);

        var summary = await _manager.SendSummaryAsync(TimeSpan.FromDays(1), CancellationToken.None);

        Assert.Equal(1, summary.OrderCount);
        Assert.Equal(100m, summary.Revenue);
        Assert.Equal(Now.AddDays(-1), summary.From);
        await _notifications.Received(1).SendAsync(
            Arg.Is<NotificationDto>(n => n.Body.StartsWith("1 order(s), revenue 100.00", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }
}
