using Company.Product.Worker.Handlers.Models;
using Rebus.Bus;
using Rebus.Config;

namespace Company.Product.Worker.Handlers.Setup;

/// <summary>Registers the handlers and the message bus.</summary>
public static class HandlersSetup
{
    /// <summary>Adds the bus (<see cref="MessagingSetup"/>) and every handler.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddHandlers(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMessaging(configuration);

        // Each handler class implements IHandleMessages<T> for one message type. Rebus creates one per message.
        services.AddRebusHandler<OrderPlacedHandler>();
        services.AddRebusHandler<ImportOrderHandler>();

        return services;
    }

    /// <summary>
    /// Subscribes this worker to every EVENT it handles, so the broker routes published events to its queue.
    /// Commands (like <see cref="ImportOrderCommand"/>) are sent straight to the queue and need no subscription.
    /// Add a line here for each new event handler.
    /// </summary>
    /// <param name="bus">The bus, once created.</param>
    /// <returns>A task that completes when the subscriptions exist.</returns>
    public static async Task SubscribeToEventsAsync(IBus bus)
    {
        await bus.Subscribe<OrderPlacedEvent>();
    }
}
