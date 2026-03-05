// ═══════════════════════════════════════════════════════════════════════════
// Feature 019 — Prisma Cloud Scan Import: MCP Tools
// Tools for importing Prisma Cloud CSV/JSON exports.
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

// ─── ImportPrismaCsvTool ─────────────────────────────────────────────────────

/// <summary>
/// MCP tool for importing a Prisma Cloud CSV alert export.
/// Accepts base64-encoded file content, decodes to bytes, validates size,
/// and delegates to <see cref="IScanImportService.ImportPrismaCsvAsync"/>.
/// Unlike CKL/XCCDF, system_id is optional — auto-resolved from Azure subscriptions.
/// </summary>
public class ImportPrismaCsvTool : BaseTool
{
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true };
    private const int MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB

    private readonly IScanImportService _importService;
    private readonly IServiceScopeFactory _scopeFactory;

    public ImportPrismaCsvTool(
        IScanImportService importService,
        IServiceScopeFactory scopeFactory,
        ILogger<ImportPrismaCsvTool> logger)
        : base(logger)
    {
        _importService = importService;
        _scopeFactory = scopeFactory;
    }

    public override string Name => "compliance_import_prisma_csv";

    public override string Description =>
        "Import a Prisma Cloud CSV alert export for compliance analysis. " +
        "Extracts NIST 800-53 control mappings, creates findings and effectiveness records. " +
        "If system_id is omitted, auto-resolves Azure subscriptions to registered systems. " +
        "Accepts base64-encoded CSV content (max 25 MB after decoding).";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["file_content"] = new()
        {
            Name = "file_content",
            Description = "Base64-encoded Prisma Cloud CSV export (required, max 25 MB after decoding).",
            Type = "string",
            Required = true
        },
        ["file_name"] = new()
        {
            Name = "file_name",
            Description = "Original file name (required, e.g., 'prisma-alerts-2026-01.csv').",
            Type = "string",
            Required = true
        },
        ["system_id"] = new()
        {
            Name = "system_id",
            Description = "Registered system ID (optional). If omitted, auto-resolves from Azure subscription IDs in the CSV. " +
                          "When provided, all alerts (including non-Azure) are imported to that system.",
            Type = "string",
            Required = false
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
        var fileContentBase64 = GetArg<string>(arguments, "file_content");
        var fileName = GetArg<string>(arguments, "file_name");
        var systemId = GetArg<string>(arguments, "system_id"); // nullable
        var conflictStr = GetArg<string>(arguments, "conflict_resolution") ?? "Skip";
        var dryRun = GetArg<bool>(arguments, "dry_run");
        var assessmentId = GetArg<string>(arguments, "assessment_id");

        // Validate required parameters
        if (string.IsNullOrWhiteSpace(fileContentBase64))
            return ErrorJson("INVALID_INPUT", "The 'file_content' parameter is required (base64-encoded CSV data).");

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
            return ErrorJson("INVALID_BASE64", "The 'file_content' parameter is not valid base64. Ensure the CSV file is base64-encoded.");
        }

        // Validate file size
        if (fileBytes.Length > MaxFileSizeBytes)
        {
            return ErrorJson("FILE_TOO_LARGE",
                $"File is {fileBytes.Length / 1024.0 / 1024.0:F1} MB, exceeding the 25 MB limit.");
        }

        // Parse conflict resolution
        if (!Enum.TryParse<ImportConflictResolution>(conflictStr, ignoreCase: true, out var resolution))
            resolution = ImportConflictResolution.Skip;

        // Normalize empty system_id to null for auto-resolution
        var effectiveSystemId = string.IsNullOrWhiteSpace(systemId) ? null : systemId;

        try
        {
            var result = await _importService.ImportPrismaCsvAsync(
                effectiveSystemId, assessmentId, fileBytes, fileName,
                resolution, dryRun, "mcp-user", cancellationToken);

            if (result.ErrorMessage != null)
            {
                return JsonSerializer.Serialize(new
                {
                    status = "error",
                    errorCode = "IMPORT_FAILED",
                    message = result.ErrorMessage,
                    metadata = new
                    {
                        tool = Name,
                        timestamp = DateTime.UtcNow.ToString("O")
                    }
                }, s_jsonOpts);
            }

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    total_processed = result.TotalProcessed,
                    total_skipped = result.TotalSkipped,
                    duration_ms = result.DurationMs,
                    dry_run = dryRun,
                    imports = result.Imports.Select(imp => new
                    {
                        system_id = imp.SystemId,
                        system_name = imp.SystemName,
                        import_record_id = imp.ImportRecordId,
                        import_status = imp.Status.ToString(),
                        total_alerts = imp.TotalAlerts,
                        summary = new
                        {
                            open = imp.OpenCount,
                            resolved = imp.ResolvedCount,
                            dismissed = imp.DismissedCount,
                            snoozed = imp.SnoozedCount
                        },
                        changes = new
                        {
                            findings_created = imp.FindingsCreated,
                            findings_updated = imp.FindingsUpdated,
                            skipped = imp.SkippedCount,
                            unmapped_policies = imp.UnmappedPolicies,
                            effectiveness_created = imp.EffectivenessRecordsCreated,
                            effectiveness_updated = imp.EffectivenessRecordsUpdated,
                            nist_controls_affected = imp.NistControlsAffected,
                            evidence_created = imp.EvidenceCreated
                        },
                        file_hash = imp.FileHash,
                        warnings = imp.Warnings
                    }),
                    unresolved_subscriptions = result.UnresolvedSubscriptions.Select(u => new
                    {
                        account_id = u.AccountId,
                        account_name = u.AccountName,
                        alert_count = u.AlertCount,
                        message = u.Message
                    }),
                    skipped_non_azure = result.SkippedNonAzure != null ? new
                    {
                        count = result.SkippedNonAzure.Count,
                        cloud_types = result.SkippedNonAzure.CloudTypes,
                        message = result.SkippedNonAzure.Message
                    } : null
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
            Logger.LogError(ex, "Prisma CSV import failed");
            return ErrorJson("IMPORT_FAILED", $"Prisma CSV import failed: {ex.Message}");
        }
    }

    private static string ErrorJson(string code, string message) =>
        JsonSerializer.Serialize(new
        {
            status = "error",
            errorCode = code,
            message
        }, s_jsonOpts);
}

