// ═══════════════════════════════════════════════════════════════════════════
// Feature 017 — SCAP/STIG Viewer Import: MCP Tools
// Tools for importing CKL/XCCDF files, exporting CKL, and managing imports.
// ═══════════════════════════════════════════════════════════════════════════

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Tools;

// ─── ImportCklTool ───────────────────────────────────────────────────────────

/// <summary>
/// MCP tool for importing a DISA STIG Viewer CKL checklist file.
/// Accepts base64-encoded file content, decodes to bytes, validates size,
/// checks for duplicate files, and delegates to <see cref="IScanImportService"/>.
/// </summary>
public class ImportCklTool : BaseTool
{
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true };
    private const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IScanImportService _importService;
    private readonly IServiceScopeFactory _scopeFactory;

    public ImportCklTool(
        IScanImportService importService,
        IServiceScopeFactory scopeFactory,
        ILogger<ImportCklTool> logger)
        : base(logger)
    {
        _importService = importService;
        _scopeFactory = scopeFactory;
    }

    public override string Name => "compliance_import_ckl";

    public override string Description =>
        "Import a DISA STIG Viewer CKL checklist file for a registered system. " +
        "Creates compliance findings, control effectiveness records, and evidence. " +
        "Accepts base64-encoded file content (max 5 MB after decoding).";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new()
        {
            Name = "system_id",
            Description = "System GUID, name, or acronym (required)",
            Type = "string",
            Required = true
        },
        ["file_content"] = new()
        {
            Name = "file_content",
            Description = "Base64-encoded CKL file content (required, max 5 MB after decoding).",
            Type = "string",
            Required = true
        },
        ["file_name"] = new()
        {
            Name = "file_name",
            Description = "Original file name (required, e.g., 'windows_server_2022.ckl').",
            Type = "string",
            Required = true
        },
        ["conflict_resolution"] = new()
        {
            Name = "conflict_resolution",
            Description = "How to handle duplicate findings: 'Skip' (default), 'Overwrite', or 'Merge'.",
            Type = "string",
            Required = false
        },
        ["dry_run"] = new()
        {
            Name = "dry_run",
            Description = "If true, preview results without persisting changes (default: false).",
            Type = "boolean",
            Required = false
        },
        ["assessment_id"] = new()
        {
            Name = "assessment_id",
            Description = "Optional assessment ID. If omitted, auto-resolves or creates one.",
            Type = "string",
            Required = false
        }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var systemId = GetArg<string>(arguments, "system_id");
        var fileContentBase64 = GetArg<string>(arguments, "file_content");
        var fileName = GetArg<string>(arguments, "file_name");
        var conflictStr = GetArg<string>(arguments, "conflict_resolution") ?? "Skip";
        var dryRun = GetArg<bool>(arguments, "dry_run");
        var assessmentId = GetArg<string>(arguments, "assessment_id");

        // Validate required parameters
        if (string.IsNullOrWhiteSpace(systemId))
            return ErrorJson("INVALID_INPUT", "The 'system_id' parameter is required.");

        if (string.IsNullOrWhiteSpace(fileContentBase64))
            return ErrorJson("INVALID_INPUT", "The 'file_content' parameter is required (base64-encoded CKL data).");

        if (string.IsNullOrWhiteSpace(fileName))
            return ErrorJson("INVALID_INPUT", "The 'file_name' parameter is required.");

        // Decode base64
        byte[] fileBytes;
        try
        {
            fileBytes = Convert.FromBase64String(fileContentBase64);
        }
        catch (FormatException)
        {
            return ErrorJson("INVALID_BASE64", "The 'file_content' parameter is not valid base64. Ensure the CKL file is base64-encoded.");
        }

        // Validate file size
        if (fileBytes.Length > MaxFileSizeBytes)
        {
            return ErrorJson("FILE_TOO_LARGE",
                $"File is {fileBytes.Length / 1024.0 / 1024.0:F1} MB, exceeding the 5 MB limit. " +
                "Consider splitting your STIGs into separate CKL files per benchmark.");
        }

        // Parse conflict resolution
        if (!Enum.TryParse<ImportConflictResolution>(conflictStr, ignoreCase: true, out var resolution))
            resolution = ImportConflictResolution.Skip;

        // Check for duplicate file (same SHA-256 hash + system)
        var warnings = new List<string>();
        var hash = ComputeSha256(fileBytes);
        await CheckDuplicateFile(systemId, hash, warnings, cancellationToken);

        try
        {
            var result = await _importService.ImportCklAsync(
                systemId, assessmentId, fileBytes, fileName,
                resolution, dryRun, "mcp-user", cancellationToken);

            // Merge duplicate warnings
            var allWarnings = warnings.Concat(result.Warnings).ToList();

            return JsonSerializer.Serialize(new
            {
                status = result.Status == ScanImportStatus.Failed ? "error" : "success",
                data = new
                {
                    import_record_id = result.ImportRecordId,
                    import_status = result.Status.ToString(),
                    dry_run = dryRun,
                    benchmark = result.BenchmarkId,
                    benchmark_title = result.BenchmarkTitle,
                    total_entries = result.TotalEntries,
                    summary = new
                    {
                        open = result.OpenCount,
                        pass = result.PassCount,
                        not_applicable = result.NotApplicableCount,
                        not_reviewed = result.NotReviewedCount,
                        error = result.ErrorCount,
                        skipped = result.SkippedCount,
                        unmatched = result.UnmatchedCount
                    },
                    changes = new
                    {
                        findings_created = result.FindingsCreated,
                        findings_updated = result.FindingsUpdated,
                        effectiveness_created = result.EffectivenessRecordsCreated,
                        effectiveness_updated = result.EffectivenessRecordsUpdated,
                        nist_controls_affected = result.NistControlsAffected
                    },
                    unmatched_rules = result.UnmatchedRules.Select(r => new
                    {
                        vuln_id = r.VulnId,
                        rule_id = r.RuleId,
                        title = r.RuleTitle
                    }),
                    warnings = allWarnings,
                    error_message = result.ErrorMessage
                },
                metadata = new
                {
                    tool = Name,
                    timestamp = DateTime.UtcNow.ToString("O")
                }
            }, s_jsonOpts);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "CKL import failed for system {SystemId}", systemId);
            return ErrorJson("IMPORT_FAILED", $"CKL import failed: {ex.Message}");
        }
    }

    private async Task CheckDuplicateFile(string systemId, string hash, List<string> warnings, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
            var existing = await ctx.ScanImportRecords
                .Where(r => r.RegisteredSystemId == systemId && r.FileHash == hash && !r.IsDryRun)
                .OrderByDescending(r => r.ImportedAt)
                .FirstOrDefaultAsync(ct);

            if (existing != null)
            {
                warnings.Add($"File previously imported on {existing.ImportedAt:yyyy-MM-dd HH:mm} UTC (import ID: {existing.Id}).");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not check for duplicate file");
        }
    }

    private static string ComputeSha256(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash);
    }

    private string ErrorJson(string code, string message)
    {
        return JsonSerializer.Serialize(new
        {
            status = "error",
            errorCode = code,
            message,
            metadata = new { tool = Name, timestamp = DateTime.UtcNow.ToString("O") }
        }, s_jsonOpts);
    }
}

