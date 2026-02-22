# Research: CAC Authentication & Privileged Identity Management

**Feature**: 003-cac-auth-pim | **Date**: 2026-02-22

## R-001: MSAL OBO Flow with CAC/PIV Certificates

**Decision**: Use `Microsoft.Identity.Web` (v3.5+) with On-Behalf-Of (OBO) flow for server-side token exchange. The client acquires a JWT via MSAL interactive flow with CAC/PIV + MFA, then the MCP server exchanges that token for downstream ARM-scoped tokens using `ITokenAcquisition.GetAccessTokenForUserAsync()`.

**Rationale**: `Microsoft.Identity.Web` wraps the MSAL confidential client OBO flow with first-class ASP.NET Core integration (automatic token caching, middleware configuration, `[Authorize]` attribute support). The OBO flow ensures all Azure operations execute under the user's identity — the MCP server never uses a service principal for resource operations. The `amr` claim in the incoming JWT is checked for `["mfa", "rsa"]` to confirm CAC/PIV was used, since Azure AD sets `amr=rsa` when certificate-based authentication completes and `amr=mfa` when multifactor is satisfied.

**Alternatives considered**:
- **Client Credentials flow**: Rejected — operations would execute under a service principal identity, violating the requirement that all Azure operations execute under the user's identity (FR-003).
- **Raw MSAL.NET ConfidentialClientApplication**: Rejected — requires manual token cache management, retry logic, and middleware wiring that `Microsoft.Identity.Web` handles automatically. No benefit for additional complexity.
- **Device Code flow**: Rejected — not applicable for server-side exchange. May be used on the client side for CLI surface but that is out of scope for server implementation.

**Implementation notes**:
- Register the MCP server as a "web API that calls downstream APIs" in Azure AD app registration.
- Configure OBO scopes: `https://management.usgovcloudapi.net/.default` for ARM operations, `https://graph.microsoft.us/.default` for Graph/PIM API.
- Token cache: Use `Microsoft.Identity.Web.TokenCacheProviders.InMemory` for dev, evaluate distributed cache for production.
- Azure Government authority: `https://login.microsoftonline.us/{tenantId}/v2.0`

---

## R-002: Azure AD PIM API via Microsoft Graph

**Decision**: Use Microsoft Graph SDK (`Microsoft.Graph` v5.70+) targeting the Microsoft Graph for US Government endpoint (`https://graph.microsoft.us/v1.0`) to manage PIM role activations, deactivations, and eligibility queries.

**Rationale**: The PIM API is exposed through Microsoft Graph's `roleManagement` resource. The Graph SDK provides strongly-typed client models, automatic pagination, retry handling, and serialization. Azure Government has a dedicated Graph endpoint that must be used instead of the commercial `graph.microsoft.com`.

**Alternatives considered**:
- **Direct HTTP calls to Graph REST API**: Rejected — requires manual serialization, pagination, error handling, and authentication header management. The SDK handles all of this with type safety.
- **Azure AD PowerShell/CLI**: Rejected — not suitable for programmatic server-side use in a C# application.
- **Azure Resource Manager (ARM) API for PIM**: The PIM API moved from the legacy `privilegedAccess` endpoint to `roleManagement` in Microsoft Graph v1.0. The ARM PIM API is deprecated.

**Key Graph API endpoints**:
- **List eligible roles**: `GET /roleManagement/directory/roleEligibilityScheduleInstances?$filter=principalId eq '{userId}'`
- **Activate role**: `POST /roleManagement/directory/roleAssignmentScheduleRequests` with `action=selfActivate`, `justification`, `ticketInfo`, `scheduleInfo`
- **Deactivate role**: `POST /roleManagement/directory/roleAssignmentScheduleRequests` with `action=selfDeactivate`
- **List active roles**: `GET /roleManagement/directory/roleAssignmentScheduleInstances?$filter=principalId eq '{userId}'`
- **Approval management**: `GET /identityGovernance/privilegedAccess/group/assignmentApprovals`, `POST .../stages/{id}` with `reviewResult=Approve|Deny`

