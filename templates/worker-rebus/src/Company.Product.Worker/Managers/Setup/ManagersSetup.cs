namespace Company.Product.Worker.Managers.Setup;

/// <summary>Registers the Manager layer.</summary>
public static class ManagersSetup
{
    /// <summary>Adds every Manager to the container as a Singleton.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddManagers(this IServiceCollection services)
    {
        services.AddSingleton<IOrderImportManager, OrderImportManager>();
        services.AddSingleton<IOrderNotificationManager, OrderNotificationManager>();

        return services;
    }
}
