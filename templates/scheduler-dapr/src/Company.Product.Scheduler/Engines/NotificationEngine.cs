using System.Globalization;
using Company.Product.Scheduler.Managers.Models;

namespace Company.Product.Scheduler.Engines;

/// <summary>Builds the messages the scheduler sends to people.</summary>
public interface INotificationEngine
{
    /// <summary>Message for a sales summary.</summary>
    /// <param name="summary">The summary.</param>
    /// <returns>The notification.</returns>
    NotificationDto SalesSummary(SalesSummaryDto summary);
}

/// <summary>Pure message formatting (invariant culture). No I/O.</summary>
public sealed class NotificationEngine : INotificationEngine
{
    /// <inheritdoc />
    public NotificationDto SalesSummary(SalesSummaryDto summary) => new(
        string.Create(CultureInfo.InvariantCulture, $"Sales summary {summary.From:yyyy-MM-dd HH:mm} to {summary.To:yyyy-MM-dd HH:mm} UTC"),
        string.Create(CultureInfo.InvariantCulture, $"{summary.OrderCount} order(s), revenue {summary.Revenue:0.00}, average order {summary.AverageOrderValue:0.00}."));
}