**Implementation notes**:
- For **subscription-level** (Azure RBAC) PIM roles (Reader, Contributor, Owner on subscriptions), the Graph endpoint is `/roleManagement/directory/roleAssignmentScheduleRequests` for Entra ID roles, but for Azure resource roles the API is under Azure Resource Manager: `PUT /providers/Microsoft.Authorization/roleAssignmentScheduleRequests/{id}`. The PimService needs to handle both.
- Use `GraphServiceClient` with a `TokenCredentialAuthenticationProvider` using the OBO-acquired Graph token.
- Azure Government Graph endpoint: `https://graph.microsoft.us` — must set `GraphServiceClient.RequestAdapter.BaseUrl` accordingly.

---

## R-003: JIT VM Access via Azure Defender for Cloud API

**Decision**: Use `Azure.ResourceManager.SecurityCenter` SDK (existing dependency) to create JIT VM access requests via the Azure Defender for Cloud Managed API. The SDK provides `JitNetworkAccessPolicyResource` and related types for creating and managing JIT requests.

**Rationale**: The JIT VM access API is part of Azure Defender for Cloud (formerly Azure Security Center). The `Azure.ResourceManager.SecurityCenter` SDK wraps the REST API with strongly-typed models. Since `Azure.ResourceManager` is already a dependency in the Core project, extending with the SecurityCenter package is consistent with the existing architecture.

**Alternatives considered**:
- **Direct ARM REST API calls**: Rejected — `Azure.ResourceManager.SecurityCenter` already wraps these endpoints with type safety, automatic authentication via `TokenCredential`, and retry logic.
- **Azure CLI / PowerShell invocation**: Rejected — not suitable for programmatic server-side use.

**Key operations**:
- **Create JIT request**: `PUT /subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.Security/locations/{loc}/jitNetworkAccessPolicies/{policyName}/initiate`
  - Body: `virtualMachines[].ports[]` with `number` (22/3389/custom), `allowedSourceAddressPrefix` (user IP), `endTimeUtc` (now + duration)
- **List active JIT sessions**: `GET /subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.Security/locations/{loc}/jitNetworkAccessPolicies`
- **Revoke JIT access**: There is no direct "revoke" API — the approach is to delete the JIT request or wait for expiration. For immediate revocation, modify the NSG rule directly via the NetworkManager SDK.

**Implementation notes**:
- Auto-detect source IP via the incoming request's `X-Forwarded-For` header or `HttpContext.Connection.RemoteIpAddress`. Allow user override for VPN/proxy scenarios.
- JIT requires Defender for Cloud Standard tier on the subscription. If not enabled, the API returns 404 — the service should detect this and return an actionable error.
- Default ports: SSH (22), RDP (3389). Support custom port specification.

---

## R-004: JWT Claim Validation for CAC Authentication

**Decision**: Validate the incoming JWT token's `amr` (Authentication Methods Reference) claim for the presence of both `"mfa"` and `"rsa"` values to confirm CAC/PIV certificate-based multifactor authentication. Additionally validate `iss` (issuer), `aud` (audience), `exp` (expiration), and `tid` (tenant ID).

**Rationale**: When a user authenticates with a CAC/PIV smart card via Azure AD, the resulting JWT contains `amr: ["mfa", "rsa"]`. The `rsa` value indicates certificate-based authentication (the private key on the smart card signed the authentication challenge), and `mfa` indicates that multifactor authentication was satisfied (the CAC PIN constitutes the second factor). Checking both values ensures the token was obtained via a genuine CAC/PIV authentication flow, not just password + software MFA.

**Alternatives considered**:
- **Check only `mfa` claim**: Rejected — `mfa` alone could be satisfied by password + Authenticator app, which would not prove CAC/PIV usage.
- **Check certificate thumbprint in token**: The standard Azure AD v2.0 token does not include the certificate thumbprint in the access token. The `x5t` header is in the ID token's JWT header for Azure AD CBA, but this is not reliably present in all token types. Using `amr` claims is the standard approach recommended by Microsoft.
- **Validate certificate chain server-side**: Rejected — certificate validation is handled by Azure AD during the authentication flow. The MCP server trusts Azure AD's validation and checks the resulting claims. Re-validating the certificate chain server-side would require access to the DoD root CA chain and adds complexity with no security benefit.

