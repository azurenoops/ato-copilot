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
/// Enhanced with XCCDF fields for full DISA STIG fidelity (Feature 015 – US4).
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
/// <param name="StigVersion">XCCDF version string (e.g., "WN22-AU-000010").</param>
/// <param name="BenchmarkId">Parent benchmark identifier (e.g., "Windows_Server_2022_STIG").</param>
/// <param name="Responsibility">Who is responsible (e.g., "System Administrator", "IA Officer").</param>
/// <param name="Documentable">Whether a documented exception is acceptable.</param>
/// <param name="MitigationGuidance">Possible mitigations for the finding.</param>
/// <param name="Weight">Rule weight (XCCDF default 10.0).</param>
/// <param name="SeverityOverrideGuidance">Guidance for overriding severity.</param>
/// <param name="ReleaseDate">STIG release date.</param>
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
    string ServiceType,
    // ── XCCDF fields (Feature 015 – US4, T058) ──
    string? StigVersion = null,
    string? BenchmarkId = null,
    string? Responsibility = null,
    bool Documentable = false,
    string? MitigationGuidance = null,
    decimal Weight = 10.0m,
    string? SeverityOverrideGuidance = null,
    DateTime? ReleaseDate = null);

// ───────────────────────────── STIG Benchmark (Feature 015 – US4, T059) ─────────────────────────────

/// <summary>
/// Represents a DISA STIG benchmark (technology-specific rule set).
/// Contains metadata about a published STIG along with rule statistics and applicable platforms.
/// </summary>
/// <param name="BenchmarkId">Benchmark identifier (e.g., "Windows_Server_2022_STIG").</param>
/// <param name="Title">Human-readable title (e.g., "Microsoft Windows Server 2022 STIG").</param>
/// <param name="Version">Release version (e.g., "V2R1").</param>
/// <param name="ReleaseDate">Publication date.</param>
/// <param name="Publisher">Publishing authority (typically "DISA").</param>
/// <param name="RuleCount">Total number of rules in the benchmark.</param>
/// <param name="CatICount">Count of CAT I (High severity) rules.</param>
/// <param name="CatIICount">Count of CAT II (Medium severity) rules.</param>
/// <param name="CatIIICount">Count of CAT III (Low severity) rules.</param>
/// <param name="ApplicablePlatforms">Platforms this benchmark targets.</param>
/// <param name="Rules">STIG rules contained in this benchmark.</param>
public record StigBenchmark(
    string BenchmarkId,
    string Title,
    string Version,
    DateTime ReleaseDate,
    string Publisher,
    int RuleCount,
    int CatICount,
    int CatIICount,
    int CatIIICount,
    List<string> ApplicablePlatforms,
    List<StigControl> Rules);

// ───────────────────────────── CCI Mapping (Feature 015 – US4, T060) ─────────────────────────────

/// <summary>
/// Represents a single CCI (Control Correlation Identifier) to NIST 800-53 control mapping.
/// CCIs are maintained by DISA and serve as the bridge between STIGs and NIST controls.
/// </summary>
/// <param name="CciId">CCI identifier (e.g., "CCI-000130").</param>
/// <param name="NistControlId">Mapped NIST 800-53 control ID (e.g., "AU-3").</param>
/// <param name="Definition">Human-readable definition of what this CCI requires.</param>
/// <param name="Status">Publication status: "published", "draft", or "deprecated".</param>
public record CciMapping(
    string CciId,
    string NistControlId,
    string Definition,
    string Status = "published");

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
