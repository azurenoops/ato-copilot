# Data Model: ATO Compliance Engine — Production Readiness

**Feature Branch**: `008-compliance-engine` | **Date**: 2026-02-23

## Entity Overview

This feature introduces two categories of data model changes:

1. **Extended EF Core Entities** — `ComplianceAssessment` and `ComplianceFinding` (already in `ComplianceModels.cs`) receive new properties for control family results, executive summaries, risk profiles, and scan source attribution.
2. **New Application Models** — `ControlFamilyAssessment`, `EvidencePackage`, `RiskProfile`, `RiskAssessment`, `ComplianceCertificate`, `ComplianceTimeline`, `ContinuousComplianceStatus`, `AssessmentProgress`, and supporting types. These are in-memory models (not EF Core entities) used by the engine and returned to callers.

The existing `NistControl` entity, OSCAL deserialization models (`OscalModels.cs`), `ComplianceAlert`, `MonitoringConfiguration`, `ComplianceBaseline`, and all Compliance Watch entities from Features 005/007 are **consumed as-is and NOT modified**.

---

## Extended Entities

### ComplianceAssessment (MODIFY)

Location: `src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs`

Existing properties are preserved. New properties are added:

| Property | Type | Default | Persisted | Description |
|----------|------|---------|-----------|-------------|
| ControlFamilyResults | `List<ControlFamilyAssessment>` | `new()` | JSON column | Per-family assessment breakdown |
| ExecutiveSummary | `string` | `""` | Yes | Markdown executive summary with score, risk, finding counts |
| RiskProfile | `RiskProfile?` | `null` | JSON column | Severity-weighted risk profile |
| EnvironmentName | `string?` | `null` | Yes | Environment identifier for multi-subscription assessments |
| SubscriptionIds | `List<string>` | `new()` | JSON column | All subscription IDs assessed (single or multi) |
| ResourceGroupFilter | `string?` | `null` | Yes | Resource group constraint (null = full subscription) |
| AssessmentDuration | `TimeSpan?` | `null` | Yes | Total wall-clock assessment time |
| ScanPillarResults | `Dictionary<string, bool>` | `new()` | JSON column | Per-pillar success/failure (ARM, Policy, Defender) |

**Relationships**: `Findings` (existing 1:N), `ControlFamilyResults` (owned JSON), `RiskProfile` (owned JSON).

### ComplianceFinding (MODIFY)

Location: `src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs`

Existing properties are preserved. New properties are added:

| Property | Type | Default | Persisted | Description |
|----------|------|---------|-----------|-------------|
| ControlTitle | `string` | `""` | Yes | Human-readable control title from NIST catalog |
| ControlDescription | `string` | `""` | Yes | Full control description from NIST catalog |
| StigFinding | `bool` | `false` | Yes | Whether this finding came from STIG validation |
| StigId | `string?` | `null` | Yes | STIG rule ID (if StigFinding = true) |
| RemediationStatus | `RemediationTrackingStatus` | `NotStarted` | Yes | Lifecycle tracking for remediation |
| RemediatedAt | `DateTime?` | `null` | Yes | When remediation was completed |
| RemediatedBy | `string?` | `null` | Yes | Who performed the remediation |

---

## New Application Models

All new models are located in `src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs`.

### ControlFamilyAssessment

Per-family scan result aggregation. Returned by each `IComplianceScanner`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| FamilyCode | `string` | `""` | Two-letter NIST family code (e.g., "AC") |
| FamilyName | `string` | `""` | Human-readable family name (from `ControlFamilies.FamilyNames`) |
| TotalControls | `int` | `0` | Number of controls in this family |
| PassedControls | `int` | `0` | Controls that passed assessment |
| FailedControls | `int` | `0` | Controls with findings |
| ComplianceScore | `double` | `0.0` | Per-family score: `passed / total * 100` |
| Findings | `List<ComplianceFinding>` | `new()` | Findings in this family |
| AssessmentDuration | `TimeSpan` | `Zero` | Time spent scanning this family |
| ScannerName | `string` | `""` | Scanner class name that handled this family |
| Status | `FamilyAssessmentStatus` | `Pending` | Pending, Completed, Failed, Skipped |
| ErrorMessage | `string?` | `null` | Error message if Status = Failed |

