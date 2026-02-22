using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Auth;
using Ato.Copilot.Core.Models.Auth;

namespace Ato.Copilot.Agents.Compliance.Tools;

// ─────────────────────────────────────────────────────────────────────────────
// CAC Authentication Tools
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Tool: cac_status — Tier 1. Check current CAC authentication status,
/// session information, and active PIM roles. No parameters required.
/// </summary>
public class CacStatusTool : BaseTool
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Initializes a new instance of the <see cref="CacStatusTool"/> class.</summary>
    public CacStatusTool(
        IServiceScopeFactory scopeFactory,
        ILogger<CacStatusTool> logger) : base(logger)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public override string Name => "cac_status";

    /// <inheritdoc />
    public override string Description => "Check current CAC authentication status, session information, and active PIM roles.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>();

    /// <inheritdoc />
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var userId = GetArg<string>(arguments, "user_id");

        if (string.IsNullOrEmpty(userId))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    authenticated = false,
                    message = "No active CAC session. Authenticate with your CAC/PIV card to access Azure operations."
                },
                metadata = new
                {
                    toolName = Name,
                    executionTimeMs = sw.ElapsedMilliseconds
                }
            });
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var cacService = scope.ServiceProvider.GetRequiredService<ICacSessionService>();

        var session = await cacService.GetActiveSessionAsync(userId, cancellationToken);

        sw.Stop();

        if (session == null)
        {
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    authenticated = false,
                    message = "No active CAC session. Authenticate with your CAC/PIV card to access Azure operations."
                },
                metadata = new
                {
                    toolName = Name,
                    executionTimeMs = sw.ElapsedMilliseconds
                }
            });
        }

        var remainingMinutes = (int)Math.Max(0, (session.ExpiresAt - DateTimeOffset.UtcNow).TotalMinutes);

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                authenticated = true,
                identity = new
                {
                    displayName = session.DisplayName,
                    email = session.Email,
                    userId = session.UserId
                },
                session = new
                {
                    sessionId = session.Id,
                    sessionStart = session.SessionStart,
                    expiresAt = session.ExpiresAt,
                    remainingMinutes,
                    clientType = session.ClientType.ToString()
                },
                activePimRoles = Array.Empty<object>()
            },
            metadata = new
            {
                toolName = Name,
                executionTimeMs = sw.ElapsedMilliseconds
            }
        });
    }
}

/// <summary>
/// Tool: cac_sign_out — Tier 2. End the current CAC session, clear cached tokens,
/// and revert to unauthenticated state. Tier 1 operations remain available.
/// </summary>
public class CacSignOutTool : BaseTool
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Initializes a new instance of the <see cref="CacSignOutTool"/> class.</summary>
    public CacSignOutTool(
        IServiceScopeFactory scopeFactory,
        ILogger<CacSignOutTool> logger) : base(logger)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public override string Name => "cac_sign_out";

    /// <inheritdoc />
    public override PimTier RequiredPimTier => PimTier.Write;

    /// <inheritdoc />
    public override string Description => "End the current CAC session, clear cached tokens, and revert to unauthenticated state. Tier 1 operations remain available.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>();

    /// <inheritdoc />
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var userId = GetArg<string>(arguments, "user_id");

        if (string.IsNullOrEmpty(userId))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "AUTH_REQUIRED",
                    message = "No authenticated session to sign out from.",
                    suggestion = "You are not currently authenticated."
                },
                metadata = new
                {
                    toolName = Name,
                    executionTimeMs = sw.ElapsedMilliseconds
                }
            });
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var cacService = scope.ServiceProvider.GetRequiredService<ICacSessionService>();

        var session = await cacService.GetActiveSessionAsync(userId, cancellationToken);

        if (session == null)
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "AUTH_REQUIRED",
                    message = "No active CAC session found.",
                    suggestion = "You are not currently authenticated."
                },
                metadata = new
                {
                    toolName = Name,
                    executionTimeMs = sw.ElapsedMilliseconds
                }
            });
        }

        await cacService.TerminateSessionAsync(session.Id, cancellationToken);

        Logger.LogInformation("CAC session {SessionId} signed out by user {UserId}", session.Id, userId);

        sw.Stop();

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                message = "CAC session terminated. You can still use local features (NIST control lookup, cached assessments, Kanban board). Azure operations will require re-authentication.",
                sessionTerminated = true,
                activePimRolesDeactivated = 0
            },
            metadata = new
            {
                toolName = Name,
                executionTimeMs = sw.ElapsedMilliseconds
            }
        });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CAC Session Configuration Tools
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Tool: cac_set_timeout — Tier 2. Set the CAC session timeout duration
/// within policy limits (1-24 hours). Returns previous and new timeout.
/// </summary>
public class CacSetTimeoutTool : BaseTool
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Initializes a new instance of the <see cref="CacSetTimeoutTool"/> class.</summary>
    public CacSetTimeoutTool(
        IServiceScopeFactory scopeFactory,
        ILogger<CacSetTimeoutTool> logger) : base(logger)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public override string Name => "cac_set_timeout";

    /// <inheritdoc />
    public override PimTier RequiredPimTier => PimTier.Write;

    /// <inheritdoc />
    public override string Description => "Set the CAC session timeout duration within policy limits.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["timeoutHours"] = new()
        {
            Type = "integer",
            Description = "Desired session timeout in hours (1-24)",
            Required = true
        }
    };

    /// <inheritdoc />
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var userId = GetArg<string>(arguments, "user_id");
        var timeoutHours = GetArg<int>(arguments, "timeoutHours");

        if (string.IsNullOrEmpty(userId))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "AUTH_REQUIRED",
                    message = "Authentication required to configure session timeout.",
                    suggestion = "Authenticate with your CAC/PIV card first."
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        if (timeoutHours < 1 || timeoutHours > 24)
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "INVALID_TIMEOUT_DURATION",
                    message = "Timeout must be between 1 and 24 hours.",
                    suggestion = $"Try a value between 1 and 24 hours."
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var cacService = scope.ServiceProvider.GetRequiredService<ICacSessionService>();

        var session = await cacService.GetActiveSessionAsync(userId, cancellationToken);
        if (session == null)
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "AUTH_REQUIRED",
                    message = "No active CAC session found.",
                    suggestion = "Authenticate with your CAC/PIV card first."
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        var previousExpiry = session.ExpiresAt;
        var previousTimeoutHours = (int)Math.Round((previousExpiry - session.SessionStart).TotalHours);

        try
        {
            var updated = await cacService.UpdateTimeoutAsync(session.Id, timeoutHours, cancellationToken);

            sw.Stop();

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    previousTimeout = $"{previousTimeoutHours} hours",
                    newTimeout = $"{timeoutHours} hours",
                    newExpiresAt = updated.ExpiresAt.ToString("o"),
                    message = $"Session timeout updated to {timeoutHours} hours. Your session now expires at {updated.ExpiresAt:HH:mm} UTC."
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }
        catch (ArgumentOutOfRangeException)
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "INVALID_TIMEOUT_DURATION",
                    message = "Timeout must be between 1 and 24 hours.",
                    suggestion = $"Try a value between 1 and 24 hours. Current session timeout: {previousTimeoutHours} hours."
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }
    }
}

