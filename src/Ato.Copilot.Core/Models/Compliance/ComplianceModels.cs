namespace Ato.Copilot.Core.Models.Compliance;

// ───────────────────────────────────────────── Enums ─────────────────────────────────────────────

/// <summary>
/// Severity level of a compliance finding, ordered from most to least critical.
/// </summary>
public enum FindingSeverity
{
    /// <summary>Immediate action required — active exploitation or total control failure.</summary>
    Critical,
    /// <summary>Significant risk — must be addressed within remediation window.</summary>
    High,
    /// <summary>Moderate risk — should be addressed in next remediation cycle.</summary>
    Medium,
    /// <summary>Minor risk — address when convenient.</summary>
    Low,
    /// <summary>No risk — informational observation only.</summary>
    Informational
}

/// <summary>
/// Status of a compliance finding through its lifecycle.
/// </summary>
public enum FindingStatus
{
    /// <summary>Finding is open and unaddressed.</summary>
    Open,
    /// <summary>Remediation is underway.</summary>
    InProgress,
    /// <summary>Finding has been remediated and validated.</summary>
    Remediated,
    /// <summary>Finding accepted as-is with documented risk acceptance.</summary>
    Accepted,
    /// <summary>Finding determined to be a false positive.</summary>
    FalsePositive
}

/// <summary>
/// Assessment lifecycle state. See data-model.md for state transition diagram.
/// Transitions: Pending → InProgress → Completed | Failed | Cancelled
/// </summary>
public enum AssessmentStatus
{
    /// <summary>Assessment created but not yet started.</summary>
    Pending,
    /// <summary>Assessment is currently running scans.</summary>
    InProgress,
    /// <summary>Assessment completed successfully.</summary>
    Completed,
    /// <summary>Assessment failed due to an error.</summary>
    Failed,
    /// <summary>Assessment was cancelled by user or timeout.</summary>
    Cancelled
}

/// <summary>
/// Identifies which scan source discovered a finding.
/// </summary>
public enum ScanSourceType
{
    /// <summary>Azure Resource Graph query.</summary>
    Resource,
    /// <summary>Azure Policy compliance state.</summary>
    Policy,
    /// <summary>Microsoft Defender for Cloud recommendation.</summary>
    Defender,
    /// <summary>Correlated from multiple scan sources.</summary>
    Combined
}

/// <summary>
/// Type of remediation action required for a finding.
/// </summary>
public enum RemediationType
{
    /// <summary>Remediation type not yet determined.</summary>
    Unknown,
    /// <summary>Direct resource configuration change (e.g., enable encryption).</summary>
    ResourceConfiguration,
    /// <summary>Azure Policy assignment or enforcement mode change.</summary>
    PolicyAssignment,
    /// <summary>Azure Policy remediation task (deploy-if-not-exists).</summary>
    PolicyRemediation,
    /// <summary>Requires manual intervention — no automated fix available.</summary>
    Manual
}

/// <summary>
/// Risk level for a finding's control family. AC, IA, SC are high-risk
/// because changes can impact user access and security boundaries.
/// </summary>
public enum RiskLevel
{
    /// <summary>Standard risk — normal remediation workflow.</summary>
    Standard,
    /// <summary>High risk (AC, IA, SC families) — requires additional approval.</summary>
    High
}

/// <summary>
/// Category of compliance evidence artifact.
/// </summary>
public enum EvidenceCategory
{
    /// <summary>Resource configuration export.</summary>
    Configuration,
    /// <summary>Azure Policy compliance snapshot.</summary>
    PolicyCompliance,
    /// <summary>Azure Resource Graph resource state.</summary>
    ResourceCompliance,
    /// <summary>Defender for Cloud security assessment.</summary>
    SecurityAssessment,
    /// <summary>Azure Activity Log entries.</summary>
    ActivityLog,
    /// <summary>Resource inventory listing.</summary>
    Inventory
}

/// <summary>
/// Remediation plan lifecycle state.
/// Transitions: Planned → Approved → InProgress → Completed | PartiallyCompleted | Failed
///              Planned → Rejected
/// </summary>
public enum RemediationStatus
{
    /// <summary>Plan created but not yet approved.</summary>
    Planned,
    /// <summary>Plan approved by ComplianceOfficer.</summary>
    Approved,
    /// <summary>Remediation steps are executing.</summary>
    InProgress,
    /// <summary>All steps completed successfully.</summary>
    Completed,
    /// <summary>Some steps succeeded, others failed.</summary>
    PartiallyCompleted,
    /// <summary>Remediation failed (see FailureReason).</summary>
    Failed,
    /// <summary>Plan rejected by ComplianceOfficer.</summary>
    Rejected
}

