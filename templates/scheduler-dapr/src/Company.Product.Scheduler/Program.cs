using Company.Product.Scheduler.Accessors.Setup;
using Company.Product.Scheduler.Engines.Setup;
using Company.Product.Scheduler.Jobs.Setup;
using Company.Product.Scheduler.Managers.Setup;
using Company.Product.Scheduler.Utilities.Setup;

// Scheduled jobs. A Dapr sidecar holds the cron schedules (dapr/components/*.yaml) and calls POST /jobs/<name>
// when one fires. This app runs as exactly ONE replica: every replica's sidecar fires every schedule.
// It doesn't reference Dapr: the sidecar just makes HTTP calls to it.
var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry logs, traces and metrics (exported over OTLP when OTEL_EXPORTER_OTLP_ENDPOINT is set).
builder.AddTelemetry();

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

builder.Services
    .AddUtilities()
    .AddAccessors(builder.Configuration)
    .AddEngines()
    .AddManagers()
    .AddJobs();

var app = builder.Build();

app.UseExceptionHandler();

app.MapJobs();
app.MapHealthChecks(TelemetrySetup.HealthCheckPath);
app.MapTelemetryEndpoints();   // Prometheus scrape endpoint: /metrics

app.Run();

/// <summary>Application entry point. Declared so tests and tooling can reference the scheduler assembly.</summary>
public partial class Program;
