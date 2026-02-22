using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Auth;
using Ato.Copilot.Core.Interfaces.Kanban;
using Ato.Copilot.Core.Models.Auth;
using Ato.Copilot.Core.Models.Kanban;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Manages CAC/PIV authentication sessions: creation, lookup, termination, and timeout updates.
/// Sessions are stored in the database with token hashes (never raw tokens).
/// </summary>
public class CacSessionService : ICacSessionService
{
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;
    private readonly CacAuthOptions _cacAuthOptions;
    private readonly PimServiceOptions _pimOptions;
    private readonly ICertificateRoleResolver? _roleResolver;
    private readonly INotificationService? _notificationService;
    private readonly ILogger<CacSessionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CacSessionService"/> class.
    /// </summary>
    public CacSessionService(
        IDbContextFactory<AtoCopilotContext> dbFactory,
        IOptions<CacAuthOptions> cacAuthOptions,
        IOptions<PimServiceOptions> pimOptions,
        ILogger<CacSessionService> logger,
        ICertificateRoleResolver? roleResolver = null,
        INotificationService? notificationService = null)
    {
        _dbFactory = dbFactory;
        _cacAuthOptions = cacAuthOptions.Value;
        _pimOptions = pimOptions.Value;
        _logger = logger;
        _roleResolver = roleResolver;
        _notificationService = notificationService;
    }

    /// <inheritdoc />
    public async Task<CacSession> CreateSessionAsync(
        string userId, string displayName, string email,
        string tokenHash, ClientType clientType, string ipAddress,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Expire any existing active sessions for this user (single active session policy)
        var existingSessions = await db.CacSessions
            .Where(s => s.UserId == userId && s.Status == SessionStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingSessions)
        {
            existing.Status = SessionStatus.Terminated;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var session = new CacSession
        {
            UserId = userId,
            DisplayName = displayName,
            Email = email,
            TokenHash = tokenHash,
            SessionStart = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(_cacAuthOptions.DefaultSessionTimeoutHours),
            ClientType = clientType,
            IpAddress = ipAddress,
            Status = SessionStatus.Active
        };

        db.CacSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        // Resolve platform role from certificate identity (FR-028, T063)
        if (_roleResolver != null)
        {
            var thumbprint = tokenHash.Length >= 40 ? tokenHash[..40] : tokenHash;
            var subject = $"CN={userId}";
            var resolvedRole = await _roleResolver.ResolveRoleAsync(
                thumbprint, subject, userId, cancellationToken);

            _logger.LogInformation(
                "Certificate role resolved for user {UserId}: {Role}",
                userId, resolvedRole);
        }

        _logger.LogInformation(
            "CAC session created for user {UserId} ({DisplayName}), expires at {ExpiresAt}",
            userId, displayName, session.ExpiresAt);

        return session;
    }

    /// <inheritdoc />
    public async Task<CacSession?> GetActiveSessionAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var session = await db.CacSessions
            .Where(s => s.UserId == userId && s.Status == SessionStatus.Active)
            .OrderByDescending(s => s.SessionStart)
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null) return null;