/// <summary>
/// Individual remediation step execution state.
/// Transitions: Pending → InProgress → Completed | Failed
///              Pending → Skipped
/// </summary>
public enum StepStatus
{
    /// <summary>Step not yet started.</summary>
    Pending,
    /// <summary>Step currently executing.</summary>
    InProgress,
    /// <summary>Step completed successfully.</summary>
    Completed,
    /// <summary>Step failed during execution.</summary>
    Failed,
    /// <summary>Step skipped (e.g., batch stop-on-failure).</summary>
    Skipped
}

/// <summary>
/// Outcome of an audited action, captured in AuditLogEntry.
/// </summary>
public enum AuditOutcome
{
    /// <summary>Action completed successfully.</summary>
    Success,
    /// <summary>Action failed with an error.</summary>
    Failure,
    /// <summary>Action partially succeeded (e.g., partial scan).</summary>
    Partial,
    /// <summary>Action denied by RBAC authorization.</summary>
    Denied
}

// ──────────────────────────────────────── Value Types ────────────────────────────────────────────

/// <summary>
/// Summary statistics for a single scan source (resource or policy).
/// Configured as an EF Core owned type of ComplianceAssessment.
/// </summary>
public class ScanSummary
{
    /// <summary>Number of Azure resources scanned.</summary>
    public int ResourcesScanned { get; set; }

    /// <summary>Number of policies evaluated (policy scan only).</summary>
    public int PoliciesEvaluated { get; set; }

    /// <summary>Number of compliant items found.</summary>
    public int Compliant { get; set; }

    /// <summary>Number of non-compliant items found.</summary>
    public int NonCompliant { get; set; }

    /// <summary>Compliance percentage (0.0 to 100.0).</summary>
    public double CompliancePercentage { get; set; }
}

/// <summary>
/// Additional metadata for generated compliance documents (SSP, SAR, POA&M).
/// Configured as an EF Core owned type of ComplianceDocument.
/// </summary>
public class DocumentMetadata
{
    /// <summary>System description for the document header.</summary>
    public string SystemDescription { get; set; } = string.Empty;

    /// <summary>Authorization boundary description.</summary>
    public string AuthorizationBoundary { get; set; } = string.Empty;

    /// <summary>Date range covered by this document.</summary>
    public string DateRange { get; set; } = string.Empty;

    /// <summary>Name of the person who prepared the document.</summary>
    public string PreparedBy { get; set; } = string.Empty;

    /// <summary>Name of the person who approved the document.</summary>
    public string ApprovedBy { get; set; } = string.Empty;
}

// ──────────────────────────────────────── Entities ───────────────────────────────────────────────

/// <summary>
/// Represents a compliance assessment result containing scan findings and statistics.
/// Extends existing entity with assessment lifecycle, baseline, progress, and scan summaries.
/// </summary>
public class ComplianceAssessment
{
    /// <summary>Unique assessment identifier (GUID string).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Azure subscription ID (must be valid GUID format).</summary>
    public string SubscriptionId { get; set; } = string.Empty;

    /// <summary>Compliance framework (NIST80053, FedRAMPHigh, FedRAMPModerate, DoDIL5).</summary>
    public string Framework { get; set; } = "NIST80053";

    /// <summary>FedRAMP baseline level (High, Moderate, Low).</summary>
    public string Baseline { get; set; } = "High";

    /// <summary>Scan type: resource, policy, or combined.</summary>
    public string ScanType { get; set; } = "combined";

    /// <summary>Assessment lifecycle state.</summary>
    public AssessmentStatus Status { get; set; } = AssessmentStatus.Pending;

    /// <summary>User or role who initiated the assessment.</summary>
    public string InitiatedBy { get; set; } = string.Empty;

    /// <summary>UTC timestamp when assessment was initiated.</summary>
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when assessment completed (null if still in progress).</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Last progress update message for real-time feedback.</summary>
    public string ProgressMessage { get; set; } = string.Empty;

