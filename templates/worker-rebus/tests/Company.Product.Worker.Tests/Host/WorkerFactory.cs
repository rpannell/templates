using Company.Product.Worker.Managers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Company.Product.Worker.Tests.Host;

/// <summary>
/// Starts the real worker in memory: the Rebus pipeline, retry strategy, fail-fast checker and telemetry step all run,
/// on Rebus's in-memory transport instead of RabbitMQ. The Managers are substitutes, so no orders API is needed.
/// </summary>
public sealed class WorkerFactory : WebApplicationFactory<Program>
{
    static WorkerFactory()
    {
        // The Messaging settings are read while Program.cs builds the host, before test configuration is applied,
        // so they are set as environment variables (which are read first).
        Environment.SetEnvironmentVariable("Messaging__Transport", "InMemory");
        Environment.SetEnvironmentVariable("Messaging__MaxDeliveryAttempts", "3");
    }

    /// <summary>Substitute for the order notification use case.</summary>
    public IOrderNotificationManager Notifications { get; } = Substitute.For<IOrderNotificationManager>();

    /// <summary>Substitute for the order import use case.</summary>
    public IOrderImportManager Imports { get; } = Substitute.For<IOrderImportManager>();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(Notifications);
            services.AddSingleton(Imports);
        });
    }
}