**Implementation notes**:
- The `amr` claim is an array: `["mfa", "rsa"]`. Validation should check that both values are present (set containment, not exact match, since additional values may be present).
- When `RequireCac` is `false` in configuration (development mode), skip the `amr` claim check but still validate `iss`, `aud`, and `exp`.
- Log the `amr` claim values at Debug level for troubleshooting authentication issues.

---

## R-005: Session Management Patterns for Token Caching

**Decision**: Implement a server-side session model using the `CacSession` entity in the database (SQLite/SQL Server) for session tracking and metadata, combined with `Microsoft.Identity.Web`'s built-in in-memory token cache for OBO token storage. Session state includes user identity, expiration, client type, and status. The token itself is never persisted to disk — only a hash is stored for validation.

**Rationale**: The session entity provides queryable audit trail and expiration management. The MSAL token cache handles the actual OAuth token lifecycle (refresh, silent acquisition). Separating concerns — session metadata in the database, token material in memory — avoids persisting sensitive credentials while maintaining full session visibility.

**Alternatives considered**:
- **Stateless JWT-only (no server session)**: Rejected — cannot track session status (terminated by user), cannot provide proactive expiration warnings, cannot link PIM activations to a session ID for audit purposes.
- **Redis distributed cache for sessions**: Over-engineered for single-server deployment. Can be added later when horizontal scaling is needed. The in-memory token cache combined with database session tracking is sufficient.
- **Store encrypted tokens in database**: Rejected — unnecessary security risk. If the database is compromised, encrypted tokens could be exposed if the encryption key is also compromised. In-memory only for token material.

**Implementation notes**:
- `CacSession` record:
  - `Id` (Guid, PK)
  - `UserId` (string, Azure AD object ID)
  - `DisplayName` (string)
  - `Email` (string)
  - `TokenHash` (string, SHA256 hash of the JWT for validation)
  - `SessionStart` (DateTimeOffset)
  - `ExpiresAt` (DateTimeOffset)
  - `ClientType` (enum: VSCode, Teams, Web, CLI)
  - `IpAddress` (string)
  - `Status` (enum: Active, Expired, Terminated)
- Session lookup: On each Tier 2 request, check session status and expiration. If expired, update status to `Expired` and prompt re-auth.
- Expiration warning: A background timer (or check on each request) evaluates sessions within 15 minutes of expiry.

---

## R-006: Azure Government-Specific Authentication Considerations

**Decision**: All authentication endpoints, Graph API endpoints, and ARM endpoints must use Azure Government URLs. No hardcoded commercial cloud URLs anywhere in the codebase.

**Rationale**: Azure Government is a physically isolated cloud environment with different endpoints. Using commercial endpoints would fail (tenant not found) or violate data sovereignty requirements.

**Reference table**:

| Service | Commercial | Azure Government |
|---------|------------|-----------------|
| Azure AD Authority | `login.microsoftonline.com` | `login.microsoftonline.us` |
| Microsoft Graph | `graph.microsoft.com` | `graph.microsoft.us` |
| ARM Management | `management.azure.com` | `management.usgovcloudapi.net` |
| Azure AD Graph (deprecated) | `graph.windows.net` | `graph.windows.net` |
| Key Vault | `vault.azure.net` | `vault.usgovcloudapi.net` |

**Implementation notes**:
- The existing `appsettings.json` already has `Instance: "https://login.microsoftonline.us/"` — this is correct.
- Use `AzureAuthorityHosts.AzureGovernment` from `Azure.Identity` for `TokenCredential` construction.
- Microsoft Graph SDK needs explicit base URL: `graphClient.RequestAdapter.BaseUrl = "https://graph.microsoft.us/v1.0"`.
- ARM endpoint for PIM Azure resource roles: `https://management.usgovcloudapi.net`.
- All URLs should be configurable via `appsettings.json` (not hardcoded) to allow environment-specific overrides.

---

## R-007: Two-Tier Operation Classification

**Decision**: Classify operations into Tier 1 (unauthenticated) and Tier 2 (CAC-authenticated) using a static registry in `AuthTierClassification`. Each MCP tool is mapped to a tier at registration time. The existing `ComplianceAuthorizationMiddleware` is extended with a tier check that runs before the existing role-based authorization.

**Rationale**: The tier classification is a static property of each tool — it doesn't change at runtime. A registry-based approach is simpler than attribute-based reflection and integrates naturally with the existing `WriteTools`/`ApprovalTools` HashSet pattern in `ComplianceAuthorizationMiddleware`.

