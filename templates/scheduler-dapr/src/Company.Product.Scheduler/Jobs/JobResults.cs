using Company.Product.Scheduler.Utilities.Scheduling;

namespace Company.Product.Scheduler.Jobs;

/// <summary>
/// Maps a run outcome to the HTTP response the Dapr sidecar sees. Completed and skipped runs are 200. A failed run is
/// 500, so the sidecar logs the failure too. Either way, the schedule keeps going and the next run happens on time.
/// </summary>
public static class JobResults
{
    /// <summary>Creates the response for a run.</summary>
    /// <param name="jobName">Job name.</param>
    /// <param name="outcome">How the run ended.</param>
    /// <returns>200 with the outcome, or a 500 ProblemDetails for a failed run.</returns>
    public static IResult From(string jobName, JobRunOutcome outcome) => outcome switch
    {
        JobRunOutcome.Failed => Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: $"Scheduled job '{jobName}' failed. See the scheduler logs."),
        _ => Results.Ok(new { job = jobName, outcome = outcome.ToString().ToLowerInvariant() }),
    };
}
