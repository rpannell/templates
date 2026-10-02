namespace Company.Product.Scheduler.Jobs.Setup;

/// <summary>Registers the jobs and maps them to the endpoints the Dapr cron bindings call.</summary>
public static class JobsSetup
{
    /// <summary>Adds every job as a Singleton.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddJobs(this IServiceCollection services)
    {
        services.AddSingleton<DailySalesSummaryJob>();
        services.AddSingleton<OrdersApiHealthJob>();

        return services;
    }

    /// <summary>
    /// Maps each job's route. For every cron component, the sidecar first sends <c>OPTIONS</c> to the route to
    /// confirm the app listens there, then sends <c>POST</c> each time the schedule fires.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The same application, for chaining.</returns>
    public static WebApplication MapJobs(this WebApplication app)
    {
        MapScheduledJob(app, DailySalesSummaryJob.Route,
            (DailySalesSummaryJob job, CancellationToken cancellationToken) => job.RunAsync(cancellationToken));

        MapScheduledJob(app, OrdersApiHealthJob.Route,
            (OrdersApiHealthJob job, CancellationToken cancellationToken) => job.RunAsync(cancellationToken));

        return app;
    }

    /// <summary>Maps a cron binding route: POST runs the job, OPTIONS confirms the route exists.</summary>
    /// <param name="app">The web application.</param>
    /// <param name="route">Route set in the cron component's <c>route</c> metadata.</param>
    /// <param name="handler">The job call.</param>
    private static void MapScheduledJob(WebApplication app, string route, Delegate handler)
    {
        app.MapPost(route, handler);
        app.MapMethods(route, [HttpMethods.Options], () => Results.Ok());
    }
}
