using System.Net;
using System.Net.Http.Json;
using Company.Product.Api.Accessors.Exceptions;
using Company.Product.Api.Accessors.Http;
using NSubstitute;

namespace Company.Product.Api.Tests.Accessors;

public class InventoryApiAccessorTests
{
    [Fact]
    public async Task GetStockLevelsAsync_calls_stock_endpoint_and_maps_response()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new[] { new { sku = "A-1", quantityOnHand = 5 } }),
        });
        var accessor = new InventoryApiAccessor(Factory(handler));

        var stockLevels = await accessor.GetStockLevelsAsync(["A-1", "B 2"], CancellationToken.None);

        var stockLevel = Assert.Single(stockLevels);
        Assert.Equal("A-1", stockLevel.Sku);
        Assert.Equal(5, stockLevel.Available);
        Assert.Equal("http://inventory/api/stock?sku=A-1&sku=B%202", handler.LastRequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task GetStockLevelsAsync_skips_the_call_for_no_skus()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("Should not be called."));
        var accessor = new InventoryApiAccessor(Factory(handler));

        var stockLevels = await accessor.GetStockLevelsAsync([], CancellationToken.None);

        Assert.Empty(stockLevels);
        Assert.Null(handler.LastRequestUri);
    }

    [Fact]
    public async Task GetStockLevelsAsync_wraps_http_failure()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var accessor = new InventoryApiAccessor(Factory(handler));

        await Assert.ThrowsAsync<ResourceAccessException>(
            () => accessor.GetStockLevelsAsync(["A-1"], CancellationToken.None));
    }

    private static IHttpClientFactory Factory(HttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(InventoryApiAccessor.HttpClientName)
            .Returns(_ => new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://inventory/") });

        return factory;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            return Task.FromResult(respond(request));
        }
    }
}
