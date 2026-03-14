namespace Ato.Copilot.Core.Models.Auth;

/// <summary>
/// Client surface type for CAC session tracking.
/// </summary>
public enum ClientType
{
    /// <summary>Visual Studio Code extension surface.</summary>
    VSCode,

    /// <summary>Microsoft Teams integration surface.</summary>
    Teams,

    /// <summary>Web application surface.</summary>
    Web,

    /// <summary>Command-line interface surface.</summary>
    CLI,

    /// <summary>
    /// Simulated CAC session for development/testing.
    /// Excluded from compliance evidence per FR-014.
    /// </summary>
    Simulated
}

/// <summary>
/// Status of a CAC authentication session.
/// </summary>
public enum SessionStatus
{
    /// <summary>Session is currently active and valid.</summary>
    Active,

    /// <summary>Session has expired due to timeout.</summary>
    Expired,

    /// <summary>Session was explicitly terminated by the user.</summary>
    Terminated
}

/// <summary>
/// Type of JIT access request (PIM role activation or VM network access).
/// </summary>
public enum JitRequestType
{
    /// <summary>Azure AD PIM role activation request.</summary>
    PimRoleActivation,

    /// <summary>Azure AD PIM group membership activation request.</summary>
    PimGroupMembership,

    /// <summary>Just-in-Time VM network access request.</summary>
    JitVmAccess
}

/// <summary>
/// Status of a JIT access request lifecycle.
/// </summary>
public enum JitRequestStatus
{
    /// <summary>Request has been submitted and is being processed.</summary>
    Submitted,

    /// <summary>Request is pending approval from a Security Lead or Compliance Officer.</summary>
    PendingApproval,

    /// <summary>Request has been approved by an approver.</summary>
    Approved,

    /// <summary>Request has been approved and role/access is active.</summary>
    Active,

    /// <summary>Request was denied by an approver.</summary>
    Denied,

    /// <summary>Role/access was explicitly deactivated by the user.</summary>
    Deactivated,

    /// <summary>Role/access expired due to time limit.</summary>
    Expired,

    /// <summary>Request failed due to an error.</summary>
    Failed,

    /// <summary>Request was cancelled by the requester before activation.</summary>
    Cancelled
}

/// <summary>
/// Defines the PIM tier required for a tool or operation.
/// Tier 1 (None): No authentication or PIM elevation required — local/cached operations.
/// Tier 2a (Read): CAC authentication + Reader-level PIM role required — read-only Azure operations.
/// Tier 2b (Write): CAC authentication + Contributor-level (or higher) PIM role required — write Azure operations.
/// </summary>
public enum PimTier
{
    /// <summary>Tier 1: No authentication or PIM elevation required.</summary>
    None = 0,

    /// <summary>Tier 2a: Requires CAC authentication and a Reader-level PIM role.</summary>
    Read = 1,

    /// <summary>Tier 2b: Requires CAC authentication and a Contributor-level (or higher) PIM role.</summary>
    Write = 2
}
