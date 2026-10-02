using System.Diagnostics.Metrics;
using Company.Product.Worker.Accessors.Http;
using Company.Product.Worker.Accessors.Messaging;
using Company.Product.Worker.Managers.Exceptions;
using Company.Product.Worker.Managers.Models;
using Company.Product.Worker.Utilities.Telemetry;

namespace Company.Product.Worker.Managers;

/// <summary>Imports orders that arrive as messages.</summary>
public interface IOrderImportManager
{
    /// <summary>Creates the order through the orders API, then announces it with an <c>OrderImported</c> event.</summary>
    /// <param name="order">The order to create.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the order exists and the event is queued.</returns>
    /// <exception cref="BusinessValidationException">The order is invalid. Retrying won't help.</exception>
    Task ImportAsync(ImportOrderDto order, CancellationToken cancellationToken);
}

/// <summary>
/// Order import use case: create the order (Accessor), then publish the follow-up event (Accessor). Rebus sends the
/// event together with the handler's transaction, so a failed import publishes nothing.
/// </summary>
/// <param name="ordersApi">Creates orders.</param>
/// <param name="orderEvents">Publishes order events.</param>
/// <param name="logger">Logger.</param>
public sealed class OrderImportManager(
    IOrdersApiAccessor ordersApi,
    IOrderEventsAccessor orderEvents,
    ILogger<OrderImportManager> logger) : IOrderImportManager
{
    private static readonly Counter<long> OrdersImported = ApplicationTelemetry.Meter.CreateCounter<long>(
        "orders.imported", unit: "{order}", description: "Orders created from messages.");

    /// <inheritdoc />
    public async Task ImportAsync(ImportOrderDto order, CancellationToken cancellationToken)
    {
        var created = await ordersApi.CreateAsync(order, cancellationToken);
        await orderEvents.PublishOrderImportedAsync(created, cancellationToken);

        OrdersImported.Add(1);
        logger.LogInformation("Imported order {Reference} as {OrderId}", order.Reference, created.Id);
    }
}
