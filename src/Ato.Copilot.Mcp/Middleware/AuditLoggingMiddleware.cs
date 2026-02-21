using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace Ato.Copilot.Mcp.Middleware;

/// <summary>
/// Middleware that logs all MCP requests for compliance audit trails
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString("N")[..12];

        // Log request
        _logger.LogInformation(
            "ATO Audit | ReqId: {RequestId} | Method: {Method} | Path: {Path} | IP: {IP} | User: {User}",
            requestId,
            context.Request.Method,
            context.Request.Path,
            context.Connection.RemoteIpAddress,
            context.User?.Identity?.Name ?? "anonymous");

        try
        {
            await _next(context);
            stopwatch.Stop();

            _logger.LogInformation(
                "ATO Audit | ReqId: {RequestId} | Status: {Status} | Duration: {Duration}ms",
                requestId,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "ATO Audit | ReqId: {RequestId} | FAILED | Duration: {Duration}ms | Error: {Error}",
                requestId,
                stopwatch.ElapsedMilliseconds,
                ex.Message);

            throw;
        }
    }
}
