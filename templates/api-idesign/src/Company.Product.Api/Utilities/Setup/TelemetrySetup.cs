using Company.Product.Api.Utilities.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Company.Product.Api.Utilities.Setup;

/// <summary>Registers OpenTelemetry logging, tracing and metrics.</summary>
public static class TelemetrySetup
{
    /// <summary>Standard OpenTelemetry setting for the OTLP collector address. Nothing is exported when it's empty.</summary>
    public const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>Standard OpenTelemetry setting that overrides <see cref="ApplicationTelemetry.ServiceName"/>.</summary>
    public const string ServiceNameKey = "OTEL_SERVICE_NAME";

    /// <summary>Health check path. Excluded from traces so probes don't flood the trace store.</summary>
    public const string HealthCheckPath = "/health";

    /// <summary>Prometheus scrape path. Excluded from traces.</summary>
    public const string MetricsPath = "/metrics";

    /// <summary>Setting that turns the Prometheus <c>/metrics</c> endpoint on or off. On by default.</summary>
    public const string PrometheusEndpointEnabledKey = "Telemetry:PrometheusEndpoint:Enabled";

    /// <summary>
    /// Sends ILogger logs, traces and metrics through OpenTelemetry. Logs carry the trace and span IDs of the
    /// request that wrote them. Exports over OTLP (push) when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set; the other
    /// standard <c>OTEL_*</c> settings (protocol, headers, resource attributes) apply as usual. Metrics are also
    /// exposed for Prometheus to scrape (pull) unless <c>Telemetry:PrometheusEndpoint:Enabled</c> is false;
    /// map the endpoint with <see cref="MapTelemetryEndpoints"/>.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IHostApplicationBuilder AddTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var prometheusEnabled = IsPrometheusEndpointEnabled(builder.Configuration);
        var serviceName = builder.Configuration[ServiceNameKey] ?? ApplicationTelemetry.ServiceName;
        var serviceVersion = typeof(TelemetrySetup).Assembly.GetName().Version?.ToString();

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: serviceVersion)
                .AddAttributes([new("deployment.environment.name", builder.Environment.EnvironmentName)]))
            .WithTracing(tracing => tracing
                .AddSource(ApplicationTelemetry.ServiceName)
                .AddSource("Npgsql")
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context =>
                        !context.Request.Path.StartsWithSegments(HealthCheckPath)
                        && !context.Request.Path.StartsWithSegments(MetricsPath))
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(ApplicationTelemetry.ServiceName)
                    .AddMeter("System.Runtime")
                    .AddMeter("Microsoft.EntityFrameworkCore")
                    .AddMeter("Npgsql")
                    .AddMeter("Polly")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (prometheusEnabled)
                {
                    metrics.AddPrometheusExporter();
                }
            });

        if (!string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointKey]))
        {
            telemetry.UseOtlpExporter();
        }

        return builder;
    }

    /// <summary>Maps the Prometheus scrape endpoint at <see cref="MetricsPath"/>, unless it's turned off.</summary>
    /// <param name="app">The web application.</param>
    /// <returns>The same application, for chaining.</returns>
    public static WebApplication MapTelemetryEndpoints(this WebApplication app)
    {
        if (IsPrometheusEndpointEnabled(app.Configuration))
        {
            app.MapPrometheusScrapingEndpoint(MetricsPath);
        }

        return app;
    }

    /// <summary>Reads <see cref="PrometheusEndpointEnabledKey"/>, defaulting to on.</summary>
    /// <param name="configuration">Application configuration.</param>
    /// <returns><see langword="true"/> unless the setting is explicitly false.</returns>
    private static bool IsPrometheusEndpointEnabled(IConfiguration configuration) =>
        configuration.GetValue(PrometheusEndpointEnabledKey, defaultValue: true);
}
