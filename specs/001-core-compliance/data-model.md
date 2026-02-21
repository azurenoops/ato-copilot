# Data Model: Core Compliance Capabilities

**Branch**: `001-core-compliance` | **Date**: 2026-02-21

This document defines all entities, their relationships, validation rules, and state
transitions for the Core Compliance Capabilities feature. It aligns with the existing
models in `Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs` and entities in
`Ato.Copilot.Core/Data/Context/AtoCopilotContext.cs`, extending them where needed.

---

## Entity Overview

```text
ConfigurationSettings ──────────────────────────────────────────┐
                                                                 │ reads defaults
ComplianceAssessment ──< ComplianceFinding >── NistControl       │
        │                      │                    │            │
        │                      │                    │            │
        ├── ComplianceEvidence ─┘                    │            │
        │                                           │            │
        ├── ComplianceDocument                      │            │
        │                                           │            │
        └── AuditLogEntry                           │            │
                                                    │            │
RemediationPlan ──< RemediationStep >── ComplianceFinding       │
                                                                 │
                                         ◄───────────────────────┘
```

---

## Entities

### 1. ComplianceAssessment (existing — extend)

**Existing fields**: `Id`, `SubscriptionId`, `Framework`, `ScanType`, `AssessedAt`,
`ComplianceScore`, `TotalControls`, `PassedControls`, `FailedControls`,
`NotAssessedControls`, `Findings`.

**Extensions needed**:

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `Baseline` | `string` | FedRAMP baseline (High/Moderate/Low) | `"High"` |
| `ScanType` | `string` | Change from `"quick"` default to `"combined"` | `"combined"` |
| `Status` | `AssessmentStatus` | Assessment lifecycle state | `Pending` |
| `InitiatedBy` | `string` | User/role who started the assessment | `""` |
| `CompletedAt` | `DateTime?` | When assessment finished | `null` |
| `ProgressMessage` | `string` | Last progress update message | `""` |
| `ResourceScanSummary` | `ScanSummary?` | Resource scan statistics | `null` |
| `PolicyScanSummary` | `ScanSummary?` | Policy scan statistics | `null` |

**Validation rules**:
- `SubscriptionId` MUST be a valid GUID format.
- `Framework` MUST be one of: `NIST80053`, `FedRAMPHigh`, `FedRAMPModerate`, `DoDIL5`.
- `ScanType` MUST be one of: `resource`, `policy`, `combined`.
- `Baseline` MUST be one of: `High`, `Moderate`, `Low`.
- `ComplianceScore` MUST be between 0.0 and 100.0.

**State transitions**:
```
Pending → InProgress → Completed
Pending → InProgress → Failed
Pending → InProgress → Cancelled
```

```csharp
public enum AssessmentStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

public class ScanSummary
{
    public int ResourcesScanned { get; set; }
    public int PoliciesEvaluated { get; set; }
    public int Compliant { get; set; }
    public int NonCompliant { get; set; }
    public double CompliancePercentage { get; set; }
}
```

---

### 2. ComplianceFinding (existing — extend)

**Existing fields**: `Id`, `ControlId`, `ControlFamily`, `Title`, `Description`,
`Severity`, `Status`, `ResourceId`, `ResourceType`, `RemediationGuidance`,
`DiscoveredAt`, `RemediationScript`, `AutoRemediable`, `Source`.

**Extensions needed**:

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `ScanSource` | `ScanSourceType` | Which scan found this | `Combined` |
| `PolicyDefinitionId` | `string?` | Azure Policy definition (policy scans) | `null` |
| `PolicyAssignmentId` | `string?` | Azure Policy assignment (policy scans) | `null` |
| `DefenderRecommendationId` | `string?` | DFC recommendation ID | `null` |
| `RemediationType` | `RemediationType` | Resource config vs. policy assignment | `Unknown` |
| `RiskLevel` | `RiskLevel` | AC/IA/SC are high-risk | `Standard` |
| `AssessmentId` | `string` | FK to parent assessment | `""` |

