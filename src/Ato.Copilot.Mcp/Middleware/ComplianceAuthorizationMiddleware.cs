using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;

namespace Ato.Copilot.Mcp.Middleware;

/// <summary>
/// Middleware for compliance-level authorization checks.
/// When enabled, validates user claims against required compliance roles.
/// </summary>
public class ComplianceAuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ComplianceAuthorizationMiddleware> _logger;

    public ComplianceAuthorizationMiddleware(RequestDelegate next, ILogger<ComplianceAuthorizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip auth for health checks
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        // In development or stdio mode, skip auth
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (environment == "Development")
        {
            await _next(context);
            return;
        }

        // Check for authenticated user
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("Unauthorized access attempt from {IP}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Authentication required" });
            return;
        }

        // Verify compliance reader role minimum
        var hasRole = context.User.IsInRole(ComplianceRoles.Viewer) ||
                      context.User.IsInRole(ComplianceRoles.Analyst) ||
                      context.User.IsInRole(ComplianceRoles.Administrator);

        if (!hasRole)
        {
            _logger.LogWarning("Access denied for user {User} — missing compliance role",
                context.User.Identity?.Name);
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { error = "Insufficient compliance permissions" });
            return;
        }

        await _next(context);
    }
}
