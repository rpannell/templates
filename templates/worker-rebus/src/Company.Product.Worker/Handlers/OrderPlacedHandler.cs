using Company.Product.Worker.Handlers.Models;
using Company.Product.Worker.Managers;
using Company.Product.Worker.Managers.Exceptions;
using Rebus.Handlers;

namespace Company.Product.Worker.Handlers;

/// <summary>
/// EVENT handler: runs for every <see cref="OrderPlacedEvent"/> this worker is subscribed to
/// (see <c>HandlersSetup.SubscribeToEventsAsync</c>).
/// </summary>
/// <param name="manager">Order event use cases.</param>
/// <param name="logger">Logger.</param>
public sealed class OrderPlacedHandler(IOrderNotificationManager manager, ILogger<OrderPlacedHandler> logger)
    : IHandleMessages<OrderPlacedEvent>
{
    /// <summary>
    /// Handles one message. An exception makes Rebus redeliver it (up to <c>Messaging:MaxDeliveryAttempts</c>),
    /// then move it to the error queue. A missing order is logged and dropped: retrying won't help.
    /// </summary>
    /// <param name="message">The event.</param>
    /// <returns>A task that completes when the message is handled.</returns>
    public async Task Handle(OrderPlacedEvent message)
    {
        try
        {
            await manager.NotifyOrderPlacedAsync(message.OrderId, CancellationToken.None);
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning("Dropping OrderPlacedEvent for {OrderId}: {Reason}", message.OrderId, ex.Message);
        }
    }
}
