namespace Ato.Copilot.Core.Models.Compliance;

/// <summary>
/// Represents a compliance assessment result
/// </summary>
public class ComplianceAssessment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SubscriptionId { get; set; } = string.Empty;
    public string Framework { get; set; } = "NIST80053";
    public string ScanType { get; set; } = "quick";
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
    public double ComplianceScore { get; set; }
    public int TotalControls { get; set; }
    public int PassedControls { get; set; }
    public int FailedControls { get; set; }
    public int NotAssessedControls { get; set; }
    public List<ComplianceFinding> Findings { get; set; } = new();
}

/// <summary>
/// Represents a compliance finding (violation or observation)
/// </summary>
public class ComplianceFinding
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ControlId { get; set; } = string.Empty;
    public string ControlFamily { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public FindingSeverity Severity { get; set; }
    public FindingStatus Status { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string RemediationGuidance { get; set; } = string.Empty;
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
    public string? RemediationScript { get; set; }
    public bool AutoRemediable { get; set; }
    public string Source { get; set; } = string.Empty;
}

public enum FindingSeverity
{
    Critical,
    High,
    Medium,
    Low,
    Informational
}

public enum FindingStatus
{
    Open,
    InProgress,
    Remediated,
    Accepted,
    FalsePositive
}

/// <summary>
/// Represents a NIST 800-53 control
/// </summary>
public class NistControl
{
    public string Id { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImpactLevel { get; set; } = string.Empty;
    public List<string> Enhancements { get; set; } = new();
    public string AzureImplementation { get; set; } = string.Empty;
}

/// <summary>
/// Remediation plan for compliance findings
/// </summary>
public class RemediationPlan
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SubscriptionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<RemediationStep> Steps { get; set; } = new();
    public int TotalFindings { get; set; }
    public int AutoRemediableCount { get; set; }
}

public class RemediationStep
{
    public string FindingId { get; set; } = string.Empty;
    public string ControlId { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Script { get; set; } = string.Empty;
    public string Effort { get; set; } = string.Empty;
    public bool AutoRemediable { get; set; }
}

/// <summary>
/// Evidence collected for compliance controls
/// </summary>
public class ComplianceEvidence
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ControlId { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string EvidenceType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
    public string CollectedBy { get; set; } = string.Empty;
}

/// <summary>
/// Generated compliance document (SSP, POA&M, SAR)
/// </summary>
public class ComplianceDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DocumentType { get; set; } = string.Empty;
    public string SystemName { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
