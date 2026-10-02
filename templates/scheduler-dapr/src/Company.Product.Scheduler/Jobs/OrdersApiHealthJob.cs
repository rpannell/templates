using Company.Product.Scheduler.Managers;
using Company.Product.Scheduler.Utilities.Scheduling;

namespace Company.Product.Scheduler.Jobs;

/// <summary>
/// Checks that the orders API is healthy and records the <c>dependency.checks</c> metric (alert on it). This is the
/// pattern for a small, frequent job. Schedule: the <c>orders-api-health</c> cron component (every 30 seconds locally,
/// every 5 minutes in <c>deploy/kubernetes/components.yaml</c>).
/// </summary>
/// <param name="manager">Dependency health use case.</param>
/// <param name="guard">Runs the job at most once at a time, with logs and metrics.</param>
public sealed class OrdersApiHealthJob(IDependencyHealthManager manager, JobRunGuard guard)
{
    /// <summary>Job name: also the Dapr cron component name.</summary>
    public const string Name = "orders-api-health";

    /// <summary>Endpoint the sidecar calls on schedule (the component's <c>route</c>).</summary>
    public const string Route = "/jobs/orders-api-health";

    /// <summary>Runs the job once.</summary>
    /// <param name="cancellationToken">Cancelled when the sidecar's request is aborted.</param>
    /// <returns>The run outcome as an HTTP response.</returns>
    public async Task<IResult> RunAsync(CancellationToken cancellationToken)
    {
        var outcome = await guard.RunAsync(Name, token => manager.CheckOrdersApiAsync(token), cancellationToken);
        return JobResults.From(Name, outcome);
    }
}
