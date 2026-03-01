using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Tools;

// ────────────────────────────────────────────────────────────────────────────
// T070: WriteNarrativeTool
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// MCP tool: compliance_write_narrative — Write or update a control implementation narrative.
/// RBAC: Compliance.PlatformEngineer, Compliance.SecurityLead
/// </summary>
public class WriteNarrativeTool : BaseTool
{
    private readonly ISspService _sspService;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public WriteNarrativeTool(
        ISspService sspService,
        ILogger<WriteNarrativeTool> logger) : base(logger)
    {
        _sspService = sspService;
    }

    public override string Name => "compliance_write_narrative";

    public override string Description =>
        "Write or update the implementation narrative for a NIST 800-53 control in a system's SSP. " +
        "Creates a new narrative or updates an existing one. " +
        "RBAC: Compliance.PlatformEngineer or Compliance.SecurityLead.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "RegisteredSystem ID (GUID)", Type = "string", Required = true },
        ["control_id"] = new() { Name = "control_id", Description = "NIST 800-53 control ID (e.g., 'AC-1')", Type = "string", Required = true },
        ["narrative"] = new() { Name = "narrative", Description = "Implementation narrative text", Type = "string", Required = true },
        ["status"] = new() { Name = "status", Description = "Implementation status: Implemented, PartiallyImplemented, Planned, NotApplicable (default: Implemented)", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var systemId = GetArg<string>(arguments, "system_id");
        var controlId = GetArg<string>(arguments, "control_id");
        var narrative = GetArg<string>(arguments, "narrative");
        var status = GetArg<string>(arguments, "status");

        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");
        if (string.IsNullOrWhiteSpace(controlId))
            return Error("INVALID_INPUT", "The 'control_id' parameter is required.");
        if (string.IsNullOrWhiteSpace(narrative))
            return Error("INVALID_INPUT", "The 'narrative' parameter is required.");

        try
        {
            var result = await _sspService.WriteNarrativeAsync(
                systemId, controlId, narrative, status, "mcp-user", cancellationToken);

            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = FormatImplementation(result),
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("WRITE_NARRATIVE_FAILED", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_write_narrative failed for '{ControlId}' in '{SystemId}'", controlId, systemId);
            return Error("WRITE_NARRATIVE_FAILED", ex.Message);
        }
    }

    private static object FormatImplementation(ControlImplementation ci) => new
    {
        id = ci.Id,
        system_id = ci.RegisteredSystemId,
        control_id = ci.ControlId,
        implementation_status = ci.ImplementationStatus.ToString(),
        narrative = ci.Narrative,
        is_auto_populated = ci.IsAutoPopulated,
        ai_suggested = ci.AiSuggested,
        authored_by = ci.AuthoredBy,
        authored_at = ci.AuthoredAt.ToString("O"),
        modified_at = ci.ModifiedAt?.ToString("O")
    };

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, JsonOpts);

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };
}

// ────────────────────────────────────────────────────────────────────────────
// T071: SuggestNarrativeTool
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// MCP tool: compliance_suggest_narrative — AI-generated draft narrative for a control.
/// </summary>
public class SuggestNarrativeTool : BaseTool
{
    private readonly ISspService _sspService;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public SuggestNarrativeTool(
        ISspService sspService,
        ILogger<SuggestNarrativeTool> logger) : base(logger)
    {
        _sspService = sspService;
    }

    public override string Name => "compliance_suggest_narrative";

