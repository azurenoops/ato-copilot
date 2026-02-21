# Research: Core Compliance Capabilities

**Branch**: `001-core-compliance` | **Date**: 2026-02-21

## R-001: Azure Resource Graph SDK

**Decision**: Use `Azure.ResourceManager.ResourceGraph` (v1.1.0) — the Track 2 ARM-based package.

**Rationale**: Integrates with the existing `ArmClient` dependency (`Azure.ResourceManager`
1.13.0 already in Core.csproj). Provides `ResourceQueryContent` for Kusto queries against
subscriptions with built-in pagination via `SkipToken` (max 1,000 rows per request).

**Alternatives considered**:
- `Microsoft.Azure.Management.ResourceGraph` — Legacy Track 1; deprecated auth model.
- Direct REST via `HttpClient` — No SDK benefits; pagination/auth are manual.

**Key details**:
- Kusto queries discover resources by type (storage, VMs, networking, Key Vault).
- Rate limits: 15 requests per 5-second window per tenant; 12,000/hour per tenant.
- Use `ArmClientOptions.Retry` with exponential back-off (max 5 retries, 1-30s delay).
- Dual-cloud: `ArmEnvironment.AzureGovernment` uses `management.usgovcloudapi.net`.

**NuGet**: `Azure.ResourceManager.ResourceGraph` 1.1.0

---

## R-002: Azure Policy Compliance SDK

**Decision**: Use `Azure.ResourceManager.PolicyInsights` (v1.2.0) for compliance data,
combined with `Azure.ResourceManager.Resources` (v1.9.0) for policy definitions/assignments.

**Rationale**: Track 2 packages that integrate with existing `ArmClient`. Provide
`GetPolicyStateQueryResults`, `SummarizeForPolicyStates`, and access to regulatory
compliance initiatives.

**Alternatives considered**:
- `Microsoft.Azure.Management.PolicyInsights` — Legacy Track 1; deprecated.
- Direct REST — No SDK benefits.

**Key details**:
- Built-in regulatory compliance initiatives exist for all target frameworks:
  - NIST 800-53 Rev 5: `179d1daa-458f-4e47-8086-2a68d0d6c38f`
  - FedRAMP High: `d5264498-16f4-418a-b659-fa7ef418175f`
  - FedRAMP Moderate: `e95f5a9f-57ad-4d03-bb0b-b1d16db93693`
  - DoD IL5: `f15e86d0-8189-4e81-9999-30e5547f5fac`
  - Note: Azure Government may have different IDs; query programmatically.
- Policy definitions within initiatives have `GroupNames` mapping to NIST control IDs.
- Initiative `PolicyDefinitionGroups` provide control family metadata (category, description).
- Policy remediation tasks can be triggered via `PolicyRemediations.CreateOrUpdateAsync`.

**NuGet**: `Azure.ResourceManager.PolicyInsights` 1.2.0,
`Azure.ResourceManager.Resources` 1.9.0

---

## R-003: Microsoft Defender for Cloud SDK

**Decision**: Use `Azure.ResourceManager.SecurityCenter` (v1.2.0).

**Rationale**: Track 2 ARM SDK for Defender for Cloud (formerly Azure Security Center).
Provides secure score, regulatory compliance assessments, and remediations.

**Alternatives considered**:
- `Microsoft.Azure.Management.Security` — Legacy Track 1; deprecated.
- Microsoft Graph Security API — Different scope; incident-focused, not compliance-focused.

**Key details**:
- Secure score: `subscription.GetSecureScores()` returns `CurrentScore`, `MaxScore`,
  `Percentage`.
- Regulatory compliance: `GetRegulatoryComplianceStandards()` → `GetRegulatoryComplianceControls()` → `GetRegulatoryComplianceAssessments()` for NIST-mapped control states.
- Remediation: DFC provides human-readable `RemediationDescription`; programmatic remediation
  uses ARM SDK to modify resources or trigger `PolicyRemediation` tasks.
- DFC serves as a third data source alongside Resource Graph and Policy.

