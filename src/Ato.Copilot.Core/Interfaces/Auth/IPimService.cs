using Ato.Copilot.Core.Models.Auth;

namespace Ato.Copilot.Core.Interfaces.Auth;

/// <summary>
/// Service interface for Azure AD Privileged Identity Management (PIM) operations.
/// Handles role activation, deactivation, approval workflows, and history queries.
/// All async methods accept CancellationToken per Constitution VIII.
/// </summary>
public interface IPimService
{
    /// <summary>Lists PIM-eligible role assignments for the authenticated user.</summary>
    Task<List<PimEligibleRole>> ListEligibleRolesAsync(
        string userId, string? scope = null,
        CancellationToken cancellationToken = default);

    /// <summary>Activates a PIM-eligible role. Returns pending approval for high-privilege roles.</summary>
    Task<PimActivationResult> ActivateRoleAsync(
        string userId, string roleName, string scope,
        string justification, string? ticketNumber, int? durationHours,
        Guid sessionId, string? conversationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Deactivates an active PIM role.</summary>
    Task<PimDeactivationResult> DeactivateRoleAsync(
        string userId, string roleName, string scope,
        CancellationToken cancellationToken = default);

    /// <summary>Extends an active PIM role's duration within policy limits.</summary>
    Task<PimExtensionResult> ExtendRoleAsync(
        string userId, string roleName, string scope, int additionalHours,
        CancellationToken cancellationToken = default);

    /// <summary>Lists currently active PIM role assignments for the user.</summary>
    Task<List<PimActiveRole>> ListActiveRolesAsync(
        string userId, CancellationToken cancellationToken = default);

    /// <summary>Queries PIM action history with optional filters.</summary>
    Task<PimHistoryResult> GetHistoryAsync(
        string userId, int days = 7, string? roleName = null,
        string? filterUserId = null, string? scope = null, bool isAuditor = false,
        CancellationToken cancellationToken = default);

    /// <summary>Submits an approval request for a high-privilege role activation.</summary>
    Task<JitRequestEntity> SubmitApprovalAsync(
        string userId, string userDisplayName, string roleName, string scope,
        string justification, string? ticketNumber, int durationHours,
        Guid sessionId, string? conversationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Approves a pending PIM activation request.</summary>
    Task<PimApprovalResult> ApproveRequestAsync(
        Guid requestId, string approverId, string approverDisplayName,
        string? comments = null, CancellationToken cancellationToken = default);

    /// <summary>Denies a pending PIM activation request.</summary>
    Task<PimApprovalResult> DenyRequestAsync(
        Guid requestId, string approverId, string approverDisplayName,
        string reason, CancellationToken cancellationToken = default);

    /// <summary>Checks whether a role name is classified as high-privilege.</summary>
    bool IsHighPrivilegeRole(string roleName);
}

/// <summary>Represents an eligible PIM role assignment.</summary>
public class PimEligibleRole
{
    /// <summary>Name of the eligible Azure AD or RBAC role.</summary>
    public string RoleName { get; set; } = string.Empty;
    /// <summary>Azure AD role definition identifier.</summary>
    public string RoleDefinitionId { get; set; } = string.Empty;
    /// <summary>Resource scope of the eligible role assignment.</summary>
    public string Scope { get; set; } = string.Empty;
    /// <summary>Human-readable display name for the scope.</summary>
    public string ScopeDisplayName { get; set; } = string.Empty;
    /// <summary>Whether this role is currently active.</summary>
    public bool IsActive { get; set; }
    /// <summary>Maximum allowed activation duration (ISO 8601).</summary>
    public string MaxDuration { get; set; } = "PT8H";
    /// <summary>Whether activation requires approval from a security lead.</summary>
    public bool RequiresApproval { get; set; }
}

/// <summary>Represents an active PIM role assignment.</summary>
public class PimActiveRole
{
    /// <summary>Name of the active role.</summary>
    public string RoleName { get; set; } = string.Empty;
    /// <summary>Resource scope of the active assignment.</summary>
    public string Scope { get; set; } = string.Empty;
    /// <summary>Timestamp when the role was activated.</summary>
    public DateTimeOffset ActivatedAt { get; set; }
    /// <summary>Timestamp when the role activation expires.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
    /// <summary>Minutes remaining until expiration.</summary>
    public int RemainingMinutes { get; set; }
    /// <summary>PIM request identifier for traceability.</summary>
    public string PimRequestId { get; set; } = string.Empty;
}

/// <summary>Result of a PIM role activation request.</summary>
public class PimActivationResult
{
    /// <summary>Whether the role was immediately activated.</summary>
    public bool Activated { get; set; }
    /// <summary>Whether the activation requires approval (high-privilege role).</summary>
    public bool PendingApproval { get; set; }
    /// <summary>PIM request identifier for tracking.</summary>
    public string PimRequestId { get; set; } = string.Empty;
    /// <summary>Name of the role being activated.</summary>
    public string RoleName { get; set; } = string.Empty;
    /// <summary>Resource scope of the activation.</summary>
    public string Scope { get; set; } = string.Empty;
    /// <summary>Timestamp when the role was activated (null if pending).</summary>
    public DateTimeOffset? ActivatedAt { get; set; }
    /// <summary>Timestamp when the activation expires (null if pending).</summary>
    public DateTimeOffset? ExpiresAt { get; set; }
    /// <summary>Duration of the activation in hours.</summary>
    public int DurationHours { get; set; }
    /// <summary>Error code if activation failed (null on success).</summary>
    public string? ErrorCode { get; set; }
    /// <summary>Human-readable result message.</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>Suggested action for the user on failure.</summary>
    public string? Suggestion { get; set; }
    /// <summary>Available eligible roles (populated when NOT_ELIGIBLE).</summary>
    public List<PimEligibleRole>? EligibleRoles { get; set; }
    /// <summary>List of approvers notified (for high-privilege requests).</summary>
    public List<string>? ApproversNotified { get; set; }
}

/// <summary>Result of a PIM role deactivation.</summary>
public class PimDeactivationResult
{
    /// <summary>Whether the role was successfully deactivated.</summary>
    public bool Deactivated { get; set; }
    /// <summary>Name of the deactivated role.</summary>
    public string RoleName { get; set; } = string.Empty;
    /// <summary>Resource scope of the deactivation.</summary>
    public string Scope { get; set; } = string.Empty;
    /// <summary>Timestamp when the role was deactivated.</summary>
    public DateTimeOffset DeactivatedAt { get; set; }
    /// <summary>Formatted string of actual time the role was active.</summary>
    public string ActualDuration { get; set; } = string.Empty;
    /// <summary>Error code if deactivation failed.</summary>
    public string? ErrorCode { get; set; }
    /// <summary>Human-readable result message.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>Result of a PIM role extension.</summary>
public class PimExtensionResult
{
    /// <summary>Whether the role was successfully extended.</summary>
    public bool Extended { get; set; }
    /// <summary>Name of the extended role.</summary>
    public string RoleName { get; set; } = string.Empty;
    /// <summary>Resource scope of the extension.</summary>
    public string Scope { get; set; } = string.Empty;
    /// <summary>Previous expiration timestamp before extension.</summary>
    public DateTimeOffset PreviousExpiresAt { get; set; }
    /// <summary>New expiration timestamp after extension.</summary>
    public DateTimeOffset NewExpiresAt { get; set; }
    /// <summary>Error code if extension failed.</summary>
    public string? ErrorCode { get; set; }
    /// <summary>Human-readable result message.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>Result of PIM history query.</summary>
public class PimHistoryResult
{
    /// <summary>List of PIM history entries matching the query.</summary>
    public List<PimHistoryEntry> History { get; set; } = new();
    /// <summary>Total count of matching entries.</summary>
    public int TotalCount { get; set; }
    /// <summary>NIST 800-53 control identifiers mapped to PIM actions.</summary>
    public IReadOnlyList<string> NistControlMapping { get; set; } = Array.Empty<string>();
}

/// <summary>A single PIM history entry.</summary>
public class PimHistoryEntry
{
    /// <summary>Type of PIM request (e.g., PimRoleActivation).</summary>
    public string RequestType { get; set; } = string.Empty;
    /// <summary>Name of the role in the history entry.</summary>
    public string RoleName { get; set; } = string.Empty;
    /// <summary>Resource scope of the role assignment.</summary>
    public string Scope { get; set; } = string.Empty;
    /// <summary>User ID who made the request.</summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>Display name of the requesting user.</summary>
    public string UserDisplayName { get; set; } = string.Empty;
    /// <summary>User-provided justification for the request.</summary>
    public string Justification { get; set; } = string.Empty;
    /// <summary>Optional ticket number from an approved system.</summary>
    public string? TicketNumber { get; set; }
    /// <summary>Current status of the request.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Timestamp when the request was submitted.</summary>
    public DateTimeOffset RequestedAt { get; set; }
    /// <summary>Timestamp when the role was activated (null if not activated).</summary>
    public DateTimeOffset? ActivatedAt { get; set; }
    /// <summary>Timestamp when the role was deactivated (null if still active).</summary>
    public DateTimeOffset? DeactivatedAt { get; set; }
    /// <summary>Formatted actual duration the role was active.</summary>
    public string? ActualDuration { get; set; }
}

/// <summary>Result of an approval or denial decision.</summary>
public class PimApprovalResult
{
    /// <summary>Whether the request was approved.</summary>
    public bool Approved { get; set; }
    /// <summary>Whether the request was denied.</summary>
    public bool Denied { get; set; }
    /// <summary>Unique identifier of the approved/denied request.</summary>
    public Guid RequestId { get; set; }
    /// <summary>Display name of the user who made the request.</summary>
    public string RequesterName { get; set; } = string.Empty;
    /// <summary>Name of the role in the request.</summary>
    public string RoleName { get; set; } = string.Empty;
    /// <summary>Resource scope of the request.</summary>
    public string Scope { get; set; } = string.Empty;
    /// <summary>Timestamp of the approval or denial decision.</summary>
    public DateTimeOffset DecisionAt { get; set; }
    /// <summary>Optional reason provided by the approver/denier.</summary>
    public string? Reason { get; set; }
    /// <summary>Error code if the decision failed.</summary>
    public string? ErrorCode { get; set; }
    /// <summary>Human-readable result message.</summary>
    public string Message { get; set; } = string.Empty;
}
