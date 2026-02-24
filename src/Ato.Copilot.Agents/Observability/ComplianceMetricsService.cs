using System.Diagnostics.Metrics;

namespace Ato.Copilot.Agents.Observability;

/// <summary>
/// Static metrics class providing structured telemetry for NIST controls API operations.
/// Uses <see cref="System.Diagnostics.Metrics.Meter"/> named "Ato.Copilot" for automatic
/// integration with OpenTelemetry exporters when configured.
/// Per FR-039: tracks nist_api_calls_total and nist_api_call_duration_seconds.
/// </summary>
public static class ComplianceMetricsService
{
    /// <summary>The meter name (shared with ToolMetrics).</summary>
    public const string MeterName = "Ato.Copilot";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>
    /// Counter for total NIST API calls. Tags: operation, success.
    /// </summary>
    public static readonly Counter<long> NistApiCalls =
        Meter.CreateCounter<long>(
            "nist_api_calls_total",
            unit: "{calls}",
            description: "Total number of NIST controls API calls");

    /// <summary>
    /// Histogram for NIST API call duration in seconds. Tags: operation.
    /// </summary>
    public static readonly Histogram<double> NistApiDuration =
        Meter.CreateHistogram<double>(
            "nist_api_call_duration_seconds",
            unit: "s",
            description: "NIST controls API call duration in seconds");

    /// <summary>
    /// Records an API call with success/failure status.
    /// </summary>
    /// <param name="operation">The operation name (e.g., "GetCatalog", "SearchControls").</param>
    /// <param name="success">Whether the call succeeded.</param>
    public static void RecordApiCall(string operation, bool success)
    {
        NistApiCalls.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("success", success));
    }

    /// <summary>
    /// Records the duration of an API call.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="durationSeconds">Duration in seconds.</param>
    public static void RecordDuration(string operation, double durationSeconds)
    {
        NistApiDuration.Record(durationSeconds,
            new KeyValuePair<string, object?>("operation", operation));
    }
}
