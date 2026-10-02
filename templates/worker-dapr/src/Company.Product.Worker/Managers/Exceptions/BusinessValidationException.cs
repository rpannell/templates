namespace Company.Product.Worker.Managers.Exceptions;

/// <summary>Input broke one or more business rules. Retrying won't help, so the message or file is rejected.</summary>
/// <param name="errors">Error messages keyed by the field or line that failed.</param>
public sealed class BusinessValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception(Describe(errors))
{
    /// <summary>Error messages keyed by the field or line that failed.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    /// <summary>Builds a one-line summary of the errors for logs.</summary>
    /// <param name="errors">The errors.</param>
    /// <returns>A message such as <c>line 3: Quantity must be greater than zero.</c></returns>
    private static string Describe(IReadOnlyDictionary<string, string[]> errors) =>
        "One or more business rules failed: " + string.Join("; ", errors.Select(e => $"{e.Key}: {string.Join(" ", e.Value)}"));
}
