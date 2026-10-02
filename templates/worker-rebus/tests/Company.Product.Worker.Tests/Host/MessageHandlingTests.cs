using Company.Product.Worker.Accessors.Exceptions;
using Company.Product.Worker.Handlers.Models;
using Company.Product.Worker.Managers.Exceptions;
using Company.Product.Worker.Managers.Models;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rebus.Bus;

namespace Company.Product.Worker.Tests.Host;

/// <summary>
/// Sends messages through the real Rebus pipeline and checks what the handlers and the retry rules do with them.
/// <c>Messaging:MaxDeliveryAttempts</c> is 3 in <see cref="WorkerFactory"/>.
/// </summary>
public class MessageHandlingTests(WorkerFactory factory) : IClassFixture<WorkerFactory>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    /// <summary>Long enough for any further delivery attempt to have happened (attempts are immediate).</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(750);

    private IBus Bus => factory.Services.GetRequiredService<IBus>();

    [Fact]
    public async Task Order_placed_message_reaches_the_manager()
    {
        var orderId = Guid.NewGuid();
        var handled = NewSignal();
        factory.Notifications.NotifyOrderPlacedAsync(orderId, Arg.Any<CancellationToken>())
            .Returns(_ => { handled.TrySetResult(); return Task.CompletedTask; });

        await Bus.SendLocal(new OrderPlacedEvent(orderId));

        await handled.Task.WaitAsync(Timeout);
    }

    [Fact]
    public async Task Command_is_mapped_to_a_dto()
    {
        var reference = "map-" + Guid.NewGuid();
        ImportOrderDto? received = null;
        var handled = NewSignal();
        factory.Imports.ImportAsync(Arg.Is<ImportOrderDto>(o => o.Reference == reference), Arg.Any<CancellationToken>())
            .Returns(call => { received = call.Arg<ImportOrderDto>(); handled.TrySetResult(); return Task.CompletedTask; });

        await Bus.SendLocal(new ImportOrderCommand(reference, "Contoso", [new ImportOrderLine("WIDGET-1", 2, 5m)]));
        await handled.Task.WaitAsync(Timeout);

        Assert.Equal("Contoso", received?.CustomerName);
        var line = Assert.Single(received!.Lines);
        Assert.Equal(("WIDGET-1", 2, 5m), (line.Sku, line.Quantity, line.UnitPrice));
    }

    [Fact]
    public async Task Unknown_order_is_dropped_without_retries()
    {
        var orderId = Guid.NewGuid();
        var calls = 0;
        var first = NewSignal();
        factory.Notifications.NotifyOrderPlacedAsync(orderId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref calls);
                first.TrySetResult();
                return Task.FromException(new NotFoundException("no such order"));
            });

        await Bus.SendLocal(new OrderPlacedEvent(orderId));
        await first.Task.WaitAsync(Timeout);
        await Task.Delay(Settle);

        Assert.Equal(1, Volatile.Read(ref calls));
    }

    [Fact]
    public async Task Permanent_failure_fails_fast_after_one_attempt()
    {
        var reference = "invalid-" + Guid.NewGuid();
        var calls = 0;
        var first = NewSignal();
        factory.Imports.ImportAsync(Arg.Is<ImportOrderDto>(o => o.Reference == reference), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref calls);
                first.TrySetResult();
                return Task.FromException(new BusinessValidationException(
                    new Dictionary<string, string[]> { [reference] = ["rejected"] }));
            });

        await Bus.SendLocal(new ImportOrderCommand(reference, "Contoso", []));
        await first.Task.WaitAsync(Timeout);
        await Task.Delay(Settle);

        Assert.Equal(1, Volatile.Read(ref calls));
    }

    [Fact]
    public async Task Transient_failure_is_retried_up_to_the_maximum_delivery_attempts()
    {
        var reference = "transient-" + Guid.NewGuid();
        var calls = 0;
        var last = NewSignal();
        factory.Imports.ImportAsync(Arg.Is<ImportOrderDto>(o => o.Reference == reference), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) >= 3)
                {
                    last.TrySetResult();
                }

                return Task.FromException(new ResourceAccessException("orders API down", new HttpRequestException()));
            });

        await Bus.SendLocal(new ImportOrderCommand(reference, "Contoso", []));
        await last.Task.WaitAsync(Timeout);
        await Task.Delay(Settle);

        Assert.Equal(3, Volatile.Read(ref calls));
    }

    [Fact]
    public async Task Handled_messages_are_counted_in_the_metrics()
    {
        var orderId = Guid.NewGuid();
        var handled = NewSignal();
        factory.Notifications.NotifyOrderPlacedAsync(orderId, Arg.Any<CancellationToken>())
            .Returns(_ => { handled.TrySetResult(); return Task.CompletedTask; });
        await Bus.SendLocal(new OrderPlacedEvent(orderId));
        await handled.Task.WaitAsync(Timeout);

        using var client = factory.CreateClient();
        var metrics = string.Empty;
        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline && !metrics.Contains("messages_handled", StringComparison.Ordinal))
        {
            await Task.Delay(100);
            metrics = await client.GetStringAsync("/metrics");
        }

        Assert.Contains("messages_handled", metrics);
        Assert.Contains("OrderPlacedEvent", metrics);
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