/// <summary>
/// Tool: cac_map_certificate — Tier 2. Map the current CAC certificate identity
/// to a platform role. Future authentications with this certificate will automatically
/// resolve to the mapped role.
/// </summary>
public class CacMapCertificateTool : BaseTool
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CacMapCertificateTool"/> class.
    /// </summary>
    public CacMapCertificateTool(IServiceScopeFactory scopeFactory, ILogger<CacMapCertificateTool> logger)
        : base(logger)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public override string Name => "cac_map_certificate";

    /// <inheritdoc />
    public override PimTier RequiredPimTier => PimTier.Write;

    /// <inheritdoc />
    public override string Description =>
        "Map the current CAC certificate identity to a platform role. Future authentications with this certificate will automatically resolve to the mapped role.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["role"] = new ToolParameter
        {
            Name = "role",
            Type = "string",
            Required = true,
            Description = "Platform role to map (Administrator, Auditor, Analyst, Viewer, SecurityLead, PlatformEngineer)"
        }
    };

    /// <inheritdoc />
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var userId = GetArg<string>(arguments, "user_id");
        var role = GetArg<string>(arguments, "role");

        if (string.IsNullOrEmpty(userId))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "AUTH_REQUIRED",
                    message = "CAC authentication required to map certificates.",
                    suggestion = "Please authenticate with your CAC/PIV card first."
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        if (string.IsNullOrEmpty(role))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "INVALID_ROLE",
                    message = "Role parameter is required.",
                    suggestion = "Specify one of: Administrator, Auditor, Analyst, Viewer, SecurityLead, PlatformEngineer"
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        // Normalize role name — accept short names like "Auditor" or full names like "Compliance.Auditor"
        var normalizedRole = NormalizeRoleName(role);
        if (normalizedRole == null)
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "INVALID_ROLE",
                    message = $"Invalid role '{role}'.",
                    suggestion = "Valid roles: Administrator, Auditor, Analyst, Viewer, SecurityLead, PlatformEngineer"
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        using var scope = _scopeFactory.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ICertificateRoleResolver>();
        var cacService = scope.ServiceProvider.GetRequiredService<ICacSessionService>();

        // Get active session to extract certificate info
        var session = await cacService.GetActiveSessionAsync(userId, cancellationToken);
        if (session == null)
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "AUTH_REQUIRED",
                    message = "No active CAC session found.",
                    suggestion = "Please authenticate with your CAC/PIV card first."
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        // Extract certificate thumbprint and subject from session context
        // In production, these come from JWT claims (x5t, sub)
        // In simulation, generate from token hash and user ID
        var thumbprint = session.TokenHash.Length >= 40
            ? session.TokenHash[..40]
            : session.TokenHash;
        var subject = $"CN={userId}";

        var mapping = await resolver.MapCertificateAsync(
            thumbprint, subject, normalizedRole, userId, cancellationToken);

        sw.Stop();
        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                certificateThumbprint = mapping.CertificateThumbprint,
                certificateSubject = mapping.CertificateSubject,
                mappedRole = mapping.MappedRole,
                message = $"Certificate mapped to {GetRoleDisplayName(normalizedRole)} role. Future CAC authentications will automatically assign this role."
            },
            metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
        });
    }

    /// <summary>
    /// Normalizes short role names (e.g., "Auditor") to full ComplianceRoles constants (e.g., "Compliance.Auditor").
    /// </summary>
    private static string? NormalizeRoleName(string role)
    {
        var trimmed = role.Trim();

        // Already a full role name
        if (CertificateRoleResolver.IsValidRole(trimmed))
            return trimmed;

        // Map short names to full names
        return trimmed.ToLowerInvariant() switch
        {
            "administrator" or "admin" => ComplianceRoles.Administrator,
            "auditor" => ComplianceRoles.Auditor,
            "analyst" => ComplianceRoles.Analyst,
            "viewer" => ComplianceRoles.Viewer,
            "securitylead" or "security lead" or "security_lead" => ComplianceRoles.SecurityLead,
            "platformengineer" or "platform engineer" or "platform_engineer" or "engineer" => ComplianceRoles.PlatformEngineer,
            _ => null
        };
    }

    /// <summary>
    /// Gets a display-friendly role name (without the "Compliance." prefix).
    /// </summary>
    private static string GetRoleDisplayName(string fullRole) =>
        fullRole.StartsWith("Compliance.") ? fullRole["Compliance.".Length..] : fullRole;
}

