using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Auth;
using Ato.Copilot.Core.Models.Auth;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Implements JIT VM access management via Azure Defender for Cloud.
/// Creates temporary NSG rules for SSH/RDP access with auto-revocation.
/// In production, this integrates with Azure.ResourceManager.SecurityCenter API.
/// Currently provides a simulation layer with full database tracking per R-003.
/// </summary>
public class JitVmAccessService : IJitVmAccessService
{
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;
    private readonly PimServiceOptions _options;
    private readonly ILogger<JitVmAccessService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JitVmAccessService"/> class.
    /// </summary>
    public JitVmAccessService(
        IDbContextFactory<AtoCopilotContext> dbFactory,
        IOptions<PimServiceOptions> options,
        ILogger<JitVmAccessService> logger)
    {
        _dbFactory = dbFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<JitAccessResult> RequestAccessAsync(
        string userId, string vmName, string resourceGroup,
        string? subscriptionId, int port, string protocol,
        string? sourceIp, int durationHours,
        string justification, string? ticketNumber,
        Guid sessionId, string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "JIT VM access request: User={UserId}, VM={VmName}, RG={ResourceGroup}, Port={Port}, Protocol={Protocol}",
            userId, vmName, resourceGroup, port, protocol);

        // ── Validate inputs ──────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(vmName))
        {
            _logger.LogWarning("JIT access request failed validation: missing VM name, user={UserId}", userId);
            return new JitAccessResult
            {
                Success = false,
                ErrorCode = "MISSING_PARAMETERS",
                Message = "VM name is required."
            };
        }

        if (string.IsNullOrWhiteSpace(resourceGroup))
        {
            _logger.LogWarning("JIT access request failed validation: missing resource group, user={UserId}", userId);
            return new JitAccessResult
            {
                Success = false,
                ErrorCode = "MISSING_PARAMETERS",
                Message = "Resource group is required."
            };
        }

        if (string.IsNullOrWhiteSpace(justification))
        {
            _logger.LogWarning("JIT access request failed validation: missing justification, user={UserId}", userId);
            return new JitAccessResult
            {
                Success = false,
                ErrorCode = "JUSTIFICATION_TOO_SHORT",
                Message = "Justification is required for JIT access."
            };
        }

        if (justification.Length < _options.MinJustificationLength)
        {
            _logger.LogWarning("JIT access request failed validation: justification too short ({Length} chars), user={UserId}", justification.Length, userId);
            return new JitAccessResult
            {
                Success = false,
                ErrorCode = "JUSTIFICATION_TOO_SHORT",
                Message = $"Justification must be at least {_options.MinJustificationLength} characters."
            };
        }

        if (durationHours < 1 || durationHours > 24)
        {
            _logger.LogWarning("JIT access request failed validation: invalid duration {Duration}h, user={UserId}", durationHours, userId);
            return new JitAccessResult
            {
                Success = false,
                ErrorCode = "INVALID_DURATION",
                Message = "Duration must be between 1 and 24 hours."
            };
        }

        // ── Validate ticket number if required ───────────────────────────
        if (_options.RequireTicketNumber && string.IsNullOrWhiteSpace(ticketNumber))
        {
            return new JitAccessResult
            {
                Success = false,
                ErrorCode = "TICKET_REQUIRED",
                Message = "Ticket number is required by server policy."
            };
        }

        // ── Normalize protocol ───────────────────────────────────────────
        var normalizedProtocol = protocol.ToUpperInvariant() switch
        {
            "SSH" => "SSH",
            "RDP" => "RDP",
            _ => "SSH"
        };

        // ── Default port based on protocol ───────────────────────────────
        var effectivePort = port;
        if (effectivePort <= 0)
        {
            effectivePort = normalizedProtocol == "RDP" ? 3389 : 22;
        }

        // ── Auto-detect source IP if not provided ────────────────────────
        var effectiveSourceIp = sourceIp ?? "10.0.0.1"; // Simulated auto-detection per R-003

        // ── Resolve subscription ─────────────────────────────────────────
        var effectiveSubscription = subscriptionId ?? "default";

        // ── In production: Azure Defender for Cloud JIT API call ─────────
        // POST /subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.Security/locations/{location}/jitNetworkAccessPolicies/{policyName}/initiate
        // For simulation, we record the request in JitRequests table.

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddHours(durationHours);
        var requestId = Guid.NewGuid();

        var entity = new JitRequestEntity
        {
            Id = requestId,
            RequestType = JitRequestType.JitVmAccess,
            UserId = userId,
            UserDisplayName = userId, // In production: resolved from token claims
            ConversationId = conversationId,
            SessionId = sessionId == Guid.Empty ? null : sessionId,
            RoleName = string.Empty, // Not applicable for JIT VM access
            Scope = $"/subscriptions/{effectiveSubscription}/resourceGroups/{resourceGroup}",
            ScopeDisplayName = resourceGroup,
            Justification = justification,
            TicketNumber = ticketNumber,
            Status = JitRequestStatus.Active,
            DurationHours = durationHours,
            RequestedAt = now,
            ActivatedAt = now,
            ExpiresAt = expiresAt,
            VmName = vmName,
            ResourceGroup = resourceGroup,
            SubscriptionId = effectiveSubscription,
            Port = effectivePort,
            Protocol = normalizedProtocol,
            SourceIp = effectiveSourceIp
        };

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.JitRequests.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "JIT VM access granted: RequestId={RequestId}, VM={VmName}, Port={Port}, ExpiresAt={ExpiresAt}",
            requestId, vmName, effectivePort, expiresAt);

