using Company.Product.Worker.Utilities.Options;
using Company.Product.Worker.Utilities.Telemetry;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Retry.FailFast;
using Rebus.Retry.Simple;
using Rebus.Transport;
using Rebus.Transport.InMem;

namespace Company.Product.Worker.Handlers.Setup;

/// <summary>
/// Configures the Rebus bus: the broker transport, retries, parallelism, graceful shutdown and the telemetry step.
/// <b>This is the one place that names a broker.</b> To use another one, add its Rebus transport package and
/// add a case to <see cref="ConfigureTransport"/> (see the README, "Switching brokers").
/// </summary>
public static class MessagingSetup
{
    /// <summary>Extra time the host waits beyond the worker shutdown timeout before it gives up and stops.</summary>
    private static readonly TimeSpan ShutdownGrace = TimeSpan.FromSeconds(5);

    /// <summary>Reads and validates <c>Messaging</c>, then adds the Rebus bus. It starts and stops with the host.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">A <c>Messaging</c> setting is missing or out of range.</exception>
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var options = Load(configuration);

        // The host waits for in-flight messages to finish before the process exits (rolling updates, scale-down).
        services.Configure<HostOptions>(host => host.ShutdownTimeout = options.WorkerShutdownTimeout + ShutdownGrace);

        services.AddRebus(
            configure => ConfigureBus(configure, options),
            onCreated: bus => options.Transport == MessagingTransport.InMemory
                ? Task.CompletedTask
                : HandlersSetup.SubscribeToEventsAsync(bus));

        return services;
    }

    /// <summary>Binds and validates the <c>Messaging</c> section.</summary>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The validated options.</returns>
    private static MessagingOptions Load(IConfiguration configuration)
    {
        var options = new MessagingOptions();
        configuration.GetSection(MessagingOptions.SectionName).Bind(options);

        if (string.IsNullOrWhiteSpace(options.InputQueue) || string.IsNullOrWhiteSpace(options.ErrorQueue))
        {
            throw new InvalidOperationException("Messaging:InputQueue and Messaging:ErrorQueue are required.");
        }

        if (options.Transport == MessagingTransport.RabbitMq && string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("Messaging:ConnectionString is required for the RabbitMq transport.");
        }

        if (options.NumberOfWorkers < 1 || options.MaxParallelism < 1 || options.MaxDeliveryAttempts < 1)
        {
            throw new InvalidOperationException(
                "Messaging:NumberOfWorkers, Messaging:MaxParallelism and Messaging:MaxDeliveryAttempts must be at least 1.");
        }

        return options;
    }

    /// <summary>Applies the settings to the Rebus configurer.</summary>
    /// <param name="configure">The Rebus configurer.</param>
    /// <param name="options">Validated messaging settings.</param>
    /// <returns>The configurer, for chaining.</returns>
    private static RebusConfigurer ConfigureBus(RebusConfigurer configure, MessagingOptions options) => configure
        .Transport(transport => ConfigureTransport(transport, options))
        .Options(o =>
        {
            o.SetNumberOfWorkers(options.NumberOfWorkers);
            o.SetMaxParallelism(options.MaxParallelism);
            o.SetWorkerShutdownTimeout(options.WorkerShutdownTimeout);

            // Delivery attempts happen back to back (no delay), then the message moves to the error queue.
            o.RetryStrategy(errorQueueAddress: options.ErrorQueue, maxDeliveryAttempts: options.MaxDeliveryAttempts);

            // Permanent failures (BusinessValidationException) skip the retries.
            o.Decorate<IFailFastChecker>(context => new PermanentFailureFailFastChecker(context.Get<IFailFastChecker>()));

            // Metrics and a span per delivery attempt.
            o.Decorate<IPipeline>(context => new PipelineStepInjector(context.Get<IPipeline>())
                .OnReceive(new MessageTelemetryStep(), PipelineRelativePosition.Before, typeof(DispatchIncomingMessageStep)));
        });

    /// <summary>Selects the broker. The only code that depends on a transport package.</summary>
    /// <param name="transport">The transport configurer.</param>
    /// <param name="options">Validated messaging settings.</param>
    private static void ConfigureTransport(StandardConfigurer<ITransport> transport, MessagingOptions options)
    {
        switch (options.Transport)
        {
            case MessagingTransport.InMemory:
                transport.UseInMemoryTransport(new InMemNetwork(), options.InputQueue);
                break;
            default:
                transport.UseRabbitMq(options.ConnectionString, options.InputQueue);
                break;
        }
    }
}