**NuGet**: `Azure.ResourceManager.SecurityCenter` 1.2.0

---

## R-004: ArmClient DI & Dual-Cloud Configuration

**Decision**: Register `ArmClient` as a singleton via DI, reading cloud environment from
`GatewayOptions.CloudEnvironment` (existing configuration).

**Rationale**: `ArmClient` is thread-safe and designed for singleton lifetime. The project
already has `CloudEnvironment` in `GatewayOptions` defaulting to `"AzureGovernment"`.

**Key details**:
- `ArmEnvironment.AzureGovernment` for gov; `ArmEnvironment.AzurePublicCloud` for commercial.
- `DefaultAzureCredential` with `AzureAuthorityHosts.AzureGovernment` for government auth.
- Auth chain: EnvironmentCredential → WorkloadIdentityCredential → ManagedIdentityCredential → AzureCliCredential.
- Developers must `az cloud set --name AzureUSGovernment && az login`.
- Retry policy: MaxRetries=5, Exponential mode, 1-30s delay range.
- All async operations accept `CancellationToken`; use `CancellationTokenSource` with 60s
  timeout for assessments.

---

## R-005: NIST 800-53 Rev 5 Control Catalog

**Decision**: Dual-source strategy — fetch from GitHub when online, fall back to embedded
resource when offline. Use the NIST OSCAL machine-readable catalog (JSON) as the
authoritative source in both cases.

**Rationale**: Azure Government and air-gapped environments cannot reach GitHub. The embedded
resource guarantees functionality in all deployment scenarios. Online fetch keeps the catalog
current without requiring app redeployment.

**Alternatives considered**:
- Embedded-only — Works offline but requires app rebuild to update catalog.
- Online-only — Fails in air-gapped/GovCloud environments.
- NIST NVD API — Runtime dependency; different data format; not suitable for offline.
- Hardcoded in code — Unmaintainable for 1,189 controls.

**Dual-source strategy**:
- **Online (primary)**: `HttpClient` fetches from GitHub raw URL at startup when
  `NistCatalog:PreferOnline` is `true` (default). Cached locally after successful fetch.
  URL: `https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json/NIST_SP-800-53_rev5_catalog.json`
- **Offline (fallback)**: Embedded resource `Compliance/Resources/NIST_SP-800-53_rev5_catalog.json`
  used when GitHub is unreachable (timeout, DNS failure, air-gapped network).
- **Cache**: Store fetched catalog in `NistCatalog:CachePath` (default: `data/nist-catalog.json`).
  If cache exists and is less than `NistCatalog:CacheMaxAgeDays` old (default: 30), use cache
  without refetching.
- **Tracking**: `LastSyncedAt` timestamp and `CatalogSource` (Online/Offline/Cached) exposed
  via `INistControlsService.GetCatalogStatus()`.
- **Config**: `NistCatalog:PreferOnline` (bool, default `true`), `NistCatalog:CachePath`
  (string), `NistCatalog:CacheMaxAgeDays` (int, default 30),
  `NistCatalog:FetchTimeoutSeconds` (int, default 15).
- **Timeout**: 15-second timeout on GitHub fetch to avoid blocking startup.

**Key details**:
- **20 control families** (not 18 as previously assumed): AC, AT, AU, CA, CM, CP, IA, IR,
  MA, MP, PE, PL, PM, PS, PT, RA, SA, SC, SI, SR. (PT and SR added in Rev 5.)
- 322 base controls + 867 enhancements = 1,189 total.
- FedRAMP baselines: Low ~156, Moderate ~325, High ~421 controls.
- DoD IL5 = FedRAMP High + DoD overlays (data residency, logical separation, personnel
  security).
- Source: `https://github.com/usnistgov/oscal-content` →
  `nist.gov/SP800-53/rev5/json/NIST_SP-800-53_rev5_catalog.json`
- FedRAMP baseline profiles: `https://github.com/GSA/fedramp-automation` →
  `baselines/rev5/json/FedRAMP_rev5_HIGH-baseline_profile.json`

**Correction**: Spec SC-003 says "18 control families" — update to 20.

