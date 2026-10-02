using System.Diagnostics.Metrics;
using Company.Product.Scheduler.Accessors.Http;
using Company.Product.Scheduler.Managers.Models;
using Company.Product.Scheduler.Utilities.Telemetry;

namespace Company.Product.Scheduler.Managers;

/// <summary>Checks the services this system depends on.</summary>
public interface IDependencyHealthManager
{
    /// <summary>Checks the orders API, records the result as a metric and logs a warning when it's unhealthy.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The check result.</returns>
    Task<DependencyHealthDto> CheckOrdersApiAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Dependency health use case: call the dependency (Accessor), record <c>dependency.checks</c> tagged with
/// <c>dependency</c> and <c>outcome</c>. Alert on the metric in Grafana or your monitoring tool, not in this code.
/// </summary>
/// <param name="ordersApi">Orders API.</param>
/// <param name="logger">Logger.</param>
public sealed class DependencyHealthManager(IOrdersApiAccessor ordersApi, ILogger<DependencyHealthManager> logger) : IDependencyHealthManager
{
    private static readonly Counter<long> Checks = ApplicationTelemetry.Meter.CreateCounter<long>(
        "dependency.checks", unit: "{check}", description: "Dependency health checks, by dependency and outcome.");

    /// <inheritdoc />
    public async Task<DependencyHealthDto> CheckOrdersApiAsync(CancellationToken cancellationToken)
    {
        var health = await ordersApi.CheckHealthAsync(cancellationToken);

        Checks.Add(
            1,
            new KeyValuePair<string, object?>("dependency", health.Name),
            new KeyValuePair<string, object?>("outcome", health.Healthy ? "healthy" : "unhealthy"));

        if (!health.Healthy)
        {
            logger.LogWarning(
                "{Dependency} is unhealthy: {Detail} ({DurationMs} ms)",
                health.Name, health.Detail, (long)health.Duration.TotalMilliseconds);
        }

        return health;
    }
}
