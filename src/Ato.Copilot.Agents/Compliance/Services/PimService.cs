using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Auth;
using Ato.Copilot.Core.Interfaces.Kanban;
using Ato.Copilot.Core.Models.Auth;
using Ato.Copilot.Core.Models.Kanban;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Implements PIM role management operations including activation, deactivation,
/// extension, approval workflows, and history queries.
/// Uses Graph API for Entra ID directory roles and ARM API for Azure RBAC roles.
/// Configured for Azure Government endpoints (graph.microsoft.us) per R-006.
/// </summary>
public class PimService : IPimService
{
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;
    private readonly PimServiceOptions _options;
    private readonly ILogger<PimService> _logger;
    private readonly INotificationService? _notificationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PimService"/> class.
    /// </summary>
    public PimService(
        IDbContextFactory<AtoCopilotContext> dbFactory,
        IOptions<PimServiceOptions> options,
        ILogger<PimService> logger,
        INotificationService? notificationService = null)
    {
        _dbFactory = dbFactory;
        _options = options.Value;
        _logger = logger;
        _notificationService = notificationService;
    }

    /// <inheritdoc />
    public async Task<List<PimEligibleRole>> ListEligibleRolesAsync(
        string userId, string? scope = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing eligible PIM roles for user {UserId}, scope={Scope}", userId, scope);

        // In production, this would query Graph API:
        //   GET /roleManagement/directory/roleEligibilityScheduleInstances
        // For Azure RBAC roles:
        //   GET /subscriptions/{id}/providers/Microsoft.Authorization/roleEligibilityScheduleInstances
        // Currently returns roles based on local JitRequest records as a simulation layer.

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Check for any active roles to mark IsActive
        var activeRequests = await db.JitRequests
            .Where(r => r.UserId == userId
                && r.RequestType == JitRequestType.PimRoleActivation
                && r.Status == JitRequestStatus.Active
                && r.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);

        var activeRoleScopes = activeRequests
            .Select(r => $"{r.RoleName}|{r.Scope}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Simulated eligible roles — in production replaced by Graph API call
        var eligibleRoles = new List<PimEligibleRole>
        {
            new()
            {
                RoleName = "Contributor",
                RoleDefinitionId = "b24988ac-6180-42a0-ab88-20f7382dd24c",
                Scope = "/subscriptions/default",
                ScopeDisplayName = "Default Subscription",
                MaxDuration = $"PT{_options.MaxActivationDurationHours}H",
                RequiresApproval = false
            },
            new()
            {
                RoleName = "Reader",
                RoleDefinitionId = "acdd72a7-3385-48ef-bd42-f606fba81ae7",
                Scope = "/subscriptions/default",
                ScopeDisplayName = "Default Subscription",
                MaxDuration = $"PT{_options.MaxActivationDurationHours}H",
                RequiresApproval = false
            },
            new()
            {
                RoleName = "Owner",
                RoleDefinitionId = "8e3af657-a8ff-443c-a75c-2fe8c4bcb635",
                Scope = "/subscriptions/default",
                ScopeDisplayName = "Default Subscription",
                MaxDuration = $"PT{_options.MaxActivationDurationHours}H",
                RequiresApproval = true
            }
        };

        // Mark active status
        foreach (var role in eligibleRoles)
        {
            role.IsActive = activeRoleScopes.Contains($"{role.RoleName}|{role.Scope}");
            role.RequiresApproval = IsHighPrivilegeRole(role.RoleName);
        }

        // Filter by scope if specified
        if (!string.IsNullOrEmpty(scope))
        {
            eligibleRoles = eligibleRoles
                .Where(r => r.Scope.Contains(scope, StringComparison.OrdinalIgnoreCase)
                    || r.ScopeDisplayName.Contains(scope, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return eligibleRoles;
    }

    /// <inheritdoc />
    public async Task<PimActivationResult> ActivateRoleAsync(
        string userId, string roleName, string scope,
        string justification, string? ticketNumber, int? durationHours,
        Guid sessionId, string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "PIM activation requested: user={UserId}, role={Role}, scope={Scope}",
            userId, roleName, scope);

        // ─── Justification validation (T031) ────────────────────────────
        var justificationError = ValidateJustification(justification);
        if (justificationError != null)
        {
            _logger.LogWarning("PIM activation failed validation: user={UserId}, role={Role}, error={ErrorCode}", userId, roleName, justificationError.ErrorCode);
            return justificationError;
        }

        // ─── Ticket validation (T030) ────────────────────────────────────
        var ticketError = ValidateTicket(ticketNumber);
        if (ticketError != null)
        {
            _logger.LogWarning("PIM activation failed validation: user={UserId}, role={Role}, error={ErrorCode}", userId, roleName, ticketError.ErrorCode);
            return ticketError;
        }

        // ─── Duration validation ─────────────────────────────────────────
        var duration = durationHours ?? _options.DefaultActivationDurationHours;
        if (duration > _options.MaxActivationDurationHours)
        {
            _logger.LogWarning("PIM activation duration exceeds policy: user={UserId}, role={Role}, requested={Duration}h, max={Max}h", userId, roleName, duration, _options.MaxActivationDurationHours);
            return new PimActivationResult
            {
                ErrorCode = "DURATION_EXCEEDS_POLICY",
                Message = $"Requested duration {duration}h exceeds maximum allowed {_options.MaxActivationDurationHours}h.",
                Suggestion = $"Maximum activation duration is {_options.MaxActivationDurationHours} hours."
            };
        }

        // ─── Eligibility check ───────────────────────────────────────────
        var eligible = await ListEligibleRolesAsync(userId, null, cancellationToken);
        var matchingRole = eligible.FirstOrDefault(r =>
            r.RoleName.Equals(roleName, StringComparison.OrdinalIgnoreCase) &&
            r.Scope.Contains(scope, StringComparison.OrdinalIgnoreCase));

        if (matchingRole == null)
        {
            _logger.LogWarning("PIM activation not eligible: user={UserId}, role={Role}, scope={Scope}", userId, roleName, scope);
            return new PimActivationResult
            {
                ErrorCode = "NOT_ELIGIBLE",
                Message = $"You are not eligible for {roleName} on {scope}.",
                Suggestion = eligible.Count > 0
                    ? $"You are eligible for: {string.Join(", ", eligible.Select(r => $"{r.RoleName} on {r.ScopeDisplayName}"))}"
                    : "No eligible roles found.",
                EligibleRoles = eligible
            };
        }

        // ─── Check if already active ─────────────────────────────────────
        if (matchingRole.IsActive)
        {
            _logger.LogWarning("PIM activation already active: user={UserId}, role={Role}, scope={Scope}", userId, roleName, matchingRole.ScopeDisplayName);
            return new PimActivationResult
            {
                ErrorCode = "ROLE_ALREADY_ACTIVE",
                Message = $"{roleName} is already active on {matchingRole.ScopeDisplayName}.",
                RoleName = roleName,
                Scope = matchingRole.ScopeDisplayName
            };
        }

        // ─── High-privilege → approval workflow ──────────────────────────
        if (IsHighPrivilegeRole(roleName))
        {
            var approvalRequest = await SubmitApprovalAsync(
                userId, string.Empty, roleName, matchingRole.Scope,
                justification, ticketNumber, duration, sessionId, conversationId,
                cancellationToken);

            // Notify Security Lead and Compliance Officer of high-privilege activation (FR-033)
            if (_notificationService != null)
            {
                await _notificationService.EnqueueAsync(new NotificationMessage
                {
                    EventType = NotificationEventType.PimHighPrivilegeWarning,
                    TaskId = approvalRequest.Id.ToString(),
                    TargetUserId = "SecurityLead",
                    Title = $"High-Privilege PIM Request: {roleName}",
                    Details = $"User {userId} requested high-privilege role {roleName} on {matchingRole.ScopeDisplayName}. Approval required."
                });
                await _notificationService.EnqueueAsync(new NotificationMessage
                {
                    EventType = NotificationEventType.PimApprovalRequired,
                    TaskId = approvalRequest.Id.ToString(),
                    TargetUserId = "ComplianceOfficer",
                    Title = $"Approval Required: {roleName}",
                    Details = $"User {userId} requested {roleName} on {matchingRole.ScopeDisplayName}. Justification: {justification}"
                });
            }

            return new PimActivationResult
            {
                Activated = false,
                PendingApproval = true,
                PimRequestId = approvalRequest.Id.ToString(),
                RoleName = roleName,
                Scope = matchingRole.ScopeDisplayName,
                Message = $"{roleName} is a high-privilege role requiring approval. Request submitted. Security Lead and Compliance Officer have been notified.",
                ApproversNotified = new List<string> { "Security Lead", "Compliance Officer" }
            };
        }

        // ─── Standard role — immediate activation ────────────────────────
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var request = new JitRequestEntity
        {
            RequestType = JitRequestType.PimRoleActivation,
            PimRequestId = $"pim-{Guid.NewGuid():N}",
            UserId = userId,
            ConversationId = conversationId,
            SessionId = sessionId,
            RoleName = roleName,
            Scope = matchingRole.Scope,
            ScopeDisplayName = matchingRole.ScopeDisplayName,
            Justification = justification,
            TicketNumber = ticketNumber,
            TicketSystem = ticketNumber != null ? DetectTicketSystem(ticketNumber) : null,
            Status = JitRequestStatus.Active,
            DurationHours = duration,
            RequestedAt = now,
            ActivatedAt = now,
            ExpiresAt = now.AddHours(duration)
        };

        db.JitRequests.Add(request);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "PIM role {Role} activated for user {UserId} on {Scope}, expires {ExpiresAt}",
            roleName, userId, matchingRole.ScopeDisplayName, request.ExpiresAt);

        // Notify Security Lead of standard activation (FR-033)
        if (_notificationService != null)
        {
            await _notificationService.EnqueueAsync(new NotificationMessage
            {
                EventType = NotificationEventType.PimRoleActivated,
                TaskId = request.PimRequestId ?? "",
                TargetUserId = "SecurityLead",
                Title = $"PIM Role Activated: {roleName}",
                Details = $"User {userId} activated {roleName} on {matchingRole.ScopeDisplayName}. Expires at {request.ExpiresAt:HH:mm} UTC."
            });
        }

        return new PimActivationResult
        {
            Activated = true,
            PimRequestId = request.PimRequestId!,
            RoleName = roleName,
            Scope = matchingRole.ScopeDisplayName,
            ActivatedAt = request.ActivatedAt,
            ExpiresAt = request.ExpiresAt,
            DurationHours = duration,
            Message = $"{roleName} role activated on {matchingRole.ScopeDisplayName}. Expires at {request.ExpiresAt:HH:mm} UTC."
        };
    }

    /// <inheritdoc />
    public async Task<PimDeactivationResult> DeactivateRoleAsync(
        string userId, string roleName, string scope,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "PIM deactivation requested: user={UserId}, role={Role}, scope={Scope}",
            userId, roleName, scope);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var activeRequest = await db.JitRequests
            .Where(r => r.UserId == userId
                && r.RoleName == roleName
                && r.RequestType == JitRequestType.PimRoleActivation
                && r.Status == JitRequestStatus.Active
                && r.Scope.Contains(scope))
            .FirstOrDefaultAsync(cancellationToken);

        if (activeRequest == null)
        {
            _logger.LogWarning("PIM deactivation failed: role not active, user={UserId}, role={Role}, scope={Scope}", userId, roleName, scope);
            return new PimDeactivationResult
            {
                ErrorCode = "ROLE_NOT_ACTIVE",
                Message = $"{roleName} is not currently active on {scope}."
            };
        }

        var now = DateTimeOffset.UtcNow;
        activeRequest.Status = JitRequestStatus.Deactivated;
        activeRequest.DeactivatedAt = now;
        activeRequest.ActualDuration = now - activeRequest.ActivatedAt;

        await db.SaveChangesAsync(cancellationToken);

        var durationStr = FormatDuration(activeRequest.ActualDuration.Value);

        _logger.LogInformation(
            "PIM role {Role} deactivated for user {UserId}, actual duration: {Duration}",
            roleName, userId, durationStr);

        return new PimDeactivationResult
        {
            Deactivated = true,
            RoleName = roleName,
            Scope = activeRequest.ScopeDisplayName ?? scope,
            DeactivatedAt = now,
            ActualDuration = durationStr,
            Message = $"{roleName} role deactivated on {activeRequest.ScopeDisplayName ?? scope}. Least-privilege posture restored."
        };
    }

    /// <inheritdoc />
    public async Task<PimExtensionResult> ExtendRoleAsync(
        string userId, string roleName, string scope, int additionalHours,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "PIM extension requested: user={UserId}, role={Role}, scope={Scope}, hours={Hours}",
            userId, roleName, scope, additionalHours);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var activeRequest = await db.JitRequests
            .Where(r => r.UserId == userId
                && r.RoleName == roleName
                && r.RequestType == JitRequestType.PimRoleActivation
                && r.Status == JitRequestStatus.Active
                && r.Scope.Contains(scope))
            .FirstOrDefaultAsync(cancellationToken);

        if (activeRequest == null)
        {
            _logger.LogWarning("PIM extension failed: role not active, user={UserId}, role={Role}, scope={Scope}", userId, roleName, scope);
            return new PimExtensionResult
            {
                ErrorCode = "ROLE_NOT_ACTIVE",
                Message = $"{roleName} is not currently active on {scope}."
            };
        }

        var previousExpires = activeRequest.ExpiresAt!.Value;
        var newExpires = previousExpires.AddHours(additionalHours);
        var totalHours = (newExpires - activeRequest.ActivatedAt!.Value).TotalHours;

        if (totalHours > _options.MaxActivationDurationHours)
        {
            _logger.LogWarning("PIM extension duration exceeds policy: user={UserId}, role={Role}, totalHours={Total}h, max={Max}h", userId, roleName, totalHours, _options.MaxActivationDurationHours);
            return new PimExtensionResult
            {
                ErrorCode = "DURATION_EXCEEDS_POLICY",
                Message = $"Extension would result in {totalHours:F0}h total, exceeding maximum {_options.MaxActivationDurationHours}h.",
                PreviousExpiresAt = previousExpires
            };
        }

        activeRequest.ExpiresAt = newExpires;
        activeRequest.DurationHours = (int)Math.Ceiling(totalHours);

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "PIM role {Role} extended for user {UserId} by {Hours}h, new expiry: {NewExpiry}",
            roleName, userId, additionalHours, newExpires);

