using Company.Product.Scheduler.Utilities.Scheduling;
using Microsoft.Extensions.Logging.Abstractions;

namespace Company.Product.Scheduler.Tests.Utilities;

public class JobRunGuardTests
{
    private readonly JobRunGuard _guard = new(NullLogger<JobRunGuard>.Instance, TimeProvider.System);

    [Fact]
    public async Task Completed_run_reports_completed()
    {
        var ran = false;

        var outcome = await _guard.RunAsync("job", _ => { ran = true; return Task.CompletedTask; }, CancellationToken.None);

        Assert.True(ran);
        Assert.Equal(JobRunOutcome.Completed, outcome);
    }

    [Fact]
    public async Task Run_that_starts_while_the_previous_one_is_running_is_skipped()
    {
        var release = new TaskCompletionSource();
        var first = _guard.RunAsync("job", _ => release.Task, CancellationToken.None);

        var second = await _guard.RunAsync("job", _ => Task.CompletedTask, CancellationToken.None);
        release.SetResult();

        Assert.Equal(JobRunOutcome.Skipped, second);
        Assert.Equal(JobRunOutcome.Completed, await first);
    }

    [Fact]
    public async Task Different_jobs_run_at_the_same_time()
    {
        var release = new TaskCompletionSource();
        var first = _guard.RunAsync("job-a", _ => release.Task, CancellationToken.None);

        var other = await _guard.RunAsync("job-b", _ => Task.CompletedTask, CancellationToken.None);
        release.SetResult();
        await first;

        Assert.Equal(JobRunOutcome.Completed, other);
    }

    [Fact]
    public async Task Failure_is_reported_and_the_next_run_is_allowed()
    {
        var failed = await _guard.RunAsync("job", _ => throw new InvalidOperationException("boom"), CancellationToken.None);
        var next = await _guard.RunAsync("job", _ => Task.CompletedTask, CancellationToken.None);

        Assert.Equal(JobRunOutcome.Failed, failed);
        Assert.Equal(JobRunOutcome.Completed, next);
    }
}
