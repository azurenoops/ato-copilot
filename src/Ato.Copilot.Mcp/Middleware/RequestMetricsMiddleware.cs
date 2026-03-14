using System.Diagnostics;
using Ato.Copilot.Core.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Mcp.Middleware;

/// <summary>
/// Records HTTP request metrics (duration, total, status) using HttpMetrics instruments.
/// Logs request summary at Information level per FR-045.
/// </summary>
public class RequestMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly HttpMetrics _metrics;
    private readonly ILogger<RequestMetricsMiddleware> _logger;

    public RequestMetricsMiddleware(
        RequestDelegate next,
        HttpMetrics metrics,
        ILogger<RequestMetricsMiddleware> logger)
    {
        _next = next;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        await _next(context);

        stopwatch.Stop();

        var endpoint = context.Request.Path.Value ?? "/";
        var method = context.Request.Method;
        var statusCode = context.Response.StatusCode;

        _metrics.RecordRequest(stopwatch.Elapsed.TotalMilliseconds, endpoint, method, statusCode);

        _logger.LogInformation(
            "HTTP {Method} {Endpoint} → {StatusCode} in {DurationMs:F1}ms | CorrelationId: {CorrelationId}",
            method,
            endpoint,
            statusCode,
            stopwatch.Elapsed.TotalMilliseconds,
            context.Items.TryGetValue("CorrelationId", out var corrId) ? corrId : "-");
    }
}