**Validation rules**:
- `ControlId` MUST match pattern `^[A-Z]{2}-\d+(\.\d+)?$` (e.g., `AC-2`, `AC-2.1`).
- `ControlFamily` MUST be one of the 20 NIST families.
- `Severity` MUST be a valid `FindingSeverity` enum value.
- At least one of `ResourceId`, `PolicyDefinitionId`, or `DefenderRecommendationId` MUST
  be non-empty.

```csharp
public enum ScanSourceType
{
    Resource,
    Policy,
    Defender,
    Combined
}

public enum RemediationType
{
    Unknown,
    ResourceConfiguration,
    PolicyAssignment,
    PolicyRemediation,
    Manual
}

public enum RiskLevel
{
    Standard,
    High  // AC, IA, SC families
}
```

---

### 3. NistControl (existing — extend)

**Existing fields**: `Id`, `Family`, `Title`, `Description`, `ImpactLevel`,
`Enhancements`, `AzureImplementation`.

**Extensions needed**:

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `Baselines` | `List<string>` | Applicable baselines (High/Moderate/Low) | `[]` |
| `FedRampParameters` | `string?` | FedRAMP-specific parameter values | `null` |
| `AzurePolicyDefinitionIds` | `List<string>` | Mapped Azure Policy definition IDs | `[]` |
| `ControlEnhancements` | `List<NistControl>` | Nested enhancements (self-ref) | `[]` |
| `ParentControlId` | `string?` | Parent control ID for enhancements | `null` |
| `IsEnhancement` | `bool` | True if this is an enhancement, not a base control | `false` |

**Validation rules**:
- `Id` MUST match pattern `^[a-z]{2}-\d+(\(\d+\))?$` (e.g., `ac-2`, `ac-2(1)`).
- `Family` MUST be a valid 2-letter abbreviation from the 20 families.
- If `IsEnhancement` is true, `ParentControlId` MUST be non-null.

---

### 4. ComplianceEvidence (existing — extend)

**Existing fields**: `Id`, `ControlId`, `SubscriptionId`, `EvidenceType`,
`Description`, `Content`, `CollectedAt`, `CollectedBy`.

**Extensions needed**:

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `AssessmentId` | `string?` | Linked assessment (optional) | `null` |
| `EvidenceCategory` | `EvidenceCategory` | Category of evidence | `Configuration` |
| `ResourceId` | `string?` | Specific resource this evidence covers | `null` |
| `ContentHash` | `string` | SHA-256 hash of Content for integrity | `""` |

**Validation rules**:
- `EvidenceType` MUST be one of: `ConfigurationExport`, `PolicySnapshot`,
  `ResourceSnapshot`, `DefenderRecommendation`, `ActivityLog`, `ResourceInventory`,
  `PolicyAssignmentListing`.
- `CollectedBy` MUST be a non-empty string.
- `Content` MUST be non-empty.

```csharp
public enum EvidenceCategory
{
    Configuration,
    PolicyCompliance,
    ResourceCompliance,
    SecurityAssessment,
    ActivityLog,
    Inventory
}
```

---

### 5. ComplianceDocument (existing — extend)

**Existing fields**: `Id`, `DocumentType`, `SystemName`, `Framework`,
`Content`, `GeneratedAt`.

**Extensions needed**:

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `AssessmentId` | `string?` | Assessment this document is based on | `null` |
| `Owner` | `string` | System owner name | `""` |
| `GeneratedBy` | `string` | User who generated the document | `""` |
| `Metadata` | `DocumentMetadata` | Additional document metadata | `new()` |

**Validation rules**:
- `DocumentType` MUST be one of: `SSP`, `SAR`, `POAM`.
- `Framework` MUST match `ComplianceAssessment.Framework` values.
- `Content` MUST be non-empty.

```csharp
public class DocumentMetadata
{
    public string SystemDescription { get; set; } = string.Empty;
    public string AuthorizationBoundary { get; set; } = string.Empty;
    public string DateRange { get; set; } = string.Empty;
    public string PreparedBy { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
}
```

---

### 6. RemediationPlan (existing — extend)

**Existing fields**: `Id`, `SubscriptionId`, `CreatedAt`, `Steps`,
`TotalFindings`, `AutoRemediableCount`.