---

## R-006: EF Core 9 Database Strategy

**Decision**: Provider-agnostic `AtoCopilotContext` with conditional DI registration. SQLite
for dev/standalone; SQL Server for production. Single migration set targeting SQLite.

**Rationale**: Both EF Core providers are already referenced in Core.csproj. The context is
defined but needs DI registration and migrations. Single SQLite migration set avoids dual
maintenance; EF Core handles SQL translation.

**Key details**:
- Register via `AddDbContext<AtoCopilotContext>` reading `Database:Provider` from config.
- Apply migrations at startup: `db.Database.MigrateAsync()`.
- Azure Government SQL endpoint: `*.database.usgovcloudapi.net` (not `*.database.windows.net`).
- Use `Authentication=Active Directory Default` for Azure AD auth in SQL connection strings.
- Require `Encrypt=True` for government workloads.
- Add `Microsoft.EntityFrameworkCore.Design` as a dev dependency for migrations tooling.

---

## R-007: NuGet Packages to Add

Based on research, these packages must be added to `Ato.Copilot.Core.csproj`:

| Package | Version | Purpose |
|---------|---------|---------|
| `Azure.ResourceManager.ResourceGraph` | 1.1.0 | Resource-based compliance scans |
| `Azure.ResourceManager.PolicyInsights` | 1.2.0 | Policy-based compliance scans |
| `Azure.ResourceManager.Resources` | 1.9.0 | Policy definitions & assignments |
| `Azure.ResourceManager.SecurityCenter` | 1.2.0 | Defender for Cloud integration |
| `Microsoft.EntityFrameworkCore.Design` | 9.0.0 | EF Core migrations tooling (dev) |

All existing packages (`Azure.Identity`, `Azure.ResourceManager`, `Microsoft.EntityFrameworkCore`,
EF Sqlite/SqlServer, `System.Text.Json`) are already at compatible versions.

---

## R-008: Architecture Gap Decisions

**Date**: 2026-02-21

These decisions resolve architectural gaps identified during checklist review.

### Service Orchestration (Gap: IAtoComplianceEngine responsibility)

`IAtoComplianceEngine` is a pure orchestrator — it delegates scan logic to
`IAzurePolicyComplianceService`, `IDefenderForCloudService`, and an internal resource
scanner. It does NOT contain scan logic itself. It merges, deduplicates, and correlates
findings from all sources by matching on `ControlId`.

### CancellationToken Timeout Scope

The 60-second timeout wraps the entire combined scan at the engine level. Sub-scans share
the same `CancellationToken`. This prevents a slow resource scan from leaving no time for
the policy scan. If the token is cancelled, all pending `await` operations throw
`OperationCanceledException`, which the engine catches and returns as `ASSESSMENT_TIMEOUT`.

### Resource Graph Query Batching

Queries execute sequentially to stay within the 15 req/5s rate limit. Each query targets a
specific resource type (Kusto `where type == ...`). Pagination via `SkipToken` happens
serially within each resource type. No parallelism on Resource Graph calls.

### Memory Management for Bulk Operations

Use `IAsyncEnumerable<T>` for streaming results from Azure API calls. Buffer at most 100
items (findings/controls) in memory before yielding to the caller. For NIST catalog loading,
parse the JSON file using `System.Text.Json.Utf8JsonReader` (streaming) rather than
deserializing the entire 4MB+ file into memory at once.

### Concurrent Configuration Access

`IAgentStateManager` uses `ConcurrentDictionary` which handles individual read/write
atomically. For `set_subscription` + `set_framework` operations that modify the serialized
`ConfigurationSettings` object, use a `SemaphoreSlim(1, 1)` lock to prevent lost updates
when two concurrent requests modify different fields.

### Agent Routing Conflict Resolution

When an intent matches both agents (rare), routing rules use a priority order:
1. Exact keyword match (highest priority).
2. Configuration keywords take precedence over compliance keywords.
3. If still ambiguous, route to `ComplianceAgent` (more common use case) and include a
   hint: "If you meant to change settings, try 'set subscription'."
