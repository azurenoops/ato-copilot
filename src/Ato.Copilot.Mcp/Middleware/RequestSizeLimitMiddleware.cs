using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Mcp.Middleware;

/// <summary>
/// Middleware that rejects HTTP requests with bodies exceeding the configured size limit.
/// Applies only to /mcp/chat and /mcp/chat/stream endpoints (FR-013).
/// </summary>
public class RequestSizeLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly long _maxRequestBodySize;
    private readonly ILogger<RequestSizeLimitMiddleware> _logger;

    public RequestSizeLimitMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<RequestSizeLimitMiddleware> logger)
    {
        _next = next;
        _maxRequestBodySize = configuration.GetValue("Server:MaxRequestBodySizeKb", 32) * 1024L;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if ((context.Request.Path.StartsWithSegments("/mcp/chat") ||
             context.Request.Path.StartsWithSegments("/mcp/chat/stream")) &&
            context.Request.ContentLength > _maxRequestBodySize)
        {
            _logger.LogWarning(
                "Request body too large for {Path}: {ContentLength} bytes exceeds {MaxSize} bytes",
                context.Request.Path,
                context.Request.ContentLength,
                _maxRequestBodySize);

            context.Response.StatusCode = StatusCodes.Status413RequestEntityTooLarge;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                "{\"errorCode\":\"PAYLOAD_TOO_LARGE\",\"message\":\"Request body exceeds maximum allowed size.\"}");
            return;
        }

        await _next(context);
    }
}