// ─────────────────────────────────────────────────────────────────────────────
// PIM Role Management Tools
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Tool: pim_list_eligible — Tier 2. List all PIM-eligible role assignments
/// for the authenticated user, optionally filtered by scope.
/// </summary>
public class PimListEligibleTool : BaseTool
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Initializes a new instance of the <see cref="PimListEligibleTool"/> class.</summary>
    public PimListEligibleTool(
        IServiceScopeFactory scopeFactory,
        ILogger<PimListEligibleTool> logger) : base(logger)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public override string Name => "pim_list_eligible";

    /// <inheritdoc />
    public override PimTier RequiredPimTier => PimTier.Read;

    /// <inheritdoc />
    public override string Description => "List all PIM-eligible role assignments for the authenticated user, optionally filtered by scope.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["scope"] = new() { Name = "scope", Description = "Filter by subscription name or resource group path", Type = "string", Required = false }
    };

    /// <inheritdoc />
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var userId = GetArg<string>(arguments, "user_id");
        var scope = GetArg<string>(arguments, "scope");

        if (string.IsNullOrEmpty(userId))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new { errorCode = "AUTH_REQUIRED", message = "Authentication required to list eligible PIM roles.", suggestion = "Authenticate with your CAC/PIV card first." },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        await using var serviceScope = _scopeFactory.CreateAsyncScope();
        var pimService = serviceScope.ServiceProvider.GetRequiredService<IPimService>();

        var roles = await pimService.ListEligibleRolesAsync(userId, scope, cancellationToken);

        sw.Stop();
        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                eligibleRoles = roles.Select(r => new
                {
                    roleName = r.RoleName,
                    roleDefinitionId = r.RoleDefinitionId,
                    scope = r.Scope,
                    scopeDisplayName = r.ScopeDisplayName,
                    isActive = r.IsActive,
                    maxDuration = r.MaxDuration,
                    requiresApproval = r.RequiresApproval
                }),
                totalCount = roles.Count
            },
            metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
        });
    }
}

/// <summary>
/// Tool: pim_activate_role — Tier 2. Activate an eligible PIM role with justification.
/// High-privilege roles are routed through the approval workflow.
/// </summary>
public class PimActivateRoleTool : BaseTool
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Initializes a new instance of the <see cref="PimActivateRoleTool"/> class.</summary>
    public PimActivateRoleTool(
        IServiceScopeFactory scopeFactory,
        ILogger<PimActivateRoleTool> logger) : base(logger)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public override string Name => "pim_activate_role";

    /// <inheritdoc />
    public override PimTier RequiredPimTier => PimTier.Write;

    /// <inheritdoc />
    public override string Description => "Activate an eligible PIM role with justification. Ticket number required when configured. High-privilege roles require approval.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["roleName"] = new() { Name = "roleName", Description = "Role to activate (e.g., Contributor, Owner)", Type = "string", Required = true },
        ["scope"] = new() { Name = "scope", Description = "Target scope (subscription name, resource group path, or resource ID)", Type = "string", Required = true },
        ["justification"] = new() { Name = "justification", Description = "Justification for activation (min 20 characters)", Type = "string", Required = true },
        ["ticketNumber"] = new() { Name = "ticketNumber", Description = "Ticket reference from an approved ticketing system", Type = "string", Required = false },
        ["durationHours"] = new() { Name = "durationHours", Description = "Activation duration in hours (default: 8, max: 24)", Type = "integer", Required = false }
    };

    /// <inheritdoc />
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var userId = GetArg<string>(arguments, "user_id");
        var roleName = GetArg<string>(arguments, "roleName");
        var scope = GetArg<string>(arguments, "scope");
        var justification = GetArg<string>(arguments, "justification");
        var ticketNumber = GetArg<string>(arguments, "ticketNumber");
        var durationHours = GetArg<int?>(arguments, "durationHours");
        var sessionId = GetArg<string>(arguments, "session_id");

        if (string.IsNullOrEmpty(userId))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new { errorCode = "AUTH_REQUIRED", message = "Authentication required to activate PIM roles.", suggestion = "Authenticate with your CAC/PIV card first." },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        if (string.IsNullOrEmpty(roleName) || string.IsNullOrEmpty(scope) || string.IsNullOrEmpty(justification))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new { errorCode = "MISSING_PARAMETERS", message = "roleName, scope, and justification are required.", suggestion = "Provide all required parameters." },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        var parsedSessionId = Guid.TryParse(sessionId, out var sid) ? sid : Guid.Empty;

        await using var serviceScope = _scopeFactory.CreateAsyncScope();
        var pimService = serviceScope.ServiceProvider.GetRequiredService<IPimService>();

        var result = await pimService.ActivateRoleAsync(
            userId, roleName, scope, justification, ticketNumber, durationHours,
            parsedSessionId, null, cancellationToken);

        sw.Stop();

        if (result.ErrorCode != null)
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = result.ErrorCode,
                    message = result.Message,
                    suggestion = result.Suggestion,
                    eligibleRoles = result.EligibleRoles?.Select(r => new { roleName = r.RoleName, scope = r.ScopeDisplayName })
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        if (result.PendingApproval)
        {
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    activated = false,
                    pendingApproval = true,
                    pimRequestId = result.PimRequestId,
                    roleName = result.RoleName,
                    scope = result.Scope,
                    requestedAt = DateTimeOffset.UtcNow,
                    approversNotified = result.ApproversNotified,
                    message = result.Message
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                activated = true,
                pimRequestId = result.PimRequestId,
                roleName = result.RoleName,
                scope = result.Scope,
                activatedAt = result.ActivatedAt,
                expiresAt = result.ExpiresAt,
                durationHours = result.DurationHours,
                message = result.Message
            },
            metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
        });
    }
}

