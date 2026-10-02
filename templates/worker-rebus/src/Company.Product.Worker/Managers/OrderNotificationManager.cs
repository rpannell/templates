using Company.Product.Worker.Accessors.Http;
using Company.Product.Worker.Accessors.Notifications;
using Company.Product.Worker.Engines;
using Company.Product.Worker.Managers.Exceptions;

namespace Company.Product.Worker.Managers;

/// <summary>Reacts to order events.</summary>
public interface IOrderNotificationManager
{
    /// <summary>Sends the "order placed" notification.</summary>
    /// <param name="orderId">The order that was placed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the notification is sent.</returns>
    /// <exception cref="NotFoundException">The orders API doesn't know the order. Retrying won't help.</exception>
    Task NotifyOrderPlacedAsync(Guid orderId, CancellationToken cancellationToken);
}

/// <summary>Order event use cases: fetch the order (Accessor), build the message (Engine), send it (Accessor).</summary>
/// <param name="ordersApi">Reads orders.</param>
/// <param name="notificationEngine">Builds messages.</param>
/// <param name="notifications">Sends messages.</param>
public sealed class OrderNotificationManager(
    IOrdersApiAccessor ordersApi,
    INotificationEngine notificationEngine,
    INotificationAccessor notifications) : IOrderNotificationManager
{
    /// <inheritdoc />
    public async Task NotifyOrderPlacedAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await ordersApi.FindAsync(orderId, cancellationToken)
            ?? throw new NotFoundException($"Order '{orderId}' was not found in the orders API.");

        await notifications.SendAsync(notificationEngine.OrderPlaced(order), cancellationToken);
    }
}