**Factory Method**: `static ControlFamilyAssessment Failed(string familyCode, string error)` — creates a failed result for error isolation.

### EvidencePackage

Aggregated evidence collection result for a control family.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| FamilyCode | `string` | `""` | Control family code |
| SubscriptionId | `string` | `""` | Azure subscription assessed |
| EvidenceItems | `List<EvidenceItem>` | `new()` | Individual evidence artifacts |
| CompletenessScore | `double` | `0.0` | `distinct_types / expected_types * 100` |
| ExpectedEvidenceTypes | `int` | `5` | Expected evidence types for this family |
| CollectedEvidenceTypes | `int` | `0` | Distinct evidence types collected |
| Summary | `string` | `""` | Human-readable evidence summary |
| AttestationStatement | `string` | `""` | Formal attestation text |
| CollectedAt | `DateTime` | `UtcNow` | Collection timestamp |

### EvidenceItem

Individual evidence artifact within a package.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| Type | `EvidenceType` | `Configuration` | Evidence category |
| Title | `string` | `""` | Evidence title |
| Description | `string` | `""` | Evidence description |
| Content | `string` | `""` | Evidence content (JSON/text) |
| ResourceId | `string?` | `null` | Azure resource ID |
| CollectedAt | `DateTime` | `UtcNow` | Timestamp |
| ContentHash | `string` | `""` | SHA-256 hash for integrity |

### RiskProfile

Severity-weighted risk summary attached to an assessment.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| RiskScore | `double` | `0.0` | Weighted score: `Σ(severity_weight × count)` |
| RiskLevel | `ComplianceRiskLevel` | `Low` | Derived from score thresholds |
| CriticalCount | `int` | `0` | Critical findings count |
| HighCount | `int` | `0` | High findings count |
| MediumCount | `int` | `0` | Medium findings count |
| LowCount | `int` | `0` | Low findings count |
| TopRisks | `List<FamilyRisk>` | `new()` | Up to 5 families with score < 70% |

**Severity Weights**: Critical = 10.0, High = 7.5, Medium = 5.0, Low = 2.5.

**Risk Level Thresholds**: Critical ≥ 100, High ≥ 50, Medium ≥ 20, Low < 20.

### FamilyRisk

Entry in `RiskProfile.TopRisks`.

| Property | Type | Description |
|----------|------|-------------|
| FamilyCode | `string` | Two-letter family code |
| FamilyName | `string` | Family name |
| ComplianceScore | `double` | Family compliance score |
| FindingCount | `int` | Number of findings in this family |

### RiskAssessment

Full 8-category risk analysis.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| SubscriptionId | `string` | `""` | Subscription assessed |
| AssessedAt | `DateTime` | `UtcNow` | Assessment timestamp |
| Categories | `List<RiskCategory>` | `new()` | 8 risk category results |
| OverallScore | `double` | `0.0` | Average across categories |
| OverallRiskLevel | `ComplianceRiskLevel` | `Low` | Derived from overall score |
| Recommendations | `List<string>` | `new()` | Mitigation recommendations |

### RiskCategory

One of 8 risk assessment categories.

| Property | Type | Description |
|----------|------|-------------|
| Name | `string` | Category name (e.g., "Data Protection", "Access Control") |
| Score | `double` | Score on 1-10 scale |
| RiskLevel | `ComplianceRiskLevel` | Derived from score |
| Findings | `int` | Related finding count |
| Mitigations | `List<string>` | Recommended mitigations |

**Category Names**: Data Protection, Access Control, Network Security, Incident Response, Business Continuity, Compliance, Third-Party Risk, Configuration Management.

### ComplianceCertificate

