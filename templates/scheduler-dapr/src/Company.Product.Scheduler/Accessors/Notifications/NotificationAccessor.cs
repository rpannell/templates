using Company.Product.Scheduler.Managers.Models;

namespace Company.Product.Scheduler.Accessors.Notifications;

/// <summary>Sends messages to people.</summary>
public interface INotificationAccessor
{
    /// <summary>Sends a notification.</summary>
    /// <param name="notification">The message.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the message is handed off.</returns>
    Task SendAsync(NotificationDto notification, CancellationToken cancellationToken);
}

/// <summary>
/// Placeholder that writes notifications to the log. Replace the body with a Dapr output binding
/// (<c>bindings.smtp</c>, <c>bindings.twilio.sendgrid</c>, <c>bindings.twilio.sms</c>) or an HTTP Accessor for your
/// chat tool. The interface and callers stay the same.
/// </summary>
/// <param name="logger">Logger.</param>
public sealed class NotificationAccessor(ILogger<NotificationAccessor> logger) : INotificationAccessor
{
    /// <inheritdoc />
    public Task SendAsync(NotificationDto notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Notification: {Subject}. {Body}", notification.Subject, notification.Body);
        return Task.CompletedTask;
    }
}
