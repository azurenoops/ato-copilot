// ═══════════════════════════════════════════════════════════════════════════
// Feature 026 — ACAS/Nessus Scan Import: List Imports Tool (T028)
// Tool for querying Nessus import history with pagination and filtering.
// See specs/026-acas-nessus-import/contracts/mcp-tools.md for contracts.
// ═══════════════════════════════════════════════════════════════════════════

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Agents.Compliance.Tools;

/// <summary>
/// MCP tool for listing ACAS/Nessus import history for a system.
/// Delegates to <see cref="IScanImportService.ListImportsAsync"/> with NessusXml type filter.
/// </summary>
public class ListNessusImportsTool : BaseTool
{
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true };

    private readonly IScanImportService _importService;

    public ListNessusImportsTool(IScanImportService importService, ILogger<ListNessusImportsTool> logger)
        : base(logger)
    {
        _importService = importService;
    }

    public override string Name => "compliance_list_nessus_imports";

    public override string Description =>
        "List ACAS/Nessus import history for a registered system. " +
        "Shows past .nessus imports with summary statistics, severity breakdowns, and pagination.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new()
        {
            Name = "system_id",
            Description = "System GUID, name, or acronym (required)",
            Type = "string",
            Required = true
        },
        ["page"] = new()
        {
            Name = "page",
            Description = "Page number, 1-based (default: 1).",
            Type = "integer",
            Required = false
        },
        ["page_size"] = new()
        {
            Name = "page_size",
            Description = "Items per page (default: 20, max: 50).",
            Type = "integer",
            Required = false
        },
        ["from_date"] = new()
        {
            Name = "from_date",
            Description = "Filter imports on or after this date (ISO 8601, e.g. '2024-01-15').",
            Type = "string",
            Required = false
        },
        ["to_date"] = new()
        {
            Name = "to_date",
            Description = "Filter imports on or before this date (ISO 8601, e.g. '2024-12-31').",
            Type = "string",
            Required = false
        },
        ["include_dry_runs"] = new()
        {
            Name = "include_dry_runs",
            Description = "Include dry-run records (default: false).",
            Type = "boolean",
            Required = false
        },
        ["user_role"] = new()
        {
            Name = "user_role",
            Description = "Caller's compliance role (auto-populated by middleware).",
            Type = "string",
            Required = false
        }
    };

    // RBAC: ISSO, SCA, SystemAdmin + ISSM, AO (read-only)
    private static readonly HashSet<string> AllowedListRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Compliance.Analyst",
        "Compliance.SecurityLead",
        "Compliance.Administrator",
        "Compliance.PlatformEngineer",
        "Compliance.Auditor",          // ISSM
        "Compliance.AuthorizingOfficial", // AO
        "Compliance.Viewer"
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var systemId = GetArg<string>(arguments, "system_id");
        if (string.IsNullOrWhiteSpace(systemId))
            return ErrorJson("INVALID_INPUT", "The 'system_id' parameter is required.");

        // RBAC check (T035)
        var userRole = GetArg<string>(arguments, "user_role") ?? string.Empty;
        if (!string.IsNullOrEmpty(userRole) && !AllowedListRoles.Contains(userRole))
            return ErrorJson("INSUFFICIENT_PERMISSIONS",
                "Listing Nessus imports requires ISSO, SCA, SystemAdmin, ISSM, or AO role.");

        var page = GetArg<int>(arguments, "page");
        if (page <= 0) page = 1;

        var pageSize = GetArg<int>(arguments, "page_size");
        if (pageSize <= 0) pageSize = 20;
        if (pageSize > 50) pageSize = 50;

        var includeDryRuns = GetArg<bool>(arguments, "include_dry_runs");

        DateTime? fromDate = null;
        var fromDateStr = GetArg<string>(arguments, "from_date");
        if (!string.IsNullOrWhiteSpace(fromDateStr) &&
            DateTime.TryParse(fromDateStr, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var parsedFrom))
        {
            fromDate = parsedFrom;
        }

        DateTime? toDate = null;
        var toDateStr = GetArg<string>(arguments, "to_date");
        if (!string.IsNullOrWhiteSpace(toDateStr) &&
            DateTime.TryParse(toDateStr, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var parsedTo))
        {
            toDate = parsedTo;
        }

        try
        {
            var (records, totalCount) = await _importService.ListImportsAsync(
                systemId, page, pageSize, benchmarkId: null, importType: "NessusXml",
                includeDryRuns, fromDate, toDate, cancellationToken);

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
                        benchmark_title = r.BenchmarkTitle,
                        status = r.ImportStatus.ToString(),
                        imported_by = r.ImportedBy,
                        imported_at = r.ImportedAt.ToString("O"),
                        is_dry_run = r.IsDryRun,
                        total_entries = r.TotalEntries,
                        severity = new
                        {
                            critical = r.NessusCriticalCount,
                            high = r.NessusHighCount,
                            medium = r.NessusMediumCount,
                            low = r.NessusLowCount,
                            informational = r.NessusInformationalCount
                        },
                        host_count = r.NessusHostCount,
                        credentialed_scan = r.NessusCredentialedScan,
                        findings_created = r.FindingsCreated,
                        findings_updated = r.FindingsUpdated,
                        skipped = r.SkippedCount,
                        scan_timestamp = r.ScanTimestamp?.ToString("O")
                    })
                },
                metadata = new { tool = Name, timestamp = DateTime.UtcNow.ToString("O") }
            }, s_jsonOpts);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "List Nessus imports failed for system {SystemId}", systemId);
            return ErrorJson("LIST_FAILED", $"Failed to list Nessus imports: {ex.Message}");
        }
    }

    private string ErrorJson(string code, string message) =>
        JsonSerializer.Serialize(new
        {
            status = "error",
            errorCode = code,
            message,
            metadata = new { tool = Name, timestamp = DateTime.UtcNow.ToString("O") }
        }, s_jsonOpts);
}
