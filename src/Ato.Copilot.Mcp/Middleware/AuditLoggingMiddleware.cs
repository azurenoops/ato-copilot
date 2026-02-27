using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;
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

    /// <summary>Initializes a new instance of the <see cref="AuditLoggingMiddleware"/> class.</summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">Logger instance.</param>
    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Invokes the middleware, logging request and response details for audit.</summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString("N")[..12];

        // Extract correlation ID set by CorrelationIdMiddleware (per FR-048)
        var correlationId = context.Items.TryGetValue("CorrelationId", out var cid)
            ? cid?.ToString() ?? requestId
            : requestId;

        // Extract tool name from MCP request path/method
        var toolName = context.Request.Path.Value?.Split('/').LastOrDefault() ?? "unknown";

        // Extract user ID — redacted: first 8 chars + "***" (per FR-048)
        var rawUserId = context.User?.Identity?.Name ?? "anonymous";
        var redactedUserId = rawUserId.Length > 8
            ? rawUserId[..8] + "***"
            : rawUserId;

        // Push structured properties to Serilog LogContext (per FR-048)
        // T022: Include PIM role and action context for infrastructure-modifying actions (FR-018d)
        var pimRole = context.User?.FindFirst("pim_role")?.Value ?? string.Empty;
        var actionName = string.Empty;
        var actionContextStr = string.Empty;

        // For chat/action requests, try to extract action and actionContext from the request body
        if (context.Request.Path.Value?.Contains("/mcp/chat") == true &&
            context.Request.ContentLength > 0 && context.Request.Body.CanSeek)
        {
            try
            {
                context.Request.Body.Position = 0;
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("action", out var actionProp))
                    actionName = actionProp.GetString() ?? string.Empty;
                if (doc.RootElement.TryGetProperty("actionContext", out var contextProp))
                    actionContextStr = contextProp.GetRawText();
            }
            catch { /* body parsing is best-effort for audit */ }
        }

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("AgentName", "compliance"))
        using (LogContext.PushProperty("ToolName", toolName))
        using (LogContext.PushProperty("UserId", redactedUserId))
        using (LogContext.PushProperty("PimRole", pimRole))
        using (LogContext.PushProperty("Action", actionName))
        {
            // Log request
            _logger.LogInformation(
                "ATO Audit | ReqId: {RequestId} | Method: {Method} | Path: {Path} | IP: {IP} | User: {UserId} | PimRole: {PimRole} | Action: {Action}",
                requestId,
                context.Request.Method,
                context.Request.Path,
                context.Connection.RemoteIpAddress,
                redactedUserId,
                pimRole,
                actionName);

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
}
