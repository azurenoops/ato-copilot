using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Auth;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Tools;

// ═══════════════════════════════════════════════════════════════════════════════
// Feature 021 — Privacy & Interconnection MCP Tools (PTA / PIA)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// MCP tool: compliance_create_pta — Conduct a Privacy Threshold Analysis.
/// RBAC: ISSO, SecurityLead, Administrator.
/// </summary>
public class CreatePtaTool : BaseTool
{
    private readonly IPrivacyService _privacyService;
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public CreatePtaTool(IPrivacyService privacyService, ILogger<CreatePtaTool> logger) : base(logger)
    {
        _privacyService = privacyService;
    }

    public override string Name => "compliance_create_pta";

    public override string Description =>
        "Conduct a Privacy Threshold Analysis (PTA) for a registered system. " +
        "Auto-detects PII from categorized information types or accepts manual PII flags.";

    public override PimTier RequiredPimTier => PimTier.Write;

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true },
        ["manual_mode"] = new() { Name = "manual_mode", Description = "If true, use explicit PII flags instead of auto-detection", Type = "boolean", Required = false },
        ["collects_pii"] = new() { Name = "collects_pii", Description = "Manual mode: whether system collects PII", Type = "boolean", Required = false },
        ["maintains_pii"] = new() { Name = "maintains_pii", Description = "Manual mode: whether system maintains PII", Type = "boolean", Required = false },
        ["disseminates_pii"] = new() { Name = "disseminates_pii", Description = "Manual mode: whether system disseminates PII", Type = "boolean", Required = false },
        ["pii_categories"] = new() { Name = "pii_categories", Description = "JSON array of PII categories", Type = "string", Required = false },
        ["estimated_record_count"] = new() { Name = "estimated_record_count", Description = "Estimated number of PII records", Type = "integer", Required = false },
        ["exemption_rationale"] = new() { Name = "exemption_rationale", Description = "Exemption justification if exempt", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var systemId = GetArg<string>(arguments, "system_id");
        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");

        var manualMode = GetArg<bool?>(arguments, "manual_mode") ?? false;
        var collectsPii = GetArg<bool?>(arguments, "collects_pii") ?? false;
        var maintainsPii = GetArg<bool?>(arguments, "maintains_pii") ?? false;
        var disseminatesPii = GetArg<bool?>(arguments, "disseminates_pii") ?? false;
        var piiCategoriesJson = GetArg<string>(arguments, "pii_categories");
        var estimatedRecordCount = GetArg<int?>(arguments, "estimated_record_count");
        var exemptionRationale = GetArg<string>(arguments, "exemption_rationale");

        List<string>? piiCategories = null;
        if (!string.IsNullOrWhiteSpace(piiCategoriesJson))
        {
            try { piiCategories = JsonSerializer.Deserialize<List<string>>(piiCategoriesJson, s_jsonOpts); }
            catch (JsonException) { return Error("INVALID_INPUT", "Invalid pii_categories JSON. Expected array of strings."); }
        }

        try
        {
            var result = await _privacyService.CreatePtaAsync(
                systemId, "mcp-user", manualMode, collectsPii, maintainsPii, disseminatesPii,
                piiCategories, estimatedRecordCount, exemptionRationale, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    pta_id = result.PtaId,
                    determination = result.Determination.ToString(),
                    collects_pii = result.CollectsPii,
                    maintains_pii = result.MaintainsPii,
                    disseminates_pii = result.DisseminatesPii,
                    pii_categories = result.PiiCategories,
                    pii_source_info_types = result.PiiSourceInfoTypes,
                    rationale = result.Rationale
                }
            }, s_jsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("NOT_FOUND", ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, s_jsonOpts);
}

/// <summary>
/// MCP tool: compliance_generate_pia — Generate a Privacy Impact Assessment.
/// RBAC: ISSO, SecurityLead, Administrator.
/// </summary>
public class GeneratePiaTool : BaseTool
{
    private readonly IPrivacyService _privacyService;
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public GeneratePiaTool(IPrivacyService privacyService, ILogger<GeneratePiaTool> logger) : base(logger)
    {
        _privacyService = privacyService;
    }

    public override string Name => "compliance_generate_pia";

    public override string Description =>
        "Generate a Privacy Impact Assessment (PIA) with 8 OMB M-03-22 sections. " +
        "Requires a completed PTA with PiaRequired determination.";

    public override PimTier RequiredPimTier => PimTier.Write;

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var systemId = GetArg<string>(arguments, "system_id");
        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");

