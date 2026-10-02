using Company.Product.Worker.Functions.Models;
using Company.Product.Worker.Managers;
using Company.Product.Worker.Managers.Exceptions;

namespace Company.Product.Worker.Functions;

/// <summary>
/// EVENT function: runs for every <c>order-placed</c> message on the <c>events</c> pub/sub component.
/// Locally that's RabbitMQ. Swapping the component file makes it Kafka, Azure Event Hubs, Azure Service Bus,
/// AWS SNS/SQS or GCP Pub/Sub, with no code change.
/// </summary>
/// <param name="manager">Order event use cases.</param>
/// <param name="logger">Logger.</param>
public sealed class OrderPlacedFunction(IOrderNotificationManager manager, ILogger<OrderPlacedFunction> logger)
{
    /// <summary>Topic (RabbitMQ exchange, Kafka topic, Service Bus topic...).</summary>
    public const string Topic = "order-placed";

    /// <summary>Endpoint Dapr delivers the messages to.</summary>
    public const string Route = "/functions/order-placed";

    /// <summary>Handles one message. Exceptions become 500s, and Dapr retries per the resiliency policy.</summary>
    /// <param name="orderPlaced">The event (unwrapped from its CloudEvent envelope).</param>
    /// <param name="cancellationToken">Cancelled when the request is aborted.</param>
    /// <returns>Success, or Drop when the order doesn't exist.</returns>
    public async Task<IResult> RunAsync(OrderPlacedEvent orderPlaced, CancellationToken cancellationToken)
    {
        try
        {
            await manager.NotifyOrderPlacedAsync(orderPlaced.OrderId, cancellationToken);
            return PubSubResults.Success();
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning("Dropping order-placed for {OrderId}: {Reason}", orderPlaced.OrderId, ex.Message);
            return PubSubResults.Drop();
        }
    }
}