/// <summary>
/// Tool: pim_deactivate_role — Tier 2. Deactivate an active PIM role to restore
/// least-privilege posture.
/// </summary>
public class PimDeactivateRoleTool : BaseTool
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Initializes a new instance of the <see cref="PimDeactivateRoleTool"/> class.</summary>
    public PimDeactivateRoleTool(
        IServiceScopeFactory scopeFactory,
        ILogger<PimDeactivateRoleTool> logger) : base(logger)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public override string Name => "pim_deactivate_role";

    /// <inheritdoc />
    public override PimTier RequiredPimTier => PimTier.Write;

    /// <inheritdoc />
    public override string Description => "Deactivate an active PIM role to restore least-privilege posture.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["roleName"] = new() { Name = "roleName", Description = "Role to deactivate", Type = "string", Required = true },
        ["scope"] = new() { Name = "scope", Description = "Scope of the role to deactivate", Type = "string", Required = true }
    };

    /// <inheritdoc />
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var userId = GetArg<string>(arguments, "user_id");
        var roleName = GetArg<string>(arguments, "roleName");
        var scope = GetArg<string>(arguments, "scope");

        if (string.IsNullOrEmpty(userId))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new { errorCode = "AUTH_REQUIRED", message = "Authentication required to deactivate PIM roles.", suggestion = "Authenticate with your CAC/PIV card first." },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        if (string.IsNullOrEmpty(roleName) || string.IsNullOrEmpty(scope))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new { errorCode = "MISSING_PARAMETERS", message = "roleName and scope are required.", suggestion = "Provide the role name and scope to deactivate." },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        await using var serviceScope = _scopeFactory.CreateAsyncScope();
        var pimService = serviceScope.ServiceProvider.GetRequiredService<IPimService>();

        var result = await pimService.DeactivateRoleAsync(userId, roleName, scope, cancellationToken);

        sw.Stop();

        if (result.ErrorCode != null)
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new { errorCode = result.ErrorCode, message = result.Message },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                deactivated = true,
                roleName = result.RoleName,
                scope = result.Scope,
                deactivatedAt = result.DeactivatedAt,
                actualDuration = result.ActualDuration,
                message = result.Message
            },
            metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
        });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// PIM Session Management Tools (Phase 6 — US4)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Tool: pim_list_active — Tier 2. List all currently active PIM role assignments
/// for the authenticated user with remaining duration information.
/// </summary>
public class PimListActiveTool : BaseTool
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Initializes a new instance of the <see cref="PimListActiveTool"/> class.</summary>
    public PimListActiveTool(
        IServiceScopeFactory scopeFactory,
        ILogger<PimListActiveTool> logger) : base(logger)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public override string Name => "pim_list_active";

    /// <inheritdoc />
    public override PimTier RequiredPimTier => PimTier.Read;

    /// <inheritdoc />
    public override string Description => "List all currently active PIM role assignments for the authenticated user.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>();

    /// <inheritdoc />
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var userId = GetArg<string>(arguments, "user_id");

        if (string.IsNullOrEmpty(userId))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "AUTH_REQUIRED",
                    message = "Authentication required to list active PIM roles.",
                    suggestion = "Authenticate with your CAC/PIV card first."
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        using var scope = _scopeFactory.CreateScope();
        var pimService = scope.ServiceProvider.GetRequiredService<IPimService>();

        var activeRoles = await pimService.ListActiveRolesAsync(userId, cancellationToken);

        sw.Stop();
        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                activeRoles = activeRoles.Select(r => new
                {
                    roleName = r.RoleName,
                    scope = r.Scope,
                    activatedAt = r.ActivatedAt,
                    expiresAt = r.ExpiresAt,
                    remainingMinutes = r.RemainingMinutes,
                    pimRequestId = r.PimRequestId
                }),
                totalCount = activeRoles.Count
            },
            metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
        });
    }
}

