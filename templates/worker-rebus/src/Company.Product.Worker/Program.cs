using Company.Product.Worker.Accessors.Setup;
using Company.Product.Worker.Engines.Setup;
using Company.Product.Worker.Handlers.Setup;
using Company.Product.Worker.Managers.Setup;
using Company.Product.Worker.Utilities.Setup;

// A message worker (think Azure Functions with a Service Bus trigger) built on Rebus. Rebus reads messages from this
// worker's queue and calls the handlers in Handlers/. The broker is chosen in configuration (Messaging section)
// and in Handlers/Setup/MessagingSetup.cs. Kestrel only serves /health and /metrics: there are no API endpoints.
// Scheduled work does NOT belong here: this worker autoscales, and a timer would then fire once per replica.
var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry logs, traces and metrics (exported over OTLP when OTEL_EXPORTER_OTLP_ENDPOINT is set).
builder.AddTelemetry();

builder.Services.AddHealthChecks();

builder.Services
    .AddUtilities()
    .AddAccessors(builder.Configuration)
    .AddEngines()
    .AddManagers()
    .AddHandlers(builder.Configuration);

var app = builder.Build();

app.MapHealthChecks(TelemetrySetup.HealthCheckPath);
app.MapTelemetryEndpoints();   // Prometheus scrape endpoint: /metrics

app.Run();

/// <summary>Application entry point. Declared so tests and tooling can reference the worker assembly.</summary>
public partial class Program;