        // Lazy cleanup: mark expired sessions on access (per T100/SC-012)
        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            session.Status = SessionStatus.Expired;
            session.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "CAC session {SessionId} for user {UserId} expired (lazy cleanup)",
                session.Id, userId);

            return null;
        }

        // Check for expiring session and PIM roles within warning window (T070/FR-032)
        var warningMinutes = _pimOptions.SessionExpirationWarningMinutes;
        if (warningMinutes > 0 && _notificationService != null)
        {
            var warningThreshold = DateTimeOffset.UtcNow.AddMinutes(warningMinutes);
            if (session.ExpiresAt <= warningThreshold)
            {
                await _notificationService.EnqueueAsync(new NotificationMessage
                {
                    EventType = NotificationEventType.PimRoleExpiring,
                    TaskId = session.Id.ToString(),
                    TargetUserId = userId,
                    Title = "Session Expiring Soon",
                    Details = $"Your CAC session expires at {session.ExpiresAt:HH:mm} UTC (within {warningMinutes} minutes)."
                });

                // Also check for PIM roles linked to this session that are expiring
                var expiringRoles = await db.JitRequests
                    .Where(r => r.SessionId == session.Id
                        && r.Status == JitRequestStatus.Active
                        && r.ExpiresAt.HasValue
                        && r.ExpiresAt <= warningThreshold)
                    .ToListAsync(cancellationToken);

                foreach (var role in expiringRoles)
                {
                    await _notificationService.EnqueueAsync(new NotificationMessage
                    {
                        EventType = NotificationEventType.PimRoleExpiring,
                        TaskId = role.PimRequestId ?? role.Id.ToString(),
                        TargetUserId = userId,
                        Title = $"PIM Role Expiring: {role.RoleName}",
                        Details = $"Your {role.RoleName} role on {role.ScopeDisplayName ?? role.Scope} expires at {role.ExpiresAt:HH:mm} UTC."
                    });
                }
            }
        }

        return session;
    }

    /// <inheritdoc />
    public async Task TerminateSessionAsync(
        Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var session = await db.CacSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
        {
            _logger.LogWarning("Attempted to terminate non-existent session {SessionId}", sessionId);
            return;
        }

        session.Status = SessionStatus.Terminated;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("CAC session {SessionId} terminated for user {UserId}",
            sessionId, session.UserId);
    }

    /// <inheritdoc />
    public async Task<CacSession> UpdateTimeoutAsync(
        Guid sessionId, int timeoutHours, CancellationToken cancellationToken = default)
    {
        if (timeoutHours < 1 || timeoutHours > _cacAuthOptions.MaxSessionTimeoutHours)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutHours),
                $"Timeout must be between 1 and {_cacAuthOptions.MaxSessionTimeoutHours} hours.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var session = await db.CacSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.Status == SessionStatus.Active, cancellationToken)
            ?? throw new InvalidOperationException($"Active session {sessionId} not found.");

        var previousExpiry = session.ExpiresAt;
        session.ExpiresAt = session.SessionStart.AddHours(timeoutHours);
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "CAC session {SessionId} timeout updated from {Previous} to {New}",
            sessionId, previousExpiry, session.ExpiresAt);

        return session;
    }

    /// <inheritdoc />
    public async Task<bool> IsSessionActiveAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var session = await GetActiveSessionAsync(userId, cancellationToken);
        return session != null;
    }

    /// <inheritdoc />
    public async Task<List<CacSession>> GetSessionsByUserAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.CacSessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.SessionStart)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<string>> GetExpirationWarningsAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var now = DateTimeOffset.UtcNow;
        var warningThreshold = TimeSpan.FromMinutes(_pimOptions.SessionExpirationWarningMinutes);

        // Check CAC session expiration
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var session = await db.CacSessions
            .Where(s => s.UserId == userId && s.Status == SessionStatus.Active)
            .OrderByDescending(s => s.SessionStart)
            .FirstOrDefaultAsync(cancellationToken);

        if (session != null && session.ExpiresAt > now)
        {
            var remaining = session.ExpiresAt - now;
            if (remaining <= warningThreshold)
            {
                warnings.Add($"CAC session expires in {remaining.TotalMinutes:F0} minutes (at {session.ExpiresAt:HH:mm} UTC).");
            }
        }

        // Check PIM role expirations (FR-015)
        var activeRoles = await db.JitRequests
            .Where(r => r.UserId == userId
                && r.RequestType == JitRequestType.PimRoleActivation
                && r.Status == JitRequestStatus.Active
                && r.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var role in activeRoles)
        {
            var remaining = role.ExpiresAt!.Value - now;
            if (remaining <= warningThreshold)
            {
                warnings.Add($"PIM role '{role.RoleName}' expires in {remaining.TotalMinutes:F0} minutes (at {role.ExpiresAt:HH:mm} UTC).");
            }
        }

        if (warnings.Count > 0)
        {
            _logger.LogWarning(
                "Expiration warnings for user {UserId}: {WarningCount} item(s) nearing expiry",
                userId, warnings.Count);
        }

        return warnings;
    }

    /// <summary>
    /// Computes the SHA-256 hash of a JWT token string.
    /// </summary>
    /// <param name="token">The JWT token to hash.</param>
    /// <returns>Hex-encoded SHA-256 hash (64 characters).</returns>
    public static string ComputeTokenHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }
}
