using Company.Product.Worker.Managers.Exceptions;
using Rebus.Retry.FailFast;

namespace Company.Product.Worker.Handlers.Setup;

/// <summary>
/// Tells Rebus not to retry failures that retrying can't fix. A <see cref="BusinessValidationException"/> moves the
/// message to the error queue after the first attempt, with the exception details in its headers. Everything else
/// is left to Rebus's default checker, so transient failures still get every delivery attempt.
/// </summary>
/// <param name="inner">The checker this one wraps (Rebus's default).</param>
public sealed class PermanentFailureFailFastChecker(IFailFastChecker inner) : IFailFastChecker
{
    /// <summary>Decides whether a failed message goes to the error queue now instead of being redelivered.</summary>
    /// <param name="messageId">Id of the failed message.</param>
    /// <param name="exception">What the handler threw.</param>
    /// <returns><see langword="true"/> for a permanent failure.</returns>
    public bool ShouldFailFast(string messageId, Exception exception) =>
        IsPermanent(exception) || inner.ShouldFailFast(messageId, exception);

    /// <summary>Looks through wrappers (aggregate and inner exceptions) for a permanent failure.</summary>
    /// <param name="exception">The exception to inspect.</param>
    /// <returns><see langword="true"/> if retrying can't help.</returns>
    private static bool IsPermanent(Exception? exception) => exception switch
    {
        null => false,
        BusinessValidationException => true,
        AggregateException aggregate => aggregate.InnerExceptions.Any(IsPermanent),
        _ => IsPermanent(exception.InnerException),
    };
}
