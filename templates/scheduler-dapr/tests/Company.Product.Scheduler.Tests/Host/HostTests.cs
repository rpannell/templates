using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Company.Product.Scheduler.Tests.Host;

/// <summary>
/// Starts the scheduler in memory, without Dapr or the orders API, and checks the wiring the cron components depend on.
/// If a route here changes, the matching <c>route</c> in dapr/components and deploy/kubernetes must change too.
/// </summary>
public class HostTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient CreateClient() =>
        factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")).CreateClient();

    [Theory]
    [InlineData("/jobs/daily-sales-summary")]
    [InlineData("/jobs/orders-api-health")]
    public async Task Job_routes_answer_options(string route)
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
    public async Task Jobs_only_accept_post_and_options()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/jobs/daily-sales-summary");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