        // ── Build connection command ─────────────────────────────────────
        var connectionCommand = normalizedProtocol == "RDP"
            ? $"mstsc /v:{vmName}:{effectivePort}"
            : $"ssh user@{vmName} -p {effectivePort}";

        return new JitAccessResult
        {
            Success = true,
            JitRequestId = requestId.ToString(),
            VmName = vmName,
            ResourceGroup = resourceGroup,
            Port = effectivePort,
            Protocol = normalizedProtocol,
            SourceIp = effectiveSourceIp,
            ActivatedAt = now,
            ExpiresAt = expiresAt,
            DurationHours = durationHours,
            ConnectionCommand = connectionCommand,
            Message = $"JIT {normalizedProtocol} access granted to {vmName} on port {effectivePort} from {effectiveSourceIp}. Expires at {expiresAt:HH:mm} UTC."
        };
    }

    /// <inheritdoc />
    public async Task<List<JitActiveSession>> ListActiveSessionsAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing active JIT sessions for user {UserId}", userId);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var activeSessions = await db.JitRequests
            .Where(r => r.UserId == userId
                && r.RequestType == JitRequestType.JitVmAccess
                && r.Status == JitRequestStatus.Active
                && r.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(r => r.ActivatedAt)
            .ToListAsync(cancellationToken);

        return activeSessions.Select(r => new JitActiveSession
        {
            JitRequestId = r.Id.ToString(),
            VmName = r.VmName ?? string.Empty,
            ResourceGroup = r.ResourceGroup ?? string.Empty,
            Port = r.Port ?? 22,
            SourceIp = r.SourceIp ?? string.Empty,
            ActivatedAt = r.ActivatedAt ?? r.RequestedAt,
            ExpiresAt = r.ExpiresAt ?? DateTimeOffset.UtcNow,
            RemainingMinutes = (int)Math.Max(0, ((r.ExpiresAt ?? DateTimeOffset.UtcNow) - DateTimeOffset.UtcNow).TotalMinutes)
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<JitRevokeResult> RevokeAccessAsync(
        string userId, string vmName, string resourceGroup,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Revoking JIT access: User={UserId}, VM={VmName}, RG={ResourceGroup}",
            userId, vmName, resourceGroup);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var activeRequest = await db.JitRequests
            .Where(r => r.UserId == userId
                && r.RequestType == JitRequestType.JitVmAccess
                && r.Status == JitRequestStatus.Active
                && r.VmName == vmName
                && r.ResourceGroup == resourceGroup
                && r.ExpiresAt > DateTimeOffset.UtcNow)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeRequest == null)
        {
            _logger.LogWarning(
                "No active JIT session found: VM={VmName}, RG={ResourceGroup}",
                vmName, resourceGroup);

            return new JitRevokeResult
            {
                Revoked = false,
                VmName = vmName,
                ResourceGroup = resourceGroup,
                ErrorCode = "SESSION_NOT_FOUND",
                Message = $"No active JIT access session found for {vmName} in {resourceGroup}."
            };
        }

        // ── In production: Remove NSG rule via Azure API ─────────────────
        // DELETE/modify the JIT network access policy to remove the allowed rule

        var now = DateTimeOffset.UtcNow;
        activeRequest.Status = JitRequestStatus.Deactivated;
        activeRequest.DeactivatedAt = now;
        activeRequest.ActualDuration = now - (activeRequest.ActivatedAt ?? activeRequest.RequestedAt);

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "JIT access revoked: RequestId={RequestId}, VM={VmName}",
            activeRequest.Id, vmName);

        return new JitRevokeResult
        {
            Revoked = true,
            VmName = vmName,
            ResourceGroup = resourceGroup,
            RevokedAt = now,
            Message = $"JIT access to {vmName} revoked. NSG rule removed."
        };
    }
}
