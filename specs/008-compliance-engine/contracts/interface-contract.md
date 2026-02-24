# Interface Contract: IAtoComplianceEngine (Expanded)

**Feature Branch**: `008-compliance-engine` | **Date**: 2026-02-23

## Overview

The existing `IAtoComplianceEngine` interface (4 methods) is **replaced** with an expanded 16-method interface. The old `RunAssessmentAsync` (7 params) is removed; `ComplianceAssessmentTool` is updated to call the new API.

All methods accept `CancellationToken` as the last parameter. All methods use structured return types (no raw `string` returns).

---

## Interface Definition

```csharp
public interface IAtoComplianceEngine
{
    // ─── Core Assessment Methods (US1) ──────────────────────────────

    /// <summary>
    /// Run a comprehensive compliance assessment for a single subscription.
    /// Orchestrates all 3 scan pillars across all 20 NIST families.
    /// </summary>
    Task<ComplianceAssessment> RunComprehensiveAssessmentAsync(
        string subscriptionId,
        string? resourceGroup = null,
        IProgress<AssessmentProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run a multi-subscription environment assessment.
    /// Pre-warms caches for all subscriptions, aggregates results.
    /// </summary>
    Task<ComplianceAssessment> RunEnvironmentAssessmentAsync(
        IEnumerable<string> subscriptionIds,
        string environmentName,
        IProgress<AssessmentProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assess a single control family within a subscription.
    /// Dispatches to the appropriate scanner via IScannerRegistry.
    /// </summary>
    Task<ControlFamilyAssessment> AssessControlFamilyAsync(
        string familyCode,
        string subscriptionId,
        string? resourceGroup = null,
        CancellationToken cancellationToken = default);

    // ─── Evidence Collection (US3) ──────────────────────────────────

    /// <summary>
    /// Collect compliance evidence for a control family or all families.
    /// </summary>
    Task<EvidencePackage> CollectEvidenceAsync(
        string familyCode,
        string subscriptionId,
        string? resourceGroup = null,
        CancellationToken cancellationToken = default);

    // ─── Risk Assessment (US4) ──────────────────────────────────────

    /// <summary>
    /// Calculate risk profile from assessment findings.
    /// Uses severity weights: Critical=10, High=7.5, Medium=5, Low=2.5.
    /// </summary>
    RiskProfile CalculateRiskProfile(ComplianceAssessment assessment);

    /// <summary>
    /// Perform full 8-category risk assessment.
    /// </summary>
    Task<RiskAssessment> PerformRiskAssessmentAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    // ─── Certificate Generation (US5) ───────────────────────────────

    /// <summary>
    /// Generate compliance certificate if score >= 80%.
    /// Certificate has 6-month validity and SHA-256 verification hash.
    /// </summary>
    Task<ComplianceCertificate> GenerateCertificateAsync(
        string subscriptionId,
        string issuedBy,
        CancellationToken cancellationToken = default);

    // ─── Continuous Monitoring (US6) ────────────────────────────────

    /// <summary>
    /// Get continuous compliance status by delegating to Compliance Watch.
    /// Aggregates IComplianceWatchService, IAlertManager, IComplianceEventSource.
    /// </summary>
    Task<ContinuousComplianceStatus> GetContinuousComplianceStatusAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate compliance timeline from historical data and Compliance Watch alerts.
    /// </summary>
    Task<ComplianceTimeline> GetComplianceTimelineAsync(
        string subscriptionId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    // ─── Data Access (US7) ──────────────────────────────────────────

    /// <summary>Get assessment history for a subscription.</summary>
    Task<List<ComplianceAssessment>> GetAssessmentHistoryAsync(
        string subscriptionId,
        int days = 30,
        CancellationToken cancellationToken = default);

    /// <summary>Get a single finding by ID.</summary>
    Task<ComplianceFinding?> GetFindingAsync(
        string findingId,
        CancellationToken cancellationToken = default);

    /// <summary>Update finding status (Open → InProgress → Remediated, etc.).</summary>
    Task<bool> UpdateFindingStatusAsync(
        string findingId,
        FindingStatus newStatus,
        CancellationToken cancellationToken = default);

    /// <summary>Save or update an assessment (upsert semantics).</summary>
    Task SaveAssessmentAsync(
        ComplianceAssessment assessment,
        CancellationToken cancellationToken = default);

    /// <summary>Get the most recent assessment for a subscription (cached 24h).</summary>
    Task<ComplianceAssessment?> GetLatestAssessmentAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>Get assessment audit log entries.</summary>
    Task<string> GetAuditLogAsync(
        string? subscriptionId = null,
        int days = 7,
        CancellationToken cancellationToken = default);

    /// <summary>Generate executive summary markdown from assessment data.</summary>
    string GenerateExecutiveSummary(ComplianceAssessment assessment);
}
```

---

## Method Contracts

### RunComprehensiveAssessmentAsync

| Aspect | Detail |
|--------|--------|
| **Preconditions** | `subscriptionId` is non-empty, valid GUID format |
| **Steps** | 1. Create assessment (Pending) → 2. Pre-warm resource cache → 3. Iterate `ControlFamilies.AllFamilies` → 4. For each: dispatch scanner via `IScannerRegistry.GetScanner(family)` → 5. Pass `INistControlsService.GetControlFamilyAsync(family)` controls → 6. Run STIG validation → 7. Correlate findings → 8. Calculate scores → 9. Generate executive summary → 10. Calculate risk profile → 11. Persist → 12. Return |
| **Postconditions** | Assessment has Status=Completed, 20 family results, all findings correlated |
| **Error handling** | Individual scanner failures → family marked Failed, assessment continues. All-fail → 0 findings, 100% score. Persistence failure → logged, assessment still returned |
| **Cancellation** | Checks token between families; marks assessment Cancelled |
| **Progress** | Reports after each family completes |

