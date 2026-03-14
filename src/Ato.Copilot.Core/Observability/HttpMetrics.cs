using System.Diagnostics.Metrics;

namespace Ato.Copilot.Core.Observability;

/// <summary>
/// HTTP-level metrics instruments for the MCP server.
/// Captures request duration, total request count, and cache hit/miss counters.
/// Uses <see cref="System.Diagnostics.Metrics.Meter"/> named "ato.copilot.http"
/// for OpenTelemetry export per FR-022.
/// </summary>
public class HttpMetrics
{
    /// <summary>The meter name for HTTP-level metrics.</summary>
    public const string MeterName = "ato.copilot.http";

    private readonly Meter _meter;

    /// <summary>
    /// Histogram for HTTP request duration in milliseconds.
    /// Tags: endpoint, method, status_code.
    /// </summary>
    public Histogram<double> RequestDuration { get; }

    /// <summary>
    /// Counter for total HTTP requests.
    /// Tags: endpoint, method, status_code.
    /// </summary>
    public Counter<long> RequestTotal { get; }

    /// <summary>
    /// Counter for cache hits.
    /// Tags: cache_name.
    /// </summary>
    public Counter<long> CacheHits { get; }

    /// <summary>
    /// Counter for cache misses.
    /// Tags: cache_name.
    /// </summary>
    public Counter<long> CacheMisses { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="HttpMetrics"/> with all instruments.
    /// </summary>
    public HttpMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        RequestDuration = _meter.CreateHistogram<double>(
            "ato.copilot.http.request.duration",
            unit: "ms",
            description: "HTTP request duration in milliseconds");

        RequestTotal = _meter.CreateCounter<long>(
            "ato.copilot.http.request.total",
            unit: "{requests}",
            description: "Total number of HTTP requests");

        CacheHits = _meter.CreateCounter<long>(
            "ato.copilot.cache.hits",
            unit: "{hits}",
            description: "Total number of cache hits");

        CacheMisses = _meter.CreateCounter<long>(
            "ato.copilot.cache.misses",
            unit: "{misses}",
            description: "Total number of cache misses");
    }

    /// <summary>
    /// Records a completed HTTP request with duration and tags.
    /// </summary>
    public void RecordRequest(double durationMs, string endpoint, string method, int statusCode)
    {
        var endpointTag = new KeyValuePair<string, object?>("endpoint", endpoint);
        var methodTag = new KeyValuePair<string, object?>("method", method);
        var statusTag = new KeyValuePair<string, object?>("status_code", statusCode.ToString());

        RequestDuration.Record(durationMs, endpointTag, methodTag, statusTag);
        RequestTotal.Add(1, endpointTag, methodTag, statusTag);
    }

    /// <summary>Records a cache hit for the specified cache.</summary>
    public void RecordCacheHit(string cacheName) =>
        CacheHits.Add(1, new KeyValuePair<string, object?>("cache_name", cacheName));

    /// <summary>Records a cache miss for the specified cache.</summary>
    public void RecordCacheMiss(string cacheName) =>
        CacheMisses.Add(1, new KeyValuePair<string, object?>("cache_name", cacheName));
}
