using System.Net;

namespace Company.Product.Worker.Tests.Host;

/// <summary>The worker starts without a broker or the orders API, and serves the endpoints Kubernetes and Prometheus use.</summary>
public class HostTests(WorkerFactory factory) : IClassFixture<WorkerFactory>
{
    [Fact]
    public async Task Health_and_metrics_are_exposed()
    {
        using var client = factory.CreateClient();

        using var health = await client.GetAsync("/health");
        var metrics = await client.GetStringAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Contains("# TYPE", metrics);
    }
}
