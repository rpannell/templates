using System.Globalization;
using Company.Product.Worker.Managers.Models;

namespace Company.Product.Worker.Engines;

/// <summary>Builds the messages the worker sends to people.</summary>
public interface INotificationEngine
{
    /// <summary>Message for a newly placed order.</summary>
    /// <param name="order">The order.</param>
    /// <returns>The notification.</returns>
    NotificationDto OrderPlaced(OrderDto order);
}

/// <summary>Pure message formatting (invariant culture). No I/O.</summary>
public sealed class NotificationEngine : INotificationEngine
{
    /// <inheritdoc />
    public NotificationDto OrderPlaced(OrderDto order) => new(
        string.Create(CultureInfo.InvariantCulture, $"Order {order.Id} placed"),
        string.Create(CultureInfo.InvariantCulture, $"{order.CustomerName} placed an order with {order.LineCount} line(s) totaling {order.Total:0.00}."));
}
