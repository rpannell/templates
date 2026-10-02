namespace Company.Product.Worker.Utilities.Options;

/// <summary>Which message broker transport the worker uses.</summary>
public enum MessagingTransport
{
    /// <summary>RabbitMQ (also the transport for local development). The default.</summary>
    RabbitMq,

    /// <summary>An in-process queue with no broker. Used by the tests; messages are lost when the process stops.</summary>
    InMemory,
}

/// <summary>
/// Message bus settings, bound from <c>Messaging</c>. The broker choice lives in
/// <c>Handlers/Setup/MessagingSetup.cs</c>; everything else is configuration.
/// </summary>
public sealed class MessagingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Messaging";

    /// <summary>Broker transport. Production uses <see cref="MessagingTransport.RabbitMq"/>.</summary>
    public MessagingTransport Transport { get; set; } = MessagingTransport.RabbitMq;

    /// <summary>Broker connection string, for example <c>amqp://user:password@host:5672</c>. Keep real credentials in a secret.</summary>
    public string ConnectionString { get; set; } = "amqp://guest:guest@localhost:5672";

    /// <summary>This worker's input queue. All replicas read from the same queue (competing consumers).</summary>
    public string InputQueue { get; set; } = "worker";

    /// <summary>Queue that receives messages that failed every delivery attempt, or failed fast.</summary>
    public string ErrorQueue { get; set; } = "error";

    /// <summary>Number of worker threads that read from the input queue. Raise <see cref="MaxParallelism"/> instead for more throughput.</summary>
    public int NumberOfWorkers { get; set; } = 1;

    /// <summary>Maximum number of messages handled at the same time by one replica.</summary>
    public int MaxParallelism { get; set; } = 8;

    /// <summary>How many times a message is delivered to a handler before it moves to the error queue.</summary>
    public int MaxDeliveryAttempts { get; set; } = 5;

    /// <summary>How long a stopping replica waits for in-flight messages to finish. Keep it below the pod's termination grace period.</summary>
    public TimeSpan WorkerShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