// ─── ImportXccdfTool ─────────────────────────────────────────────────────────

/// <summary>
/// MCP tool for importing a SCAP Compliance Checker XCCDF results file.
/// Same interface pattern as <see cref="ImportCklTool"/>.
/// </summary>
public class ImportXccdfTool : BaseTool
{
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true };
    private const int MaxFileSizeBytes = 5 * 1024 * 1024;

    private readonly IScanImportService _importService;

    public ImportXccdfTool(IScanImportService importService, ILogger<ImportXccdfTool> logger)
        : base(logger)
    {
        _importService = importService;
    }

    public override string Name => "compliance_import_xccdf";

    public override string Description =>
        "Import a SCAP Compliance Checker XCCDF results file for a registered system. " +
        "Creates compliance findings and control effectiveness records from automated scan results.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym (required)", Type = "string", Required = true },
        ["file_content"] = new() { Name = "file_content", Description = "Base64-encoded XCCDF file content (required).", Type = "string", Required = true },
        ["file_name"] = new() { Name = "file_name", Description = "Original file name (required).", Type = "string", Required = true },
        ["conflict_resolution"] = new() { Name = "conflict_resolution", Description = "How to handle duplicates: 'Skip' (default), 'Overwrite', or 'Merge'.", Type = "string", Required = false },
        ["dry_run"] = new() { Name = "dry_run", Description = "If true, preview without persisting (default: false).", Type = "boolean", Required = false },
        ["assessment_id"] = new() { Name = "assessment_id", Description = "Optional assessment ID.", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var systemId = GetArg<string>(arguments, "system_id");
        var fileContentBase64 = GetArg<string>(arguments, "file_content");
        var fileName = GetArg<string>(arguments, "file_name");

        if (string.IsNullOrWhiteSpace(systemId))
            return ErrorJson("INVALID_INPUT", "The 'system_id' parameter is required.");
        if (string.IsNullOrWhiteSpace(fileContentBase64))
            return ErrorJson("INVALID_INPUT", "The 'file_content' parameter is required.");
        if (string.IsNullOrWhiteSpace(fileName))
            return ErrorJson("INVALID_INPUT", "The 'file_name' parameter is required.");

        byte[] fileBytes;
        try { fileBytes = Convert.FromBase64String(fileContentBase64); }
        catch (FormatException) { return ErrorJson("INVALID_BASE64", "The 'file_content' is not valid base64."); }

        if (fileBytes.Length > MaxFileSizeBytes)
            return ErrorJson("FILE_TOO_LARGE", $"File exceeds 5 MB limit ({fileBytes.Length / 1024.0 / 1024.0:F1} MB).");

        var conflictStr = GetArg<string>(arguments, "conflict_resolution") ?? "Skip";
        if (!Enum.TryParse<ImportConflictResolution>(conflictStr, ignoreCase: true, out var resolution))
            resolution = ImportConflictResolution.Skip;
        var dryRun = GetArg<bool>(arguments, "dry_run");
        var assessmentId = GetArg<string>(arguments, "assessment_id");

        try
        {
            var result = await _importService.ImportXccdfAsync(
                systemId, assessmentId, fileBytes, fileName,
                resolution, dryRun, "mcp-user", cancellationToken);

            return JsonSerializer.Serialize(new
            {
                status = result.Status == ScanImportStatus.Failed ? "error" : "success",
                data = new
                {
                    import_record_id = result.ImportRecordId,
                    import_status = result.Status.ToString(),
                    dry_run = dryRun,
                    benchmark = result.BenchmarkId,
                    total_entries = result.TotalEntries,
                    summary = new { open = result.OpenCount, pass = result.PassCount, not_applicable = result.NotApplicableCount, error = result.ErrorCount, skipped = result.SkippedCount, unmatched = result.UnmatchedCount },
                    changes = new { findings_created = result.FindingsCreated, findings_updated = result.FindingsUpdated, effectiveness_created = result.EffectivenessRecordsCreated, effectiveness_updated = result.EffectivenessRecordsUpdated, nist_controls_affected = result.NistControlsAffected },
                    warnings = result.Warnings,
                    error_message = result.ErrorMessage
                },
                metadata = new { tool = Name, timestamp = DateTime.UtcNow.ToString("O") }
            }, s_jsonOpts);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "XCCDF import failed for system {SystemId}", systemId);
            return ErrorJson("IMPORT_FAILED", $"XCCDF import failed: {ex.Message}");
        }
    }

    private string ErrorJson(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message, metadata = new { tool = Name, timestamp = DateTime.UtcNow.ToString("O") } }, s_jsonOpts);
}

