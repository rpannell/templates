namespace Company.Product.Api.Managers.Exceptions;

/// <summary>A request broke one or more business rules. Mapped to HTTP 400.</summary>
/// <param name="errors">Error messages keyed by the field that failed.</param>
public sealed class BusinessValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more business rules failed.")
{
    /// <summary>Error messages keyed by the field that failed.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
