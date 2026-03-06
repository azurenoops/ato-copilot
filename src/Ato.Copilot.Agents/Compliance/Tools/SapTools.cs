using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Tools;

// ═══════════════════════════════════════════════════════════════════════════════
// Feature 018 — SAP (Security Assessment Plan) MCP Tools
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// MCP tool: compliance_generate_sap — Generate a Security Assessment Plan for a registered system.
/// Auto-populates from baseline, OSCAL objectives, STIG mappings, and evidence data.
/// RBAC: Analyst, SecurityLead, Administrator.
/// </summary>
/// <remarks>Feature 018, T017.</remarks>
public class GenerateSapTool : BaseTool
{
    private readonly ISapService _sapService;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public GenerateSapTool(
        ISapService sapService,
        ILogger<GenerateSapTool> logger) : base(logger)
    {
        _sapService = sapService;
    }

    public override string Name => "compliance_generate_sap";

    public override string Description =>
        "Generate a Security Assessment Plan (SAP) for a registered system. " +
        "Auto-populates from control baseline, OSCAL assessment objectives, STIG mappings, " +
        "and evidence data. Accepts SCA overrides for schedule, team, scope, and per-control " +
        "assessment methods. Produces a Markdown SAP document with 15 sections.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true },
        ["assessment_id"] = new() { Name = "assessment_id", Description = "Optional assessment cycle ID to link SAP to", Type = "string", Required = false },
        ["schedule_start"] = new() { Name = "schedule_start", Description = "Assessment start date (ISO 8601)", Type = "string", Required = false },
        ["schedule_end"] = new() { Name = "schedule_end", Description = "Assessment end date (ISO 8601)", Type = "string", Required = false },
        ["team_members"] = new() { Name = "team_members", Description = "JSON array of { name, organization, role, contact_info? }", Type = "string", Required = false },
        ["scope_notes"] = new() { Name = "scope_notes", Description = "SCA-provided assessment scope notes", Type = "string", Required = false },
        ["method_overrides"] = new() { Name = "method_overrides", Description = "JSON array of { control_id, methods[], rationale? }", Type = "string", Required = false },
        ["rules_of_engagement"] = new() { Name = "rules_of_engagement", Description = "Assessment constraints and availability windows", Type = "string", Required = false },
        ["format"] = new() { Name = "format", Description = "Output format: 'markdown' (default), 'docx', or 'pdf'", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var systemId = GetArg<string>(arguments, "system_id");
        var assessmentId = GetArg<string>(arguments, "assessment_id");
        var scheduleStartStr = GetArg<string>(arguments, "schedule_start");
        var scheduleEndStr = GetArg<string>(arguments, "schedule_end");
        var teamMembersJson = GetArg<string>(arguments, "team_members");
        var scopeNotes = GetArg<string>(arguments, "scope_notes");
        var methodOverridesJson = GetArg<string>(arguments, "method_overrides");
        var roe = GetArg<string>(arguments, "rules_of_engagement");
        var format = GetArg<string>(arguments, "format") ?? "markdown";

        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");

        // Validate format
        if (!string.Equals(format, "markdown", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(format, "docx", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
            return Error("INVALID_FORMAT", $"Unsupported format '{format}'. Valid formats: markdown, docx, pdf.");

        // Parse optional dates
        DateTime? scheduleStart = null;
        DateTime? scheduleEnd = null;
        if (!string.IsNullOrWhiteSpace(scheduleStartStr))
        {
            if (!DateTime.TryParse(scheduleStartStr, out var parsedStart))
                return Error("INVALID_INPUT", $"Invalid schedule_start date '{scheduleStartStr}'. Use ISO 8601 format.");
            scheduleStart = DateTime.SpecifyKind(parsedStart, DateTimeKind.Utc);
        }
        if (!string.IsNullOrWhiteSpace(scheduleEndStr))
        {
            if (!DateTime.TryParse(scheduleEndStr, out var parsedEnd))
                return Error("INVALID_INPUT", $"Invalid schedule_end date '{scheduleEndStr}'. Use ISO 8601 format.");
            scheduleEnd = DateTime.SpecifyKind(parsedEnd, DateTimeKind.Utc);
        }

        // Parse optional team members
        List<SapTeamMemberInput>? teamMembers = null;
        if (!string.IsNullOrWhiteSpace(teamMembersJson))
        {
            try
            {
                teamMembers = JsonSerializer.Deserialize<List<SapTeamMemberInput>>(teamMembersJson, JsonOpts);
            }
            catch (JsonException)
            {
                return Error("INVALID_INPUT", "Invalid team_members JSON. Expected array of { name, organization, role, contact_info? }.");
            }
        }

        // Parse optional method overrides
        List<SapMethodOverrideInput>? methodOverrides = null;
        if (!string.IsNullOrWhiteSpace(methodOverridesJson))
        {
            try
            {
                methodOverrides = JsonSerializer.Deserialize<List<SapMethodOverrideInput>>(methodOverridesJson, JsonOpts);
            }
            catch (JsonException)
            {
                return Error("INVALID_INPUT", "Invalid method_overrides JSON. Expected array of { control_id, methods[], rationale? }.");
            }
        }

        try
        {
            var input = new SapGenerationInput(
                SystemId: systemId,
                AssessmentId: assessmentId,
                ScheduleStart: scheduleStart,
                ScheduleEnd: scheduleEnd,
                ScopeNotes: scopeNotes,
                RulesOfEngagement: roe,
                TeamMembers: teamMembers,
                MethodOverrides: methodOverrides,
                Format: format);

            var doc = await _sapService.GenerateSapAsync(input, cancellationToken: cancellationToken);

            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    sap_id = doc.SapId,
                    system_id = doc.SystemId,
                    assessment_id = doc.AssessmentId,
                    title = doc.Title,
                    status = doc.Status,
                    format = doc.Format,
                    baseline_level = doc.BaselineLevel,
                    total_controls = doc.TotalControls,
                    customer_controls = doc.CustomerControls,
                    inherited_controls = doc.InheritedControls,
                    shared_controls = doc.SharedControls,
                    stig_benchmark_count = doc.StigBenchmarkCount,
                    controls_with_objectives = doc.ControlsWithObjectives,
                    evidence_gaps = doc.EvidenceGaps,
                    family_summaries = doc.FamilySummaries.Select(f => new
                    {
                        family = f.Family,
                        control_count = f.ControlCount,
                        customer_count = f.CustomerCount,
                        inherited_count = f.InheritedCount,
                        methods = f.Methods
                    }),
                    content = doc.Content,
                    generated_at = doc.GeneratedAt.ToString("O"),
                    warnings = doc.Warnings
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("System", StringComparison.OrdinalIgnoreCase) &&
            ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return Error("SYSTEM_NOT_FOUND", ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("baseline", StringComparison.OrdinalIgnoreCase))
        {
            return Error("BASELINE_NOT_FOUND", ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("method", StringComparison.OrdinalIgnoreCase))
        {
            return Error("INVALID_METHOD", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Error("GENERATE_SAP_FAILED", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_generate_sap failed for '{SystemId}'", systemId);
            return Error("GENERATE_SAP_FAILED", ex.Message);
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

// ─────────────────────────────────────────────────────────────────────────────
// T027: UpdateSapTool
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// MCP tool: compliance_update_sap — Update a Draft SAP's schedule, scope, team, methods, or ROE.
/// RBAC: Analyst, SecurityLead, Administrator.
/// </summary>
/// <remarks>Feature 018, T027.</remarks>
public class UpdateSapTool : BaseTool
{
    private readonly ISapService _sapService;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public UpdateSapTool(
        ISapService sapService,
        ILogger<UpdateSapTool> logger) : base(logger)
    {
        _sapService = sapService;
    }

    public override string Name => "compliance_update_sap";

    public override string Description =>
        "Update a Draft SAP's schedule, scope, team, assessment methods, or rules of engagement. " +
        "Team replacement is atomic. Method overrides are additive (only specified controls updated). " +
        "Finalized SAPs cannot be modified.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["sap_id"] = new() { Name = "sap_id", Description = "SAP ID to update", Type = "string", Required = true },
        ["schedule_start"] = new() { Name = "schedule_start", Description = "Updated assessment start date (ISO 8601)", Type = "string", Required = false },
        ["schedule_end"] = new() { Name = "schedule_end", Description = "Updated assessment end date (ISO 8601)", Type = "string", Required = false },
        ["scope_notes"] = new() { Name = "scope_notes", Description = "Updated scope notes", Type = "string", Required = false },
        ["rules_of_engagement"] = new() { Name = "rules_of_engagement", Description = "Updated rules of engagement", Type = "string", Required = false },
        ["team_members"] = new() { Name = "team_members", Description = "JSON array of { name, organization, role, contact_info? } — replaces entire team", Type = "string", Required = false },
        ["method_overrides"] = new() { Name = "method_overrides", Description = "JSON array of { control_id, methods[], rationale? } — additive per-control overrides", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var sapId = GetArg<string>(arguments, "sap_id");
        var scheduleStartStr = GetArg<string>(arguments, "schedule_start");
        var scheduleEndStr = GetArg<string>(arguments, "schedule_end");
        var scopeNotes = GetArg<string>(arguments, "scope_notes");
        var roe = GetArg<string>(arguments, "rules_of_engagement");
        var teamMembersJson = GetArg<string>(arguments, "team_members");
        var methodOverridesJson = GetArg<string>(arguments, "method_overrides");

        if (string.IsNullOrWhiteSpace(sapId))
            return Error("INVALID_INPUT", "The 'sap_id' parameter is required.");

        // Parse optional dates
        DateTime? scheduleStart = null;
        DateTime? scheduleEnd = null;
        if (!string.IsNullOrWhiteSpace(scheduleStartStr))
        {
            if (!DateTime.TryParse(scheduleStartStr, out var parsedStart))
                return Error("INVALID_INPUT", $"Invalid schedule_start date '{scheduleStartStr}'. Use ISO 8601 format.");
            scheduleStart = DateTime.SpecifyKind(parsedStart, DateTimeKind.Utc);
        }
        if (!string.IsNullOrWhiteSpace(scheduleEndStr))
        {
            if (!DateTime.TryParse(scheduleEndStr, out var parsedEnd))
                return Error("INVALID_INPUT", $"Invalid schedule_end date '{scheduleEndStr}'. Use ISO 8601 format.");
            scheduleEnd = DateTime.SpecifyKind(parsedEnd, DateTimeKind.Utc);
        }

        // Parse optional team members
        List<SapTeamMemberInput>? teamMembers = null;
        if (!string.IsNullOrWhiteSpace(teamMembersJson))
        {
            try { teamMembers = JsonSerializer.Deserialize<List<SapTeamMemberInput>>(teamMembersJson, JsonOpts); }
            catch (JsonException) { return Error("INVALID_INPUT", "Invalid team_members JSON. Expected array of { name, organization, role, contact_info? }."); }
        }

        // Parse optional method overrides
        List<SapMethodOverrideInput>? methodOverrides = null;
        if (!string.IsNullOrWhiteSpace(methodOverridesJson))
        {
            try { methodOverrides = JsonSerializer.Deserialize<List<SapMethodOverrideInput>>(methodOverridesJson, JsonOpts); }
            catch (JsonException) { return Error("INVALID_INPUT", "Invalid method_overrides JSON. Expected array of { control_id, methods[], rationale? }."); }
        }

        try
        {
            var updateInput = new SapUpdateInput(
                SapId: sapId,
                ScheduleStart: scheduleStart,
                ScheduleEnd: scheduleEnd,
                ScopeNotes: scopeNotes,
                RulesOfEngagement: roe,
                TeamMembers: teamMembers,
                MethodOverrides: methodOverrides);

            var doc = await _sapService.UpdateSapAsync(updateInput, cancellationToken);

            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    sap_id = doc.SapId,
                    status = doc.Status,
                    content = doc.Content,
                    updated_at = DateTime.UtcNow.ToString("O")
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("finalized", StringComparison.OrdinalIgnoreCase))
        {
            return Error("SAP_FINALIZED", ex.Message);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) &&
            ex.Message.Contains("SAP", StringComparison.OrdinalIgnoreCase))
        {
            return Error("SAP_NOT_FOUND", ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("method", StringComparison.OrdinalIgnoreCase))
        {
            return Error("INVALID_METHOD", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Error("UPDATE_SAP_FAILED", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_update_sap failed for '{SapId}'", sapId);
            return Error("UPDATE_SAP_FAILED", ex.Message);
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

// ─────────────────────────────────────────────────────────────────────────────
// T028: FinalizeSapTool
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// MCP tool: compliance_finalize_sap — Lock a Draft SAP as Finalized with SHA-256 integrity hash.
/// RBAC: Analyst, SecurityLead, Administrator.
/// </summary>
/// <remarks>Feature 018, T028.</remarks>
public class FinalizeSapTool : BaseTool
{
    private readonly ISapService _sapService;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public FinalizeSapTool(
        ISapService sapService,
        ILogger<FinalizeSapTool> logger) : base(logger)
    {
        _sapService = sapService;
    }

    public override string Name => "compliance_finalize_sap";

    public override string Description =>
        "Finalize a Draft SAP — locks it with SHA-256 content hash. " +
        "Finalized SAPs are immutable: no updates, no re-finalization. " +
        "Sets FinalizedBy, FinalizedAt, and ContentHash fields.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["sap_id"] = new() { Name = "sap_id", Description = "SAP ID to finalize", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var sapId = GetArg<string>(arguments, "sap_id");

        if (string.IsNullOrWhiteSpace(sapId))
            return Error("INVALID_INPUT", "The 'sap_id' parameter is required.");

        try
        {
            var doc = await _sapService.FinalizeSapAsync(sapId, cancellationToken: cancellationToken);

            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    sap_id = doc.SapId,
                    status = doc.Status,
                    content_hash = doc.ContentHash,
                    finalized_by = "mcp-user",
                    finalized_at = doc.FinalizedAt?.ToString("O"),
                    total_controls = doc.TotalControls,
                    title = doc.Title
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("finalized", StringComparison.OrdinalIgnoreCase))
        {
            return Error("SAP_FINALIZED", ex.Message);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) &&
            ex.Message.Contains("SAP", StringComparison.OrdinalIgnoreCase))
        {
            return Error("SAP_NOT_FOUND", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Error("FINALIZE_SAP_FAILED", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_finalize_sap failed for '{SapId}'", sapId);
            return Error("FINALIZE_SAP_FAILED", ex.Message);
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

// ═══════════════════════════════════════════════════════════════════════════════
// T041: GetSapTool
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// MCP tool: compliance_get_sap — Retrieve a specific SAP by ID or the latest SAP for a system.
/// RBAC: All roles except Viewer.
/// </summary>
/// <remarks>Feature 018, T041.</remarks>
public class GetSapTool : BaseTool
{
    private readonly ISapService _sapService;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public GetSapTool(
        ISapService sapService,
        ILogger<GetSapTool> logger) : base(logger)
    {
        _sapService = sapService;
    }

    public override string Name => "compliance_get_sap";

    public override string Description =>
        "Retrieve a specific Security Assessment Plan (SAP) by its ID, or the latest SAP " +
        "for a system. If both sap_id and system_id are provided, sap_id takes precedence. " +
        "When retrieving by system_id, prefers Finalized SAPs over Drafts.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["sap_id"] = new() { Name = "sap_id", Description = "Specific SAP ID to retrieve", Type = "string", Required = false },
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym — returns latest SAP (prefers Finalized)", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var sapId = GetArg<string>(arguments, "sap_id");
        var systemId = GetArg<string>(arguments, "system_id");

        if (string.IsNullOrWhiteSpace(sapId) && string.IsNullOrWhiteSpace(systemId))
            return Error("MISSING_PARAMETER", "Either 'sap_id' or 'system_id' must be provided.");

        try
        {
            var doc = await _sapService.GetSapAsync(sapId, systemId, cancellationToken);

            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    sap_id = doc.SapId,
                    system_id = doc.SystemId,
                    assessment_id = doc.AssessmentId,
                    title = doc.Title,
                    status = doc.Status,
                    format = doc.Format,
                    baseline_level = doc.BaselineLevel,
                    total_controls = doc.TotalControls,
                    customer_controls = doc.CustomerControls,
                    inherited_controls = doc.InheritedControls,
                    shared_controls = doc.SharedControls,
                    stig_benchmark_count = doc.StigBenchmarkCount,
                    controls_with_objectives = doc.ControlsWithObjectives,
                    content = doc.Content,
                    content_hash = doc.ContentHash,
                    generated_at = doc.GeneratedAt.ToString("O"),
                    finalized_at = doc.FinalizedAt?.ToString("O"),
                    family_summaries = doc.FamilySummaries.Select(f => new
                    {
                        family = f.Family,
                        control_count = f.ControlCount,
                        customer_count = f.CustomerCount,
                        inherited_count = f.InheritedCount,
                        methods = f.Methods
                    })
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return Error("SAP_NOT_FOUND", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_get_sap failed");
            return Error("GET_SAP_FAILED", ex.Message);
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

// ═══════════════════════════════════════════════════════════════════════════════
// T042: ListSapsTool
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// MCP tool: compliance_list_saps — List all SAPs for a system with status, dates, and scope summary.
/// RBAC: All roles except Viewer.
/// </summary>
/// <remarks>Feature 018, T042.</remarks>
public class ListSapsTool : BaseTool
{
    private readonly ISapService _sapService;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ListSapsTool(
        ISapService sapService,
        ILogger<ListSapsTool> logger) : base(logger)
    {
        _sapService = sapService;
    }

    public override string Name => "compliance_list_saps";

    public override string Description =>
        "List all Security Assessment Plans (SAPs) for a system, including Draft and Finalized " +
        "history. Returns status, dates, and scope summary per SAP. Content is omitted for brevity.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var systemId = GetArg<string>(arguments, "system_id");

        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");

        try
        {
            var saps = await _sapService.ListSapsAsync(systemId, cancellationToken);

            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    system_id = systemId,
                    sap_count = saps.Count,
                    saps = saps.Select(doc => new
                    {
                        sap_id = doc.SapId,
                        title = doc.Title,
                        status = doc.Status,
                        baseline_level = doc.BaselineLevel,
                        total_controls = doc.TotalControls,
                        customer_controls = doc.CustomerControls,
                        inherited_controls = doc.InheritedControls,
                        shared_controls = doc.SharedControls,
                        generated_at = doc.GeneratedAt.ToString("O"),
                        finalized_at = doc.FinalizedAt?.ToString("O")
                    })
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_list_saps failed for '{SystemId}'", systemId);
            return Error("LIST_SAPS_FAILED", ex.Message);
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