ATO compliance certificate.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| CertificateId | `string` | `NewGuid()` | Unique certificate ID |
| SubscriptionId | `string` | `""` | Subscription certified |
| Framework | `string` | `"NIST80053"` | Compliance framework |
| ComplianceScore | `double` | `0.0` | Score at time of certification |
| IssuedAt | `DateTime` | `UtcNow` | Certificate issue date |
| ExpiresAt | `DateTime` | `UtcNow + 180d` | 6-month validity |
| IssuedBy | `string` | `""` | Issuer identity |
| FamilyAttestations | `List<FamilyAttestation>` | `new()` | Per-family attestation |
| CoverageFamilies | `List<string>` | `new()` | Families covered |
| VerificationHash | `string` | `""` | SHA-256 of certificate content |
| Status | `CertificateStatus` | `Active` | Active, Expired, Revoked |

### FamilyAttestation

Per-family entry in a certificate.

| Property | Type | Description |
|----------|------|-------------|
| FamilyCode | `string` | Family code |
| FamilyName | `string` | Family name |
| ComplianceScore | `double` | Score at certification time |
| ControlsAssessed | `int` | Controls evaluated |
| ControlsPassed | `int` | Controls that passed |
| AttestationText | `string` | Formal attestation statement |

### ComplianceTimeline

Historical compliance trend data.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| SubscriptionId | `string` | `""` | Subscription ID |
| StartDate | `DateTime` | — | Timeline start |
| EndDate | `DateTime` | — | Timeline end |
| DataPoints | `List<TimelineDataPoint>` | `new()` | Daily data points |
| SignificantEvents | `List<SignificantEvent>` | `new()` | Detected events |
| Trend | `TrendDirection` | `Stable` | Overall direction |
| Insights | `List<string>` | `new()` | Auto-generated insights |

### TimelineDataPoint

Daily compliance snapshot.

| Property | Type | Description |
|----------|------|-------------|
| Date | `DateTime` | Day |
| ComplianceScore | `double` | Score on this day |
| FindingCount | `int` | Total findings |
| CriticalCount | `int` | Critical findings |
| HighCount | `int` | High findings |

### SignificantEvent

Notable compliance event in a timeline.

| Property | Type | Description |
|----------|------|-------------|
| Date | `DateTime` | Event date |
| EventType | `TimelineEventType` | Event category |
| Description | `string` | Human-readable description |
| ScoreChange | `double` | Score delta (signed) |
| FindingChange | `int` | Finding count delta (signed) |

### ContinuousComplianceStatus

Real-time compliance posture aggregated from Compliance Watch.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| SubscriptionId | `string` | `""` | Subscription |
| OverallScore | `double` | `0.0` | Latest compliance score |
| MonitoringEnabled | `bool` | `false` | Whether Compliance Watch is enabled |
| DriftDetected | `bool` | `false` | From `IComplianceWatchService.DetectDriftAsync` |
| ActiveAlerts | `int` | `0` | From `IAlertManager.GetAlertsAsync` count |
| LastAssessedAt | `DateTime?` | `null` | Most recent assessment date |
| LastDriftCheckAt | `DateTime?` | `null` | Most recent drift check |
| ControlStatuses | `List<ControlComplianceStatus>` | `new()` | Per-control status |
| AutoRemediationEnabled | `bool` | `false` | Whether auto-remediation rules exist |

### ControlComplianceStatus

Per-control monitoring entry within continuous status.

| Property | Type | Description |
|----------|------|-------------|
| ControlId | `string` | NIST control ID |
| Status | `FindingStatus` | Current status |
| DriftDetected | `bool` | Drift flag for this control |
| LastCheckedAt | `DateTime` | Last check timestamp |

### AssessmentProgress

Progress reporting during long-running assessments.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| TotalFamilies | `int` | `20` | Total families to scan |
| CompletedFamilies | `int` | `0` | Families completed |
| CurrentFamily | `string?` | `null` | Family currently being scanned |
| PercentComplete | `double` | `0.0` | `completed / total * 100` |
| EstimatedTimeRemaining | `TimeSpan?` | `null` | ETA based on average family scan time |
| FamilyResults | `List<string>` | `new()` | Completed family codes |

---

## New Enums

### ComplianceRiskLevel

Risk level for risk profiles and assessments (distinct from existing `RiskLevel` enum which only has Standard/High).

```csharp
public enum ComplianceRiskLevel { Low, Medium, High, Critical }
```

### FamilyAssessmentStatus

