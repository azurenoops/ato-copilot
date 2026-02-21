using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;

namespace Ato.Copilot.Mcp.Middleware;

/// <summary>
/// Middleware for compliance-level authorization checks.
/// Validates user claims against required compliance roles and enforces
/// tool-level RBAC: Auditor is read-only, Analyst cannot approve, Administrator has full access.
/// </summary>
public class ComplianceAuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ComplianceAuthorizationMiddleware> _logger;

    /// <summary>Tools that modify compliance state (write operations).</summary>
    private static readonly HashSet<string> WriteTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "compliance_remediate",
        "compliance_validate_remediation",
        "compliance_remediation_plan",
        "compliance_collect_evidence"
    };

    /// <summary>Tools that require administrator/officer approval authority.</summary>
    private static readonly HashSet<string> ApprovalTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "compliance_validate_remediation"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplianceAuthorizationMiddleware"/> class.
    /// </summary>
    public ComplianceAuthorizationMiddleware(RequestDelegate next, ILogger<ComplianceAuthorizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Processes the HTTP request, enforcing authentication and role-based tool access.
    /// </summary>
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
                      context.User.IsInRole(ComplianceRoles.Auditor) ||
                      context.User.IsInRole(ComplianceRoles.Administrator);

        if (!hasRole)
        {
            _logger.LogWarning("Access denied for user {User} — missing compliance role",
                context.User.Identity?.Name);
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { error = "Insufficient compliance permissions" });
            return;
        }

        // Tool-level RBAC enforcement
        var toolName = context.Items["ToolName"] as string;
        if (!string.IsNullOrEmpty(toolName))
        {
            // Auditor: read-only — deny write tools
            if (context.User.IsInRole(ComplianceRoles.Auditor) && !context.User.IsInRole(ComplianceRoles.Administrator))
            {
                if (WriteTools.Contains(toolName))
                {
                    _logger.LogWarning("Auditor {User} denied access to write tool {Tool}",
                        context.User.Identity?.Name, toolName);
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Auditors have read-only access. Write operations require Analyst or Administrator role."
                    });
                    return;
                }
            }

            // Analyst/Viewer: deny approval tools
            if ((context.User.IsInRole(ComplianceRoles.Analyst) || context.User.IsInRole(ComplianceRoles.Viewer))
                && !context.User.IsInRole(ComplianceRoles.Administrator))
            {
                if (ApprovalTools.Contains(toolName))
                {
                    _logger.LogWarning("User {User} denied access to approval tool {Tool}",
                        context.User.Identity?.Name, toolName);
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Approval operations require Administrator role."
                    });
                    return;
                }
            }
        }

        await _next(context);
    }

    /// <summary>
    /// Checks whether a given tool name requires write access.
    /// </summary>
    public static bool IsWriteTool(string toolName) => WriteTools.Contains(toolName);

    /// <summary>
    /// Checks whether a given tool name requires approval authority.
    /// </summary>
    public static bool IsApprovalTool(string toolName) => ApprovalTools.Contains(toolName);
}
