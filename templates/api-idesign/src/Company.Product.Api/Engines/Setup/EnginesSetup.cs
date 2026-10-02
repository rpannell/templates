using Company.Product.Api.Engines.Options;

namespace Company.Product.Api.Engines.Setup;

/// <summary>Registers the Engine layer.</summary>
public static class EnginesSetup
{
    /// <summary>Adds every Engine to the container as a Singleton and binds Engine options.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddEngines(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PricingOptions>(configuration.GetSection(PricingOptions.SectionName));

        services.AddSingleton<IOrderValidationEngine, OrderValidationEngine>();
        services.AddSingleton<IOrderPricingEngine, OrderPricingEngine>();
        services.AddSingleton<IOrderFulfillmentEngine, OrderFulfillmentEngine>();

        return services;
    }
}