    /// <summary>Overall compliance score (0.0 to 100.0).</summary>
    public double ComplianceScore { get; set; }

    /// <summary>Total number of controls evaluated.</summary>
    public int TotalControls { get; set; }

    /// <summary>Number of controls that passed.</summary>
    public int PassedControls { get; set; }

    /// <summary>Number of controls that failed.</summary>
    public int FailedControls { get; set; }

    /// <summary>Number of controls not assessed (e.g., no data available).</summary>
    public int NotAssessedControls { get; set; }

    /// <summary>Resource Graph scan statistics. EF Core owned type (columns in Assessments table).</summary>
    public ScanSummary? ResourceScanSummary { get; set; }

    /// <summary>Policy compliance scan statistics. EF Core owned type (columns in Assessments table).</summary>
    public ScanSummary? PolicyScanSummary { get; set; }

    /// <summary>Findings discovered during this assessment.</summary>
    public List<ComplianceFinding> Findings { get; set; } = new();
}

/// <summary>
/// Represents a compliance finding (violation or observation) linked to a specific
/// NIST 800-53 control and Azure resource.
/// </summary>
public class ComplianceFinding
{
    /// <summary>Unique finding identifier (GUID string).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>NIST control ID (e.g., "AC-2", "AC-2.1"). Must match ^[A-Z]{2}-\d+(\.\d+)?$.</summary>
    public string ControlId { get; set; } = string.Empty;

    /// <summary>Two-letter NIST control family abbreviation (e.g., "AC", "AU").</summary>
    public string ControlFamily { get; set; } = string.Empty;

    /// <summary>Brief human-readable title of the finding.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Detailed description of the compliance gap.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Finding severity level.</summary>
    public FindingSeverity Severity { get; set; }

    /// <summary>Current finding lifecycle status.</summary>
    public FindingStatus Status { get; set; }

    /// <summary>Azure resource ID affected by this finding.</summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>Azure resource type (e.g., "Microsoft.Storage/storageAccounts").</summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>Human-readable remediation guidance.</summary>
    public string RemediationGuidance { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the finding was discovered.</summary>
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;

    /// <summary>PowerShell or Azure CLI remediation script, if available.</summary>
    public string? RemediationScript { get; set; }

    /// <summary>Whether this finding can be remediated automatically.</summary>
    public bool AutoRemediable { get; set; }

    /// <summary>Original source of the finding (e.g., "ResourceGraph", "PolicyInsights").</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Which scan source discovered this finding.</summary>
    public ScanSourceType ScanSource { get; set; } = ScanSourceType.Combined;

    /// <summary>Azure Policy definition ID (for policy-based findings).</summary>
    public string? PolicyDefinitionId { get; set; }

    /// <summary>Azure Policy assignment ID (for policy-based findings).</summary>
    public string? PolicyAssignmentId { get; set; }

    /// <summary>Defender for Cloud recommendation ID (for Defender findings).</summary>
    public string? DefenderRecommendationId { get; set; }

    /// <summary>Type of remediation required for this finding.</summary>
    public RemediationType RemediationType { get; set; } = RemediationType.Unknown;

    /// <summary>Risk level based on control family (AC, IA, SC = High).</summary>
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Standard;

    /// <summary>Foreign key to parent ComplianceAssessment.</summary>
    public string AssessmentId { get; set; } = string.Empty;
}

/// <summary>
/// Represents a NIST 800-53 Rev 5 control loaded from the OSCAL catalog.
/// Supports self-referential enhancements with depth limit of 2.
/// </summary>
public class NistControl
{
    /// <summary>NIST control ID (e.g., "ac-2", "ac-2(1)"). Must match ^[a-z]{2}-\d+(\(\d+\))?$.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Two-letter control family abbreviation (e.g., "AC").</summary>
    public string Family { get; set; } = string.Empty;

    /// <summary>Control title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Full control description text.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Impact level (Low, Moderate, High).</summary>
    public string ImpactLevel { get; set; } = string.Empty;

    /// <summary>Legacy enhancements list (retained for backward compatibility).</summary>
    public List<string> Enhancements { get; set; } = new();

    /// <summary>Azure implementation guidance text.</summary>
    public string AzureImplementation { get; set; } = string.Empty;

