using Company.Product.Api.Accessors.Setup;
using Company.Product.Api.Controllers.ErrorHandling;
using Company.Product.Api.Engines.Setup;
using Company.Product.Api.Managers.Setup;
using Company.Product.Api.Utilities.Setup;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry logs, traces and metrics (exported over OTLP when OTEL_EXPORTER_OTLP_ENDPOINT is set).
builder.AddTelemetry();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks();

builder.Services
    .AddUtilities()
    .AddAccessors(builder.Configuration)
    .AddEngines(builder.Configuration)
    .AddManagers();

var app = builder.Build();

app.UseExceptionHandler();

// API docs: OpenAPI document at /openapi/v1.json, Scalar UI at /scalar.
// On in Development by default; set ApiDocs__Enabled=true to turn on elsewhere.
if (app.Configuration.GetValue("ApiDocs:Enabled", app.Environment.IsDevelopment()))
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();
}

app.MapControllers();
app.MapHealthChecks(TelemetrySetup.HealthCheckPath);
app.MapTelemetryEndpoints();   // Prometheus scrape endpoint: /metrics

app.Run();

/// <summary>Application entry point. Declared so tests and tooling can reference the API assembly.</summary>
public partial class Program;