/// <summary>
/// Tool: pim_extend_role — Tier 2. Extend an active PIM role's duration within policy limits.
/// </summary>
public class PimExtendRoleTool : BaseTool
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Initializes a new instance of the <see cref="PimExtendRoleTool"/> class.</summary>
    public PimExtendRoleTool(
        IServiceScopeFactory scopeFactory,
        ILogger<PimExtendRoleTool> logger) : base(logger)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public override string Name => "pim_extend_role";

    /// <inheritdoc />
    public override PimTier RequiredPimTier => PimTier.Write;

    /// <inheritdoc />
    public override string Description => "Extend an active PIM role's duration within policy limits.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["roleName"] = new ToolParameter { Type = "string", Description = "Role to extend", Required = true },
        ["scope"] = new ToolParameter { Type = "string", Description = "Scope of the role", Required = true },
        ["additionalHours"] = new ToolParameter { Type = "integer", Description = "Hours to add to the current expiration", Required = true }
    };

    /// <inheritdoc />
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var userId = GetArg<string>(arguments, "user_id");
        var roleName = GetArg<string>(arguments, "roleName");
        var scope = GetArg<string>(arguments, "scope");
        var additionalHours = GetArg<int>(arguments, "additionalHours");

        if (string.IsNullOrEmpty(userId))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "AUTH_REQUIRED",
                    message = "Authentication required to extend PIM roles.",
                    suggestion = "Authenticate with your CAC/PIV card first."
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        if (string.IsNullOrEmpty(roleName) || string.IsNullOrEmpty(scope) || additionalHours <= 0)
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "MISSING_PARAMETERS",
                    message = "roleName, scope, and additionalHours (> 0) are required.",
                    suggestion = "Provide all required parameters."
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        using var serviceScope = _scopeFactory.CreateScope();
        var pimService = serviceScope.ServiceProvider.GetRequiredService<IPimService>();

        var result = await pimService.ExtendRoleAsync(userId, roleName, scope, additionalHours, cancellationToken);

        sw.Stop();

        if (!string.IsNullOrEmpty(result.ErrorCode))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = result.ErrorCode,
                    message = result.Message,
                    previousExpiresAt = result.PreviousExpiresAt
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                extended = true,
                roleName = result.RoleName,
                scope = result.Scope,
                previousExpiresAt = result.PreviousExpiresAt,
                newExpiresAt = result.NewExpiresAt,
                message = result.Message
            },
            metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
        });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// PIM Approval Workflow Tools (Phase 7 — US5)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Tool: pim_approve_request — Tier 2. Approve a pending PIM activation request
