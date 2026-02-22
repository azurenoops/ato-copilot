# Data Model: CAC Authentication & Privileged Identity Management

**Feature**: 003-cac-auth-pim | **Date**: 2026-02-22

## Entity Relationship Overview

```
CacSession 1──* JitRequestEntity
    │
    └── userId ──> Azure AD (external)

CertificateRoleMapping (standalone lookup)

PimServiceOptions (standalone config, optional per-scope)
```

---

## Entity: CacSession

**Purpose**: Represents an active CAC/PIV authentication session. Created when a user successfully authenticates via MSAL OBO flow with CAC/PIV certificate. Used to gate Tier 2 operations.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| `Id` | `Guid` | PK, auto-generated | Unique session identifier |
| `UserId` | `string` | Required, max 128 | Azure AD object ID |
| `DisplayName` | `string` | Required, max 256 | User's display name from Azure AD |
| `Email` | `string` | Required, max 256 | User's email (UPN) |
| `TokenHash` | `string` | Required, max 128 | SHA256 hash of the JWT (for validation, never store plaintext token) |
| `SessionStart` | `DateTimeOffset` | Required | When the session was established |
| `ExpiresAt` | `DateTimeOffset` | Required | When the session expires |
| `ClientType` | `ClientType` (enum) | Required | VSCode = 0, Teams = 1, Web = 2, CLI = 3 |
| `IpAddress` | `string` | Required, max 45 | Client IP address (IPv4 or IPv6) |
| `Status` | `SessionStatus` (enum) | Required | Active = 0, Expired = 1, Terminated = 2 |
| `CreatedAt` | `DateTimeOffset` | Required, auto-set | Record creation timestamp |
| `UpdatedAt` | `DateTimeOffset` | Required, auto-set | Last modification timestamp |

**Indexes**:
- `IX_CacSession_UserId_Status` on (`UserId`, `Status`) — lookup active sessions for a user
- `IX_CacSession_ExpiresAt` on (`ExpiresAt`) — find sessions approaching expiration

**Validation rules**:
- `ExpiresAt` must be after `SessionStart`
- `ExpiresAt - SessionStart` must be between 1 hour and 24 hours (policy limits from FR-004)
- `TokenHash` must be exactly 64 characters (SHA256 hex digest)
- `IpAddress` must be a valid IPv4 or IPv6 address

**State transitions**:
```
[New] ──> Active (on successful MSAL OBO authentication)
Active ──> Expired (on timeout, detected by background check or next request)
Active ──> Terminated (on user "Sign out" / "Lock my session" command)
Expired ──> Active (on re-authentication, creates new session)
```

---

## Entity: JitRequestEntity

**Purpose**: Records every PIM action (role activation, deactivation, approval, JIT VM access) with full provenance for audit trail and compliance evidence. Tracks the complete lifecycle of a PIM or JIT request.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| `Id` | `Guid` | PK, auto-generated | Unique request identifier |
| `RequestType` | `JitRequestType` (enum) | Required | PimRoleActivation = 0, PimGroupMembership = 1, JitVmAccess = 2 |
| `PimRequestId` | `string` | Nullable, max 256 | Azure AD PIM request ID (returned by Graph API) |
| `UserId` | `string` | Required, max 128 | Azure AD object ID of the requester |
| `UserDisplayName` | `string` | Required, max 256 | Requester's display name |
| `ConversationId` | `string` | Required, max 256 | MCP conversation/session ID for audit linking |
| `SessionId` | `Guid` | Required, FK → CacSession.Id | The CAC session under which this request was made |
| `RoleName` | `string` | Required, max 256 | Role display name (e.g., "Contributor", "Owner") |
| `RoleDefinitionId` | `string` | Nullable, max 256 | Azure AD role definition ID |
| `Scope` | `string` | Required, max 512 | Target scope (subscription ID, resource group path, or resource ID) |
| `ScopeDisplayName` | `string` | Nullable, max 256 | Human-readable scope name (e.g., "Production Subscription") |
| `Justification` | `string` | Required, min 20, max 2000 | User-provided justification for the activation |
| `TicketNumber` | `string` | Nullable, max 128 | Ticket reference from approved ticketing system (required only when RequireTicketNumber=true) |
| `TicketSystem` | `string` | Nullable, max 64 | Ticketing system name (ServiceNow, Jira, Remedy, AzureDevOps); set when ticket is provided |
| `RequestedDuration` | `TimeSpan` | Required | Duration requested by the user |
| `ActualDuration` | `TimeSpan` | Nullable | Actual duration (set on deactivation/expiration) |
| `Status` | `JitRequestStatus` (enum) | Required | See state transitions below |
| `ApproverUserId` | `string` | Nullable, max 128 | Azure AD object ID of the approver (for high-privilege roles) |
| `ApproverDisplayName` | `string` | Nullable, max 256 | Approver's display name |
| `ApprovalTimestamp` | `DateTimeOffset` | Nullable | When the request was approved/denied |
| `ApprovalComments` | `string` | Nullable, max 2000 | Approver's comments |
| `RequestedAt` | `DateTimeOffset` | Required, auto-set | When the request was submitted |
| `ActivatedAt` | `DateTimeOffset` | Nullable | When the role was actually activated in Azure AD |
| `DeactivatedAt` | `DateTimeOffset` | Nullable | When the role was deactivated (manual or auto) |
| `ExpiresAt` | `DateTimeOffset` | Nullable | When the role activation will expire |
| `FailureReason` | `string` | Nullable, max 1000 | Reason for failure (if Status = Failed) |
| `CreatedAt` | `DateTimeOffset` | Required, auto-set | Record creation timestamp |
| `UpdatedAt` | `DateTimeOffset` | Required, auto-set | Last modification timestamp |

