// ═══════════════════════════════════════════════════════════════════════════
// Feature 017 — SCAP/STIG Viewer Import: Entities, Enums, and DTOs
// See specs/017-scap-stig-import/data-model.md for full specification.
// ═══════════════════════════════════════════════════════════════════════════

namespace Ato.Copilot.Core.Models.Compliance;

// ─── Enums ───────────────────────────────────────────────────────────────────

/// <summary>
/// Type of scan import file. Determines which parser is used.
/// </summary>
public enum ScanImportType
{
    /// <summary>DISA STIG Viewer checklist (.ckl) — XML format with manual assessments.</summary>
    Ckl,

    /// <summary>SCAP Compliance Checker XCCDF results (.xml) — automated scan output.</summary>
    Xccdf
}

/// <summary>
/// Final status of a scan import operation.
/// </summary>
public enum ScanImportStatus
{
    /// <summary>All entries processed successfully with no warnings.</summary>
    Completed,

    /// <summary>Processed but with unmatched rules or baseline mismatches.</summary>
    CompletedWithWarnings,

    /// <summary>Fatal error — malformed XML, system not found, etc.</summary>
    Failed
}

/// <summary>
/// Strategy for handling duplicate findings during re-import.
/// </summary>
public enum ImportConflictResolution
{
    /// <summary>Keep existing findings, skip duplicates.</summary>
    Skip,

    /// <summary>Replace existing findings with imported data.</summary>
    Overwrite,

    /// <summary>Keep more-recent data, append details if different.</summary>
    Merge
}

/// <summary>
/// Action taken for each individual finding during import.
/// Stored on <see cref="ScanImportFinding"/> for audit trail.
/// </summary>
public enum ImportFindingAction
{
    /// <summary>New <see cref="ComplianceFinding"/> created.</summary>
    Created,

    /// <summary>Existing finding updated via overwrite or merge.</summary>
    Updated,

    /// <summary>Duplicate skipped (skip conflict resolution).</summary>
    Skipped,

    /// <summary>STIG rule not found in curated library.</summary>
    Unmatched,

    /// <summary>CKL Not_Applicable or XCCDF notapplicable — no ComplianceFinding created.</summary>
    NotApplicable,

    /// <summary>CKL Not_Reviewed or XCCDF notchecked — ComplianceFinding created as Open.</summary>
    NotReviewed,

    /// <summary>XCCDF error/unknown result — flagged for manual review, no finding created.</summary>
    Error
}

// ─── Entities ────────────────────────────────────────────────────────────────

/// <summary>
/// Tracks each file import operation. One record per imported file.
/// See data-model.md §ScanImportRecord for field descriptions.
/// </summary>
public class ScanImportRecord
{
    /// <summary>Unique identifier (GUID).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>FK to <see cref="RegisteredSystem"/> this import belongs to.</summary>
    public string RegisteredSystemId { get; set; } = string.Empty;

    /// <summary>FK to <see cref="ComplianceAssessment"/> providing context for findings.</summary>
    public string AssessmentId { get; set; } = string.Empty;

    /// <summary>Type of import file (CKL or XCCDF).</summary>
    public ScanImportType ImportType { get; set; }

    /// <summary>Original file name as uploaded.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of raw file content (before base64 encoding).</summary>
    public string FileHash { get; set; } = string.Empty;

    /// <summary>File size in bytes before base64 encoding.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>STIG benchmark ID (e.g., <c>Windows_Server_2022_STIG</c>).</summary>
    public string? BenchmarkId { get; set; }

    /// <summary>STIG release version.</summary>
    public string? BenchmarkVersion { get; set; }

    /// <summary>STIG benchmark display title.</summary>
    public string? BenchmarkTitle { get; set; }

    /// <summary>Target system hostname from scan.</summary>
    public string? TargetHostName { get; set; }

    /// <summary>Target IP address.</summary>
    public string? TargetIpAddress { get; set; }

    /// <summary>
    /// When the scan was performed (UTC).
    /// XCCDF: from <c>start-time</c> attribute.
    /// CKL: <c>null</c> (CKL format has no scan timestamp; STIG_INFO releaseinfo is the benchmark release date).
    /// </summary>
    public DateTime? ScanTimestamp { get; set; }

    /// <summary>Total rules/VULNs in the file.</summary>
    public int TotalEntries { get; set; }