Status of a per-family scan result.

```csharp
public enum FamilyAssessmentStatus { Pending, Completed, Failed, Skipped }
```

### EvidenceType

Evidence artifact category (distinct from existing `EvidenceCategory`).

```csharp
public enum EvidenceType { Configuration, Log, Metric, Policy, AccessControl }
```

### TimelineEventType

Significant event categories for compliance timelines.

```csharp
public enum TimelineEventType
{
    ScoreImprovement,
    ScoreDegradation,
    FindingSpike,
    FindingResolution,
    CertificateIssued,
    CertificateExpired,
    BaselineChanged,
    RemediationCompleted,
    DriftDetected,
    AlertEscalated
}
```

### TrendDirection

Overall compliance trend.

```csharp
public enum TrendDirection { Improving, Stable, Degrading }
```

### CertificateStatus

Certificate lifecycle.

```csharp
public enum CertificateStatus { Active, Expired, Revoked }
```

### RemediationTrackingStatus

Finding-level remediation tracking (distinct from plan-level `RemediationStatus`).

```csharp
public enum RemediationTrackingStatus { NotStarted, InProgress, Completed, WontFix, Deferred }
```

---

## New Interfaces

### IComplianceScanner

Scanner strategy interface for family-specific compliance checks.

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| FamilyCode | — (property) | `string` | Family this scanner handles |
| ScanAsync | `string subscriptionId, string? resourceGroup, IEnumerable<NistControl> controls, CancellationToken ct` | `Task<ControlFamilyAssessment>` | Execute family scan |

### IEvidenceCollector

Evidence collection strategy interface.

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| FamilyCode | — (property) | `string` | Family this collector handles |
| CollectAsync | `string subscriptionId, string? resourceGroup, CancellationToken ct` | `Task<EvidencePackage>` | Collect evidence for family |

### IScannerRegistry

Scanner dispatch registry.

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| GetScanner | `string familyCode` | `IComplianceScanner` | Returns specialized or default scanner |

### IEvidenceCollectorRegistry

Evidence collector dispatch registry.

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| GetCollector | `string familyCode` | `IEvidenceCollector` | Returns specialized or default collector |

### IAzureResourceService

ARM SDK wrapper for Azure resource queries.

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| GetResourcesAsync | `string subscriptionId, string? resourceGroup, string? resourceType, CancellationToken ct` | `Task<IReadOnlyList<GenericResource>>` | Enumerate resources |
| GetRoleAssignmentsAsync | `string subscriptionId, CancellationToken ct` | `Task<IReadOnlyList<RoleAssignmentResource>>` | Get RBAC assignments |
| PreWarmCacheAsync | `string subscriptionId, CancellationToken ct` | `Task` | Pre-warm resource cache |
| GetDiagnosticSettingsAsync | `string resourceId, CancellationToken ct` | `Task<IReadOnlyList<DiagnosticSettingsResource>>` | Get diagnostic settings |
| GetResourceLocksAsync | `string subscriptionId, string? resourceGroup, CancellationToken ct` | `Task<IReadOnlyList<ManagementLockResource>>` | Get resource locks |

### IAssessmentPersistenceService

Database persistence abstraction (separates EF Core from engine).

| Method | Parameters | Returns | Description |
|--------|-----------|---------|-------------|
| SaveAssessmentAsync | `ComplianceAssessment assessment, CancellationToken ct` | `Task` | Upsert assessment |
| GetAssessmentAsync | `string assessmentId, CancellationToken ct` | `Task<ComplianceAssessment?>` | Get by ID |
| GetLatestAssessmentAsync | `string subscriptionId, CancellationToken ct` | `Task<ComplianceAssessment?>` | Latest assessment |
| GetAssessmentHistoryAsync | `string subscriptionId, int days, CancellationToken ct` | `Task<List<ComplianceAssessment>>` | History query |
| GetFindingAsync | `string findingId, CancellationToken ct` | `Task<ComplianceFinding?>` | Get finding by ID |
| UpdateFindingStatusAsync | `string findingId, FindingStatus status, CancellationToken ct` | `Task<bool>` | Update finding status |

### Knowledge Base Interfaces (Stubs)