**Alternatives considered**:
- **Attribute decoration on tool classes** (`[RequiresTier2]`): Rejected — requires reflection to discover tier at runtime, adds a parallel metadata system to the existing tool registration pattern.
- **Policy-based middleware (ASP.NET Core authorization policies)**: Over-engineered for binary tier classification. Authorization policies are designed for complex claim/requirement matrices, not a simple two-way gate.
- **Check inside each tool's ExecuteAsync**: Rejected — scatters authentication enforcement across many files instead of centralizing in middleware. Easy to forget on new tools.

**Tier 1 operations** (no authentication required):
- NIST control queries (knowledge base lookups)
- Cached assessment result viewing
- Local template generation
- Kanban board viewing, commenting
- Configuration viewing
- Help commands
- Authentication status query (`cac_status`)

**Tier 2 operations** (CAC authentication required):
- Live compliance assessments
- Remediation execution
- Evidence collection
- Resource discovery
- PIM role activation/deactivation/extension
- JIT VM access requests
- Any operation that calls Azure ARM or Graph APIs

**Implementation notes**:
- Add a `Tier2Tools` HashSet to `ComplianceAuthorizationMiddleware` alongside the existing `WriteTools` and `ApprovalTools` sets.
- Before role authorization, check if the requested tool is in `Tier2Tools`. If yes and no active CAC session, return a structured auth-required response.
- The `cac_status` tool is Tier 1 — users should always be able to check their authentication status.

---

## R-008: High-Privilege Role Classification

**Decision**: Classify the following Azure AD / Azure RBAC roles as "high-privilege" requiring approval workflow: Owner, User Access Administrator, Security Administrator, Global Administrator, Privileged Role Administrator. This list is configurable via `PimServiceOptions`.

**Rationale**: These roles grant the ability to modify other users' access (Owner, User Access Administrator), modify security policy (Security Administrator), or elevate privileges (Global Administrator, Privileged Role Administrator). Per NIST AC-6(1) (Authorize Access to Security Functions), activation of these roles requires additional oversight.

**Alternatives considered**:
- **All roles require approval**: Rejected — excessive friction for standard roles (Reader, Contributor) that don't grant privilege escalation capability. Would make the system unusable for daily operations.
- **Inherit PIM policy from Azure AD**: Azure AD PIM does have its own approval configuration per role. The platform could defer entirely to Azure AD PIM's approval settings. However, the spec requires the platform to manage its own approval workflow (via conversational commands), so a local classification is needed. The local classification should be aligned with the Azure AD PIM policy but is independently enforced.

**Implementation notes**:
- `PimServiceOptions.HighPrivilegeRoles` is a `List<string>` of role definition names.
- Default configuration includes: `["Owner", "User Access Administrator", "Security Administrator", "Global Administrator", "Privileged Role Administrator"]`.
- The high-privilege check runs after PIM eligibility is confirmed but before the activation request is submitted to Azure AD.

---

## R-009: Ticket System Validation

**Decision**: Ticket number collection is optional, controlled by `RequireTicketNumber` (default: false). When enabled, validate ticket numbers by format/prefix only — no integration with external ticketing systems. Supported systems: ServiceNow (`SNOW-*`), Jira (`[A-Z]+-[0-9]+`), Remedy (`HD-*`), Azure DevOps (`AB#[0-9]+`). When disabled, ticket number is not collected and validation is skipped. If a ticket is voluntarily provided (even when not required), it is still validated against configured patterns.

**Rationale**: Not all organizations use a ticketing system, so requiring a ticket number by default would block adoption. Making it opt-in via configuration allows organizations with ticketing workflows to enforce ticket references for audit trail quality, while those without can still use PIM activation with justification alone.

**Alternatives considered**:
- **Always require ticket**: Rejected — blocks organizations without ticketing systems from using PIM activation.
- **No validation when provided**: Rejected — if a ticket is provided (even optionally), validating format ensures audit trail quality.
- **Full API integration**: Rejected — per spec assumptions, out of scope. Would require credentials for each system and handle availability failure modes.
- **Custom regex per organization**: The format patterns are configurable via `PimServiceOptions.ApprovedTicketSystems`, allowing organizations to add or modify patterns.

