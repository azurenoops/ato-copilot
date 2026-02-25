using System.Text.Json.Serialization;

namespace Ato.Copilot.Core.Models.Compliance;

// ───────────────────────────── STIG Entities (Feature 010) ─────────────────────────────

/// <summary>
/// Severity level for a STIG finding, correlating to DoD Categories.
/// High = CAT I, Medium = CAT II, Low = CAT III.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StigSeverity
{
    /// <summary>CAT I — Critical vulnerability, immediate risk.</summary>
    High,
    /// <summary>CAT II — Significant vulnerability, moderate risk.</summary>
    Medium,
    /// <summary>CAT III — Minor vulnerability, low risk.</summary>
    Low
}

/// <summary>
/// Represents a STIG finding from curated JSON data.
/// Contains severity, check/fix procedures, NIST mappings, and Azure implementation guidance.
/// </summary>
/// <param name="StigId">STIG identifier (e.g., "V-12345").</param>
/// <param name="VulnId">Vulnerability ID.</param>
/// <param name="RuleId">Rule identifier.</param>
/// <param name="Title">Short title of the finding.</param>
/// <param name="Description">Full description of the vulnerability.</param>
/// <param name="Severity">Severity level (High/Medium/Low → CAT I/II/III).</param>
/// <param name="Category">STIG category grouping.</param>
/// <param name="StigFamily">Parent STIG family.</param>
/// <param name="NistControls">Mapped NIST 800-53 control IDs.</param>
/// <param name="CciRefs">Control Correlation Identifier references.</param>
/// <param name="CheckText">Verification/check procedure.</param>
/// <param name="FixText">Remediation steps.</param>
/// <param name="AzureImplementation">Azure-specific guidance keyed by aspect (Service, Configuration, Policy, Automation).</param>
/// <param name="ServiceType">Azure service type applicable to this STIG.</param>
public record StigControl(
    string StigId,
    string VulnId,
    string RuleId,
    string Title,
    string Description,
    StigSeverity Severity,
    string Category,
    string StigFamily,
    List<string> NistControls,
    List<string> CciRefs,
    string CheckText,
    string FixText,
    Dictionary<string, string> AzureImplementation,
    string ServiceType);

/// <summary>
/// Cross-reference between a STIG finding and its related NIST controls and DoD instructions.
/// </summary>
/// <param name="StigId">STIG identifier.</param>
/// <param name="Stig">Full STIG control record.</param>
/// <param name="NistControlIds">Mapped NIST control IDs.</param>
/// <param name="RelatedInstructions">Related DoD instructions.</param>
public record StigCrossReference(
    string StigId,
    StigControl Stig,
    List<string> NistControlIds,
    List<DoDInstruction> RelatedInstructions);