    /// <summary>Findings with status Open/Fail.</summary>
    public int OpenCount { get; set; }

    /// <summary>Findings with status NotAFinding/Pass.</summary>
    public int PassCount { get; set; }

    /// <summary>Findings with status Not_Applicable.</summary>
    public int NotApplicableCount { get; set; }

    /// <summary>Findings not evaluated (CKL Not_Reviewed / XCCDF notchecked).</summary>
    public int NotReviewedCount { get; set; }

    /// <summary>XCCDF error/unknown results.</summary>
    public int ErrorCount { get; set; }

    /// <summary>Entries skipped due to conflict resolution.</summary>
    public int SkippedCount { get; set; }

    /// <summary>STIG rules not found in curated library.</summary>
    public int UnmatchedCount { get; set; }

    /// <summary>New <see cref="ComplianceFinding"/> records created.</summary>
    public int FindingsCreated { get; set; }

    /// <summary>Existing findings updated (overwrite/merge).</summary>
    public int FindingsUpdated { get; set; }

    /// <summary>New <see cref="ControlEffectiveness"/> records created.</summary>
    public int EffectivenessRecordsCreated { get; set; }

    /// <summary>Existing effectiveness records updated.</summary>
    public int EffectivenessRecordsUpdated { get; set; }

    /// <summary>Unique NIST 800-53 controls touched by this import.</summary>
    public int NistControlsAffected { get; set; }

    /// <summary>Conflict resolution strategy applied during this import.</summary>
    public ImportConflictResolution ConflictResolution { get; set; }

    /// <summary>Whether this was a preview-only import (no DB writes).</summary>
    public bool IsDryRun { get; set; }

    /// <summary>XCCDF compliance score (null for CKL imports).</summary>
    public decimal? XccdfScore { get; set; }

    /// <summary>Final import status.</summary>
    public ScanImportStatus ImportStatus { get; set; }