All return sensible defaults. Full implementations deferred.

| Interface | Key Methods | Stub Behavior |
|-----------|------------|---------------|
| IStigValidationService | `ValidateAsync(familyCode, controls, subscriptionId)` | Returns empty findings list |
| IRmfKnowledgeService | `GetGuidanceAsync(controlId)` | Returns generic RMF guidance string |
| IStigKnowledgeService | `GetStigMappingAsync(controlId)` | Returns empty STIG mapping |
| IDoDInstructionService | `GetInstructionAsync(controlId)` | Returns generic DoD instruction |
| IDoDWorkflowService | `GetWorkflowAsync(assessmentType)` | Returns standard workflow steps |

---

## Relationships

```
ComplianceAssessment (EXTENDED)
├── Findings: List<ComplianceFinding> (EXTENDED, existing 1:N)
├── ControlFamilyResults: List<ControlFamilyAssessment> (NEW, JSON)
│   └── Findings: List<ComplianceFinding> (transient ref)
├── RiskProfile (NEW, JSON)
│   └── TopRisks: List<FamilyRisk>
└── ScanPillarResults: Dictionary<string, bool> (NEW, JSON)

RiskAssessment (NEW, transient)
└── Categories: List<RiskCategory>

ComplianceCertificate (NEW, transient → evidence storage)
└── FamilyAttestations: List<FamilyAttestation>

ComplianceTimeline (NEW, transient)
├── DataPoints: List<TimelineDataPoint>
└── SignificantEvents: List<SignificantEvent>

ContinuousComplianceStatus (NEW, transient)
└── ControlStatuses: List<ControlComplianceStatus>

EvidencePackage (NEW, transient → evidence storage)
└── EvidenceItems: List<EvidenceItem>
```

---

## State Transitions

### ComplianceAssessment.Status

```
Pending → InProgress → Completed
                    → Failed
                    → Cancelled
```

**Unchanged from existing model.** The `InProgress` state now implies scanners are dispatching per-family.

### ControlFamilyAssessment.Status

```
Pending → Completed  (scanner succeeded)
       → Failed      (scanner threw exception)
       → Skipped     (cancellation during scan)
```

### ComplianceCertificate.Status

```
Active → Expired     (ExpiresAt < UtcNow)
      → Revoked     (manual revocation)
```

---

## Validation Rules

| Entity | Field | Rule |
|--------|-------|------|
| ComplianceAssessment | SubscriptionId | Valid GUID format |
| ComplianceAssessment | ComplianceScore | 0.0 ≤ x ≤ 100.0 |
| ComplianceFinding | ControlId | Matches `^[A-Z]{2}-\d+(\.\d+)?$` |
| ComplianceFinding | ControlFamily | `ControlFamilies.IsValidFamily(family)` must be true |
| ControlFamilyAssessment | FamilyCode | `ControlFamilies.IsValidFamily(code)` must be true |
| ControlFamilyAssessment | ComplianceScore | 0.0 ≤ x ≤ 100.0 |
| RiskCategory | Score | 1.0 ≤ x ≤ 10.0 |
| ComplianceCertificate | ComplianceScore | ≥ 80.0 (certificate issuance threshold) |
| ComplianceCertificate | ExpiresAt | IssuedAt + 180 days |
| SignificantEvent | ScoreChange | abs(x) ≥ 10.0 triggers ScoreImprovement/ScoreDegradation |
| SignificantEvent | FindingChange | x ≥ 5 triggers FindingSpike |

---

## EF Core Configuration Notes

- `ControlFamilyResults` and `RiskProfile` on `ComplianceAssessment` are stored as JSON columns using `.HasConversion()` with `System.Text.Json` serializer.
- `ScanPillarResults` and `SubscriptionIds` are also JSON columns.
- New properties on `ComplianceFinding` are simple scalar columns added to the existing `Findings` table.
- New enums (`ComplianceRiskLevel`, `FamilyAssessmentStatus`, etc.) are stored as `int` by EF Core convention.
- No new migration is created in this feature — the `AtoCopilotContext` uses `EnsureCreated()` for dev and explicit migrations for prod.
