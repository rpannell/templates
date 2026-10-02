using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Company.Product.Scheduler.Utilities.Telemetry;

namespace Company.Product.Scheduler.Utilities.Scheduling;

/// <summary>How a scheduled run ended.</summary>
public enum JobRunOutcome
{
    /// <summary>The job ran to completion.</summary>
    Completed,

    /// <summary>The job didn't run because its previous run was still in progress.</summary>
    Skipped,

    /// <summary>The job threw. The error is logged and counted; the next scheduled run is unaffected.</summary>
    Failed,
}

/// <summary>
/// Runs scheduled jobs with the same guarantees for every job: one run at a time per job (a run that starts while
/// the previous one is still going is skipped, not queued), a log line at start and end with the duration, and
/// the <c>jobs.runs</c> / <c>jobs.duration</c> metrics. The one stateful Utility: it holds a lock per job, which is
/// correct because the scheduler runs as a single replica.
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="timeProvider">Clock used to measure run duration.</param>
public sealed class JobRunGuard(ILogger<JobRunGuard> logger, TimeProvider timeProvider)
{
    private static readonly Counter<long> Runs = ApplicationTelemetry.Meter.CreateCounter<long>(
        "jobs.runs", unit: "{run}", description: "Scheduled job runs, by job.name and outcome (completed, skipped, failed).");

    private static readonly Histogram<double> Duration = ApplicationTelemetry.Meter.CreateHistogram<double>(
        "jobs.duration", unit: "s", description: "Scheduled job run duration, by job.name and outcome.");

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    /// <summary>Runs a job unless its previous run is still in progress.</summary>
    /// <param name="jobName">Job name, used for the lock, logs and metrics (the Dapr component name).</param>
    /// <param name="work">The job's work, usually one Manager call.</param>
    /// <param name="cancellationToken">Cancelled when the sidecar's request is aborted or the app shuts down.</param>
    /// <returns>How the run ended. Failures are logged and returned, not thrown.</returns>
    public async Task<JobRunOutcome> RunAsync(string jobName, Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        var gate = _gates.GetOrAdd(jobName, static _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            logger.LogWarning("Skipped {Job}: the previous run is still in progress", jobName);
            Record(jobName, JobRunOutcome.Skipped, elapsed: null);
            return JobRunOutcome.Skipped;
        }

        var started = timeProvider.GetTimestamp();
        try
        {
            logger.LogInformation("Starting {Job}", jobName);
            await work(cancellationToken);

            var elapsed = timeProvider.GetElapsedTime(started);
            logger.LogInformation("Completed {Job} in {DurationMs} ms", jobName, (long)elapsed.TotalMilliseconds);
            Record(jobName, JobRunOutcome.Completed, elapsed);
            return JobRunOutcome.Completed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var elapsed = timeProvider.GetElapsedTime(started);
            logger.LogError(ex, "Failed {Job} after {DurationMs} ms", jobName, (long)elapsed.TotalMilliseconds);
            Record(jobName, JobRunOutcome.Failed, elapsed);
            return JobRunOutcome.Failed;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Records the run metrics.</summary>
    /// <param name="jobName">Job name tag.</param>
    /// <param name="outcome">Outcome tag.</param>
    /// <param name="elapsed">Run duration, or <see langword="null"/> for a skipped run.</param>
    private static void Record(string jobName, JobRunOutcome outcome, TimeSpan? elapsed)
    {
        var job = new KeyValuePair<string, object?>("job.name", jobName);
        var result = new KeyValuePair<string, object?>("outcome", outcome.ToString().ToLowerInvariant());

        Runs.Add(1, job, result);
        if (elapsed is { } duration)
        {
            Duration.Record(duration.TotalSeconds, job, result);
        }
    }
}
