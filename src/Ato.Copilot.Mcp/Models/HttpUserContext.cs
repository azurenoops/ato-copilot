using Microsoft.AspNetCore.Http;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Auth;

namespace Ato.Copilot.Mcp.Models;

/// <summary>
/// ASP.NET Core implementation of <see cref="IUserContext"/> that reads
/// authenticated user identity from <see cref="IHttpContextAccessor"/>.
/// Properties are lazily extracted from <c>HttpContext.User</c> claims
/// and cached for the lifetime of the request.
/// When <c>HttpContext</c> is null (e.g., stdio mode, background services),
/// all properties return their fallback defaults.
/// </summary>
public class HttpUserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private string? _userId;
    private string? _displayName;
    private string? _role;
    private bool? _isAuthenticated;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpUserContext"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">Accessor for the current HTTP context.</param>
    public HttpUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public string UserId => _userId ??= ResolveUserId();

    /// <inheritdoc />
    public string DisplayName => _displayName ??= ResolveDisplayName();

    /// <inheritdoc />
    public string Role => _role ??= ResolveRole();

    /// <inheritdoc />
    public bool IsAuthenticated => _isAuthenticated ??= ResolveIsAuthenticated();

    private string ResolveUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return "anonymous";

        var oid = user.FindFirst("oid")?.Value;
        if (!string.IsNullOrEmpty(oid))
            return oid;

        var sub = user.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(sub))
            return sub;

        return "anonymous";
    }

    private string ResolveDisplayName()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return UserId;

        var name = user.Identity?.Name;
        return !string.IsNullOrEmpty(name) ? name : UserId;
    }

    private string ResolveRole()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return ComplianceRoles.Viewer;

        // Check roles in priority order — return highest-privilege match
        if (user.IsInRole(ComplianceRoles.Administrator))
            return ComplianceRoles.Administrator;
        if (user.IsInRole(ComplianceRoles.SecurityLead))
            return ComplianceRoles.SecurityLead;
        if (user.IsInRole(ComplianceRoles.Analyst))
            return ComplianceRoles.Analyst;
        if (user.IsInRole(ComplianceRoles.Auditor))
            return ComplianceRoles.Auditor;
        if (user.IsInRole(ComplianceRoles.PlatformEngineer))
            return ComplianceRoles.PlatformEngineer;

        return ComplianceRoles.Viewer;
    }

    private bool ResolveIsAuthenticated()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    }
}
