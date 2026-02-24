# Research: ARM SDK Compliance Scanners in .NET 9

**Feature**: 008-compliance-engine | **Date**: 2026-02-23 | **Status**: Complete

## Table of Contents

1. [Resource Enumeration Pattern](#1-resource-enumeration-pattern)
2. [Resource Type Checking per Scanner Family](#2-resource-type-checking-per-scanner-family)
3. [Caching Pattern](#3-caching-pattern)
4. [Error Resilience](#4-error-resilience)
5. [Scanner Strategy Pattern](#5-scanner-strategy-pattern)
6. [Decision Summary](#6-decision-summary)

---

## 1. Resource Enumeration Pattern

### Context

The engine needs `IAzureResourceService` to enumerate Azure resources within a subscription, with optional resource group scoping. The existing codebase already uses `ArmClient` (registered as singleton via `CoreServiceExtensions.RegisterArmClient`) with dual-cloud support (AzureGovernment/AzureCloud). Two services already demonstrate the pattern: `AzurePolicyComplianceService` and `DefenderForCloudService`.

### Decision: Subscription-First with Optional Resource Group Filter

Use `ArmClient.GetSubscriptionResource()` as the entry point, then branch to either subscription-wide or resource-group-scoped enumeration. The `IAzureResourceService` wrapper centralizes all ARM queries behind a single interface that scanners consume.

### Pattern

```csharp
public class AzureResourceService : IAzureResourceService
{
    private readonly ArmClient _armClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AzureResourceService> _logger;

    // ─── Subscription-wide enumeration ──────────────────────────────
    public async Task<IReadOnlyList<GenericResource>> GetResourcesAsync(
        string subscriptionId,
        string? resourceGroup = null,
        string? resourceType = null,
        CancellationToken ct = default)
    {
        var sub = _armClient.GetSubscriptionResource(
            new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

        if (!string.IsNullOrEmpty(resourceGroup))
        {
            // Scoped: enumerate within a single resource group
            var rg = sub.GetResourceGroup(resourceGroup);
            var rgResource = (await rg.GetAsync(ct)).Value;
            return await EnumerateResourcesAsync(
                rgResource.GetGenericResources(), resourceType, ct);
        }

        // Full subscription: enumerate across all resource groups
        return await EnumerateResourcesAsync(
            sub.GetGenericResources(), resourceType, ct);
    }

    private static async Task<IReadOnlyList<GenericResource>> EnumerateResourcesAsync(
        GenericResourceCollection collection,
        string? resourceType,
        CancellationToken ct)
    {
        var results = new List<GenericResource>();
        var filter = resourceType != null
            ? $"resourceType eq '{resourceType}'"
            : null;

        await foreach (var resource in collection.GetAllAsync(filter: filter, cancellationToken: ct))
        {
            ct.ThrowIfCancellationRequested();
            results.Add(resource);
            if (results.Count >= 10_000) break; // Safety limit
        }
        return results;
    }
}
```

### Key Design Points

| Point | Decision | Rationale |
|-------|----------|-----------|
| Entry point | `ArmClient.GetSubscriptionResource()` | Already used by `DefenderForCloudService` and `AzurePolicyComplianceService`; consistent with codebase |
| RG scoping | `sub.GetResourceGroup(name)` then enumerate | Avoids fetching all resources then filtering client-side; ARM does the filtering server-side |
| Resource type filter | Server-side OData `$filter` on `resourceType` | Reduces payload size at the API level; `GenericResourceCollection.GetAllAsync(filter:)` supports this |
| Pagination | `await foreach` with `AsyncPageable<T>` | Built into Azure.ResourceManager; handles `nextLink` transparently |
| Safety limit | 10,000 resources per call | Prevents unbounded enumeration; matches existing pattern (5,000 in Policy, 2,000 in Defender) |

### Alternatives Considered

| Alternative | Why Rejected |
|-------------|-------------|
| Azure Resource Graph (`Resources.CreateResourceQuery()`) | Already referenced as ResourceGraph v1.1.0 in codebase but adds KQL complexity; direct ARM enumeration is simpler and type-safe for our scanner needs. Resource Graph is better for cross-subscription aggregation queries — reserve for environment-level assessments. |
| REST API calls via `HttpClient` | Loses type safety, pagination handling, and retry built into SDK. Already decided against in codebase (ArmClient used everywhere). |
| `GetAllAsync()` at subscription root | `SubscriptionResource.GetGenericResources()` already returns `GenericResourceCollection` which handles this. No separate root call needed. |

### Specialized Resource Accessors

For typed resource operations (role assignments, NSGs, etc.), use the ARM SDK's resource-specific collections rather than generic enumeration:

```csharp
// Role assignments — typed accessor
public async Task<IReadOnlyList<RoleAssignmentResource>> GetRoleAssignmentsAsync(
    string subscriptionId, CancellationToken ct = default)
{
    var sub = _armClient.GetSubscriptionResource(
        new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
    var assignments = new List<RoleAssignmentResource>();
    await foreach (var ra in sub.GetRoleAssignments().GetAllAsync(cancellationToken: ct))
    {
        ct.ThrowIfCancellationRequested();
        assignments.Add(ra);
    }
    return assignments;
}

// NSG rules — via resource group
public async Task<IReadOnlyList<NetworkSecurityGroupResource>> GetNsgsAsync(
    string subscriptionId, string? resourceGroup, CancellationToken ct = default)
{
    // Use generic resources with filter, then get typed resource
    var resources = await GetResourcesAsync(
        subscriptionId, resourceGroup,
        "Microsoft.Network/networkSecurityGroups", ct);

    return resources.Select(r => _armClient.GetNetworkSecurityGroupResource(r.Id))
                    .ToList();
}
```

**Important**: For typed resource types not in the base `Azure.ResourceManager` package, additional NuGet packages are needed (see Section 2 for per-family details).

---

## 2. Resource Type Checking per Scanner Family

### Context

Each of 11 scanners inspects specific Azure resource types. The existing codebase already has `Azure.ResourceManager` (1.13.2), `Azure.ResourceManager.Resources` (1.9.0), `Azure.ResourceManager.PolicyInsights` (1.2.0), `Azure.ResourceManager.SecurityCenter` (1.2.0-beta.6), `Azure.ResourceManager.ResourceGraph` (1.1.0), and `Microsoft.Graph` (5.70.0 in Agents project).

### Per-Family Resource Checks

#### AC — Access Control Scanner

**What to check**: RBAC role assignments, role definitions, custom roles, PIM eligibility, conditional access policies.

**ARM SDK approach**:
```csharp
// RBAC role assignments — available in base Azure.ResourceManager
var sub = _armClient.GetSubscriptionResource(subId);
await foreach (var assignment in sub.GetRoleAssignments().GetAllAsync(ct: ct))
{
    // Check for overly-permissive assignments
    // Check Owner/Contributor assignments at subscription scope
    // Check for assignments to individual users (should be groups)
    var roleDefId = assignment.Data.RoleDefinitionId;
    var principalType = assignment.Data.PrincipalType; // User, Group, ServicePrincipal
    var scope = assignment.Data.Scope;
}
```

| Check | SDK/API | NuGet Package | Notes |
|-------|---------|---------------|-------|
| Role assignments | `SubscriptionResource.GetRoleAssignments()` | `Azure.ResourceManager` (exists) | Available directly; `RoleAssignmentData.PrincipalType`, `RoleDefinitionId`, `Scope` |
| Role definitions | `SubscriptionResource.GetAuthorizationRoleDefinitions()` | `Azure.ResourceManager` (exists) | Check for custom roles, over-permissive scopes |
| PIM policies | Microsoft Graph API | `Microsoft.Graph` (exists in Agents) | `GraphServiceClient.RoleManagement.Directory.RoleAssignmentScheduleInstances` |
| Conditional Access | Microsoft Graph API | `Microsoft.Graph` (exists in Agents) | `GraphServiceClient.Identity.ConditionalAccess.Policies` |

**Decision**: Role assignments and definitions via ARM SDK directly. PIM and Conditional Access via `Microsoft.Graph` (already referenced). No new NuGet packages needed.

#### AU — Audit Scanner

**What to check**: Diagnostic settings on resources, activity log configuration, log retention policies, Log Analytics workspace configuration.

**ARM SDK approach**:
```csharp
// Diagnostic settings — require Azure.ResourceManager.Monitor
// Alternative for now: use generic ARM REST via ArmClient.GetGenericResource()
var resourceId = new ResourceIdentifier($"/subscriptions/{subscriptionId}");
var diagnosticSettings = _armClient.GetDiagnosticSettings(resourceId);

await foreach (var ds in diagnosticSettings.GetAllAsync(ct))
{
    var hasLogAnalytics = ds.Data.WorkspaceId != null;
    var hasStorageAccount = ds.Data.StorageAccountId != null;
    var retentionDays = ds.Data.Logs?
        .Where(l => l.IsEnabled == true)
        .Select(l => l.RetentionPolicy?.Days ?? 0)
        .DefaultIfEmpty(0)
        .Min();
}
```

| Check | SDK/API | NuGet Package | Notes |
|-------|---------|---------------|-------|
| Diagnostic settings | `ArmClient.GetDiagnosticSettings(scope)` | `Azure.ResourceManager.Monitor` (**NEW**) | Extension method on `ArmClient`; scoped to any resource/sub |
| Activity log profiles | `SubscriptionResource.GetLogProfiles()` | `Azure.ResourceManager.Monitor` (**NEW**) | Deprecated API, but still relevant for legacy configs |
| Log Analytics workspaces | `SubscriptionResource.GetOperationalInsightsWorkspaces()` | `Azure.ResourceManager.OperationalInsights` (**NEW**) | Check workspace retention, data caps |

**Decision**: Add `Azure.ResourceManager.Monitor` to Core.csproj. For Log Analytics, either add `Azure.ResourceManager.OperationalInsights` or use generic resource queries. Recommend adding both.

**Fallback**: If typed packages are deferred, use generic ARM resource queries via `ArmClient.GetGenericResource(ResourceIdentifier)` and inspect `Data.Properties` as `BinaryData`. This is less type-safe but avoids package additions.

#### SC — Security Communications Scanner

**What to check**: NSG rules (inbound from Any/Internet), TLS 1.2+ enforcement on App Services/SQL, encryption at rest (Key Vault, Storage, Disk Encryption), VNet service endpoints.

```csharp
// NSGs — require Azure.ResourceManager.Network
var sub = _armClient.GetSubscriptionResource(subId);
await foreach (var nsg in sub.GetNetworkSecurityGroups().GetAllAsync(ct: ct))
{
    foreach (var rule in nsg.Data.SecurityRules)
    {
        // Flag: SourceAddressPrefix == "*" or "Internet" with Allow
        if (rule.Access == SecurityRuleAccess.Allow &&
            (rule.SourceAddressPrefix == "*" || rule.SourceAddressPrefix == "Internet"))
        {
            // Finding: overly permissive inbound rule
        }
    }
}

// Storage encryption — require Azure.ResourceManager.Storage
await foreach (var account in sub.GetStorageAccounts().GetAllAsync(ct: ct))
{
    var encryption = account.Data.Encryption;
    var enforceHttps = account.Data.EnableHttpsTrafficOnly;
    var minTls = account.Data.MinimumTlsVersion; // TLS1_2
}
```

| Check | SDK/API | NuGet Package | Notes |
|-------|---------|---------------|-------|
| NSG rules | `SubscriptionResource.GetNetworkSecurityGroups()` | `Azure.ResourceManager.Network` (**NEW**) | Check `SecurityRules` for overly permissive inbound |
| TLS settings (App Service) | `SubscriptionResource.GetWebSites()` | `Azure.ResourceManager.AppService` (**NEW**) | `SiteData.SiteConfig.MinTlsVersion` |
| TLS settings (SQL) | `SubscriptionResource.GetSqlServers()` | `Azure.ResourceManager.Sql` (**NEW**) | `SqlServerData.MinimalTlsVersion` |
| Encryption at rest (Storage) | `SubscriptionResource.GetStorageAccounts()` | `Azure.ResourceManager.Storage` (**NEW**) | `StorageAccountData.Encryption`, `EnableHttpsTrafficOnly` |
| Disk encryption | `SubscriptionResource.GetManagedDisks()` | `Azure.ResourceManager.Compute` (**NEW**) | `ManagedDiskData.Encryption` |
| Key Vault | `SubscriptionResource.GetKeyVaults()` | `Azure.ResourceManager.KeyVault` (**NEW**) | Check access policies, soft delete, purge protection |

**Decision**: SC has the most NuGet dependencies. **Recommendation**: Add `Azure.ResourceManager.Network` and `Azure.ResourceManager.Storage` for the most critical checks. Defer AppService, SQL, Compute, KeyVault to generic ARM queries initially, adding typed packages in a follow-up.

**Alternative (recommended for v1)**: Use generic resource enumeration + `BinaryData` properties inspection for all resource types. This avoids 6 new NuGet packages and keeps the scanner working against any resource type:

```csharp
var resources = await _resourceService.GetResourcesAsync(subscriptionId, resourceGroup,
    "Microsoft.Network/networkSecurityGroups", ct);

foreach (var nsg in resources)
{
    // Get full resource with properties
    var fullResource = await _armClient.GetGenericResource(nsg.Id).GetAsync(ct);
    var properties = fullResource.Value.Data.Properties;
    // properties is BinaryData — parse with JsonDocument
    using var doc = JsonDocument.Parse(properties.ToString());
    var securityRules = doc.RootElement.GetProperty("securityRules");
    // ... inspect rules
}
```

#### SI — System Integrity Scanner

**What to check**: VM patching status, antimalware extensions, Windows Update configuration, vulnerability assessment extensions.

| Check | SDK/API | NuGet Package | Notes |
|-------|---------|---------------|-------|
| VM extensions (antimalware) | `VirtualMachineResource.GetVirtualMachineExtensions()` | `Azure.ResourceManager.Compute` (**NEW**) | Check for `IaaSAntimalware` or `MDE.Linux/Windows` extension |
| Update management | Azure Update Manager via REST | Generic ARM or Defender | Check update compliance state |
| Guest configuration | Policy compliance states | `Azure.ResourceManager.PolicyInsights` (exists) | Guest config policies map to SI controls |

**Decision**: Use generic ARM queries for VM extensions. The existing Policy scan already covers SI controls via guest configuration policies. Defer `Azure.ResourceManager.Compute` unless full VM enumeration is needed.

#### CP — Contingency Planning Scanner

**What to check**: Recovery Services vaults, backup policies, geo-replication (Storage, SQL), availability sets/zones.

| Check | SDK/API | NuGet Package | Notes |
|-------|---------|---------------|-------|
| Recovery Services vaults | Generic ARM query | `Azure.ResourceManager.RecoveryServices` (**NEW, optional**) | `Microsoft.RecoveryServices/vaults` resource type |
| Backup policies | Vault sub-resources | Same or generic | Check backup frequency, retention |
| Storage geo-replication | Storage account replication type | `Azure.ResourceManager.Storage` (same as SC) | `StorageAccountData.SkuName` contains GRS/RAGRS |
| SQL geo-replication | SQL databases | `Azure.ResourceManager.Sql` (same as SC) | Check failover groups, read replicas |
| Availability | VM availability sets/zones | `Azure.ResourceManager.Compute` (same as SI) | Check `AvailabilitySetId` or `Zones` |

**Decision**: Use generic ARM queries for CP. Most CP checks can be derived from resource properties without typed packages.

#### IA — Identification/Authentication Scanner

**What to check**: MFA enforcement, password policies, service principal credential expiry, managed identity usage.

| Check | SDK/API | NuGet Package | Notes |
|-------|---------|---------------|-------|
| MFA status | Microsoft Graph | `Microsoft.Graph` (exists) | `GraphServiceClient.Users` → authentication methods |
| Password policies | Microsoft Graph | `Microsoft.Graph` (exists) | Directory-level policies |
| Service principal credentials | Microsoft Graph | `Microsoft.Graph` (exists) | Check credential expiry dates |
| Managed identities | ARM SDK | `Azure.ResourceManager` (exists) | `GenericResourceData.Identity` — check `SystemAssigned` vs `UserAssigned` |
| Conditional Access (MFA enforcement) | Microsoft Graph | `Microsoft.Graph` (exists) | Policies requiring MFA; shared with AC scanner |

**Decision**: IA is primarily a Microsoft Graph consumer. `Microsoft.Graph 5.70.0` is already in Agents.csproj. ARM SDK provides managed identity checks via `GenericResourceData.Identity`. No new NuGet packages needed.

**Important**: Microsoft Graph requires separate authentication (`GraphServiceClient` with `TokenCredential`). The existing `DefaultAzureCredential` can be reused but needs Graph-specific scopes (`.default` on `https://graph.microsoft.com`). This should be registered in DI similarly to `ArmClient`.

#### CM — Configuration Management Scanner

**What to check**: Resource locks, required tags, naming conventions, resource deployment modes.

```csharp
// Resource locks — available in base Azure.ResourceManager
var sub = _armClient.GetSubscriptionResource(subId);

// Subscription-level locks
await foreach (var lockResource in sub.GetManagementLocks().GetAllAsync(ct: ct))
{
    // Check CanNotDelete or ReadOnly lock exists on critical resources
    var lockLevel = lockResource.Data.Level; // CanNotDelete, ReadOnly
}

// Tags — on every resource
var resources = await _resourceService.GetResourcesAsync(subscriptionId, resourceGroup, ct: ct);
foreach (var resource in resources)
{
    var tags = resource.Data.Tags;
    // Check required tags: Environment, Owner, CostCenter, etc.
    var missingRequired = RequiredTags.Except(tags.Keys, StringComparer.OrdinalIgnoreCase);
}
```

| Check | SDK/API | NuGet Package | Notes |
|-------|---------|---------------|-------|
| Resource locks | `SubscriptionResource.GetManagementLocks()` | `Azure.ResourceManager` (exists) | Also per-RG and per-resource scope |
| Tags | `GenericResourceData.Tags` | `Azure.ResourceManager` (exists) | Available on every resource |
| Resource policies | Policy compliance | `Azure.ResourceManager.PolicyInsights` (exists) | CM policies already scanned |

**Decision**: CM requires no new NuGet packages. All checks use base ARM SDK capabilities.

#### IR — Incident Response Scanner

**What to check**: Action groups, alert rules, Azure Monitor configuration, incident response playbooks (Logic Apps), Service Health alerts.

| Check | SDK/API | NuGet Package | Notes |
|-------|---------|---------------|-------|
| Action groups | `ArmClient.GetActionGroups(scope)` | `Azure.ResourceManager.Monitor` (same as AU) | Check SMS, email, webhook targets |
| Alert rules | `ArmClient.GetMetricAlerts(scope)` | `Azure.ResourceManager.Monitor` (same as AU) | Check critical resource alert coverage |
| Activity log alerts | `ArmClient.GetActivityLogAlerts(scope)` | `Azure.ResourceManager.Monitor` (same as AU) | Check for SecurityIncident, Administrative |
| Logic Apps (playbooks) | Generic ARM query | None | `Microsoft.Logic/workflows` resource type |

**Decision**: Shares `Azure.ResourceManager.Monitor` with AU scanner. No additional packages needed.

#### RA — Risk Assessment Scanner

**What to check**: Defender for Cloud assessments, vulnerability assessment results, threat intelligence.

| Check | SDK/API | NuGet Package | Notes |
|-------|---------|---------------|-------|
| Security assessments | `IDefenderForCloudService` | `Azure.ResourceManager.SecurityCenter` (exists) | Already implemented |
| Vulnerability assessments | Defender sub-assessments | Same | `_armClient.GetSecurityAssessmentsAsync()` |
| Secure score | `IDefenderForCloudService.GetSecureScoreAsync()` | Same | Already implemented |

**Decision**: RA is fully served by the existing `IDefenderForCloudService`. The scanner delegates to it. No new packages needed.

#### CA — Certification/Accreditation Scanner

**What to check**: Defender recommendations, regulatory compliance assessments, compliance standards.

| Check | SDK/API | NuGet Package | Notes |
|-------|---------|---------------|-------|
| Regulatory compliance | `DefenderForCloudService.GetRegulatoryComplianceAsync()` | `Azure.ResourceManager.SecurityCenter` (exists) | Already implemented |
| Recommendations | `IDefenderForCloudService.GetRecommendationsAsync()` | Same | Already implemented |

**Decision**: CA is fully served by the existing `IDefenderForCloudService`. No new packages needed.

### NuGet Package Decision Summary

| Package | Version | Needed By | Priority | Decision |
|---------|---------|-----------|----------|----------|
| `Azure.ResourceManager` | 1.13.2 | ALL | — | **EXISTS** |
| `Azure.ResourceManager.Resources` | 1.9.0 | ALL | — | **EXISTS** |
| `Azure.ResourceManager.PolicyInsights` | 1.2.0 | AU, CM, SI | — | **EXISTS** |
| `Azure.ResourceManager.SecurityCenter` | 1.2.0-beta.6 | RA, CA | — | **EXISTS** |
| `Azure.ResourceManager.ResourceGraph` | 1.1.0 | Environment scans | — | **EXISTS** |
| `Microsoft.Graph` | 5.70.0 | AC, IA | — | **EXISTS** (Agents) |
| `Azure.ResourceManager.Monitor` | ~1.1.0 | AU, IR | P1 | **ADD** — high-value, covers 2 scanners |
| `Azure.ResourceManager.Network` | ~1.9.0 | SC | P2 | **DEFER** — use generic ARM for v1 |
| `Azure.ResourceManager.Storage` | ~1.3.0 | SC, CP | P2 | **DEFER** — use generic ARM for v1 |
| `Azure.ResourceManager.Compute` | ~1.6.0 | SI, CP | P3 | **DEFER** — existing Policy scan covers SI |
| `Azure.ResourceManager.KeyVault` | ~1.3.0 | SC | P3 | **DEFER** — use generic ARM for v1 |
| Others (AppService, Sql, etc.) | — | SC | P3 | **DEFER** — generic ARM queries |

**Rationale for Generic ARM v1 Approach**: Adding 6+ NuGet packages increases build complexity, version conflicts risk, and binary size. The generic `GetGenericResource()` + `BinaryData` properties approach works for all resource types and is the pattern used when the specific ARM management package isn't available. Typed packages can be added incrementally as scanner fidelity requirements increase.

---

## 3. Caching Pattern

### Context

The engine pre-warms resource caches per subscription with 5-minute TTL. The codebase uses `IMemoryCache` (9.0.0) already for NIST catalog caching (24h TTL) in `NistControlsService`. `services.AddMemoryCache()` is called in `ServiceCollectionExtensions.AddComplianceAgent()`.

### Decision: Per-Subscription, Per-Resource-Type Cache Keys

Cache at the **subscription + resource type** granularity. This avoids re-fetching the same resource type within a single assessment while allowing different resource types to be cached/evicted independently.

### Pattern

```csharp
public class AzureResourceService : IAzureResourceService
{
    private readonly ArmClient _armClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AzureResourceService> _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Pre-warm resource cache for a subscription. Called before scanning begins.
    /// </summary>
    public async Task PreWarmCacheAsync(
        string subscriptionId,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Pre-warming resource cache for {Sub}", subscriptionId);

        // Warm generic resources (all types)
        var key = CacheKey(subscriptionId, resourceType: null);
        if (!_cache.TryGetValue(key, out _))
        {
            var resources = await FetchResourcesAsync(subscriptionId, null, null, ct);
            _cache.Set(key, resources, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl,
                Priority = CacheItemPriority.High,
                Size = resources.Count // For bounded cache
            });
            _logger.LogDebug("Cached {Count} resources for {Sub}", resources.Count, subscriptionId);
        }
    }

    public async Task<IReadOnlyList<GenericResource>> GetResourcesAsync(
        string subscriptionId,
        string? resourceGroup = null,
        string? resourceType = null,
        CancellationToken ct = default)
    {
        var key = CacheKey(subscriptionId, resourceType);

        return await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            entry.Priority = CacheItemPriority.High;
            return await FetchResourcesAsync(subscriptionId, resourceGroup, resourceType, ct);
        }) ?? [];
    }

    private static string CacheKey(string subscriptionId, string? resourceType) =>
        $"arm:resources:{subscriptionId}:{resourceType ?? "all"}";
}
```

### Cache Strategy Comparison

| Strategy | Pros | Cons | Decision |
|----------|------|------|----------|
| Per-subscription (all resources in one entry) | Simple; one cache hit for all queries | Large memory footprint; can't evict by type; pre-warm is slow | **Rejected** |
| Per-subscription + per-type | Type-specific cache hits; independent eviction; smaller entries | More cache keys; slightly more API calls if pre-warm doesn't cover all types | **Selected** |
| Per-resource (individual resource cache) | Maximum reuse | Enormous key count; cache overhead exceeds value | **Rejected** |
| Per-resource-group | Good for RG-scoped scans | Breaks down for subscription-wide scans; many keys | **Rejected** |

### Pre-Warming Strategy

Pre-warm during assessment initialization, before scanners run:

```csharp
// In AtoComplianceEngine.RunComprehensiveAssessmentAsync:
// Step 1: Pre-warm resource caches
foreach (var subscriptionId in subscriptionIds)
{
    await _resourceService.PreWarmCacheAsync(subscriptionId, ct);
}

// Step 2: Run scanners (each scanner's GetResourcesAsync hits cache)
foreach (var family in ControlFamilies.AllFamilies)
{
    var scanner = GetScannerForFamily(family);
    await scanner.ScanAsync(subscriptionId, resourceGroup, ct);
    // Scanner internally calls _resourceService.GetResourcesAsync()
    // which returns cached data
}
```

### TTL Rationale

| TTL | Use Case | Notes |
|-----|----------|-------|
| 5 minutes | Azure resource cache | Matches spec requirement; ARM resources rarely change within an assessment window |
| 24 hours | NIST catalog cache | Already established in `NistControlsService`; catalog is static |

### Memory Bounds

Set cache size limits to prevent OOM under bulk operations:

```csharp
services.AddMemoryCache(options =>
{
    options.SizeLimit = 50_000; // Total cache entries
    // Each entry Size = resource count in that entry
    // 50,000 accommodates ~10 subscriptions × ~5,000 resources each
});
```

**Note**: `SizeLimit` requires every `Set()` call to specify `Size`. The existing `NistControlsService` does NOT set `Size` on its cache entries. If `SizeLimit` is added globally, `NistControlsService` needs to be updated. **Recommendation**: Do NOT set `SizeLimit` globally. Instead, use separate cache key prefixes and per-service entry count limits enforced in code.

---

## 4. Error Resilience

### Context

Azure ARM APIs have rate limits (per-subscription, per-region), return 429s under load, and can experience transient failures. The existing `DefenderForCloudService` and `AzurePolicyComplianceService` catch `Exception` and return error JSON rather than throwing. `NistControlsService` uses Polly retry via `Microsoft.Extensions.Http.Resilience`.

### Decision: Three-Layer Resilience

#### Layer 1: ARM Client Retry (Built-in)

`ArmClient` already has built-in retry policies via `Azure.Core.RetryOptions`. Configure in `ArmClientOptions`:

```csharp
return new ArmClient(credential, default, new ArmClientOptions
{
    Environment = armEnvironment,
    Retry =
    {
        MaxRetries = 3,
        Delay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(30),
        Mode = RetryMode.Exponential,
        // NetworkTimeout defaults to 100s
    }
});
```

**Status**: The existing `RegisterArmClient` does NOT configure retry options explicitly, so it uses Azure SDK defaults (3 retries, 800ms initial delay, exponential backoff). This is **acceptable for v1** — the defaults handle 429s with automatic `Retry-After` header parsing.

#### Layer 2: Per-Scanner Error Isolation

Each scanner catches exceptions and returns partial results. The engine continues with remaining families:

```csharp
// In AtoComplianceEngine
foreach (var family in families)
{
    try
    {
        var result = await scanner.ScanAsync(subscriptionId, resourceGroup, ct);
        familyResults.Add(result);
    }
    catch (OperationCanceledException) { throw; } // Propagate cancellation
    catch (RequestFailedException ex) when (ex.Status == 429)
    {
        _logger.LogWarning("Throttled scanning {Family}, retrying after delay", family);
        await Task.Delay(TimeSpan.FromSeconds(ex.GetRetryAfterDelay()?.TotalSeconds ?? 30), ct);
        // Retry once
        try
        {
            var result = await scanner.ScanAsync(subscriptionId, resourceGroup, ct);
            familyResults.Add(result);
        }
        catch (Exception retryEx)
        {
            _logger.LogError(retryEx, "Family {Family} failed after retry", family);
            familyResults.Add(ControlFamilyAssessment.Failed(family, retryEx.Message));
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Scanner failed for family {Family}", family);
        familyResults.Add(ControlFamilyAssessment.Failed(family, ex.Message));
    }
}
```

#### Layer 3: Graceful Degradation

| Failure Scenario | Behavior | Matches Spec |
|------------------|----------|--------------|
| Single scanner throws | Family marked as failed with warning; assessment continues | Yes (edge case) |
| All 3 scan pillars fail | Assessment completes with 0 findings, 100% score | Yes (edge case) |
| DB unavailable at persist time | Assessment returned to caller; persist failure logged | Yes (FR-019) |
| Blob storage unavailable | Evidence returned; storage failure logged | Yes (edge case) |
| `CancellationToken` cancelled | `OperationCanceledException` propagated; assessment marked cancelled | Yes (FR-015) |

### Throttling-Specific Patterns

```csharp
// Azure.Core's RequestFailedException has status code
catch (RequestFailedException ex) when (ex.Status == 429)
{
    // Option A: Use Retry-After header (parsed by Azure.Core)
    var retryAfter = ex.GetRawResponse()?.Headers
        .TryGetValue("Retry-After", out var val) == true
        ? TimeSpan.FromSeconds(int.Parse(val))
        : TimeSpan.FromSeconds(30);

    await Task.Delay(retryAfter, ct);
}
```

### Pagination Safety

All existing services apply safety limits (5,000 for Policy, 2,000 for Defender). Apply the same pattern to ARM resource enumeration:

```csharp
await foreach (var resource in collection.GetAllAsync(cancellationToken: ct))
{
    ct.ThrowIfCancellationRequested();
    results.Add(resource);
    if (results.Count >= MaxResourcesPerQuery) // 10,000
    {
        _logger.LogWarning("Resource enumeration hit safety limit of {Limit}", MaxResourcesPerQuery);
        break;
    }
}
```

### Alternatives Considered

| Alternative | Why Rejected |
|-------------|-------------|
| Polly wrapping all ARM calls | Azure.Core already has built-in retry; double-layer retry causes excessive delays |
| Circuit breaker per scanner | Over-engineering for v1; scanner error isolation achieves same goal more simply |
| Global retry middleware via `DelegatingHandler` | ArmClient doesn't use `HttpClient` from DI; it manages its own pipeline |

---

## 5. Scanner Strategy Pattern

### Context

The engine dispatches each of 20 control families to one of 11 specialized scanners + 1 default. The plan names these: AC, AU, SC, SI, CP, IA, CM, IR, RA, CA (10 specialized) + Default for AT, MA, MP, PE, PL, PM, PS, PT, SA, SR.

### Decision: Dictionary-Based Registry with DI

Use `IReadOnlyDictionary<string, IComplianceScanner>` populated during DI registration. This is explicit, debuggable, and consistent with how the existing codebase registers services.

### Pattern

```csharp
// ─── Interface ──────────────────────────────────────────────────
public interface IComplianceScanner
{
    /// <summary>The control family this scanner handles (e.g., "AC").</summary>
    string FamilyCode { get; }

    /// <summary>Scan a control family within a subscription.</summary>
    Task<ControlFamilyAssessment> ScanAsync(
        string subscriptionId,
        string? resourceGroup,
        IEnumerable<NistControl> controls,
        CancellationToken ct = default);
}

// ─── Abstract Base ──────────────────────────────────────────────
public abstract class BaseComplianceScanner : IComplianceScanner
{
    protected readonly IAzureResourceService ResourceService;
    protected readonly ILogger Logger;

    public abstract string FamilyCode { get; }

    protected BaseComplianceScanner(
        IAzureResourceService resourceService,
        ILogger logger)
    {
        ResourceService = resourceService;
        Logger = logger;
    }

    public async Task<ControlFamilyAssessment> ScanAsync(
        string subscriptionId,
        string? resourceGroup,
        IEnumerable<NistControl> controls,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var findings = new List<AtoFinding>();
        var controlList = controls.ToList();
        int passed = 0, failed = 0;

        try
        {
            findings = await ScanFamilyAsync(subscriptionId, resourceGroup, controlList, ct);
            var failedControlIds = findings.Select(f => f.ControlId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            passed = controlList.Count(c => !failedControlIds.Contains(c.Id));
            failed = controlList.Count - passed;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Scanner {Family} failed", FamilyCode);
            return ControlFamilyAssessment.Failed(FamilyCode, ex.Message);
        }

        sw.Stop();
        return new ControlFamilyAssessment
        {
            FamilyCode = FamilyCode,
            TotalControls = controlList.Count,
            PassedControls = passed,
            FailedControls = failed,
            ComplianceScore = controlList.Count > 0
                ? Math.Round((double)passed / controlList.Count * 100, 1) : 100.0,
            Findings = findings,
            AssessmentDuration = sw.Elapsed
        };
    }

    /// <summary>Override in derived scanners to perform family-specific checks.</summary>
    protected abstract Task<List<AtoFinding>> ScanFamilyAsync(
        string subscriptionId,
        string? resourceGroup,
        IReadOnlyList<NistControl> controls,
        CancellationToken ct);
}

// ─── Concrete Scanner Example ───────────────────────────────────
public class AccessControlScanner : BaseComplianceScanner
{
    public override string FamilyCode => ControlFamilies.AccessControl;

    public AccessControlScanner(
        IAzureResourceService resourceService,
        ILogger<AccessControlScanner> logger)
        : base(resourceService, logger) { }

    protected override async Task<List<AtoFinding>> ScanFamilyAsync(
        string subscriptionId,
        string? resourceGroup,
        IReadOnlyList<NistControl> controls,
        CancellationToken ct)
    {
        var findings = new List<AtoFinding>();
        var roleAssignments = await ResourceService.GetRoleAssignmentsAsync(subscriptionId, ct);

        // AC-2: Account Management — check overly permissive role assignments
        // AC-6: Least Privilege — check Owner/Contributor at subscription scope
        // AC-17: Remote Access — check conditional access policies
        // ... scanner-specific logic

        return findings;
    }
}
```

### DI Registration Pattern

```csharp
// In ServiceCollectionExtensions.cs
public static IServiceCollection AddComplianceScanners(this IServiceCollection services)
{
    // Register individual scanners
    services.AddSingleton<IComplianceScanner, AccessControlScanner>();
    services.AddSingleton<IComplianceScanner, AuditScanner>();
    services.AddSingleton<IComplianceScanner, SecurityCommunicationsScanner>();
    services.AddSingleton<IComplianceScanner, SystemIntegrityScanner>();
    services.AddSingleton<IComplianceScanner, ContingencyPlanningScanner>();
    services.AddSingleton<IComplianceScanner, IdentificationAuthScanner>();
    services.AddSingleton<IComplianceScanner, ConfigManagementScanner>();
    services.AddSingleton<IComplianceScanner, IncidentResponseScanner>();
    services.AddSingleton<IComplianceScanner, RiskAssessmentScanner>();
    services.AddSingleton<IComplianceScanner, CertAccreditationScanner>();
    services.AddSingleton<IComplianceScanner, DefaultComplianceScanner>();

    // Register scanner registry (dictionary lookup)
    services.AddSingleton<IScannerRegistry>(sp =>
    {
        var scanners = sp.GetServices<IComplianceScanner>();
        return new ScannerRegistry(scanners);
    });

    return services;
}

// ─── Registry ───────────────────────────────────────────────────
public interface IScannerRegistry
{
    IComplianceScanner GetScanner(string familyCode);
}

public class ScannerRegistry : IScannerRegistry
{
    private readonly IReadOnlyDictionary<string, IComplianceScanner> _scanners;
    private readonly IComplianceScanner _default;

    public ScannerRegistry(IEnumerable<IComplianceScanner> scanners)
    {
        var list = scanners.ToList();
        _default = list.Single(s => s is DefaultComplianceScanner);
        _scanners = list
            .Where(s => s is not DefaultComplianceScanner)
            .ToDictionary(s => s.FamilyCode, s => s, StringComparer.OrdinalIgnoreCase);
    }

    public IComplianceScanner GetScanner(string familyCode) =>
        _scanners.TryGetValue(familyCode, out var scanner) ? scanner : _default;
}
```

### Engine Dispatch

```csharp
// In AtoComplianceEngine
private async Task<ControlFamilyAssessment> AssessControlFamilyAsync(
    string familyCode,
    string subscriptionId,
    string? resourceGroup,
    CancellationToken ct)
{
    var scanner = _scannerRegistry.GetScanner(familyCode);
    var controls = await _nistService.GetControlFamilyAsync(familyCode, false, ct);

    _logger.LogInformation("Scanning {Family} with {Scanner} ({ControlCount} controls)",
        familyCode, scanner.GetType().Name, controls.Count);

    return await scanner.ScanAsync(subscriptionId, resourceGroup, controls, ct);
}
```

### Alternatives Considered

| Pattern | Pros | Cons | Decision |
|---------|------|------|----------|
| `Dictionary<string, IComplianceScanner>` via DI | Explicit, debuggable, O(1) lookup, easy to test | Manual registration per scanner | **Selected** |
| Assembly scanning (`Assembly.GetTypes().Where(t => typeof(IComplianceScanner)...)`) | Auto-discovers new scanners | Fragile, hard to debug, hidden failures, reflection overhead | **Rejected** |
| Switch expression in engine | Simple, no extra abstractions | Violates OCP, engine grows with every scanner, poor testability | **Rejected** |
| `IServiceProvider.GetKeyedService<T>(family)` (.NET 8+) | Uses built-in keyed DI | Ties to DI container; harder to get all scanners for enumeration; `GetScanner` needs fallback logic inline | **Rejected** — considered but registry is more explicit |
| Attribute-based `[ScannerFamily("AC")]` | Self-documenting | Requires assembly scanning to discover; same issues as option 2 | **Rejected** |

### Why Dictionary-Based Registry Wins

1. **Explicit**: Every scanner is visible in `ServiceCollectionExtensions.cs` — no hidden registrations
2. **Testable**: `ScannerRegistry` can be unit tested with mock scanners
3. **Fallback**: `DefaultComplianceScanner` is a first-class citizen, not a special case
4. **Consistent**: Matches existing codebase style (explicit singleton registrations in `AddComplianceAgent`)
5. **Debuggable**: Set a breakpoint on `GetScanner()` to see which scanner handles which family
6. **OCP-compliant**: Adding a new scanner = add one `AddSingleton<>` line + the scanner class

---

## 6. Decision Summary

| # | Topic | Decision | Key Rationale |
|---|-------|----------|---------------|
| D1 | Resource enumeration | `ArmClient.GetSubscriptionResource()` → `GetGenericResources()` with optional RG scoping | Consistent with existing `DefenderForCloudService` and `AzurePolicyComplianceService` patterns |
| D2 | Typed vs generic ARM | Primarily generic ARM queries (`GetGenericResource()` + `BinaryData`) for v1, with typed resources for RBAC (`RoleAssignmentResource`), diagnostics (`DiagnosticSettingsResource`), and locks (`ManagementLockResource`) where type safety is critical; add remaining typed packages incrementally | Avoids 6+ new NuGet packages; works for all resource types; typed resources used selectively where ARM SDK provides direct API methods |
| D3 | New NuGet packages | Add only `Azure.ResourceManager.Monitor` for v1 (AU + IR scanners) | Highest value-to-cost ratio; covers 2 scanners; other families work via generic ARM or existing packages |
| D4 | Cache granularity | Per-subscription + per-resource-type keys, 5-minute TTL, `IMemoryCache` | Balances reuse with memory; independent eviction per type; matches spec requirement |
| D5 | Cache pre-warming | `PreWarmCacheAsync` before scanner dispatch; warm all resources for each subscription | Reduces API calls during scanning; 5-min TTL covers full assessment window |
| D6 | Error resilience | Three-layer: ARM SDK built-in retry → per-scanner try/catch → graceful degradation | Azure.Core built-in handles 429s; scanner isolation prevents cascade; matches spec edge cases |
| D7 | Retry configuration | Use Azure SDK defaults (3 retries, exponential backoff) for v1 | Defaults already handle `Retry-After` headers; avoid double-retry with Polly |
| D8 | Scanner dispatch | Dictionary-based `IScannerRegistry` with `DefaultComplianceScanner` fallback | Explicit, testable, debuggable; consistent with existing DI pattern |
| D9 | Scanner base class | `BaseComplianceScanner` abstract class with template method | Shared timing, scoring, error handling; scanners only implement `ScanFamilyAsync` |
| D10 | Graph dependency (IA, AC) | Inject `GraphServiceClient` into IA and AC scanners; reuse existing `DefaultAzureCredential` | `Microsoft.Graph 5.70.0` already in Agents.csproj; shared auth via `DefaultAzureCredential` |
| D11 | Scanner registration | Explicit `AddSingleton<IComplianceScanner, T>()` per scanner in `AddComplianceScanners()` | Matches existing `AddComplianceAgent()` style; no assembly scanning magic |

### Open Questions for Implementation

1. **Graph auth scope**: Does the existing `DefaultAzureCredential` have Graph API permissions in the target Azure Government environment? If not, IA and AC scanners should gracefully degrade to ARM-only checks.
2. **Resource Graph for environment scans**: `RunEnvironmentAssessmentAsync` (multi-subscription) may benefit from Azure Resource Graph for cross-subscription queries vs. iterating subscriptions sequentially. The `Azure.ResourceManager.ResourceGraph` package is already referenced.
3. **Monitor package version**: `Azure.ResourceManager.Monitor` latest stable is ~1.1.0; confirm compatibility with `Azure.ResourceManager 1.13.2` before adding.
4. **VM extension checks (SI)**: The existing Policy scan already captures guest configuration and update compliance. Does the SI scanner need direct VM extension enumeration, or is Policy data sufficient for v1?