**Implementation notes**:
- `ApprovedTicketSystems` in configuration maps system name to regex pattern.
- Default patterns:
  - ServiceNow: `^SNOW-[A-Z]+-\d+$`
  - Jira: `^[A-Z]{2,10}-\d+$`
  - Remedy: `^HD-\d+$`
  - Azure DevOps: `^AB#\d+$`
- Validation error returns the list of supported formats.
---

## R-010: Tier 2a/2b Sub-Tier Model for PIM Read/Write Gating

**Decision**: Extend the existing two-tier model (Tier 1 = no auth, Tier 2 = CAC required) with sub-tiers: Tier 2a (read — Reader PIM sufficient) and Tier 2b (write — Contributor+ PIM required). Each tool declares its required PIM tier via a new `RequiredPimTier` enum property on `BaseTool`. The `ComplianceAuthorizationMiddleware` evaluates the tool's declared tier against the user's active PIM role assignments.

**Rationale**: FR-001 and FR-013 require write operations (remediations, policy modifications) to require a higher PIM tier than read-only operations (assessments). The sub-tier model maps naturally to Azure RBAC's Reader/Contributor/Owner hierarchy. The tier declaration on each tool keeps authorization metadata co-located with the tool definition — consistent with the existing `Name`, `Description`, `Parameters` pattern in `BaseTool`.

**Alternatives considered**:
- **Role-based only (implicit)**: Each tool already requires specific Azure RBAC roles to execute (Reader for assessments, Contributor for remediations). The PIM tier is technically implicit in the role requirement. However, making it explicit via a `RequiredPimTier` enum provides clear documentation, simplifies middleware gating, and enables descriptive error messages per FR-034.
- **Middleware body parsing**: Parse the MCP JSON-RPC request body in middleware to extract the tool name and look up its tier. Rejected — middleware runs before the endpoint handler parses the body, and current architecture has this limitation (documented during T098 implementation).
- **Separate policy engine**: Over-engineered for a binary read/write distinction.

**Implementation notes**:
- Add `PimTier` enum: `None = 0` (Tier 1), `Read = 1` (Tier 2a), `Write = 2` (Tier 2b)
- Add virtual `RequiredPimTier` property to `BaseTool` (default: `PimTier.None`)
- Override in each tool class to declare its tier
- Tier 2a tools: `cac_status` (None, already Tier 1), assessment tools, evidence collection, `pim_list_eligible`, `pim_list_active`, `pim_history`, `jit_list_sessions`
- Tier 2b tools: remediation tools, `pim_activate_role`, `pim_deactivate_role`, `pim_extend_role`, `pim_approve_request`, `pim_deny_request`, `jit_request_access`, `jit_revoke_access`
- PIM activation tools themselves are Tier 2b because they modify Azure AD state
- Error messages include `requiredPimTier` field: "This operation requires a write-eligible PIM role (Contributor or higher). Your current elevation: Reader."
- `AuthTierClassification` updated with `Tier2aTools` and `Tier2bTools` HashSets alongside existing `Tier2Tools`

---

## R-011: Data Retention Policies

**Decision**: Implement retention via configurable `RetentionPolicyOptions` (loaded from `appsettings.json` `Retention` section) with a `RetentionCleanupHostedService` that runs daily. Assessment data retains for 3 years, audit logs for 7 years. Audit log immutability enforced by: (1) EF Core entity configuration that marks the entity as read-only (no `Update`/`Delete` methods on the service), (2) database-level constraints for production.

**Rationale**: FR-042 (3-year assessment retention) and FR-043 (7-year immutable audit logs) are federal data retention requirements. The retention cleanup service pattern matches the existing `OverdueScanHostedService` and `SessionCleanupHostedService` patterns — a `BackgroundService` with a `PeriodicTimer`. Immutability at the application layer (no update/delete methods) is enforced in code; physical immutability at the database layer is a deployment concern.

**Alternatives considered**:
- **Database-level TTL / row expiration**: SQL Server and SQLite don't natively support TTL. Would require temporal tables in SQL Server — over-complex for initial implementation.
- **External archival service**: Move expired data to blob storage. Premature for single-tenant deployment.
- **No automatic cleanup**: Let data accumulate. Rejected — unbounded data growth violates Constitution VIII (bounded result sets).

