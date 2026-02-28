// ─────────────────────────────────────────────────────────────────────────────
// Feature 015 · Phase 12 — eMASS & OSCAL Interoperability (US10)
// T145: IEmassExportService interface
// ─────────────────────────────────────────────────────────────────────────────

using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Service for eMASS-compatible Excel export, import with conflict resolution,
/// and OSCAL JSON export (SSP, assessment-results, POA&amp;M).
/// </summary>
public interface IEmassExportService
{
    /// <summary>
    /// Export control compliance data to eMASS-compatible Excel (.xlsx) format.
    /// Worksheet: "Controls" with column headers matching the eMASS import template.
    /// </summary>
    /// <param name="registeredSystemId">FK to RegisteredSystem.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Excel workbook bytes (.xlsx).</returns>
    Task<byte[]> ExportControlsAsync(
        string registeredSystemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Export POA&amp;M items to eMASS-compatible Excel (.xlsx) format.
    /// Worksheet: "POAM" with column headers matching the eMASS POA&amp;M import template.
    /// </summary>
    /// <param name="registeredSystemId">FK to RegisteredSystem.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Excel workbook bytes (.xlsx).</returns>
    Task<byte[]> ExportPoamAsync(
        string registeredSystemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Import eMASS control export (Excel .xlsx) with conflict resolution.
    /// Supports dry-run mode to preview changes without applying them.
    /// </summary>
    /// <param name="fileBytes">Excel file content.</param>
    /// <param name="registeredSystemId">Target system ID.</param>
    /// <param name="options">Import options (conflict strategy, dry-run, field selectors).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Import result summary.</returns>
    Task<EmassImportResult> ImportAsync(
        byte[] fileBytes,
        string registeredSystemId,
        EmassImportOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Export system data in OSCAL JSON format (v1.0.6).
    /// Supported models: SSP, assessment-results, POA&amp;M.
    /// </summary>
    /// <param name="registeredSystemId">FK to RegisteredSystem.</param>
    /// <param name="model">OSCAL model type to export.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>OSCAL JSON string.</returns>
    Task<string> ExportOscalAsync(
        string registeredSystemId,
        OscalModelType model,
        CancellationToken cancellationToken = default);
}
