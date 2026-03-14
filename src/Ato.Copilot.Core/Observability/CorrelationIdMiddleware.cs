using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using System.Diagnostics;

namespace Ato.Copilot.Core.Observability;

/// <summary>
/// Middleware that ensures every request has a Correlation ID for end-to-end tracing.
/// Reads <c>X-Correlation-ID</c> from the request header; if missing, generates a new GUID.
/// Stores the ID in <c>HttpContext.Items["CorrelationId"]</c>, pushes it into the
/// Serilog <see cref="LogContext"/>, and adds it to the response headers.
/// Must run as the first middleware in the pipeline (before <c>UseSerilogRequestLogging</c>)
/// per R-012.
/// </summary>
public class CorrelationIdMiddleware
{
    /// <summary>The HTTP header name used for correlation IDs.</summary>
    public const string HeaderName = "X-Correlation-ID";

    /// <summary>The key used to store the correlation ID in <see cref="HttpContext.Items"/>.</summary>
    public const string ItemsKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CorrelationIdMiddleware"/>.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">Logger instance.</param>
    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware. Extracts or generates a Correlation ID, pushes it to
    /// Serilog LogContext, stores it in HttpContext.Items, and adds it to the response headers.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        // Store in HttpContext.Items for downstream middleware
        context.Items[ItemsKey] = correlationId;

        // Add to response headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // Push into Serilog LogContext so all downstream log statements include it
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", Activity.Current?.TraceId.ToString() ?? ""))
        using (LogContext.PushProperty("SpanId", Activity.Current?.SpanId.ToString() ?? ""))
        {
            _logger.LogDebug("Correlation ID: {CorrelationId}", correlationId);
            await _next(context);
        }
    }
}