**Extensions needed**:

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `Status` | `RemediationStatus` | Plan lifecycle state | `Planned` |
| `DryRun` | `bool` | Whether this is a dry-run plan | `true` |
| `ApprovedBy` | `string?` | Compliance Officer who approved | `null` |
| `ApprovedAt` | `DateTime?` | When approval was given | `null` |
| `CompletedAt` | `DateTime?` | When remediation completed | `null` |
| `FailedStepId` | `string?` | Step that caused failure (if any) | `null` |
| `FailureReason` | `string?` | Why remediation failed | `null` |

**State transitions**:
```
Planned → Approved → InProgress → Completed
Planned → Approved → InProgress → Failed
Planned → Approved → InProgress → PartiallyCompleted
Planned → Rejected
```

```csharp
public enum RemediationStatus
{
    Planned,
    Approved,
    InProgress,
    Completed,
    PartiallyCompleted,
    Failed,
    Rejected
}
```

---

### 7. RemediationStep (existing — extend)

**Existing fields**: `Id` (implicit), `FindingId`, `ControlId`, `Priority`,
`Description`, `Script`, `Effort`, `AutoRemediable`.

**Extensions needed**:

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `Id` | `string` | Unique step ID | `Guid.NewGuid()` |
| `RemediationType` | `RemediationType` | Resource config vs. policy | `Unknown` |
| `Status` | `StepStatus` | Step execution status | `Pending` |
| `BeforeState` | `string?` | Resource state before change | `null` |
| `AfterState` | `string?` | Resource state after change | `null` |
| `ExecutedAt` | `DateTime?` | When step was executed | `null` |
| `ResourceId` | `string?` | Specific resource modified | `null` |
| `RiskLevel` | `RiskLevel` | High-risk for AC/IA/SC families | `Standard` |

**State transitions**:
```
Pending → InProgress → Completed
Pending → InProgress → Failed
Pending → Skipped
```

```csharp
public enum StepStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped
}
```

---

### 8. ConfigurationSettings (NEW)

Stored in `IAgentStateManager` shared state, not in EF Core.

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `SubscriptionId` | `string?` | Default Azure subscription | `null` |
| `Framework` | `string` | Default compliance framework | `"NIST80053"` |
| `Baseline` | `string` | Default baseline level | `"High"` |
| `CloudEnvironment` | `string` | Azure cloud environment | `"AzureGovernment"` |
| `DryRunDefault` | `bool` | Default dry-run preference | `true` |
| `DefaultScanType` | `string` | Default scan type | `"combined"` |
| `Region` | `string` | Preferred Azure region | `"usgovvirginia"` |
| `LastUpdated` | `DateTime` | When settings were last changed | `DateTime.UtcNow` |

**Validation rules**:
- `SubscriptionId`, if set, MUST be a valid GUID format.
- `Framework` MUST be one of: `NIST80053`, `FedRAMPHigh`, `FedRAMPModerate`, `DoDIL5`.
- `Baseline` MUST be one of: `High`, `Moderate`, `Low`.
- `CloudEnvironment` MUST be one of: `AzureGovernment`, `AzureCloud`.
- `DefaultScanType` MUST be one of: `resource`, `policy`, `combined`.

---

### 9. AuditLogEntry (NEW)

Persisted in EF Core via `AtoCopilotContext`.

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `Id` | `string` | Unique log entry ID | `Guid.NewGuid()` |
| `UserId` | `string` | Who initiated the action | `""` |
| `UserRole` | `string` | Role of the user | `""` |
| `Action` | `string` | Action type (Assessment, Remediation, etc.) | `""` |
| `ScanType` | `string?` | Scan type if applicable | `null` |
| `Timestamp` | `DateTime` | When the action occurred (UTC) | `DateTime.UtcNow` |
| `SubscriptionId` | `string?` | Target subscription | `null` |
| `AffectedResources` | `List<string>` | Resource IDs affected | `[]` |
| `AffectedControls` | `List<string>` | Control IDs affected | `[]` |
| `Outcome` | `AuditOutcome` | Result of the action | `Success` |
| `Details` | `string` | Additional context/ error details | `""` |
| `Duration` | `TimeSpan?` | How long the action took | `null` |

