namespace Company.Product.Worker.Engines.Setup;

/// <summary>Registers the Engine layer.</summary>
public static class EnginesSetup
{
    /// <summary>Adds every Engine to the container as a Singleton.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddEngines(this IServiceCollection services)
    {
        services.AddSingleton<IObjectNotificationEngine, ObjectNotificationEngine>();
        services.AddSingleton<ICsvOrderImportEngine, CsvOrderImportEngine>();
        services.AddSingleton<INotificationEngine, NotificationEngine>();

        return services;
    }
}