// ─── ImportPrismaApiTool ─────────────────────────────────────────────────────

/// <summary>
/// MCP tool for importing a Prisma Cloud API JSON (RQL alert) export.
/// Accepts base64-encoded JSON, delegates to <see cref="IScanImportService.ImportPrismaApiAsync"/>.
/// Exposes enhanced fields: remediation scripts, policy labels, alert history.
/// </summary>
public class ImportPrismaApiTool : BaseTool
{
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true };
    private const int MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB

    private readonly IScanImportService _importService;
    private readonly IServiceScopeFactory _scopeFactory;

    public ImportPrismaApiTool(
        IScanImportService importService,
        IServiceScopeFactory scopeFactory,
        ILogger<ImportPrismaApiTool> logger)
        : base(logger)
    {
        _importService = importService;
        _scopeFactory = scopeFactory;
    }

    public override string Name => "compliance_import_prisma_api";

    public override string Description =>
        "Import a Prisma Cloud API JSON (RQL alert) export for compliance analysis. " +
        "Extracts NIST 800-53 control mappings, remediation scripts, alert history, and policy labels. " +
        "Creates findings and effectiveness records. " +
        "If system_id is omitted, auto-resolves Azure subscriptions to registered systems. " +
        "Accepts base64-encoded JSON content (max 25 MB after decoding).";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["file_content"] = new()
        {
            Name = "file_content",
            Description = "Base64-encoded Prisma Cloud API JSON export (required, max 25 MB after decoding).",
            Type = "string",
            Required = true
        },
        ["file_name"] = new()
        {
            Name = "file_name",
            Description = "Original file name (required, e.g., 'prisma-alerts-2026-01.json').",
            Type = "string",
            Required = true
        },
        ["system_id"] = new()
        {
            Name = "system_id",
            Description = "Registered system ID (optional). If omitted, auto-resolves from Azure subscription IDs in the JSON. " +
                          "When provided, all alerts (including non-Azure) are imported to that system.",
            Type = "string",
            Required = false
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
        var fileContentBase64 = GetArg<string>(arguments, "file_content");
        var fileName = GetArg<string>(arguments, "file_name");
        var systemId = GetArg<string>(arguments, "system_id");
        var conflictStr = GetArg<string>(arguments, "conflict_resolution") ?? "Skip";
        var dryRun = GetArg<bool>(arguments, "dry_run");
        var assessmentId = GetArg<string>(arguments, "assessment_id");

        if (string.IsNullOrWhiteSpace(fileContentBase64))
            return ErrorJson("INVALID_INPUT", "The 'file_content' parameter is required (base64-encoded JSON data).");

        if (string.IsNullOrWhiteSpace(fileName))
            return ErrorJson("INVALID_INPUT", "The 'file_name' parameter is required.");

        byte[] fileBytes;
        try
        {
            fileBytes = Convert.FromBase64String(fileContentBase64);
        }
        catch (FormatException)
        {
            return ErrorJson("INVALID_BASE64", "The 'file_content' parameter is not valid base64. Ensure the JSON file is base64-encoded.");
        }

        if (fileBytes.Length > MaxFileSizeBytes)
        {
            return ErrorJson("FILE_TOO_LARGE",
                $"File is {fileBytes.Length / 1024.0 / 1024.0:F1} MB, exceeding the 25 MB limit.");
        }

        if (!Enum.TryParse<ImportConflictResolution>(conflictStr, ignoreCase: true, out var resolution))
            resolution = ImportConflictResolution.Skip;

        var effectiveSystemId = string.IsNullOrWhiteSpace(systemId) ? null : systemId;

        try
        {
            var result = await _importService.ImportPrismaApiAsync(
                effectiveSystemId, assessmentId, fileBytes, fileName,
                resolution, dryRun, "mcp-user", cancellationToken);

            if (result.ErrorMessage != null)
            {
                return JsonSerializer.Serialize(new
                {
                    status = "error",
                    errorCode = "IMPORT_FAILED",
                    message = result.ErrorMessage,
                    metadata = new
                    {
                        tool = Name,
                        timestamp = DateTime.UtcNow.ToString("O")
                    }
                }, s_jsonOpts);
            }

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    total_processed = result.TotalProcessed,
                    total_skipped = result.TotalSkipped,
                    duration_ms = result.DurationMs,
                    dry_run = dryRun,
                    imports = result.Imports.Select(imp => new
                    {
                        system_id = imp.SystemId,
                        system_name = imp.SystemName,
                        import_record_id = imp.ImportRecordId,
                        import_status = imp.Status.ToString(),
                        total_alerts = imp.TotalAlerts,
                        summary = new
                        {
                            open = imp.OpenCount,
                            resolved = imp.ResolvedCount,
                            dismissed = imp.DismissedCount,
                            snoozed = imp.SnoozedCount
                        },
                        changes = new
                        {
                            findings_created = imp.FindingsCreated,
                            findings_updated = imp.FindingsUpdated,
                            skipped = imp.SkippedCount,
                            unmapped_policies = imp.UnmappedPolicies,
                            effectiveness_created = imp.EffectivenessRecordsCreated,
                            effectiveness_updated = imp.EffectivenessRecordsUpdated,
                            nist_controls_affected = imp.NistControlsAffected,
                            evidence_created = imp.EvidenceCreated
                        },
                        enhanced = new
                        {
                            remediable_count = imp.RemediableCount,
                            cli_scripts_extracted = imp.CliScriptsExtracted,
                            policy_labels_found = imp.PolicyLabelsFound,
                            alerts_with_history = imp.AlertsWithHistory
                        },
                        file_hash = imp.FileHash,
                        warnings = imp.Warnings
                    }),
                    unresolved_subscriptions = result.UnresolvedSubscriptions.Select(u => new
                    {
                        account_id = u.AccountId,
                        account_name = u.AccountName,
                        alert_count = u.AlertCount,
                        message = u.Message
                    }),
                    skipped_non_azure = result.SkippedNonAzure != null ? new
                    {
                        count = result.SkippedNonAzure.Count,
                        cloud_types = result.SkippedNonAzure.CloudTypes,
                        message = result.SkippedNonAzure.Message
                    } : null
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
            Logger.LogError(ex, "Prisma API JSON import failed");
            return ErrorJson("IMPORT_FAILED", $"Prisma API JSON import failed: {ex.Message}");
        }
    }

    private static string ErrorJson(string code, string message) =>
        JsonSerializer.Serialize(new
        {
            status = "error",
            errorCode = code,
            message
        }, s_jsonOpts);
}