**Validation rules**:
- `UserId` MUST be non-empty.
- `Action` MUST be one of: `Assessment`, `Remediation`, `EvidenceCollection`,
  `DocumentGeneration`, `ConfigurationChange`, `ControlQuery`, `StatusQuery`,
  `AuditLogQuery`.
- `Timestamp` MUST be UTC.

```csharp
public enum AuditOutcome
{
    Success,
    Failure,
    Partial,
    Denied
}
```

---

## EF Core Entity Updates

The existing `AtoCopilotContext` needs these additions:

```csharp
// New DbSets
public DbSet<ComplianceDocumentEntity> Documents => Set<ComplianceDocumentEntity>();
public DbSet<NistControlEntity> NistControls => Set<NistControlEntity>();
public DbSet<AuditLogEntryEntity> AuditLogs => Set<AuditLogEntryEntity>();
public DbSet<RemediationPlanEntity> RemediationPlans => Set<RemediationPlanEntity>();
```

Existing `ComplianceAssessmentEntity` and `ComplianceFindingEntity` need the new columns
from extensions above. `ComplianceEvidenceEntity` needs new columns.

### EF Core Value Conversions

`List<string>` properties MUST use a value converter to persist as JSON in both SQLite and
SQL Server. Apply in `OnModelCreating`:

```csharp
var stringListConverter = new ValueConverter<List<string>, string>(
    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new()
);

// Apply to all List<string> properties:
// AuditLogEntry.AffectedResources, AuditLogEntry.AffectedControls
// NistControl.Baselines, NistControl.AzurePolicyDefinitionIds
// RemediationPlan.AffectedControls (if added)
```

### Owned Entity Types

- `RemediationStep` MUST be configured as an **owned collection** of `RemediationPlan`
  using `OwnsMany<RemediationStep>()`. Steps are stored in a separate table
  (`RemediationSteps`) with an implicit FK to `RemediationPlan.Id`.
- `ScanSummary` MUST be configured as an **owned type** of `ComplianceAssessment`
  using `OwnsOne<ScanSummary>()` for both `ResourceScanSummary` and `PolicyScanSummary`.
  These are stored as columns in the `ComplianceAssessments` table.
- `DocumentMetadata` MUST be configured as an **owned type** of `ComplianceDocument`
  using `OwnsOne<DocumentMetadata>()`.

### Database Indexes

Apply the following indexes for query performance:

```csharp
// ComplianceAssessment
modelBuilder.Entity<ComplianceAssessment>()
    .HasIndex(a => a.SubscriptionId);
modelBuilder.Entity<ComplianceAssessment>()
    .HasIndex(a => a.AssessedAt);
modelBuilder.Entity<ComplianceAssessment>()
    .HasIndex(a => new { a.SubscriptionId, a.Framework });

// ComplianceFinding
modelBuilder.Entity<ComplianceFinding>()
    .HasIndex(f => f.ControlId);
modelBuilder.Entity<ComplianceFinding>()
    .HasIndex(f => f.AssessmentId);
modelBuilder.Entity<ComplianceFinding>()
    .HasIndex(f => f.ControlFamily);
modelBuilder.Entity<ComplianceFinding>()
    .HasIndex(f => new { f.AssessmentId, f.Severity });

// NistControl
modelBuilder.Entity<NistControl>()
    .HasIndex(c => c.Family);
modelBuilder.Entity<NistControl>()
    .HasIndex(c => c.ParentControlId);

// AuditLogEntry
modelBuilder.Entity<AuditLogEntry>()
    .HasIndex(a => a.Timestamp);
modelBuilder.Entity<AuditLogEntry>()
    .HasIndex(a => a.SubscriptionId);
modelBuilder.Entity<AuditLogEntry>()
    .HasIndex(a => new { a.UserId, a.Timestamp });

// ComplianceEvidence
modelBuilder.Entity<ComplianceEvidence>()
    .HasIndex(e => e.ControlId);
modelBuilder.Entity<ComplianceEvidence>()
    .HasIndex(e => e.AssessmentId);
```