        try
        {
            var result = await _privacyService.GeneratePiaAsync(systemId, "mcp-user", cancellationToken);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    pia_id = result.PiaId,
                    pia_status = result.Status.ToString(),
                    version = result.Version,
                    total_sections = result.TotalSections,
                    pre_populated_sections = result.PrePopulatedSections,
                    sections = result.Sections.Select(s => new
                    {
                        section_id = s.SectionId,
                        title = s.Title,
                        question = s.Question,
                        answer = s.Answer,
                        is_pre_populated = s.IsPrePopulated
                    })
                }
            }, s_jsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("PRECONDITION_FAILED", ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, s_jsonOpts);
}

/// <summary>
/// MCP tool: compliance_review_pia — Review a PIA (approve or request revision).
/// RBAC: ISSM, AuthorizingOfficial, Administrator.
/// </summary>
public class ReviewPiaTool : BaseTool
{
    private readonly IPrivacyService _privacyService;
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public ReviewPiaTool(IPrivacyService privacyService, ILogger<ReviewPiaTool> logger) : base(logger)
    {
        _privacyService = privacyService;
    }

    public override string Name => "compliance_review_pia";

    public override string Description =>
        "Review a Privacy Impact Assessment — approve or request revision with deficiency notes.";

    public override PimTier RequiredPimTier => PimTier.Write;

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true },
        ["decision"] = new() { Name = "decision", Description = "Review decision: 'Approved' or 'RequestRevision'", Type = "string", Required = true },
        ["reviewer_comments"] = new() { Name = "reviewer_comments", Description = "Reviewer notes and observations", Type = "string", Required = true },
        ["deficiencies"] = new() { Name = "deficiencies", Description = "JSON array of deficiency strings (required for RequestRevision)", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var systemId = GetArg<string>(arguments, "system_id");
        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");

        var decisionStr = GetArg<string>(arguments, "decision");
        if (string.IsNullOrWhiteSpace(decisionStr))
            return Error("INVALID_INPUT", "The 'decision' parameter is required.");

        if (!Enum.TryParse<PiaReviewDecision>(decisionStr, ignoreCase: true, out var decision))
            return Error("INVALID_INPUT", $"Invalid decision '{decisionStr}'. Valid values: Approved, RequestRevision.");

        var reviewerComments = GetArg<string>(arguments, "reviewer_comments") ?? string.Empty;
        var deficienciesJson = GetArg<string>(arguments, "deficiencies");

        List<string>? deficiencies = null;
        if (!string.IsNullOrWhiteSpace(deficienciesJson))
        {
            try { deficiencies = JsonSerializer.Deserialize<List<string>>(deficienciesJson, s_jsonOpts); }
            catch (JsonException) { return Error("INVALID_INPUT", "Invalid deficiencies JSON. Expected array of strings."); }
        }

        try
        {
            var result = await _privacyService.ReviewPiaAsync(
                systemId, decision, reviewerComments, "mcp-user", deficiencies, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    pia_id = result.PiaId,
                    decision = result.Decision.ToString(),
                    new_status = result.NewStatus.ToString(),
                    reviewer_comments = result.ReviewerComments,
                    deficiencies = result.Deficiencies,
                    expiration_date = result.ExpirationDate?.ToString("O")
                }
            }, s_jsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("PRECONDITION_FAILED", ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, s_jsonOpts);
}

/// <summary>
/// MCP tool: compliance_check_privacy_compliance — Get privacy compliance status.
/// RBAC: All compliance roles.
/// </summary>
public class CheckPrivacyComplianceTool : BaseTool
{
    private readonly IPrivacyService _privacyService;
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public CheckPrivacyComplianceTool(IPrivacyService privacyService, ILogger<CheckPrivacyComplianceTool> logger) : base(logger)
    {
        _privacyService = privacyService;
    }

    public override string Name => "compliance_check_privacy_compliance";

    public override string Description =>
        "Get privacy compliance dashboard for a system — aggregates PTA, PIA, and gate status.";

    public override PimTier RequiredPimTier => PimTier.Read;

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var systemId = GetArg<string>(arguments, "system_id");
        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");

        try
        {
            var result = await _privacyService.GetPrivacyComplianceAsync(systemId, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    system_id = result.SystemId,
                    system_name = result.SystemName,
                    pta_determination = result.PtaDetermination?.ToString(),
                    pia_status = result.PiaStatus?.ToString(),
                    privacy_gate_satisfied = result.PrivacyGateSatisfied,
                    active_interconnections = result.ActiveInterconnections,
                    interconnections_with_agreements = result.InterconnectionsWithAgreements,
                    expired_agreements = result.ExpiredAgreements,
                    expiring_within_90_days = result.ExpiringWithin90Days,
                    interconnection_gate_satisfied = result.InterconnectionGateSatisfied,
                    has_no_external_interconnections = result.HasNoExternalInterconnections,
                    overall_status = result.OverallStatus
                }
            }, s_jsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("NOT_FOUND", ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, s_jsonOpts);
}