    public override string Description =>
        "Generate an AI-suggested implementation narrative for a NIST 800-53 control " +
        "based on system context, control requirements, and inheritance data. " +
        "Returns a draft narrative with confidence score and reference sources.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "RegisteredSystem ID (GUID)", Type = "string", Required = true },
        ["control_id"] = new() { Name = "control_id", Description = "NIST 800-53 control ID (e.g., 'AC-2')", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var systemId = GetArg<string>(arguments, "system_id");
        var controlId = GetArg<string>(arguments, "control_id");

        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");
        if (string.IsNullOrWhiteSpace(controlId))
            return Error("INVALID_INPUT", "The 'control_id' parameter is required.");

        try
        {
            var suggestion = await _sspService.SuggestNarrativeAsync(
                systemId, controlId, cancellationToken);

            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    control_id = suggestion.ControlId,
                    suggested_narrative = suggestion.Narrative,
                    confidence = suggestion.Confidence,
                    references = suggestion.References
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("SUGGEST_NARRATIVE_FAILED", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_suggest_narrative failed for '{ControlId}' in '{SystemId}'", controlId, systemId);
            return Error("SUGGEST_NARRATIVE_FAILED", ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, JsonOpts);

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };
}

// ────────────────────────────────────────────────────────────────────────────
// T072: BatchPopulateNarrativesTool
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// MCP tool: compliance_batch_populate_narratives — Auto-populate inherited control narratives.
/// </summary>
public class BatchPopulateNarrativesTool : BaseTool
{
    private readonly ISspService _sspService;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public BatchPopulateNarrativesTool(
        ISspService sspService,
        ILogger<BatchPopulateNarrativesTool> logger) : base(logger)
    {
        _sspService = sspService;
    }

    public override string Name => "compliance_batch_populate_narratives";

    public override string Description =>
        "Auto-populate implementation narratives for inherited and/or shared controls " +
        "using provider templates. Skips controls that already have narratives (idempotent). " +
        "Significantly speeds up SSP authoring by pre-filling inherited control documentation.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "RegisteredSystem ID (GUID)", Type = "string", Required = true },
        ["inheritance_type"] = new() { Name = "inheritance_type", Description = "Filter: 'Inherited', 'Shared', or omit for both", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var systemId = GetArg<string>(arguments, "system_id");
        var inheritanceType = GetArg<string>(arguments, "inheritance_type");

        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");

        try
        {
            var result = await _sspService.BatchPopulateNarrativesAsync(
                systemId, inheritanceType, "mcp-user", progress: null, cancellationToken);

            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    populated_count = result.PopulatedCount,
                    skipped_count = result.SkippedCount,
                    populated_control_ids = result.PopulatedControlIds,
                    skipped_control_ids = result.SkippedControlIds
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("BATCH_POPULATE_FAILED", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_batch_populate_narratives failed for '{SystemId}'", systemId);
            return Error("BATCH_POPULATE_FAILED", ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, JsonOpts);

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };
}

// ────────────────────────────────────────────────────────────────────────────
// T073: NarrativeProgressTool
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// MCP tool: compliance_narrative_progress — Track SSP narrative completion status.
/// </summary>
public class NarrativeProgressTool : BaseTool
{
    private readonly ISspService _sspService;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public NarrativeProgressTool(
        ISspService sspService,
        ILogger<NarrativeProgressTool> logger) : base(logger)
    {
        _sspService = sspService;
    }

    public override string Name => "compliance_narrative_progress";

    public override string Description =>
        "Get SSP narrative completion status for a system. Shows per-family progress " +
        "(total, completed, draft, missing controls) and overall completion percentage. " +
        "Useful for tracking SSP readiness before assessment.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "RegisteredSystem ID (GUID)", Type = "string", Required = true },
        ["family_filter"] = new() { Name = "family_filter", Description = "Filter by control family prefix (e.g., 'AC')", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var systemId = GetArg<string>(arguments, "system_id");
        var familyFilter = GetArg<string>(arguments, "family_filter");

        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");

        try
        {
            var progress = await _sspService.GetNarrativeProgressAsync(
                systemId, familyFilter, cancellationToken);

            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    system_id = progress.SystemId,
                    total_controls = progress.TotalControls,
                    completed_narratives = progress.CompletedNarratives,
                    draft_narratives = progress.DraftNarratives,
                    missing_narratives = progress.MissingNarratives,
                    overall_percentage = progress.OverallPercentage,
                    family_breakdowns = progress.FamilyBreakdowns.Select(f => new
                    {
                        family = f.Family,
                        total = f.Total,
                        completed = f.Completed,
                        draft = f.Draft,
                        missing = f.Missing
                    })
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("PROGRESS_FAILED", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_narrative_progress failed for '{SystemId}'", systemId);
            return Error("PROGRESS_FAILED", ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, JsonOpts);

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };
}

// ────────────────────────────────────────────────────────────────────────────
// T074: GenerateSspTool
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// MCP tool: compliance_generate_ssp — Generate the System Security Plan document.
/// </summary>
public class GenerateSspTool : BaseTool
{
    private readonly ISspService _sspService;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public GenerateSspTool(
        ISspService sspService,
        ILogger<GenerateSspTool> logger) : base(logger)
    {
        _sspService = sspService;
    }

    public override string Name => "compliance_generate_ssp";

    public override string Description =>
        "Generate the System Security Plan (SSP) document for a registered system. " +
        "Produces a Markdown document containing system information, security categorization, " +
        "control baseline, and per-control implementation narratives. " +
        "Warns about controls with missing narratives.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "RegisteredSystem ID (GUID)", Type = "string", Required = true },
        ["format"] = new() { Name = "format", Description = "Output format: 'markdown' (default) or 'docx'", Type = "string", Required = false },
        ["sections"] = new() { Name = "sections", Description = "Specific sections to include (comma-separated): system_information, categorization, baseline, controls. Default: all.", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var systemId = GetArg<string>(arguments, "system_id");
        var format = GetArg<string>(arguments, "format") ?? "markdown";
        var sectionsStr = GetArg<string>(arguments, "sections");

        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");

        IEnumerable<string>? sections = null;
        if (!string.IsNullOrWhiteSpace(sectionsStr))
        {
            sections = sectionsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        try
        {
            var doc = await _sspService.GenerateSspAsync(
                systemId, format, sections, progress: null, cancellationToken);

            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    system_id = doc.SystemId,
                    system_name = doc.SystemName,
                    format = doc.Format,
                    total_controls = doc.TotalControls,
                    controls_with_narratives = doc.ControlsWithNarratives,
                    controls_missing_narratives = doc.ControlsMissingNarratives,
                    sections = doc.Sections,
                    warnings = doc.Warnings,
                    content = doc.Content,
                    generated_at = doc.GeneratedAt.ToString("O")
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("GENERATE_SSP_FAILED", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_generate_ssp failed for '{SystemId}'", systemId);
            return Error("GENERATE_SSP_FAILED", ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, JsonOpts);

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };
}