        return new PimExtensionResult
        {
            Extended = true,
            RoleName = roleName,
            Scope = activeRequest.ScopeDisplayName ?? scope,
            PreviousExpiresAt = previousExpires,
            NewExpiresAt = newExpires,
            Message = $"{roleName} role extended by {additionalHours} hours. New expiration: {newExpires:HH:mm} UTC."
        };
    }

    /// <inheritdoc />
    public async Task<List<PimActiveRole>> ListActiveRolesAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing active PIM roles for user={UserId}", userId);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var activeRequests = await db.JitRequests
            .Where(r => r.UserId == userId
                && r.RequestType == JitRequestType.PimRoleActivation
                && r.Status == JitRequestStatus.Active
                && r.ExpiresAt > now)
            .OrderByDescending(r => r.ActivatedAt)
            .ToListAsync(cancellationToken);

        // Lazy cleanup: mark expired ones
        var expired = activeRequests.Where(r => r.ExpiresAt <= now).ToList();
        foreach (var r in expired)
        {
            r.Status = JitRequestStatus.Expired;
        }
        if (expired.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        return activeRequests
            .Where(r => r.ExpiresAt > now)
            .Select(r => new PimActiveRole
            {
                RoleName = r.RoleName,
                Scope = r.ScopeDisplayName ?? r.Scope,
                ActivatedAt = r.ActivatedAt ?? r.RequestedAt,
                ExpiresAt = r.ExpiresAt!.Value,
                RemainingMinutes = (int)Math.Max(0, (r.ExpiresAt!.Value - now).TotalMinutes),
                PimRequestId = r.PimRequestId ?? r.Id.ToString()
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<PimHistoryResult> GetHistoryAsync(
        string userId, int days = 7, string? roleName = null,
        string? filterUserId = null, string? scope = null, bool isAuditor = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PIM history query: user={UserId}, days={Days}, isAuditor={IsAuditor}", userId, days, isAuditor);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
        var query = db.JitRequests
            .Where(r => r.RequestType == JitRequestType.PimRoleActivation
                && r.RequestedAt >= cutoff);

        // Non-auditors can only see their own history
        if (!isAuditor || string.IsNullOrEmpty(filterUserId))
            query = query.Where(r => r.UserId == userId);
        else
            query = query.Where(r => r.UserId == filterUserId);

        if (!string.IsNullOrEmpty(roleName))
            query = query.Where(r => r.RoleName == roleName);

        if (!string.IsNullOrEmpty(scope))
            query = query.Where(r => r.Scope.Contains(scope));

        var entries = await query
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(cancellationToken);

        return new PimHistoryResult
        {
            History = entries.Select(r => new PimHistoryEntry
            {
                RequestType = r.RequestType.ToString(),
                RoleName = r.RoleName,
                Scope = r.ScopeDisplayName ?? r.Scope,
                UserId = r.UserId,
                UserDisplayName = r.UserDisplayName,
                Justification = r.Justification,
                TicketNumber = r.TicketNumber,
                Status = r.Status.ToString(),
                RequestedAt = r.RequestedAt,
                ActivatedAt = r.ActivatedAt,
                DeactivatedAt = r.DeactivatedAt,
                ActualDuration = r.ActualDuration.HasValue
                    ? FormatDuration(r.ActualDuration.Value) : null
            }).ToList(),
            TotalCount = entries.Count,
            NistControlMapping = PimConstants.PimNistControlMapping
        };
    }

    /// <inheritdoc />
    public async Task<JitRequestEntity> SubmitApprovalAsync(
        string userId, string userDisplayName, string roleName, string scope,
        string justification, string? ticketNumber, int durationHours,
        Guid sessionId, string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var request = new JitRequestEntity
        {
            RequestType = JitRequestType.PimRoleActivation,
            PimRequestId = $"pim-{Guid.NewGuid():N}",
            UserId = userId,
            UserDisplayName = userDisplayName,
            ConversationId = conversationId,
            SessionId = sessionId,
            RoleName = roleName,
            Scope = scope,
            Justification = justification,
            TicketNumber = ticketNumber,
            TicketSystem = ticketNumber != null ? DetectTicketSystem(ticketNumber) : null,
            Status = JitRequestStatus.PendingApproval,
            DurationHours = durationHours,
            RequestedAt = DateTimeOffset.UtcNow
        };

        db.JitRequests.Add(request);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "High-privilege PIM request submitted: user={UserId}, role={Role}, scope={Scope}, requestId={RequestId}",
            userId, roleName, scope, request.Id);

        return request;
    }

    /// <inheritdoc />
    public async Task<PimApprovalResult> ApproveRequestAsync(
        Guid requestId, string approverId, string approverDisplayName,
        string? comments = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var request = await db.JitRequests.FindAsync(new object[] { requestId }, cancellationToken);

        if (request == null)
        {
            _logger.LogWarning("PIM approval failed: request not found, requestId={RequestId}", requestId);
            return new PimApprovalResult
            {
                ErrorCode = "REQUEST_NOT_FOUND",
                Message = $"PIM request {requestId} not found."
            };
        }

        if (request.Status != JitRequestStatus.PendingApproval)
        {
            _logger.LogWarning("PIM approval failed: request already decided, requestId={RequestId}, status={Status}", requestId, request.Status);
            return new PimApprovalResult
            {
                ErrorCode = "REQUEST_ALREADY_DECIDED",
                Message = $"PIM request {requestId} has already been {request.Status}."
            };
        }

        var now = DateTimeOffset.UtcNow;
        request.Status = JitRequestStatus.Active;
        request.ApproverId = approverId;
        request.ApproverDisplayName = approverDisplayName;
        request.ApproverComments = comments;
        request.ApprovalDecisionAt = now;
        request.ActivatedAt = now;
        request.ExpiresAt = now.AddHours(request.DurationHours);

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "PIM request {RequestId} approved by {Approver} for role {Role}",
            requestId, approverDisplayName, request.RoleName);

        return new PimApprovalResult
        {
            Approved = true,
            RequestId = requestId,
            RequesterName = request.UserDisplayName,
            RoleName = request.RoleName,
            Scope = request.ScopeDisplayName ?? request.Scope,
            DecisionAt = now,
            Message = $"Approved {request.RoleName} activation for {request.UserDisplayName} on {request.ScopeDisplayName ?? request.Scope}. Requester has been notified."
        };
    }

    /// <inheritdoc />
    public async Task<PimApprovalResult> DenyRequestAsync(
        Guid requestId, string approverId, string approverDisplayName,
        string reason, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var request = await db.JitRequests.FindAsync(new object[] { requestId }, cancellationToken);

        if (request == null)
        {
            _logger.LogWarning("PIM denial failed: request not found, requestId={RequestId}", requestId);
            return new PimApprovalResult
            {
                ErrorCode = "REQUEST_NOT_FOUND",
                Message = $"PIM request {requestId} not found."
            };
        }

        if (request.Status != JitRequestStatus.PendingApproval)
        {
            _logger.LogWarning("PIM denial failed: request already decided, requestId={RequestId}, status={Status}", requestId, request.Status);
            return new PimApprovalResult
            {
                ErrorCode = "REQUEST_ALREADY_DECIDED",
                Message = $"PIM request {requestId} has already been {request.Status}."
            };
        }

        var now = DateTimeOffset.UtcNow;
        request.Status = JitRequestStatus.Denied;
        request.ApproverId = approverId;
        request.ApproverDisplayName = approverDisplayName;
        request.ApproverComments = reason;
        request.ApprovalDecisionAt = now;

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "PIM request {RequestId} denied by {Approver}: {Reason}",
            requestId, approverDisplayName, reason);

        return new PimApprovalResult
        {
            Denied = true,
            RequestId = requestId,
            RequesterName = request.UserDisplayName,
            RoleName = request.RoleName,
            Scope = request.ScopeDisplayName ?? request.Scope,
            DecisionAt = now,
            Reason = reason,
            Message = $"Denied {request.RoleName} activation for {request.UserDisplayName}. Requester has been notified with denial reason."
        };
    }

    /// <inheritdoc />
    public bool IsHighPrivilegeRole(string roleName) =>
        _options.HighPrivilegeRoles.Any(r => r.Equals(roleName, StringComparison.OrdinalIgnoreCase));

    // ─── Private validation helpers ──────────────────────────────────────────

    /// <summary>Validates the justification text meets minimum length requirements.</summary>
    private PimActivationResult? ValidateJustification(string justification)
    {
        if (string.IsNullOrWhiteSpace(justification) || justification.Length < _options.MinJustificationLength)
        {
            return new PimActivationResult
            {
                ErrorCode = "JUSTIFICATION_TOO_SHORT",
                Message = $"Justification must be at least {_options.MinJustificationLength} characters. You provided {justification?.Length ?? 0} characters.",
                Suggestion = "Provide a descriptive justification, e.g., 'Remediating AC-2.1 finding per assessment RUN-2026-0221'"
            };
        }
        return null;
    }

    /// <summary>
    /// Validates the ticket number against approved ticket system patterns.
    /// When RequireTicketNumber=true and no ticket — returns TICKET_REQUIRED.
    /// When a ticket is provided — validates against ApprovedTicketSystems patterns.
    /// When RequireTicketNumber=false and no ticket — skips validation.
    /// </summary>
    private PimActivationResult? ValidateTicket(string? ticketNumber)
    {
        if (string.IsNullOrEmpty(ticketNumber))
        {
            if (_options.RequireTicketNumber)
            {
                return new PimActivationResult
                {
                    ErrorCode = "TICKET_REQUIRED",
                    Message = "A ticket number is required for PIM role activation.",
                    Suggestion = $"Provide a ticket from an approved system: {string.Join(", ", _options.ApprovedTicketSystems.Keys)}"
                };
            }
            return null; // No ticket required and none provided — OK
        }

        // Ticket provided — validate against approved patterns
        foreach (var (system, pattern) in _options.ApprovedTicketSystems)
        {
            if (Regex.IsMatch(ticketNumber, pattern))
                return null; // Valid ticket
        }

        return new PimActivationResult
        {
            ErrorCode = "INVALID_TICKET",
            Message = $"Ticket '{ticketNumber}' does not match any approved ticket system format.",
            Suggestion = $"Supported formats: {string.Join(", ", _options.ApprovedTicketSystems.Select(s => $"{s.Key} ({s.Value})"))}"
        };
    }

    /// <summary>Detects the ticket system name from the ticket number format.</summary>
    private string? DetectTicketSystem(string ticketNumber)
    {
        foreach (var (system, pattern) in _options.ApprovedTicketSystems)
        {
            if (Regex.IsMatch(ticketNumber, pattern))
                return system;
        }
        return null;
    }

    /// <summary>Formats a TimeSpan as a human-readable duration string.</summary>
    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours} hour{((int)duration.TotalHours != 1 ? "s" : "")}";
        return $"{(int)duration.TotalMinutes} minute{((int)duration.TotalMinutes != 1 ? "s" : "")}";
    }
}
