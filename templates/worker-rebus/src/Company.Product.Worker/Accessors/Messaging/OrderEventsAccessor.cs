using Company.Product.Worker.Accessors.Exceptions;
using Company.Product.Worker.Accessors.Models;
using Company.Product.Worker.Managers.Models;
using Rebus.Bus;

namespace Company.Product.Worker.Accessors.Messaging;

/// <summary>Publishes order events to the message bus. Only Accessors touch <see cref="IBus"/>.</summary>
public interface IOrderEventsAccessor
{
    /// <summary>Publishes <c>OrderImported</c> to every subscriber.</summary>
    /// <param name="order">The imported order.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the event is handed to the bus.</returns>
    /// <exception cref="ResourceAccessException">The bus could not accept the event. Retrying may help.</exception>
    Task PublishOrderImportedAsync(OrderDto order, CancellationToken cancellationToken);
}

/// <summary>
/// Publishes through <see cref="IBus"/>. Inside a handler the event is enlisted in the message's transaction: it is
/// sent when the handler succeeds and discarded when it fails.
/// </summary>
/// <param name="bus">The Rebus bus.</param>
public sealed class OrderEventsAccessor(IBus bus) : IOrderEventsAccessor
{
    /// <inheritdoc />
    public async Task PublishOrderImportedAsync(OrderDto order, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await bus.Publish(new OrderImportedEntity(order.Id, order.CustomerName, order.Total));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ResourceAccessException($"Could not publish OrderImported for order '{order.Id}'.", ex);
        }
    }
}
