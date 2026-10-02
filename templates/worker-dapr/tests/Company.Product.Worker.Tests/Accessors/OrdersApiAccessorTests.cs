using System.Net;
using System.Net.Http.Json;
using Company.Product.Worker.Accessors.Exceptions;
using Company.Product.Worker.Accessors.Http;
using Company.Product.Worker.Managers.Exceptions;
using Company.Product.Worker.Managers.Models;
using NSubstitute;

namespace Company.Product.Worker.Tests.Accessors;

public class OrdersApiAccessorTests
{
    private static readonly ImportOrderDto Order = new("A-1", "Contoso", [new ImportOrderLineDto("WIDGET-1", 2, 5m)]);

    [Fact]
    public async Task Create_posts_the_order_and_maps_the_response()
    {
        var id = Guid.NewGuid();
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(new
            {
                id,
                customerName = "Contoso",
                lines = new[] { new { lineNumber = 1, sku = "WIDGET-1", quantity = 2, unitPrice = 5m, lineTotal = 10m } },
                subtotal = 10m,
                discount = 0m,
                total = 10m,
                createdAt = DateTimeOffset.UnixEpoch,
            }),
        });

        var order = await Accessor(handler).CreateAsync(Order, CancellationToken.None);

        Assert.Equal(id, order.Id);
        Assert.Equal(1, order.LineCount);
        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("http://orders-api/api/orders", handler.LastRequest?.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task Find_returns_null_for_404()
    {
        var accessor = Accessor(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        Assert.Null(await accessor.FindAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Create_rejected_by_api_is_a_business_error()
    {
        var accessor = Accessor(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{}") }));

        await Assert.ThrowsAsync<BusinessValidationException>(() => accessor.CreateAsync(Order, CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Server_errors_are_transient(HttpStatusCode status)
    {
        var accessor = Accessor(new StubHandler(_ => new HttpResponseMessage(status)));

        await Assert.ThrowsAsync<ResourceAccessException>(() => accessor.FindAsync(Guid.NewGuid(), CancellationToken.None));
    }

    private static OrdersApiAccessor Accessor(HttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(OrdersApiAccessor.HttpClientName)
            .Returns(_ => new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://orders-api/") });

        return new OrdersApiAccessor(factory);
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
