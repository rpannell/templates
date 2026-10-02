using Company.Product.Worker.Accessors.Setup;
using Company.Product.Worker.Engines.Setup;
using Company.Product.Worker.Functions.Setup;
using Company.Product.Worker.Managers.Setup;
using Company.Product.Worker.Utilities.Setup;

// A Functions-style worker. Dapr (a sidecar) turns events and storage notifications into HTTP calls to the
// endpoints in Functions/. Which broker or storage is used is decided by the Dapr component files, not by this code.
// Scheduled jobs live in a separate single-replica app (the scheduler-dapr template) so this worker can autoscale.
var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry logs, traces and metrics (exported over OTLP when OTEL_EXPORTER_OTLP_ENDPOINT is set).
builder.AddTelemetry();

builder.Services.AddDaprClient();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

builder.Services
    .AddUtilities()
    .AddAccessors(builder.Configuration)
    .AddEngines()
    .AddManagers()
    .AddFunctions();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCloudEvents();          // unwraps CloudEvents from Dapr pub/sub before model binding
app.MapSubscribeHandler();     // GET /dapr/subscribe: tells Dapr which topics the functions listen to

app.MapFunctions();
app.MapHealthChecks(TelemetrySetup.HealthCheckPath);
app.MapTelemetryEndpoints();   // Prometheus scrape endpoint: /metrics

app.Run();

/// <summary>Application entry point. Declared so tests and tooling can reference the worker assembly.</summary>
public partial class Program;