    /// <summary>Error details if import failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Non-fatal warnings (unmatched rules, baseline mismatches). Stored as JSON column.</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>Identity of the user who performed the import.</summary>
    public string ImportedBy { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the import was performed.</summary>
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Per-finding audit trail for each import. Links the raw parsed data
/// to the resulting <see cref="ComplianceFinding"/>.
/// See data-model.md §ScanImportFinding for field descriptions.
/// </summary>
public class ScanImportFinding
{
    /// <summary>Unique identifier (GUID).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>FK to parent <see cref="ScanImportRecord"/>.</summary>
    public string ScanImportRecordId { get; set; } = string.Empty;

    /// <summary>STIG Vulnerability ID (e.g., <c>V-254239</c>).</summary>
    public string VulnId { get; set; } = string.Empty;

    /// <summary>STIG Rule ID (e.g., <c>SV-254239r849090_rule</c>).</summary>
    public string? RuleId { get; set; }

    /// <summary>Rule version (e.g., <c>WN22-AU-000010</c>).</summary>
    public string? StigVersion { get; set; }

    /// <summary>Original CKL STATUS or XCCDF result value.</summary>
    public string RawStatus { get; set; } = string.Empty;

    /// <summary>Original severity text (<c>high</c>/<c>medium</c>/<c>low</c>).</summary>
    public string RawSeverity { get; set; } = string.Empty;

    /// <summary>Resolved CAT severity (null if severity could not be mapped).</summary>
    public CatSeverity? MappedSeverity { get; set; }

    /// <summary>CKL FINDING_DETAILS or XCCDF message.</summary>
    public string? FindingDetails { get; set; }

    /// <summary>CKL COMMENTS field.</summary>
    public string? Comments { get; set; }

    /// <summary>CKL SEVERITY_OVERRIDE value.</summary>
    public string? SeverityOverride { get; set; }

    /// <summary>CKL SEVERITY_JUSTIFICATION value.</summary>
    public string? SeverityJustification { get; set; }

    /// <summary>Matched <c>StigControl.StigId</c> (null if unmatched).</summary>
    public string? ResolvedStigControlId { get; set; }

    /// <summary>NIST 800-53 control IDs resolved via CCI chain. Stored as JSON column.</summary>
    public List<string> ResolvedNistControlIds { get; set; } = new();

    /// <summary>CCI references from the CKL/XCCDF entry. Stored as JSON column.</summary>
    public List<string> ResolvedCciRefs { get; set; } = new();

    /// <summary>Action taken for this finding during import.</summary>
    public ImportFindingAction ImportAction { get; set; }

    /// <summary>FK to created/updated <see cref="ComplianceFinding"/> (null if skipped/unmatched).</summary>
    public string? ComplianceFindingId { get; set; }
}

// ─── DTOs (Not Persisted) ────────────────────────────────────────────────────

/// <summary>
/// Intermediate DTO from CKL parser — a single VULN entry.
/// Not stored in database.
/// </summary>
/// <param name="VulnId">STIG Vulnerability ID (e.g., <c>V-254239</c>).</param>
/// <param name="RuleId">STIG Rule ID (e.g., <c>SV-254239r849090_rule</c>).</param>
/// <param name="StigVersion">Rule version (e.g., <c>WN22-AU-000010</c>).</param>
/// <param name="RuleTitle">Descriptive title of the STIG rule.</param>
/// <param name="Severity">Severity string: <c>high</c>, <c>medium</c>, or <c>low</c>.</param>
/// <param name="Status">CKL status: <c>Open</c>, <c>NotAFinding</c>, <c>Not_Applicable</c>, <c>Not_Reviewed</c>.</param>
/// <param name="FindingDetails">CKL FINDING_DETAILS field.</param>
/// <param name="Comments">CKL COMMENTS field.</param>
/// <param name="SeverityOverride">CKL SEVERITY_OVERRIDE value.</param>
/// <param name="SeverityJustification">CKL SEVERITY_JUSTIFICATION value.</param>
/// <param name="CciRefs">CCI references from the CKL entry.</param>
/// <param name="GroupTitle">SRG reference (group title).</param>
public record ParsedCklEntry(
    string VulnId,
    string? RuleId,
    string? StigVersion,
    string? RuleTitle,
    string Severity,
    string Status,
    string? FindingDetails,
    string? Comments,
    string? SeverityOverride,
    string? SeverityJustification,
    List<string> CciRefs,
    string? GroupTitle);

/// <summary>
/// Top-level CKL parse result containing asset info, STIG metadata, and all VULN entries.
/// </summary>
/// <param name="Asset">Parsed ASSET section.</param>
/// <param name="StigInfo">Parsed STIG_INFO section.</param>
/// <param name="Entries">All parsed VULN entries.</param>
public record ParsedCklFile(
    CklAssetInfo Asset,
    CklStigInfo StigInfo,
    List<ParsedCklEntry> Entries);

/// <summary>
/// CKL ASSET section — target system identification.
/// </summary>
/// <param name="HostName">Target hostname.</param>
/// <param name="HostIp">Target IP address.</param>
/// <param name="HostFqdn">Fully-qualified domain name.</param>
/// <param name="HostMac">MAC address.</param>
/// <param name="AssetType">Asset type (e.g., <c>Computing</c>).</param>
/// <param name="TargetKey">DISA target key.</param>
public record CklAssetInfo(
    string? HostName,
    string? HostIp,
    string? HostFqdn,
    string? HostMac,
    string? AssetType,
    string? TargetKey);

/// <summary>
/// CKL STIG_INFO section — benchmark metadata.
/// </summary>
/// <param name="StigId">Benchmark ID (e.g., <c>Windows_Server_2022_STIG</c>).</param>
/// <param name="Version">STIG release version.</param>
/// <param name="ReleaseInfo">Release info string (benchmark release date, not scan date).</param>
/// <param name="Title">Benchmark display title.</param>
public record CklStigInfo(
    string? StigId,
    string? Version,
    string? ReleaseInfo,
    string? Title);

/// <summary>
/// Intermediate DTO from XCCDF parser — a single rule-result.
/// Not stored in database.
/// </summary>
/// <param name="RuleIdRef">Full XCCDF idref string.</param>
/// <param name="ExtractedRuleId">Extracted <c>SV-XXXXX</c> portion from the XCCDF idref.</param>
/// <param name="Result">XCCDF result: <c>pass</c>, <c>fail</c>, <c>error</c>, <c>notapplicable</c>, <c>notchecked</c>, etc.</param>
/// <param name="Severity">Severity string from the rule-result.</param>
/// <param name="Weight">Rule weight.</param>
/// <param name="Timestamp">Result timestamp (if available).</param>
/// <param name="Message">XCCDF message element content.</param>
/// <param name="CheckRef">OVAL check reference.</param>
public record ParsedXccdfResult(
    string RuleIdRef,
    string ExtractedRuleId,
    string Result,
    string Severity,
    decimal Weight,
    DateTime? Timestamp,
    string? Message,
    string? CheckRef);

/// <summary>
/// Top-level XCCDF parse result containing benchmark/target info, scores, and all rule-results.
/// </summary>
/// <param name="BenchmarkHref">Benchmark reference URI.</param>
/// <param name="Title">Test result title.</param>
/// <param name="Target">Target system identifier.</param>
/// <param name="TargetAddress">Target IP or hostname.</param>
/// <param name="StartTime">Scan start time (UTC).</param>
/// <param name="EndTime">Scan end time (UTC).</param>
/// <param name="Score">Achieved compliance score.</param>
/// <param name="MaxScore">Maximum possible score.</param>
/// <param name="TargetFacts">Additional target facts (OS, FQDN, etc.).</param>
/// <param name="Results">All parsed rule-results.</param>
public record ParsedXccdfFile(
    string? BenchmarkHref,
    string? Title,
    string? Target,
    string? TargetAddress,
    DateTime? StartTime,
    DateTime? EndTime,
    decimal? Score,
    decimal? MaxScore,
    Dictionary<string, string> TargetFacts,
    List<ParsedXccdfResult> Results);

/// <summary>
/// Return value from import operations. Contains all counts, warnings, and unmatched rule details.
/// </summary>
/// <param name="ImportRecordId">ID of the created <see cref="ScanImportRecord"/>.</param>
/// <param name="Status">Final import status.</param>
/// <param name="BenchmarkId">Detected benchmark ID.</param>
/// <param name="BenchmarkTitle">Detected benchmark title.</param>
/// <param name="TotalEntries">Total rules/VULNs in the file.</param>
/// <param name="OpenCount">Findings with status Open/Fail.</param>
/// <param name="PassCount">Findings with status NotAFinding/Pass.</param>
/// <param name="NotApplicableCount">Findings marked Not_Applicable.</param>
/// <param name="NotReviewedCount">Findings not evaluated.</param>
/// <param name="ErrorCount">XCCDF error/unknown results.</param>
/// <param name="SkippedCount">Entries skipped due to conflict resolution.</param>
/// <param name="UnmatchedCount">STIG rules not found in curated library.</param>
/// <param name="FindingsCreated">New ComplianceFinding records created.</param>
/// <param name="FindingsUpdated">Existing findings updated.</param>
/// <param name="EffectivenessRecordsCreated">New ControlEffectiveness records created.</param>
/// <param name="EffectivenessRecordsUpdated">Existing effectiveness records updated.</param>
/// <param name="NistControlsAffected">Unique NIST controls touched.</param>
/// <param name="Warnings">Non-fatal warning messages.</param>
/// <param name="UnmatchedRules">Details of unmatched STIG rules.</param>
/// <param name="ErrorMessage">Error details if import failed.</param>
public record ImportResult(
    string ImportRecordId,
    ScanImportStatus Status,
    string BenchmarkId,
    string? BenchmarkTitle,
    int TotalEntries,
    int OpenCount,
    int PassCount,
    int NotApplicableCount,
    int NotReviewedCount,
    int ErrorCount,
    int SkippedCount,
    int UnmatchedCount,
    int FindingsCreated,
    int FindingsUpdated,
    int EffectivenessRecordsCreated,
    int EffectivenessRecordsUpdated,
    int NistControlsAffected,
    List<string> Warnings,
    List<UnmatchedRuleInfo> UnmatchedRules,
    string? ErrorMessage);

/// <summary>
/// Details of a STIG rule that could not be matched to the curated library.
/// </summary>
/// <param name="VulnId">STIG Vulnerability ID.</param>
/// <param name="RuleId">STIG Rule ID (if available).</param>
/// <param name="RuleTitle">Rule title (if available).</param>
/// <param name="Severity">Severity string from the source file.</param>
public record UnmatchedRuleInfo(
    string VulnId,
    string? RuleId,
    string? RuleTitle,
    string Severity);
