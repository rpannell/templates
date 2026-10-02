namespace Company.Product.Scheduler.Managers.Models;

/// <summary>Result of one health check against a dependency.</summary>
/// <param name="Name">Dependency name (for example <c>orders-api</c>).</param>
/// <param name="Healthy">True when the dependency answered its health endpoint with a success status.</param>
/// <param name="Detail">HTTP status or error message.</param>
/// <param name="Duration">How long the check took.</param>
public sealed record DependencyHealthDto(string Name, bool Healthy, string Detail, TimeSpan Duration);
