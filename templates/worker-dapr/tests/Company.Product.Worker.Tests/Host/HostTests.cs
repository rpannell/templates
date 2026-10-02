using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Company.Product.Worker.Tests.Host;

/// <summary>
/// Starts the worker in memory, without Dapr, a broker or the orders API, and checks the wiring Dapr depends on:
/// the subscription list, the binding routes, health and metrics.
/// </summary>
public class HostTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient CreateClient() =>
        factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")).CreateClient();

    [Fact]
    public async Task Dapr_subscribe_lists_the_event_functions()
    {
        using var client = CreateClient();

        var json = await client.GetStringAsync("/dapr/subscribe");
        using var document = JsonDocument.Parse(json);

        var subscription = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal("events", subscription.GetProperty("pubsubname").GetString());
        Assert.Equal("order-placed", subscription.GetProperty("topic").GetString());
        Assert.Equal("functions/order-placed", subscription.GetProperty("route").GetString()?.TrimStart('/'));
    }

    [Theory]
    [InlineData("/functions/object-created")]
    public async Task Binding_routes_answer_options(string route)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, route);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_and_metrics_are_exposed()
    {
        using var client = CreateClient();

        using var health = await client.GetAsync("/health");
        var metrics = await client.GetStringAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Contains("# TYPE", metrics);
    }

    [Fact]
    public async Task Invalid_object_notification_is_acknowledged_not_retried()
    {
        using var client = CreateClient();

        using var response = await client.PostAsync("/functions/object-created", new StringContent("""{"unexpected":true}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
