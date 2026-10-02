using Company.Product.Worker.Accessors.Http;
using Company.Product.Worker.Accessors.Messaging;
using Company.Product.Worker.Accessors.Notifications;

namespace Company.Product.Worker.Accessors.Setup;

/// <summary>Registers the Accessor layer.</summary>
public static class AccessorsSetup
{
    /// <summary>Configuration key for the orders API base address.</summary>
    public const string OrdersBaseAddressKey = "ExternalApis:Orders";

    /// <summary>
    /// Default orders API address. Service discovery resolves <c>orders-api</c> from <c>Services:orders-api</c>
    /// configuration, or passes it through to DNS (a Kubernetes Service name) when nothing is configured.
    /// </summary>
    public const string DefaultOrdersBaseAddress = "https+http://orders-api";

    /// <summary>Adds service discovery, the named HTTP clients and every Accessor as a Singleton.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddAccessors(this IServiceCollection services, IConfiguration configuration)
    {
        // Other APIs: IHttpClientFactory named clients with service discovery and standard resilience
        // (retry, circuit breaker, timeouts). Added per client, not as defaults, so nothing else is affected.
        services.AddServiceDiscovery();
        var ordersApi = services.AddHttpClient(OrdersApiAccessor.HttpClientName, client =>
            client.BaseAddress = new Uri(configuration[OrdersBaseAddressKey] ?? DefaultOrdersBaseAddress));
        ordersApi.AddStandardResilienceHandler();
        ordersApi.AddServiceDiscovery();

        services.AddSingleton<IOrdersApiAccessor, OrdersApiAccessor>();
        services.AddSingleton<IOrderEventsAccessor, OrderEventsAccessor>();
        services.AddSingleton<INotificationAccessor, NotificationAccessor>();

        return services;
    }
}
