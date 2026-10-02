using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Company.Product.Scheduler.Utilities.Telemetry;

/// <summary>
/// This service's own OpenTelemetry sources. Use <see cref="ActivitySource"/> for custom spans and
/// <see cref="Meter"/> for custom metrics. Both are exported by <c>TelemetrySetup</c>.
/// </summary>
public static class ApplicationTelemetry
{
    /// <summary>Default service name, and the name of the ActivitySource and Meter.</summary>
    public const string ServiceName = "Company.Product.Scheduler";

    /// <summary>Source for custom spans, for example around a use case.</summary>
    public static readonly ActivitySource ActivitySource = new(ServiceName);

    /// <summary>Meter for custom business metrics, for example orders created.</summary>
    public static readonly Meter Meter = new(ServiceName);
}