/// for a high-privilege role. Restricted to SecurityLead and Administrator roles.
/// </summary>
public class PimApproveRequestTool : BaseTool
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Initializes a new instance of the <see cref="PimApproveRequestTool"/> class.</summary>
    public PimApproveRequestTool(
        IServiceScopeFactory scopeFactory,
        ILogger<PimApproveRequestTool> logger) : base(logger)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public override string Name => "pim_approve_request";

    /// <inheritdoc />
    public override PimTier RequiredPimTier => PimTier.Write;

    /// <inheritdoc />
    public override string Description => "Approve a pending PIM activation request for a high-privilege role.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["requestId"] = new ToolParameter { Type = "string", Description = "PIM request ID to approve", Required = true },
        ["comments"] = new ToolParameter { Type = "string", Description = "Approval comments", Required = false }
    };

    /// <inheritdoc />
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var userId = GetArg<string>(arguments, "user_id");
        var userRole = GetArg<string>(arguments, "user_role");
        var requestIdStr = GetArg<string>(arguments, "requestId");
        var comments = GetArg<string>(arguments, "comments");

        if (string.IsNullOrEmpty(userId))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "AUTH_REQUIRED",
                    message = "Authentication required to approve PIM requests.",
                    suggestion = "Authenticate with your CAC/PIV card first."
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        if (!IsApproverRole(userRole))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "INSUFFICIENT_ROLE",
                    message = "Only Security Leads and Compliance Administrators can approve PIM requests.",
                    suggestion = "Contact a Security Lead or Compliance Administrator."
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        if (string.IsNullOrEmpty(requestIdStr) || !Guid.TryParse(requestIdStr, out var requestId))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "MISSING_PARAMETERS",
                    message = "A valid requestId is required.",
                    suggestion = "Provide the PIM request ID to approve."
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        using var scope = _scopeFactory.CreateScope();
        var pimService = scope.ServiceProvider.GetRequiredService<IPimService>();

        var result = await pimService.ApproveRequestAsync(
            requestId, userId, userRole ?? "SecurityLead", comments, cancellationToken);

        sw.Stop();

        if (!string.IsNullOrEmpty(result.ErrorCode))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = result.ErrorCode,
                    message = result.Message
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                approved = result.Approved,
                requestId = result.RequestId,
                requester = result.RequesterName,
                roleName = result.RoleName,
                scope = result.Scope,
                approvedAt = result.DecisionAt,
                message = result.Message
            },
            metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
        });
    }

    private static bool IsApproverRole(string? role) =>
        role != null && (
            role.Equals("SecurityLead", StringComparison.OrdinalIgnoreCase) ||
            role.Equals(Ato.Copilot.Core.Constants.ComplianceRoles.SecurityLead, StringComparison.OrdinalIgnoreCase) ||
            role.Equals("Administrator", StringComparison.OrdinalIgnoreCase) ||
            role.Equals(Ato.Copilot.Core.Constants.ComplianceRoles.Administrator, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Tool: pim_deny_request — Tier 2. Deny a pending PIM activation request
/// for a high-privilege role. Restricted to SecurityLead and Administrator roles.
/// </summary>
public class PimDenyRequestTool : BaseTool
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Initializes a new instance of the <see cref="PimDenyRequestTool"/> class.</summary>
    public PimDenyRequestTool(
        IServiceScopeFactory scopeFactory,
        ILogger<PimDenyRequestTool> logger) : base(logger)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public override string Name => "pim_deny_request";

    /// <inheritdoc />
    public override PimTier RequiredPimTier => PimTier.Write;

    /// <inheritdoc />
    public override string Description => "Deny a pending PIM activation request for a high-privilege role.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["requestId"] = new ToolParameter { Type = "string", Description = "PIM request ID to deny", Required = true },
        ["reason"] = new ToolParameter { Type = "string", Description = "Reason for denial", Required = true }
    };

    /// <inheritdoc />
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var userId = GetArg<string>(arguments, "user_id");
        var userRole = GetArg<string>(arguments, "user_role");
        var requestIdStr = GetArg<string>(arguments, "requestId");
        var reason = GetArg<string>(arguments, "reason");

        if (string.IsNullOrEmpty(userId))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "AUTH_REQUIRED",
                    message = "Authentication required to deny PIM requests.",
                    suggestion = "Authenticate with your CAC/PIV card first."
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        if (!IsApproverRole(userRole))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "INSUFFICIENT_ROLE",
                    message = "Only Security Leads and Compliance Administrators can deny PIM requests.",
                    suggestion = "Contact a Security Lead or Compliance Administrator."
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        if (string.IsNullOrEmpty(requestIdStr) || !Guid.TryParse(requestIdStr, out var requestId))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "MISSING_PARAMETERS",
                    message = "A valid requestId is required.",
                    suggestion = "Provide the PIM request ID to deny."
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        if (string.IsNullOrEmpty(reason))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "MISSING_PARAMETERS",
                    message = "A reason is required when denying a PIM request.",
                    suggestion = "Provide a reason for the denial."
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        using var scope = _scopeFactory.CreateScope();
        var pimService = scope.ServiceProvider.GetRequiredService<IPimService>();

        var result = await pimService.DenyRequestAsync(
            requestId, userId, userRole ?? "SecurityLead", reason, cancellationToken);

        sw.Stop();

        if (!string.IsNullOrEmpty(result.ErrorCode))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = result.ErrorCode,
                    message = result.Message
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                denied = result.Denied,
                requestId = result.RequestId,
                requester = result.RequesterName,
                roleName = result.RoleName,
                reason = result.Reason,
                deniedAt = result.DecisionAt,
                message = result.Message
            },
            metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
        });
    }

    private static bool IsApproverRole(string? role) =>
        role != null && (
            role.Equals("SecurityLead", StringComparison.OrdinalIgnoreCase) ||
            role.Equals(Ato.Copilot.Core.Constants.ComplianceRoles.SecurityLead, StringComparison.OrdinalIgnoreCase) ||
            role.Equals("Administrator", StringComparison.OrdinalIgnoreCase) ||
            role.Equals(Ato.Copilot.Core.Constants.ComplianceRoles.Administrator, StringComparison.OrdinalIgnoreCase));
}

