using Company.Product.Scheduler.Utilities.Scheduling;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Company.Product.Scheduler.Utilities.Setup;

/// <summary>Registers cross-cutting Utilities.</summary>
public static class UtilitiesSetup
{
    /// <summary>Adds Utilities: <see cref="TimeProvider"/> and the <see cref="JobRunGuard"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddUtilities(this IServiceCollection services)
    {
        // Inject TimeProvider instead of calling DateTime.UtcNow so time is testable.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<JobRunGuard>();

        return services;
    }
}