// ─── ExportCklTool ───────────────────────────────────────────────────────────

/// <summary>
/// MCP tool for exporting a CKL checklist from assessment data.
/// </summary>
public class ExportCklTool : BaseTool
{
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true };

    private readonly IScanImportService _importService;

    public ExportCklTool(IScanImportService importService, ILogger<ExportCklTool> logger)
        : base(logger)
    {
        _importService = importService;
    }

    public override string Name => "compliance_export_ckl";

    public override string Description =>
        "Export a CKL checklist file for a system and STIG benchmark. " +
        "Returns base64-encoded XML content suitable for DISA STIG Viewer or eMASS upload.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym (required)", Type = "string", Required = true },
        ["benchmark_id"] = new() { Name = "benchmark_id", Description = "STIG benchmark ID (required, e.g., 'Windows_Server_2022_STIG').", Type = "string", Required = true },
        ["assessment_id"] = new() { Name = "assessment_id", Description = "Optional assessment ID (uses latest if omitted).", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var systemId = GetArg<string>(arguments, "system_id");
        var benchmarkId = GetArg<string>(arguments, "benchmark_id");
        var assessmentId = GetArg<string>(arguments, "assessment_id");

        if (string.IsNullOrWhiteSpace(systemId))
            return ErrorJson("INVALID_INPUT", "The 'system_id' parameter is required.");
        if (string.IsNullOrWhiteSpace(benchmarkId))
            return ErrorJson("INVALID_INPUT", "The 'benchmark_id' parameter is required.");

        try
        {
            var base64Content = await _importService.ExportCklAsync(systemId, benchmarkId, assessmentId, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    file_content = base64Content,
                    file_name = $"{benchmarkId}_{systemId}.ckl",
                    content_type = "application/xml",
                    benchmark_id = benchmarkId,
                    system_id = systemId
                },
                metadata = new { tool = Name, timestamp = DateTime.UtcNow.ToString("O") }
            }, s_jsonOpts);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "CKL export failed for system {SystemId}, benchmark {BenchmarkId}", systemId, benchmarkId);
            return ErrorJson("EXPORT_FAILED", $"CKL export failed: {ex.Message}");
        }
    }

    private string ErrorJson(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message, metadata = new { tool = Name, timestamp = DateTime.UtcNow.ToString("O") } }, s_jsonOpts);
}