// ─────────────────────────────────────────────────────────────────────────────
// JIT VM Access Tools (Phase 9 — US7)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Tool: jit_request_access — Tier 2. Request Just-in-Time VM access through
/// Azure Defender for Cloud. Creates a temporary NSG rule for the specified
/// port and source IP with auto-revocation on expiration.
/// </summary>
public class JitRequestAccessTool : BaseTool
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Initializes a new instance of the <see cref="JitRequestAccessTool"/> class.</summary>
    public JitRequestAccessTool(
        IServiceScopeFactory scopeFactory,
        ILogger<JitRequestAccessTool> logger) : base(logger)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public override string Name => "jit_request_access";

    /// <inheritdoc />
    public override PimTier RequiredPimTier => PimTier.Write;

    /// <inheritdoc />
    public override string Description => "Request Just-in-Time VM access through Azure Defender for Cloud. Creates a temporary NSG rule for the specified port and source IP.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["vmName"] = new() { Name = "vmName", Description = "Target VM name", Type = "string", Required = true },
        ["resourceGroup"] = new() { Name = "resourceGroup", Description = "Resource group containing the VM", Type = "string", Required = true },
        ["subscriptionId"] = new() { Name = "subscriptionId", Description = "Subscription ID (uses default if not specified)", Type = "string", Required = false },
        ["port"] = new() { Name = "port", Description = "Port number (default: 22 for SSH)", Type = "integer", Required = false },
        ["protocol"] = new() { Name = "protocol", Description = "ssh or rdp (default: ssh)", Type = "string", Required = false },
        ["sourceIp"] = new() { Name = "sourceIp", Description = "Source IP address (auto-detected if not provided)", Type = "string", Required = false },
        ["durationHours"] = new() { Name = "durationHours", Description = "Access duration in hours (default: 3, max: 24)", Type = "integer", Required = false },
        ["justification"] = new() { Name = "justification", Description = "Justification for access", Type = "string", Required = true },
        ["ticketNumber"] = new() { Name = "ticketNumber", Description = "Ticket reference (required when RequireTicketNumber=true)", Type = "string", Required = false }
    };

    /// <inheritdoc />
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var userId = GetArg<string>(arguments, "user_id");
        var vmName = GetArg<string>(arguments, "vmName");
        var resourceGroup = GetArg<string>(arguments, "resourceGroup");
        var subscriptionId = GetArg<string>(arguments, "subscriptionId");
        var port = GetArg<int?>(arguments, "port") ?? 22;
        var protocol = GetArg<string>(arguments, "protocol") ?? "ssh";
        var sourceIp = GetArg<string>(arguments, "sourceIp");
        var durationHours = GetArg<int?>(arguments, "durationHours") ?? 3;
        var justification = GetArg<string>(arguments, "justification");
        var ticketNumber = GetArg<string>(arguments, "ticketNumber");
        var sessionId = GetArg<string>(arguments, "session_id");

        if (string.IsNullOrEmpty(userId))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new { errorCode = "AUTH_REQUIRED", message = "Authentication required to request JIT VM access.", suggestion = "Authenticate with your CAC/PIV card first." },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        if (string.IsNullOrEmpty(vmName) || string.IsNullOrEmpty(resourceGroup))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new { errorCode = "MISSING_PARAMETERS", message = "vmName and resourceGroup are required.", suggestion = "Provide the target VM name and its resource group." },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        if (string.IsNullOrEmpty(justification))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new { errorCode = "MISSING_PARAMETERS", message = "justification is required.", suggestion = "Provide a justification for the access request." },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        var parsedSessionId = Guid.TryParse(sessionId, out var sid) ? sid : Guid.Empty;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var jitService = scope.ServiceProvider.GetRequiredService<IJitVmAccessService>();

        var result = await jitService.RequestAccessAsync(
            userId, vmName, resourceGroup, subscriptionId,
            port, protocol, sourceIp, durationHours,
            justification, ticketNumber, parsedSessionId, null,
            cancellationToken);

        sw.Stop();

        if (!result.Success)
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new { errorCode = result.ErrorCode, message = result.Message, suggestion = "Check VM name, resource group, and justification." },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                jitRequestId = result.JitRequestId,
                vmName = result.VmName,
                resourceGroup = result.ResourceGroup,
                port = result.Port,
                protocol = result.Protocol,
                sourceIp = result.SourceIp,
                activatedAt = result.ActivatedAt,
                expiresAt = result.ExpiresAt,
                durationHours = result.DurationHours,
                connectionCommand = result.ConnectionCommand,
                message = result.Message
            },
            metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
        });
    }
}

/// <summary>
/// Tool: jit_list_sessions — Tier 2. List all active JIT VM access sessions
/// for the authenticated user.
/// </summary>
public class JitListSessionsTool : BaseTool
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Initializes a new instance of the <see cref="JitListSessionsTool"/> class.</summary>
    public JitListSessionsTool(
        IServiceScopeFactory scopeFactory,
        ILogger<JitListSessionsTool> logger) : base(logger)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public override string Name => "jit_list_sessions";

    /// <inheritdoc />
    public override PimTier RequiredPimTier => PimTier.Read;

    /// <inheritdoc />
    public override string Description => "List all active JIT VM access sessions for the authenticated user.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>();

    /// <inheritdoc />
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var userId = GetArg<string>(arguments, "user_id");

        if (string.IsNullOrEmpty(userId))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new { errorCode = "AUTH_REQUIRED", message = "Authentication required to list JIT sessions.", suggestion = "Authenticate with your CAC/PIV card first." },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var jitService = scope.ServiceProvider.GetRequiredService<IJitVmAccessService>();

        var sessions = await jitService.ListActiveSessionsAsync(userId, cancellationToken);

        sw.Stop();

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                activeSessions = sessions.Select(s => new
                {
                    jitRequestId = s.JitRequestId,
                    vmName = s.VmName,
                    resourceGroup = s.ResourceGroup,
                    port = s.Port,
                    sourceIp = s.SourceIp,
                    activatedAt = s.ActivatedAt,
                    expiresAt = s.ExpiresAt,
                    remainingMinutes = s.RemainingMinutes
                }),
                totalCount = sessions.Count
            },
            metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
        });
    }
}