// ═══════════════════════════════════════════════════════════════════════════
// ListPrismaPoliciesTool — US3 Policy Catalog
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// MCP tool: List unique Prisma policies observed across scan imports for a system
/// with NIST control mappings, finding counts, and affected resource types.
/// </summary>
public class ListPrismaPoliciesTool : BaseTool
{
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true };
    private readonly IScanImportService _importService;

    public ListPrismaPoliciesTool(
        IScanImportService importService,
        ILogger<ListPrismaPoliciesTool> logger)
        : base(logger)
    {
        _importService = importService;
    }

    public override string Name => "compliance_list_prisma_policies";

    public override string Description =>
        "List unique Prisma Cloud policies observed across scan imports for a system. " +
        "Returns NIST 800-53 control mappings, open/resolved/dismissed counts, " +
        "affected resource types, and last-seen import details.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new()
        {
            Name = "system_id",
            Description = "RegisteredSystem ID (required).",
            Type = "string",
            Required = true
        }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var systemId = GetArg<string>(arguments, "system_id");

        if (string.IsNullOrWhiteSpace(systemId))
            return ErrorJson("INVALID_INPUT", "The 'system_id' parameter is required.");

        try
        {
            var result = await _importService.ListPrismaPoliciesAsync(systemId, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    system_id = result.SystemId,
                    total_policies = result.TotalPolicies,
                    policies = result.Policies.Select(p => new
                    {
                        policy_name = p.PolicyName,
                        policy_type = p.PolicyType,
                        severity = p.Severity,
                        nist_control_ids = p.NistControlIds,
                        open_count = p.OpenCount,
                        resolved_count = p.ResolvedCount,
                        dismissed_count = p.DismissedCount,
                        affected_resource_types = p.AffectedResourceTypes,
                        last_seen_import_id = p.LastSeenImportId,
                        last_seen_at = p.LastSeenAt.ToString("O")
                    })
                },
                metadata = new
                {
                    tool = Name,
                    timestamp = DateTime.UtcNow.ToString("O")
                }
            }, s_jsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorJson("NOT_FOUND", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error listing Prisma policies");
            return ErrorJson("QUERY_FAILED", $"Failed to list Prisma policies: {ex.Message}");
        }
    }

    private static string ErrorJson(string code, string message) =>
        JsonSerializer.Serialize(new
        {
            status = "error",
            errorCode = code,
            message
        }, s_jsonOpts);
}

