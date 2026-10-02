using Company.Product.Scheduler.Accessors.Http;
using Company.Product.Scheduler.Accessors.Notifications;
using Company.Product.Scheduler.Engines;
using Company.Product.Scheduler.Managers.Models;

namespace Company.Product.Scheduler.Managers;

/// <summary>Scheduled sales reporting.</summary>
public interface ISalesSummaryManager
{
    /// <summary>Summarizes the orders created in the last <paramref name="window"/> and sends the summary.</summary>
    /// <param name="window">How far back to look, ending now.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The summary that was sent.</returns>
    Task<SalesSummaryDto> SendSummaryAsync(TimeSpan window, CancellationToken cancellationToken);
}

/// <summary>Sales summary use case: list orders (Accessor), total them (Engine), send the message (Accessor).</summary>
/// <param name="ordersApi">Lists orders.</param>
/// <param name="summaryEngine">Totals orders.</param>
/// <param name="notificationEngine">Builds messages.</param>
/// <param name="notifications">Sends messages.</param>
/// <param name="timeProvider">Source of the current time.</param>
public sealed class SalesSummaryManager(
    IOrdersApiAccessor ordersApi,
    ISalesSummaryEngine summaryEngine,
    INotificationEngine notificationEngine,
    INotificationAccessor notifications,
    TimeProvider timeProvider) : ISalesSummaryManager
{
    /// <summary>Most orders the summary reads (the orders API's page limit).</summary>
    public const int MaxOrders = 200;

    /// <inheritdoc />
    public async Task<SalesSummaryDto> SendSummaryAsync(TimeSpan window, CancellationToken cancellationToken)
    {
        var to = timeProvider.GetUtcNow();
        var orders = await ordersApi.ListRecentAsync(MaxOrders, cancellationToken);
        var summary = summaryEngine.Summarize(orders, to - window, to);

        await notifications.SendAsync(notificationEngine.SalesSummary(summary), cancellationToken);
        return summary;
    }
}