// ─── ListImportsTool ─────────────────────────────────────────────────────────

/// <summary>
/// MCP tool for listing import history for a system.
/// </summary>
public class ListImportsTool : BaseTool
{
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true };

    private readonly IScanImportService _importService;

    public ListImportsTool(IScanImportService importService, ILogger<ListImportsTool> logger)
        : base(logger)
    {
        _importService = importService;
    }

    public override string Name => "compliance_list_imports";

    public override string Description =>
        "List import history for a registered system. Shows CKL and XCCDF imports with summary statistics.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym (required)", Type = "string", Required = true },
        ["page"] = new() { Name = "page", Description = "Page number, 1-based (default: 1).", Type = "integer", Required = false },
        ["page_size"] = new() { Name = "page_size", Description = "Items per page (default: 20, max: 100).", Type = "integer", Required = false },
        ["benchmark_id"] = new() { Name = "benchmark_id", Description = "Optional benchmark filter.", Type = "string", Required = false },
        ["include_dry_runs"] = new() { Name = "include_dry_runs", Description = "Include dry-run records (default: false).", Type = "boolean", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var systemId = GetArg<string>(arguments, "system_id");
        if (string.IsNullOrWhiteSpace(systemId))
            return ErrorJson("INVALID_INPUT", "The 'system_id' parameter is required.");

        var page = GetArg<int>(arguments, "page");
        if (page <= 0) page = 1;
        var pageSize = GetArg<int>(arguments, "page_size");
        if (pageSize <= 0) pageSize = 20;
        if (pageSize > 100) pageSize = 100;
        var benchmarkId = GetArg<string>(arguments, "benchmark_id");
        var includeDryRuns = GetArg<bool>(arguments, "include_dry_runs");

        try
        {
            var (records, totalCount) = await _importService.ListImportsAsync(
                systemId, page, pageSize, benchmarkId, null, includeDryRuns, null, null, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    total_count = totalCount,
                    page,
                    page_size = pageSize,
                    imports = records.Select(r => new
                    {
                        id = r.Id,
                        file_name = r.FileName,
                        import_type = r.ImportType.ToString(),
                        benchmark_id = r.BenchmarkId,
                        benchmark_title = r.BenchmarkTitle,
                        status = r.ImportStatus.ToString(),
                        imported_by = r.ImportedBy,
                        imported_at = r.ImportedAt.ToString("O"),
                        is_dry_run = r.IsDryRun,
                        total_entries = r.TotalEntries,
                        open_count = r.OpenCount,
                        pass_count = r.PassCount,
                        findings_created = r.FindingsCreated,
                        findings_updated = r.FindingsUpdated
                    })
                },
                metadata = new { tool = Name, timestamp = DateTime.UtcNow.ToString("O") }
            }, s_jsonOpts);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "List imports failed for system {SystemId}", systemId);
            return ErrorJson("LIST_FAILED", $"Failed to list imports: {ex.Message}");
        }
    }

    private string ErrorJson(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message, metadata = new { tool = Name, timestamp = DateTime.UtcNow.ToString("O") } }, s_jsonOpts);
}

