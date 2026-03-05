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
}
