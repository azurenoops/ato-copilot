namespace Ato.Copilot.Core.Models;

/// <summary>
/// Configuration for OpenTelemetry metrics and tracing export.
/// </summary>
public class OpenTelemetryOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "OpenTelemetry";

    /// <summary>Whether OpenTelemetry export is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Exporter type: "otlp" or "prometheus".</summary>
    public string ExporterType { get; set; } = "otlp";

    /// <summary>OTLP collector endpoint URL.</summary>
    public string OtlpEndpoint { get; set; } = "http://localhost:4317";

    /// <summary>Whether to expose a Prometheus scrape endpoint at /metrics.</summary>
    public bool EnablePrometheus { get; set; }

    /// <summary>Service name reported to the telemetry backend.</summary>
    public string ServiceName { get; set; } = "ato-copilot-mcp";
}
