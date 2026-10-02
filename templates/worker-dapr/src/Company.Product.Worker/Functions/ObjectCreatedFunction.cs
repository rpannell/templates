using Company.Product.Worker.Managers;
using Company.Product.Worker.Managers.Exceptions;

namespace Company.Product.Worker.Functions;

/// <summary>
/// FILE function (object storage): runs for every "file created" notification on the <c>object-created</c> input
/// binding. In the cloud the store publishes the notification (S3 to SQS, Blob Storage to Event Grid to Service Bus,
/// GCS to Pub/Sub); locally it comes from RabbitMQ. The file itself is read through the <c>object-storage</c> binding.
/// </summary>
/// <param name="manager">File import use case.</param>
/// <param name="logger">Logger.</param>
public sealed class ObjectCreatedFunction(IFileImportManager manager, ILogger<ObjectCreatedFunction> logger)
{
    /// <summary>Input binding component name.</summary>
    public const string BindingName = "object-created";

    /// <summary>Endpoint Dapr delivers notifications to (set as the binding's <c>route</c>).</summary>
    public const string Route = "/functions/object-created";

    /// <summary>
    /// Handles one notification. Invalid notifications and files are logged and acknowledged (200) so they aren't
    /// redelivered forever. Transient failures become 500s, so the broker redelivers the notification.
    /// </summary>
    /// <param name="body">Raw notification body.</param>
    /// <param name="cancellationToken">Cancelled when the request is aborted.</param>
    /// <returns>200 OK when handled or rejected.</returns>
    public async Task<IResult> RunAsync(Stream body, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(body);
        var notification = await reader.ReadToEndAsync(cancellationToken);

        try
        {
            await manager.ImportFromNotificationAsync(notification, cancellationToken);
        }
        catch (BusinessValidationException ex)
        {
            logger.LogWarning("Rejected object-created notification: {Reason}", ex.Message);
        }

        return Results.Ok();
    }
}
