using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Company.Product.Api.Tests.Telemetry;

/// <summary>
/// Starts the API in memory and checks that Prometheus can scrape it. No database or collector is needed:
/// nothing here touches the database, and OTLP export is off outside Development.
/// </summary>
public class MetricsEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Metrics_endpoint_exposes_prometheus_metrics()
    {
        using var client = factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"))
            .CreateClient();

        using var health = await client.GetAsync("/health");
        using var response = await client.GetAsync("/metrics");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"GET /metrics returned {(int)response.StatusCode}");
        Assert.Contains("# TYPE", body);
        Assert.Contains("dotnet_", body);
    }

    [Fact]
    public async Task Metrics_endpoint_can_be_turned_off()
    {
        using var client = factory
            .WithWebHostBuilder(builder => builder
                .UseEnvironment("Testing")
                .UseSetting("Telemetry:PrometheusEndpoint:Enabled", "false"))
            .CreateClient();

        using var response = await client.GetAsync("/metrics");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
