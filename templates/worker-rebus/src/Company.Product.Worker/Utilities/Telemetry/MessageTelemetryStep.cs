using System.Diagnostics;
using System.Diagnostics.Metrics;
using Rebus.Messages;
using Rebus.Pipeline;

namespace Company.Product.Worker.Utilities.Telemetry;

/// <summary>
/// Rebus pipeline step that wraps every delivery attempt of an incoming message in a trace span and records
/// <c>messages.handled</c> and <c>messages.duration</c>. A failed attempt is recorded and the exception rethrown,
/// so Rebus's retry and error-queue handling is unchanged.
/// </summary>
public sealed class MessageTelemetryStep : IIncomingStep
{
    private static readonly Counter<long> Handled = ApplicationTelemetry.Meter.CreateCounter<long>(
        "messages.handled", unit: "{message}", description: "Delivery attempts, by message type and outcome.");

    private static readonly Histogram<double> Duration = ApplicationTelemetry.Meter.CreateHistogram<double>(
        "messages.duration", unit: "s", description: "Time spent handling a message, by message type and outcome.");

    /// <summary>Runs the rest of the pipeline (the handlers) and records how it went.</summary>
    /// <param name="context">The incoming message context.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>A task that completes when the handlers are done.</returns>
    public async Task Process(IncomingStepContext context, Func<Task> next)
    {
        var messageType = context.Load<Message>().Body.GetType().Name;

        using var activity = ApplicationTelemetry.ActivitySource.StartActivity($"handle {messageType}", ActivityKind.Consumer);
        activity?.SetTag("messaging.message.type", messageType);

        var started = Stopwatch.GetTimestamp();
        var outcome = "success";
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            outcome = "failure";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            var tags = new TagList { { "message.type", messageType }, { "outcome", outcome } };
            Handled.Add(1, tags);
            Duration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tags);
        }
    }
}
