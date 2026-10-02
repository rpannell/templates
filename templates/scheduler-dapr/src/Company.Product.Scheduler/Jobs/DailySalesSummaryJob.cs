using Company.Product.Scheduler.Managers;
using Company.Product.Scheduler.Utilities.Scheduling;

namespace Company.Product.Scheduler.Jobs;

/// <summary>
/// Sends yesterday's sales summary. Schedule: the <c>daily-sales-summary</c> cron component
/// (every minute locally, 06:00 UTC daily in <c>deploy/kubernetes/components.yaml</c>).
/// </summary>
/// <param name="manager">Sales summary use case.</param>
/// <param name="guard">Runs the job at most once at a time, with logs and metrics.</param>
public sealed class DailySalesSummaryJob(ISalesSummaryManager manager, JobRunGuard guard)
{
    /// <summary>Job name: also the Dapr cron component name.</summary>
    public const string Name = "daily-sales-summary";

    /// <summary>Endpoint the sidecar calls on schedule (the component's <c>route</c>).</summary>
    public const string Route = "/jobs/daily-sales-summary";

    /// <summary>How far back each summary looks, ending at the time the job runs.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromDays(1);

    /// <summary>Runs the job once.</summary>
    /// <param name="cancellationToken">Cancelled when the sidecar's request is aborted.</param>
    /// <returns>The run outcome as an HTTP response.</returns>
    public async Task<IResult> RunAsync(CancellationToken cancellationToken)
    {
        var outcome = await guard.RunAsync(Name, token => manager.SendSummaryAsync(Window, token), cancellationToken);
        return JobResults.From(Name, outcome);
    }
}