// ═══════════════════════════════════════════════════════════════════════════
// PrismaTrendTool — US3 Trend Analysis
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// MCP tool: Compare Prisma findings across scan imports for a system,
/// showing remediation progress, new/resolved/persistent findings, and
/// optional group-by breakdowns.
/// </summary>
public class PrismaTrendTool : BaseTool
{
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true };
    private readonly IScanImportService _importService;

    public PrismaTrendTool(
        IScanImportService importService,
        ILogger<PrismaTrendTool> logger)
        : base(logger)
    {
        _importService = importService;
    }

    public override string Name => "compliance_prisma_trend";

    public override string Description =>
        "Compare Prisma Cloud findings across scan imports to track remediation progress. " +
        "Shows new, resolved, and persistent findings with remediation rate calculation. " +
        "Supports optional group_by for resource_type or nist_control breakdowns.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new()
        {
            Name = "system_id",
            Description = "RegisteredSystem ID (required).",
            Type = "string",
            Required = true
        },
        ["import_ids"] = new()
        {
            Name = "import_ids",
            Description = "Specific import IDs to compare (optional, JSON array). If omitted, uses last 2 Prisma imports.",
            Type = "string",
            Required = false
        },
        ["group_by"] = new()
        {
            Name = "group_by",
            Description = "Group results by 'resource_type' or 'nist_control' (optional).",
            Type = "string",
            Required = false
        }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var systemId = GetArg<string>(arguments, "system_id");
        var importIdsStr = GetArg<string>(arguments, "import_ids");
        var groupBy = GetArg<string>(arguments, "group_by");

        if (string.IsNullOrWhiteSpace(systemId))
            return ErrorJson("INVALID_INPUT", "The 'system_id' parameter is required.");

        List<string>? importIds = null;
        if (!string.IsNullOrWhiteSpace(importIdsStr))
        {
            try
            {
                importIds = JsonSerializer.Deserialize<List<string>>(importIdsStr);
            }
            catch
            {
                return ErrorJson("INVALID_INPUT", "The 'import_ids' parameter must be a valid JSON array of strings.");
            }
        }

        try
        {
            var result = await _importService.GetPrismaTrendAsync(systemId, importIds, groupBy, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    system_id = result.SystemId,
                    imports = result.Imports.Select(i => new
                    {
                        import_id = i.ImportId,
                        imported_at = i.ImportedAt.ToString("O"),
                        file_name = i.FileName,
                        total_alerts = i.TotalAlerts,
                        open_count = i.OpenCount,
                        resolved_count = i.ResolvedCount,
                        dismissed_count = i.DismissedCount
                    }),
                    new_findings = result.NewFindings,
                    resolved_findings = result.ResolvedFindings,
                    persistent_findings = result.PersistentFindings,
                    remediation_rate = result.RemediationRate,
                    resource_type_breakdown = result.ResourceTypeBreakdown,
                    nist_control_breakdown = result.NistControlBreakdown
                },
                metadata = new
                {
                    tool = Name,
                    timestamp = DateTime.UtcNow.ToString("O")
                }
            }, s_jsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return ErrorJson("NOT_FOUND", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating Prisma trend analysis");
            return ErrorJson("QUERY_FAILED", $"Failed to generate Prisma trend: {ex.Message}");
        }
    }

    private static string ErrorJson(string code, string message) =>
        JsonSerializer.Serialize(new
        {
            status = "error",
            errorCode = code,
            message
        }, s_jsonOpts);
}