    /// <summary>Applicable FedRAMP baselines (High, Moderate, Low). Persisted as JSON.</summary>
    public List<string> Baselines { get; set; } = new();

    /// <summary>FedRAMP-specific parameter values, if any.</summary>
    public string? FedRampParameters { get; set; }

    /// <summary>Mapped Azure Policy definition IDs. Persisted as JSON.</summary>
    public List<string> AzurePolicyDefinitionIds { get; set; } = new();

    /// <summary>Nested control enhancements (self-referential, depth limit 2).</summary>
    public List<NistControl> ControlEnhancements { get; set; } = new();

    /// <summary>Parent control ID for enhancements (null for base controls).</summary>
    public string? ParentControlId { get; set; }

    /// <summary>True if this is an enhancement, not a base control.</summary>
    public bool IsEnhancement { get; set; }
}

/// <summary>
/// Remediation plan for compliance findings. Contains ordered steps
/// with dry-run-by-default behavior and approval workflow.
/// </summary>
public class RemediationPlan
{
    /// <summary>Unique plan identifier (GUID string).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Azure subscription ID targeted for remediation.</summary>
    public string SubscriptionId { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the plan was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Ordered remediation steps (EF Core owned collection in RemediationSteps table).</summary>
    public List<RemediationStep> Steps { get; set; } = new();

    /// <summary>Total number of findings addressed by this plan.</summary>
    public int TotalFindings { get; set; }

    /// <summary>Number of findings that can be auto-remediated.</summary>
    public int AutoRemediableCount { get; set; }

    /// <summary>Plan lifecycle state.</summary>
    public RemediationStatus Status { get; set; } = RemediationStatus.Planned;

    /// <summary>Whether this is a dry-run plan (default true per SEC-018).</summary>
    public bool DryRun { get; set; } = true;

    /// <summary>ComplianceOfficer who approved the plan (null if not yet approved).</summary>
    public string? ApprovedBy { get; set; }

    /// <summary>UTC timestamp when approval was given.</summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>UTC timestamp when remediation completed.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>ID of the step that caused failure, if any.</summary>
    public string? FailedStepId { get; set; }

    /// <summary>Reason for failure, if Status is Failed.</summary>
    public string? FailureReason { get; set; }
}

/// <summary>
/// Individual step within a RemediationPlan. Configured as EF Core
/// owned entity (OwnsMany) with implicit FK to RemediationPlan.Id.
/// </summary>
public class RemediationStep
{
    /// <summary>Unique step identifier (GUID string).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Finding ID this step addresses.</summary>
    public string FindingId { get; set; } = string.Empty;

    /// <summary>NIST control ID related to this step.</summary>
    public string ControlId { get; set; } = string.Empty;

    /// <summary>Execution priority (lower = higher priority).</summary>
    public int Priority { get; set; }

    /// <summary>Human-readable description of the remediation action.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>PowerShell or Azure CLI script to execute.</summary>
    public string Script { get; set; } = string.Empty;

    /// <summary>Estimated effort level (e.g., "Low", "Medium", "High").</summary>
    public string Effort { get; set; } = string.Empty;

    /// <summary>Whether this step can be executed automatically.</summary>
    public bool AutoRemediable { get; set; }

    /// <summary>Type of remediation action for this step.</summary>
    public RemediationType RemediationType { get; set; } = RemediationType.Unknown;

    /// <summary>Step execution status.</summary>
    public StepStatus Status { get; set; } = StepStatus.Pending;

    /// <summary>Resource state captured before the change (JSON snapshot).</summary>
    public string? BeforeState { get; set; }

    /// <summary>Resource state captured after the change (JSON snapshot).</summary>
    public string? AfterState { get; set; }

    /// <summary>UTC timestamp when the step was executed.</summary>
    public DateTime? ExecutedAt { get; set; }

    /// <summary>Azure resource ID being modified.</summary>
    public string? ResourceId { get; set; }

    /// <summary>Risk level based on control family (AC, IA, SC = High).</summary>
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Standard;
}

/// <summary>
/// Evidence collected for compliance controls, with SHA-256 content hash
/// for integrity verification during audit.
/// </summary>
public class ComplianceEvidence
{
    /// <summary>Unique evidence identifier (GUID string).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>NIST control ID this evidence supports.</summary>
    public string ControlId { get; set; } = string.Empty;

