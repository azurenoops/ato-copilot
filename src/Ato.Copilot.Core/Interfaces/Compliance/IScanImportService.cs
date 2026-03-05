// ═══════════════════════════════════════════════════════════════════════════
// Feature 017 — SCAP/STIG Viewer Import: Service Interface
// See specs/017-scap-stig-import/contracts/mcp-tools.md for tool contracts.
// ═══════════════════════════════════════════════════════════════════════════

using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Service for importing SCAP/STIG scan results, exporting CKL checklists,
/// and managing import history. Service receives raw bytes (tool layer decodes base64).
/// </summary>
public interface IScanImportService
{
    /// <summary>
    /// Import a DISA STIG Viewer CKL file. Creates ComplianceFindings, ControlEffectiveness,
    /// and ComplianceEvidence records from parsed CKL data.
    /// </summary>
    /// <param name="systemId">Registered system ID.</param>
    /// <param name="assessmentId">Assessment context for findings (optional — auto-resolved if null).</param>
    /// <param name="fileContent">Raw CKL file bytes (UTF-8 XML).</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="resolution">Conflict resolution strategy for duplicate findings.</param>
    /// <param name="dryRun">If true, parse and report without persisting.</param>
    /// <param name="importedBy">Identity of the importing user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Import result with counts, warnings, and unmatched rules.</returns>
    Task<ImportResult> ImportCklAsync(
        string systemId,
        string? assessmentId,
        byte[] fileContent,
        string fileName,
        ImportConflictResolution resolution,
        bool dryRun,
        string importedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Import a SCAP Compliance Checker XCCDF results file. Same downstream pipeline as CKL.
    /// </summary>
    Task<ImportResult> ImportXccdfAsync(
        string systemId,
        string? assessmentId,
        byte[] fileContent,
        string fileName,
        ImportConflictResolution resolution,
        bool dryRun,
        string importedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Export a CKL checklist for a system and benchmark, with current assessment status.
    /// </summary>
    /// <param name="systemId">Registered system ID.</param>
    /// <param name="benchmarkId">STIG benchmark ID (e.g., "Windows_Server_2022_STIG").</param>
    /// <param name="assessmentId">Optional assessment context (uses latest if null).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Base64-encoded CKL XML content.</returns>
    Task<string> ExportCklAsync(
        string systemId,
        string benchmarkId,
        string? assessmentId,
        CancellationToken ct = default);

    /// <summary>
    /// List import history for a system with pagination and filtering.
    /// </summary>
    /// <param name="systemId">Registered system ID.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Items per page (default 20, max 100).</param>
    /// <param name="benchmarkId">Optional benchmark filter.</param>
    /// <param name="importType">Optional type filter ("Ckl" or "Xccdf").</param>
    /// <param name="includeDryRuns">Whether to include dry-run records (default false).</param>
    /// <param name="fromDate">Optional start date filter (UTC).</param>
    /// <param name="toDate">Optional end date filter (UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated list of import records.</returns>
    Task<(List<ScanImportRecord> Records, int TotalCount)> ListImportsAsync(
        string systemId,
        int page,
        int pageSize,
        string? benchmarkId,
        string? importType,
        bool includeDryRuns,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct = default);

    /// <summary>
    /// Get detailed summary of a specific import operation.
    /// </summary>
    /// <param name="importId">Import record ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Import record with all findings, or null if not found.</returns>
    Task<(ScanImportRecord Record, List<ScanImportFinding> Findings)?> GetImportSummaryAsync(
        string importId,
        CancellationToken ct = default);

    // ─── Feature 019: Prisma Cloud Import ─────────────────────────────────

    /// <summary>
    /// Import a Prisma Cloud CSPM compliance CSV export file.
    /// Parses the CSV, auto-resolves Azure subscriptions to registered systems,
    /// creates ComplianceFinding + ControlEffectiveness + ComplianceEvidence records.
    /// </summary>
    /// <param name="systemId">Optional system ID. If null, auto-resolves from subscription IDs.</param>
    /// <param name="assessmentId">Assessment context (optional — auto-resolved if null).</param>
    /// <param name="fileContent">Raw CSV file bytes (UTF-8).</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="resolution">Conflict resolution strategy.</param>
    /// <param name="dryRun">If true, preview without persisting.</param>
    /// <param name="importedBy">Identity of the importing user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Import result with per-system breakdown.</returns>
    Task<PrismaImportResult> ImportPrismaCsvAsync(
        string? systemId,
        string? assessmentId,
        byte[] fileContent,
        string fileName,
        ImportConflictResolution resolution,
        bool dryRun,
        string importedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Import a Prisma Cloud API JSON response (RQL alert data) with enhanced
    /// remediation guidance, CLI scripts, alert history, and policy metadata.
    /// </summary>
    /// <param name="systemId">Optional system ID. If null, auto-resolves from subscription IDs.</param>
    /// <param name="assessmentId">Assessment context (optional).</param>
    /// <param name="fileContent">Raw JSON file bytes (UTF-8).</param>
    /// <param name="fileName">Original file name.</param>
    /// <param name="resolution">Conflict resolution strategy.</param>
    /// <param name="dryRun">If true, preview without persisting.</param>
    /// <param name="importedBy">Identity of the importing user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Import result with per-system breakdown and enhanced metadata.</returns>
    Task<PrismaImportResult> ImportPrismaApiAsync(
        string? systemId,
        string? assessmentId,
        byte[] fileContent,
        string fileName,
        ImportConflictResolution resolution,
        bool dryRun,
        string importedBy,
        CancellationToken ct = default);

    /// <summary>
    /// List unique Prisma policies observed across imports for a system,
    /// with NIST control mappings, open/resolved/dismissed counts, and affected resource types.
    /// </summary>
    /// <param name="systemId">Registered system ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Policy catalog for the system.</returns>
    Task<PrismaPolicyListResult> ListPrismaPoliciesAsync(
        string systemId,
        CancellationToken ct = default);

    /// <summary>
    /// Compare Prisma findings across scan imports to track remediation progress.
    /// Shows new, resolved, and persistent findings with optional group-by breakdowns.
    /// </summary>
    /// <param name="systemId">Registered system ID.</param>
    /// <param name="importIds">Specific import IDs to compare (null = last 2).</param>
    /// <param name="groupBy">Optional grouping: <c>resource_type</c> or <c>nist_control</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Trend analysis result.</returns>
    Task<PrismaTrendResult> GetPrismaTrendAsync(
        string systemId,
        List<string>? importIds,
        string? groupBy,
        CancellationToken ct = default);
}