/// <summary>
/// Tool: jit_revoke_access — Tier 2. Immediately revoke JIT VM access,
/// removing the NSG rule. Access is terminated and the session marked as deactivated.
/// </summary>
public class JitRevokeAccessTool : BaseTool
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Initializes a new instance of the <see cref="JitRevokeAccessTool"/> class.</summary>
    public JitRevokeAccessTool(
        IServiceScopeFactory scopeFactory,
        ILogger<JitRevokeAccessTool> logger) : base(logger)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public override string Name => "jit_revoke_access";

    /// <inheritdoc />
    public override PimTier RequiredPimTier => PimTier.Write;

    /// <inheritdoc />
    public override string Description => "Immediately revoke JIT VM access, removing the NSG rule.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["vmName"] = new() { Name = "vmName", Description = "VM name to revoke access for", Type = "string", Required = true },
        ["resourceGroup"] = new() { Name = "resourceGroup", Description = "Resource group containing the VM", Type = "string", Required = true }
    };

    /// <inheritdoc />
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var userId = GetArg<string>(arguments, "user_id");
        var vmName = GetArg<string>(arguments, "vmName");
        var resourceGroup = GetArg<string>(arguments, "resourceGroup");

        if (string.IsNullOrEmpty(userId))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new { errorCode = "AUTH_REQUIRED", message = "Authentication required to revoke JIT access.", suggestion = "Authenticate with your CAC/PIV card first." },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        if (string.IsNullOrEmpty(vmName) || string.IsNullOrEmpty(resourceGroup))
        {
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new { errorCode = "MISSING_PARAMETERS", message = "vmName and resourceGroup are required.", suggestion = "Provide the VM name and resource group to revoke access." },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var jitService = scope.ServiceProvider.GetRequiredService<IJitVmAccessService>();

        var result = await jitService.RevokeAccessAsync(userId, vmName, resourceGroup, cancellationToken);

        sw.Stop();

        if (!result.Revoked)
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new { errorCode = result.ErrorCode, message = result.Message, suggestion = "Verify the VM name and resource group, and ensure you have an active JIT session." },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                revoked = result.Revoked,
                vmName = result.VmName,
                resourceGroup = result.ResourceGroup,
                revokedAt = result.RevokedAt,
                message = result.Message
            },
            metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
        });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// PIM Audit Trail Tools
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Tool: pim_history — Tier 2. Query PIM action history for compliance evidence
/// and audit trail. Auditors can query across users; non-auditors see only their own.
/// </summary>
public class PimHistoryTool : BaseTool
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PimHistoryTool"/> class.
    /// </summary>
    public PimHistoryTool(IServiceScopeFactory scopeFactory, ILogger<PimHistoryTool> logger)
        : base(logger)
    {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public override string Name => "pim_history";

    /// <inheritdoc />
    public override PimTier RequiredPimTier => PimTier.Read;

    /// <inheritdoc />
    public override string Description =>
        "Query PIM action history for compliance evidence and audit trail.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["days"] = new ToolParameter
        {
            Name = "days",
            Type = "integer",
            Required = false,
            Description = "Number of days to look back (default: 7, max: 365)"
        },
        ["roleName"] = new ToolParameter
        {
            Name = "roleName",
            Type = "string",
            Required = false,
            Description = "Filter by role name"
        },
        ["filterUserId"] = new ToolParameter
        {
            Name = "filterUserId",
            Type = "string",
            Required = false,
            Description = "Filter by user ID (admin/auditor only)"
        },
        ["scope"] = new ToolParameter
        {
            Name = "scope",
            Type = "string",
            Required = false,
            Description = "Filter by scope"
        }
    };

    /// <inheritdoc />
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var userId = GetArg<string>(arguments, "user_id");
        var days = GetArg<int?>(arguments, "days") ?? 7;
        var roleName = GetArg<string>(arguments, "roleName");
        var filterUserId = GetArg<string>(arguments, "filterUserId");
        var scope = GetArg<string>(arguments, "scope");
        var isAuditor = GetArg<bool?>(arguments, "is_auditor") ?? false;

        if (string.IsNullOrEmpty(userId))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                data = new
                {
                    errorCode = "AUTH_REQUIRED",
                    message = "CAC authentication required to query PIM history.",
                    suggestion = "Please authenticate with your CAC/PIV card first."
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
            });
        }

        // Clamp days to valid range
        if (days < 1) days = 1;
        if (days > 365) days = 365;

        using var scopeObj = _scopeFactory.CreateScope();
        var pimService = scopeObj.ServiceProvider.GetRequiredService<IPimService>();

        var result = await pimService.GetHistoryAsync(
            userId, days, roleName, filterUserId, scope, isAuditor, cancellationToken);

        sw.Stop();
        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                history = result.History.Select(h => new
                {
                    h.RequestType,
                    h.RoleName,
                    h.Scope,
                    h.UserId,
                    h.UserDisplayName,
                    h.Justification,
                    h.TicketNumber,
                    h.Status,
                    h.RequestedAt,
                    h.ActivatedAt,
                    h.DeactivatedAt,
                    h.ActualDuration
                }),
                totalCount = result.TotalCount,
                nistControlMapping = result.NistControlMapping
            },
            metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds }
        });
    }
}