    /// <summary>Azure subscription ID the evidence was collected from.</summary>
    public string SubscriptionId { get; set; } = string.Empty;

    /// <summary>Evidence type (ConfigurationExport, PolicySnapshot, ResourceSnapshot, etc.).</summary>
    public string EvidenceType { get; set; } = string.Empty;

    /// <summary>Human-readable description of the evidence artifact.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Evidence content (JSON, text, or other format).</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the evidence was collected.</summary>
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Identity of the user or service that collected the evidence.</summary>
    public string CollectedBy { get; set; } = string.Empty;

    /// <summary>Assessment ID this evidence is linked to (optional).</summary>
    public string? AssessmentId { get; set; }

    /// <summary>Category of evidence artifact.</summary>
    public EvidenceCategory EvidenceCategory { get; set; } = EvidenceCategory.Configuration;

    /// <summary>Specific Azure resource ID this evidence covers.</summary>
    public string? ResourceId { get; set; }

    /// <summary>SHA-256 hash of Content for integrity verification.</summary>
    public string ContentHash { get; set; } = string.Empty;
}

/// <summary>
/// Generated compliance document (SSP, SAR, POA&M) in FedRAMP Markdown format.
/// </summary>
public class ComplianceDocument
{
    /// <summary>Unique document identifier (GUID string).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Document type: SSP, SAR, or POAM.</summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>System name for the document header.</summary>
    public string SystemName { get; set; } = string.Empty;

    /// <summary>Compliance framework this document covers.</summary>
    public string Framework { get; set; } = string.Empty;

    /// <summary>Generated document content (Markdown format).</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the document was generated.</summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Assessment ID this document is based on (optional).</summary>
    public string? AssessmentId { get; set; }

    /// <summary>System owner name.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>User who generated the document.</summary>
    public string GeneratedBy { get; set; } = string.Empty;

    /// <summary>Additional document metadata. EF Core owned type.</summary>
    public DocumentMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Configuration settings stored in IAgentStateManager shared state (NOT EF Core).
/// Thread-safe access via SemaphoreSlim for multi-step operations.
/// </summary>
public class ConfigurationSettings
{
    /// <summary>Default Azure subscription ID (GUID format or null).</summary>
    public string? SubscriptionId { get; set; }

    /// <summary>Default compliance framework.</summary>
    public string Framework { get; set; } = "NIST80053";

    /// <summary>Default FedRAMP baseline level.</summary>
    public string Baseline { get; set; } = "High";

    /// <summary>Azure cloud environment (AzureGovernment or AzureCloud).</summary>
    public string CloudEnvironment { get; set; } = "AzureGovernment";

    /// <summary>Default dry-run preference for remediations.</summary>
    public bool DryRunDefault { get; set; } = true;

    /// <summary>Default scan type (resource, policy, combined).</summary>
    public string DefaultScanType { get; set; } = "combined";

    /// <summary>Preferred Azure region.</summary>
    public string Region { get; set; } = "usgovvirginia";

    /// <summary>UTC timestamp when settings were last changed.</summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Audit log entry for compliance-related actions. Persisted in EF Core.
/// Retention: 730 days per SEC-015.
/// </summary>
public class AuditLogEntry
{
    /// <summary>Unique log entry identifier (GUID string).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Identity of the user who initiated the action (must be non-empty).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Role of the user (Administrator, Auditor, Analyst, Viewer).</summary>
    public string UserRole { get; set; } = string.Empty;

    /// <summary>Action type (Assessment, Remediation, EvidenceCollection, DocumentGeneration, etc.).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Scan type if applicable (resource, policy, combined).</summary>
    public string? ScanType { get; set; }

    /// <summary>UTC timestamp when the action occurred.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Target Azure subscription ID.</summary>
    public string? SubscriptionId { get; set; }

    /// <summary>Azure resource IDs affected by the action. Persisted as JSON.</summary>
    public List<string> AffectedResources { get; set; } = new();

    /// <summary>NIST control IDs affected by the action. Persisted as JSON.</summary>
    public List<string> AffectedControls { get; set; } = new();

    /// <summary>Outcome of the action.</summary>
    public AuditOutcome Outcome { get; set; } = AuditOutcome.Success;

    /// <summary>Additional context or error details.</summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>Duration of the action.</summary>
    public TimeSpan? Duration { get; set; }
}
