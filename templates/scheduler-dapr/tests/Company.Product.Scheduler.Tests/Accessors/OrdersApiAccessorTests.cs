using System.Net;
using System.Net.Http.Json;
using Company.Product.Scheduler.Accessors.Exceptions;
using Company.Product.Scheduler.Accessors.Http;
using NSubstitute;

namespace Company.Product.Scheduler.Tests.Accessors;

public class OrdersApiAccessorTests
{
    [Fact]
    public async Task List_maps_orders()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new[]
            {
                new
                {
                    id = Guid.NewGuid(),
                    customerName = "Contoso",
                    lines = new[] { new { lineNumber = 1, sku = "WIDGET-1", quantity = 2, unitPrice = 5m, lineTotal = 10m } },
                    subtotal = 10m,
                    discount = 0m,
                    total = 10m,
                    createdAt = DateTimeOffset.UnixEpoch,
                },
            }),
        });

        var orders = await Accessor(handler).ListRecentAsync(50, CancellationToken.None);

        var order = Assert.Single(orders);
        Assert.Equal(10m, order.Total);
        Assert.Equal("http://orders-api/api/orders?skip=0&take=50", handler.LastRequest?.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task List_failure_is_transient()
    {
        var accessor = Accessor(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        await Assert.ThrowsAsync<ResourceAccessException>(() => accessor.ListRecentAsync(50, CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    public async Task Health_reflects_the_status_code(HttpStatusCode status, bool healthy)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status));

        var health = await Accessor(handler).CheckHealthAsync(CancellationToken.None);

        Assert.Equal(healthy, health.Healthy);
        Assert.Equal("orders-api", health.Name);
        Assert.Equal("http://orders-api/health", handler.LastRequest?.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task Unreachable_api_is_unhealthy_not_an_exception()
    {
        var accessor = Accessor(new StubHandler(_ => throw new HttpRequestException("connection refused")));

        var health = await accessor.CheckHealthAsync(CancellationToken.None);

        Assert.False(health.Healthy);
        Assert.Contains("connection refused", health.Detail, StringComparison.Ordinal);
    }

    private static OrdersApiAccessor Accessor(HttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(OrdersApiAccessor.HttpClientName)
            .Returns(_ => new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://orders-api/") });

        return new OrdersApiAccessor(factory, TimeProvider.System);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }
}