### Cascade Delete Behavior

| Parent → Child | Behavior | Rationale |
|---------------|----------|-----------|
| Assessment → Findings | `Cascade` | Findings belong to assessment |
| Assessment → Evidence | `SetNull` | Evidence may outlive assessment |
| Assessment → Documents | `SetNull` | Documents may outlive assessment |
| RemediationPlan → RemediationSteps | `Cascade` (owned) | Steps are owned by plan |
| NistControl → NistControl (enhancements) | `Cascade` | Enhancements belong to parent |
| NistControl → ComplianceFinding | `Restrict` | Findings must not be orphaned silently |

### NistControl Seed Data Strategy

- At startup, `INistControlsService` loads the NIST catalog (online or offline per FR-017).
- The parsed controls are **upserted** into the `NistControls` table using
  `AddOrUpdate` semantics (match on `Id`).
- This is NOT an EF Core migration seed — it is application-level seed via `IHostedService`
  running after migrations complete.
- The `NistControl.ControlEnhancements` self-referential relationship has a **depth limit
  of 2** (base control → enhancement → sub-enhancement). OSCAL data does not exceed this.

### Database Connection Resiliency

- **SQL Server**: Use `EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null)` in `UseSqlServer()` options.
- **SQLite**: No retry needed (local file, no transient network failures).
- Both providers MUST apply `CommandTimeout(30)` for individual queries.
- The startup `MigrateAsync()` call MUST have its own `CancellationTokenSource` with 30-second timeout.

---

## Relationships Summary

| Parent | Child | Cardinality | FK |
|--------|-------|-------------|-----|
| ComplianceAssessment | ComplianceFinding | 1:N | `Finding.AssessmentId` |
| ComplianceAssessment | ComplianceEvidence | 1:N | `Evidence.AssessmentId` |
| ComplianceAssessment | ComplianceDocument | 1:N | `Document.AssessmentId` |
| RemediationPlan | RemediationStep | 1:N | (owned collection) |
| NistControl | NistControl (enhancements) | 1:N | `Control.ParentControlId` |
| NistControl | ComplianceFinding | 1:N | `Finding.ControlId` |

---

## Constants

### Control Families (20)

```csharp
public static class ControlFamilies
{
    public const string AccessControl = "AC";
    public const string AwarenessTraining = "AT";
    public const string AuditAccountability = "AU";
    public const string AssessmentAuthorization = "CA";
    public const string ConfigurationManagement = "CM";
    public const string ContingencyPlanning = "CP";
    public const string IdentificationAuthentication = "IA";
    public const string IncidentResponse = "IR";
    public const string Maintenance = "MA";
    public const string MediaProtection = "MP";
    public const string PhysicalEnvironmental = "PE";
    public const string Planning = "PL";
    public const string ProgramManagement = "PM";
    public const string PersonnelSecurity = "PS";
    public const string PiiProcessing = "PT";
    public const string RiskAssessment = "RA";
    public const string SystemServicesAcquisition = "SA";
    public const string SystemCommunications = "SC";
    public const string SystemInformationIntegrity = "SI";
    public const string SupplyChainRisk = "SR";

    public static readonly HashSet<string> HighRiskFamilies = new() { "AC", "IA", "SC" };
}
```

### Compliance Frameworks

```csharp
public static class ComplianceFrameworks
{
    public const string Nist80053 = "NIST80053";
    public const string FedRampHigh = "FedRAMPHigh";
    public const string FedRampModerate = "FedRAMPModerate";
    public const string DoDIL5 = "DoDIL5";
}
```

### User Roles (aligned with existing `ComplianceRoles`)

The existing `ComplianceRoles` has: `Administrator`, `Auditor`, `Analyst`, `Viewer`.
These map to the spec personas:

| Spec Persona | Existing Role | Permissions |
|-------------|---------------|-------------|
| Compliance Officer | `Administrator` | Full access: assess, approve, remediate, generate docs, view all history |
| Platform Engineer | `Analyst` | Assess, execute remediations (not approve), collect evidence, view own history |
| Auditor | `Auditor` | Read-only: view assessments, evidence, documents, audit logs |
