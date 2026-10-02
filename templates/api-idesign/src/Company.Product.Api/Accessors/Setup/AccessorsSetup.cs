using Company.Product.Api.Accessors.Database;
using Company.Product.Api.Accessors.Http;
using Microsoft.EntityFrameworkCore;

namespace Company.Product.Api.Accessors.Setup;

/// <summary>Registers the Accessor layer.</summary>
public static class AccessorsSetup
{
    /// <summary>Name of the connection string for the service database.</summary>
    public const string ConnectionStringName = "Database";

    /// <summary>Configuration key for the inventory service's base address.</summary>
    public const string InventoryBaseAddressKey = "ExternalApis:Inventory";

    /// <summary>
    /// Default inventory base address. <c>https+http</c> prefers HTTPS and falls back to HTTP. Service discovery
    /// resolves <c>inventory</c> from <c>Services:inventory</c> configuration, or passes it through to DNS
    /// (a Kubernetes Service name) when nothing is configured.
    /// </summary>
    public const string DefaultInventoryBaseAddress = "https+http://inventory";

    /// <summary>
    /// Adds the database context factory, service discovery, the named HTTP clients and every Accessor as a Singleton.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration. Must contain the <c>Database</c> connection string.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The connection string is missing.</exception>
    public static IServiceCollection AddAccessors(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        // Database: always through the factory. Singleton Accessors create one short-lived DbContext per call.
        // Never register AppDbContext with AddDbContext, and never inject DbContext (an architecture test checks this).
        services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(connectionString));

        // Other APIs: IHttpClientFactory named clients. Every client gets resilience (retry, circuit breaker,
        // timeouts) and service discovery, so base addresses are logical names such as https+http://inventory.
        services.AddServiceDiscovery();
        services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        services.AddHttpClient(InventoryApiAccessor.HttpClientName, client =>
            client.BaseAddress = new Uri(configuration[InventoryBaseAddressKey] ?? DefaultInventoryBaseAddress));

        services.AddSingleton<IOrderAccessor, OrderAccessor>();
        services.AddSingleton<IInventoryApiAccessor, InventoryApiAccessor>();

        return services;
    }
}
