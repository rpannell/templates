using Company.Product.Worker.Functions.Models;

namespace Company.Product.Worker.Functions.Setup;

/// <summary>Registers the functions and maps them to the endpoints Dapr calls.</summary>
public static class FunctionsSetup
{
    /// <summary>Name of the Dapr pub/sub component that event functions subscribe to.</summary>
    public const string PubSubName = "events";

    /// <summary>Adds every function. Functions are Singletons; the drop folder poller is a hosted service.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddFunctions(this IServiceCollection services)
    {
        services.AddSingleton<OrderPlacedFunction>();
        services.AddSingleton<ObjectCreatedFunction>();
        services.AddHostedService<DropFolderFunction>();

        return services;
    }

    /// <summary>
    /// Maps each function to its endpoint. Pub/sub endpoints declare their topic with <c>WithTopic</c> (Dapr reads
    /// them from <c>GET /dapr/subscribe</c>). Binding endpoints also answer <c>OPTIONS</c>, which Dapr uses to check
    /// that the app listens on a binding's route.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The same application, for chaining.</returns>
    public static WebApplication MapFunctions(this WebApplication app)
    {
        // EVENT
        app.MapPost(OrderPlacedFunction.Route,
                (OrderPlacedEvent orderPlaced, OrderPlacedFunction function, CancellationToken cancellationToken) =>
                    function.RunAsync(orderPlaced, cancellationToken))
            .WithTopic(PubSubName, OrderPlacedFunction.Topic);

        // FILE (object storage notifications). The drop folder poller is a hosted service and needs no endpoint.
        MapBinding(app, ObjectCreatedFunction.Route,
            (HttpRequest request, ObjectCreatedFunction function, CancellationToken cancellationToken) =>
                function.RunAsync(request.Body, cancellationToken));

        return app;
    }

    /// <summary>Maps a Dapr input binding route: POST runs the function, OPTIONS confirms the route exists.</summary>
    /// <param name="app">The web application.</param>
    /// <param name="route">Route set in the binding component's <c>route</c> metadata.</param>
    /// <param name="handler">The function call.</param>
    private static void MapBinding(WebApplication app, string route, Delegate handler)
    {
        app.MapPost(route, handler);
        app.MapMethods(route, [HttpMethods.Options], () => Results.Ok());
    }
}