### RunEnvironmentAssessmentAsync

| Aspect | Detail |
|--------|--------|
| **Preconditions** | `subscriptionIds` non-empty; `environmentName` non-empty |
| **Steps** | 1. Pre-warm caches for all subscriptions → 2. For each subscription, run per-family scans → 3. Aggregate findings across subscriptions → 4. Calculate combined scores → 5. Set `EnvironmentName` and `SubscriptionIds` on result |
| **Postconditions** | Single `ComplianceAssessment` with all subscription findings aggregated |

### AssessControlFamilyAsync

| Aspect | Detail |
|--------|--------|
| **Preconditions** | `familyCode` passes `ControlFamilies.IsValidFamily()` |
| **Steps** | 1. Get scanner → 2. Get controls via `INistControlsService.GetControlFamilyAsync` → 3. Scanner.ScanAsync → 4. STIG validation → 5. Return family result |
| **Error handling** | Invalid family → `ArgumentException`. Scanner failure → `ControlFamilyAssessment.Failed()` |

### CollectEvidenceAsync

| Aspect | Detail |
|--------|--------|
| **Preconditions** | `familyCode` is valid or "All" |
| **Steps** | 1. Get collector via `IEvidenceCollectorRegistry` → 2. Collect 5 evidence types → 3. Score completeness → 4. Generate attestation → 5. Store via `IEvidenceStorageService` → 6. Return package |
| **Postconditions** | `EvidencePackage.CompletenessScore` = `collected / expected * 100` |

### CalculateRiskProfile

| Aspect | Detail |
|--------|--------|
| **Pure function** | No I/O, no async. Synchronous calculation from assessment findings |
| **Formula** | `score = Σ(Critical×10 + High×7.5 + Medium×5 + Low×2.5)` |
| **Risk levels** | score ≥ 100 → Critical, ≥ 50 → High, ≥ 20 → Medium, < 20 → Low |
| **TopRisks** | Up to 5 families with score < 70%, ordered ascending |

### PerformRiskAssessmentAsync

| Aspect | Detail |
|--------|--------|
| **Preconditions** | Latest assessment exists for the subscription |
| **Steps** | 1. Get latest assessment via `IAssessmentPersistenceService` → 2. Map findings to 8 risk categories by control family (Data Protection: SC/SI, Access Control: AC/IA, Network Security: SC, Incident Response: IR, Business Continuity: CP, Compliance: CA/PM, Third-Party Risk: SA/SR, Configuration Management: CM/MA) → 3. Score each category: `1 + (average_family_compliance_percent / 100 × 9)`, clamped to [1, 10] → 4. Calculate overall score as average of 8 category scores → 5. Derive risk level: ≥ 8 = Low, ≥ 5 = Medium, ≥ 3 = High, < 3 = Critical → 6. Generate mitigation recommendations for categories scoring below 5 |
| **Postconditions** | `RiskAssessment` with 8 categories, each scored 1-10, overall score, risk level, and recommendations |
| **Error handling** | No assessment found → `InvalidOperationException` |

### GenerateCertificateAsync

| Aspect | Detail |
|--------|--------|
| **Preconditions** | Latest assessment exists; score ≥ 80% |
| **Steps** | 1. Get latest assessment → 2. Validate score → 3. Build certificate → 4. Generate SHA-256 hash → 5. Store via evidence service |
| **Error handling** | No assessment → `InvalidOperationException`. Score < 80% → `InvalidOperationException` with score in message |
| **Postconditions** | Certificate with 6-month validity, per-family attestations |

### GetContinuousComplianceStatusAsync

| Aspect | Detail |
|--------|--------|
| **Dependencies** | `IComplianceWatchService`, `IAlertManager`, `IAssessmentPersistenceService` |
| **Steps** | 1. Get monitoring status → 2. Detect drift → 3. Get active alerts → 4. Get latest assessment → 5. Aggregate into `ContinuousComplianceStatus` |
| **Fallback** | If Compliance Watch not enabled → status based on assessment history only, `DriftDetected = false`, `MonitoringEnabled = false` |

### GetComplianceTimelineAsync

| Aspect | Detail |
|--------|--------|
| **Steps** | 1. Get assessments in date range → 2. Build daily data points → 3. Detect significant events (score Δ ≥ 10%, finding Δ ≥ 5) → 4. Calculate trend → 5. Generate insights |
| **Significant events** | 10 event types (see `TimelineEventType` enum) |
| **Insights** | Trajectory ("improving by X%/week"), volatility ("high" if avg Δ > 8%), remediation effectiveness |

---

## Error Responses

| Scenario | Exception | HTTP Status (MCP) |
|----------|-----------|-------------------|
| Invalid subscription ID | `ArgumentException` | 400 |
| Invalid family code | `ArgumentException` | 400 |
| Assessment not found | `InvalidOperationException` | 404 |
| Certificate score < 80% | `InvalidOperationException` | 422 |
| Cancellation requested | `OperationCanceledException` | 499 |
| Azure API throttled | Retried internally (3x) | — |
| Database unavailable | Logged, assessment returned | — |

---

## Breaking Changes

| Change | Migration Path |
|--------|---------------|
| `RunAssessmentAsync` removed | `ComplianceAssessmentTool` updated to call `RunComprehensiveAssessmentAsync` |
| Return type changes | All methods now return typed models instead of some returning `string` |
| `SaveAssessmentAsync` now has upsert semantics | Existing callers unaffected (same signature) |