**Implementation notes**:
- `RetentionPolicyOptions`: `AssessmentRetentionDays: 1095` (3 years), `AuditLogRetentionDays: 2555` (7 years), `CleanupIntervalHours: 24`
- `RetentionCleanupHostedService`: Runs every 24 hours. Deletes assessment records past retention. Does NOT delete audit logs (they are immutable; only retained metadata is tracked).
- Add `CreatedAt` index on assessment and audit tables for efficient retention queries.
- Audit log immutability: `AuditLogEntry` service exposes only `AddAsync` — no update or delete.

---

## R-012: Observability — Health, Metrics, and Correlation IDs

**Decision**: Implement observability in three layers: (1) ASP.NET Core health checks with per-agent availability via `IHealthCheck` implementations, (2) custom metrics via `System.Diagnostics.Metrics` (the .NET 8+ built-in metrics API), (3) correlation ID propagation via middleware that reads/generates `X-Correlation-ID` header and stores it in `Activity.Current` / Serilog `LogContext`.

**Rationale**: FR-045–048 require health checks, structured metrics, and correlation IDs. Using the built-in `System.Diagnostics.Metrics` API provides: (a) zero-dependency metrics collection, (b) automatic integration with OpenTelemetry exporters if added later, (c) alignment with .NET 9 metrics best practices. The health check system uses ASP.NET Core's built-in `IHealthCheck` / `MapHealthChecks()` pattern.

**Alternatives considered**:
- **Application Insights SDK directly**: Already configured via Serilog sink for production. However, App Insights metrics API is proprietary and doesn't work offline. `System.Diagnostics.Metrics` works everywhere.
- **Prometheus.NET**: Adds a dependency. `System.Diagnostics.Metrics` + OpenTelemetry exporter achieves the same with standard APIs.

**Implementation notes**:
- `CorrelationIdMiddleware`: Already started by user. Reads `X-Correlation-ID` from request headers or generates GUID. Sets on `HttpContext.Items["CorrelationId"]` and pushes to Serilog `LogContext`. Adds to response headers.
- `AgentHealthCheck : IHealthCheck`: One health check per agent. Returns `Healthy`, `Degraded`, or `Unhealthy`.
- `ToolMetrics`: Static class using `Meter` named `"Ato.Copilot"`. Instruments: `Counter<long> ToolInvocations`, `Histogram<double> ToolDuration`, `Counter<long> ToolErrors`, `UpDownCounter<long> ActiveSessions`.
- Health endpoint: `app.MapHealthChecks("/health")` with custom JSON writer.
- Middleware pipeline order: `CorrelationIdMiddleware` first (before `UseSerilogRequestLogging`)
- All Serilog log entries enriched with: `CorrelationId`, `AgentName`, `ToolName`, `UserId` (redacted)

---

## R-013: Secret Management — Azure Key Vault Integration

**Decision**: Use `Azure.Extensions.AspNetCore.Configuration.Secrets` to add Azure Key Vault as a configuration provider. In production, secrets load from Key Vault. In development, `appsettings.Development.json` and environment variables serve as fallback. Key Vault URI configured via `KeyVault:VaultUri` in appsettings.

**Rationale**: FR-038 mandates Key Vault for production secrets with managed identity. The ASP.NET Core Configuration provider model makes this transparent — code reads `IConfiguration["AzureAd:ClientSecret"]` regardless of source. `DefaultAzureCredential` handles auth: managed identity in Azure, Azure CLI locally.

**Alternatives considered**:
- **Environment variables only**: Inadequate for production — env vars can leak through process inspection. Key Vault provides audit trail, rotation, and FIPS 140-2 Level 2 HSM backing.
- **HashiCorp Vault**: Not Azure-native, adds operational complexity in Azure Government.

**Implementation notes**:
- In `Program.cs`, add Key Vault provider for non-Development environments
- Key Vault secret names use `--` as section delimiter: `AzureAd--ClientSecret`
- Azure Government Key Vault endpoint: `vault.usgovcloudapi.net`
- Package: `Azure.Extensions.AspNetCore.Configuration.Secrets`
- FIPS 140-2 Level 2: Enforced by Azure Key Vault HSM in Azure Government — no app-level action needed