// ─── GetImportSummaryTool ────────────────────────────────────────────────────

/// <summary>
/// MCP tool for getting detailed summary of a specific import.
/// </summary>
public class GetImportSummaryTool : BaseTool
{
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true };

    private readonly IScanImportService _importService;

    public GetImportSummaryTool(IScanImportService importService, ILogger<GetImportSummaryTool> logger)
        : base(logger)
    {
        _importService = importService;
    }

    public override string Name => "compliance_get_import_summary";

    public override string Description =>
        "Get detailed summary of a specific import operation, including per-finding breakdown and unmatched rules.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["import_id"] = new() { Name = "import_id", Description = "Import record ID (required).", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var importId = GetArg<string>(arguments, "import_id");
        if (string.IsNullOrWhiteSpace(importId))
            return ErrorJson("INVALID_INPUT", "The 'import_id' parameter is required.");

        try
        {
            var summary = await _importService.GetImportSummaryAsync(importId, cancellationToken);
            if (summary is null)
                return ErrorJson("NOT_FOUND", $"Import record '{importId}' not found.");

            var (record, findings) = summary.Value;

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    id = record.Id,
                    file_name = record.FileName,
                    file_hash = record.FileHash,
                    import_type = record.ImportType.ToString(),
                    import_status = record.ImportStatus.ToString(),
                    benchmark_id = record.BenchmarkId,
                    benchmark_title = record.BenchmarkTitle,
                    target_host = record.TargetHostName,
                    imported_by = record.ImportedBy,
                    imported_at = record.ImportedAt.ToString("O"),
                    is_dry_run = record.IsDryRun,
                    conflict_resolution = record.ConflictResolution.ToString(),
                    counts = new
                    {
                        total = record.TotalEntries,
                        open = record.OpenCount,
                        pass = record.PassCount,
                        not_applicable = record.NotApplicableCount,
                        not_reviewed = record.NotReviewedCount,
                        error = record.ErrorCount,
                        skipped = record.SkippedCount,
                        unmatched = record.UnmatchedCount,
                        findings_created = record.FindingsCreated,
                        findings_updated = record.FindingsUpdated,
                        effectiveness_created = record.EffectivenessRecordsCreated,
                        effectiveness_updated = record.EffectivenessRecordsUpdated,
                        nist_controls_affected = record.NistControlsAffected
                    },
                    warnings = record.Warnings,
                    findings = findings.Select(f => new
                    {
                        vuln_id = f.VulnId,
                        rule_id = f.RuleId,
                        raw_status = f.RawStatus,
                        mapped_severity = f.MappedSeverity?.ToString(),
                        action = f.ImportAction.ToString(),
                        resolved_stig_id = f.ResolvedStigControlId,
                        resolved_nist_controls = f.ResolvedNistControlIds,
                        compliance_finding_id = f.ComplianceFindingId
                    })
                },
                metadata = new { tool = Name, timestamp = DateTime.UtcNow.ToString("O") }
            }, s_jsonOpts);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Get import summary failed for {ImportId}", importId);
            return ErrorJson("SUMMARY_FAILED", $"Failed to get import summary: {ex.Message}");
        }
    }

    private string ErrorJson(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message, metadata = new { tool = Name, timestamp = DateTime.UtcNow.ToString("O") } }, s_jsonOpts);
}