**Enums**:

```csharp
public enum JitRequestType
{
    PimRoleActivation = 0,
    PimGroupMembership = 1,
    JitVmAccess = 2
}

public enum JitRequestStatus
{
    Submitted = 0,
    PendingApproval = 1,
    Approved = 2,
    Denied = 3,
    Active = 4,
    Expired = 5,
    Deactivated = 6,
    Failed = 7,
    Cancelled = 8
}
```

**Indexes**:
- `IX_JitRequest_UserId_Status` on (`UserId`, `Status`) — find active requests for a user
- `IX_JitRequest_SessionId` on (`SessionId`) — link requests to a CAC session
- `IX_JitRequest_RequestedAt` on (`RequestedAt`) — time-range queries for audit
- `IX_JitRequest_RoleName_Scope` on (`RoleName`, `Scope`) — detect duplicate activations
- `IX_JitRequest_Status_ExpiresAt` on (`Status`, `ExpiresAt`) — find expiring activations for warnings

**Validation rules**:
- `Justification` must be at least 20 characters (FR-010)
- When `RequireTicketNumber` is enabled: `TicketNumber` must be non-null and match one of the approved ticket system patterns (R-009). When disabled: `TicketNumber` is optional; if provided, it is still validated against approved patterns.
- `RequestedDuration` must be between 1 hour and `MaxActivationDuration` (default 8 hours) for `PimRoleActivation`/`PimGroupMembership` requests, or between 1 hour and `MaxJitDuration` (default 24 hours) for `JitVmAccess` requests (policy configurable per request type)
- For `RequestType = JitVmAccess`: `Scope` must be a full Azure resource ID for a VM
- `SessionId` must reference an active `CacSession`

**State transitions**:

```
[New] ──> Submitted (initial request recorded)
Submitted ──> Active (standard role: immediate activation)
Submitted ──> PendingApproval (high-privilege role: awaiting approval)
PendingApproval ──> Approved (approver grants)
PendingApproval ──> Denied (approver denies)
PendingApproval ──> Cancelled (requester cancels)
Approved ──> Active (role activated after approval)
Active ──> Deactivated (user manually deactivates or auto-deactivate after remediation)
Active ──> Expired (duration elapsed)
Submitted ──> Failed (API error during activation)
Approved ──> Failed (API error during activation after approval)
```

---

## Entity: CertificateRoleMapping

**Purpose**: Maps a CAC certificate identity (thumbprint or subject) to a platform role. Enables automatic role resolution on authentication based on the client certificate used. Part of the 4-tier role resolution chain (FR-028).

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| `Id` | `Guid` | PK, auto-generated | Unique mapping identifier |
| `CertificateThumbprint` | `string` | Nullable, max 128 | SHA1 thumbprint of the certificate (hex, case-insensitive) |
| `CertificateSubject` | `string` | Nullable, max 512 | Certificate subject DN (e.g., `CN=LAST.FIRST.MI.DOD_ID`) |
| `MappedRole` | `string` | Required, max 64 | Platform role name (from ComplianceRoles) |
| `CreatedBy` | `string` | Required, max 256 | User ID or display name of the admin who created the mapping |
| `CreatedAt` | `DateTimeOffset` | Required, auto-set | When the mapping was created |
| `IsActive` | `bool` | Required, default true | Whether this mapping is currently active |

**Indexes**:
- `IX_CertMapping_Thumbprint` on (`CertificateThumbprint`) — UNIQUE where not null
- `IX_CertMapping_Subject` on (`CertificateSubject`) — UNIQUE where not null
- `IX_CertMapping_IsActive` on (`IsActive`) — filter inactive mappings

**Validation rules**:
- At least one of `CertificateThumbprint` or `CertificateSubject` must be non-null
- `MappedRole` must be a valid value from `ComplianceRoles` constants
- `CertificateThumbprint` if provided must be a valid SHA1 hex string (40 characters)

---

## Entity: PimServiceOptions (Configuration Entity — Not EF)

**Purpose**: Stores PIM policy settings. This is a configuration object loaded from `appsettings.json` `Pim` section via `IOptions<PimServiceOptions>`, not a database entity. Provided here for completeness since the spec references it as a key entity.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `DefaultActivationDurationHours` | `int` | 4 | Default PIM role activation duration in hours (FR-010, IL5/IL6 policy). Converted to `TimeSpan` in code. |
| `MaxActivationDurationHours` | `int` | 8 | Maximum allowed activation duration in hours (FR-010, Azure AD PIM policy). Converted to `TimeSpan` in code. |
| `DefaultJitDurationHours` | `int` | 3 | Default JIT VM access duration in hours. Converted to `TimeSpan` in code. |
| `MaxJitDurationHours` | `int` | 24 | Maximum JIT VM access duration in hours. Converted to `TimeSpan` in code. |
| `RequireTicketNumber` | `bool` | false | Whether a ticket number is required for activation. Set to true for organizations that use ticketing systems. |
| `MinJustificationLength` | `int` | 20 | Minimum justification text length |
| `MaxJustificationLength` | `int` | 2000 | Maximum justification text length |
| `ApprovedTicketSystems` | `Dictionary<string, string>` | See R-009 | Ticket system name → regex pattern |
| `HighPrivilegeRoles` | `List<string>` | See R-008 | Role names requiring approval workflow |
| `ApprovalTimeoutMinutes` | `int` | 60 | Minutes before approval request times out |
| `SessionExpirationWarningMinutes` | `int` | 15 | Minutes before session expiry to warn |
| `AutoDeactivateAfterRemediation` | `bool` | false | Auto-deactivate PIM role after remediation completes |

**Validation rules**:
- `MaxActivationDurationHours >= DefaultActivationDurationHours`
- `MaxJitDurationHours >= DefaultJitDurationHours`
- `MinJustificationLength >= 10` and `<= MaxJustificationLength`
- `ApprovedTicketSystems` entries must have valid regex patterns (ignored when RequireTicketNumber=false and no ticket is provided)
- `HighPrivilegeRoles` must not be empty

---

## Enum Definitions

```csharp
public enum ClientType
{
    VSCode = 0,
    Teams = 1,
    Web = 2,
    CLI = 3
}

public enum SessionStatus
{
    Active = 0,
    Expired = 1,
    Terminated = 2
}

/// <summary>
/// Declares the PIM elevation tier required by a tool (FR-001, FR-013).
/// </summary>
public enum PimTier
{
    /// <summary>Tier 1 — no authentication or PIM required (local/cached operations).</summary>
    None = 0,
    /// <summary>Tier 2a — CAC + read-eligible PIM role (Reader) required (assessments, evidence, queries).</summary>
    Read = 1,
    /// <summary>Tier 2b — CAC + write-eligible PIM role (Contributor+) required (remediations, PIM actions, JIT).</summary>
    Write = 2
}
```

---

## Entity: RetentionPolicyOptions (Configuration Entity — Not EF)

**Purpose**: Stores data retention policy settings. Loaded from `appsettings.json` `Retention` section via `IOptions<RetentionPolicyOptions>`. Used by `RetentionCleanupHostedService` to enforce FR-042, FR-043, FR-044.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `AssessmentRetentionDays` | `int` | 1095 (3 years) | Days to retain assessment results and evidence packages (FR-042) |
| `AuditLogRetentionDays` | `int` | 2555 (7 years) | Minimum days to retain audit log entries — immutable, append-only (FR-043) |
| `CleanupIntervalHours` | `int` | 24 | How often the retention cleanup service runs |
| `EnableAutomaticCleanup` | `bool` | true | Whether the cleanup hosted service is active |

**Validation rules**:
- `AssessmentRetentionDays` must be >= 365 (minimum 1 year)
- `AuditLogRetentionDays` must be >= 2555 (minimum 7 years, cannot be reduced below federal requirement)
- `CleanupIntervalHours` must be >= 1

---

## DbContext Extensions

The following DbSets are added to `AtoCopilotContext`:

```csharp
public DbSet<CacSession> CacSessions { get; set; } = null!;
public DbSet<JitRequestEntity> JitRequests { get; set; } = null!;
public DbSet<CertificateRoleMapping> CertificateRoleMappings { get; set; } = null!;
```

**Migration**: A new EF Core migration (`AddCacAuthPimEntities`) adds tables `CacSessions`, `JitRequests`, and `CertificateRoleMappings` with all indexes defined above.
