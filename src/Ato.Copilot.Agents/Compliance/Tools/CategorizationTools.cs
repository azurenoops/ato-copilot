using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Tools;

// ────────────────────────────────────────────────────────────────────────────
// T038: CategorizeSystemTool
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// MCP tool: compliance_categorize_system — Perform FIPS 199 security categorization.
/// RBAC: Compliance.Administrator, Compliance.PlatformEngineer, ISSM
/// </summary>
public class CategorizeSystemTool : BaseTool
{
    private readonly ICategorizationService _categorizationService;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public CategorizeSystemTool(
        ICategorizationService categorizationService,
        ILogger<CategorizeSystemTool> logger) : base(logger)
    {
        _categorizationService = categorizationService;
    }

    public override string Name => "compliance_categorize_system";

    public override string Description =>
        "Perform or update FIPS 199 / SP 800-60 security categorization for a registered system. " +
        "Provide information types with C/I/A impact levels. Returns computed high-water mark, DoD IL, and NIST baseline.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true },
        ["information_types"] = new() { Name = "information_types", Description = "Array of info types with sp800_60_id, name, confidentiality_impact, integrity_impact, availability_impact (Low|Moderate|High)", Type = "array", Required = true },
        ["is_national_security_system"] = new() { Name = "is_national_security_system", Description = "Whether the system is designated NSS (affects IL derivation)", Type = "boolean", Required = false },
        ["justification"] = new() { Name = "justification", Description = "Overall categorization rationale", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var systemId = GetArg<string>(arguments, "system_id");
        var justification = GetArg<string>(arguments, "justification");
        var isNss = GetArg<bool?>(arguments, "is_national_security_system") ?? false;

        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");

        // Parse information_types from the argument
        var infoTypesRaw = GetArg<object>(arguments, "information_types");
        if (infoTypesRaw == null)
            return Error("INVALID_INPUT", "The 'information_types' parameter is required.");

        List<InformationTypeInput> infoTypes;
        try
        {
            infoTypes = ParseInformationTypes(infoTypesRaw);
        }
        catch (Exception ex)
        {
            return Error("INVALID_INPUT", $"Failed to parse information_types: {ex.Message}");
        }

        if (infoTypes.Count == 0)
            return Error("INVALID_INPUT", "At least one information type is required.");

        try
        {
            var result = await _categorizationService.CategorizeSystemAsync(
                systemId, infoTypes, "mcp-user", isNss, justification, cancellationToken);

            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = FormatCategorization(result),
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("CATEGORIZATION_FAILED", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_categorize_system failed for '{SystemId}'", systemId);
            return Error("CATEGORIZATION_FAILED", ex.Message);
        }
    }

    private static List<InformationTypeInput> ParseInformationTypes(object raw)
    {
        // Handle JsonElement (from MCP JSON deserialization)
        if (raw is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("information_types must be an array.");

            var result = new List<InformationTypeInput>();
            foreach (var item in jsonElement.EnumerateArray())
            {
                // Try case-insensitive deserialization first (handles camelCase, PascalCase, snake_case)
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsed = JsonSerializer.Deserialize<InformationTypeInput>(item.GetRawText(), opts);
                if (parsed != null && !string.IsNullOrWhiteSpace(parsed.Sp80060Id))
                {
                    result.Add(parsed);
                    continue;
                }

                // Fallback: try known property name variations manually
                result.Add(new InformationTypeInput
                {
                    Sp80060Id = GetJsonProp(item, "sp800_60_id", "sp80060Id", "sp80060_id", "Sp80060Id", "SP800_60_ID", "id") ?? "",
                    Name = GetJsonProp(item, "name", "Name", "info_type_name", "infoTypeName") ?? "",
                    Category = GetJsonProp(item, "category", "Category"),
                    ConfidentialityImpact = GetJsonProp(item, "confidentiality_impact", "confidentialityImpact", "ConfidentialityImpact", "confidentiality") ?? "Low",
                    IntegrityImpact = GetJsonProp(item, "integrity_impact", "integrityImpact", "IntegrityImpact", "integrity") ?? "Low",
                    AvailabilityImpact = GetJsonProp(item, "availability_impact", "availabilityImpact", "AvailabilityImpact", "availability") ?? "Low",
                    UsesProvisional = item.TryGetProperty("uses_provisional", out var up) ? up.GetBoolean()
                        : item.TryGetProperty("usesProvisional", out up) ? up.GetBoolean() : true,
                    AdjustmentJustification = GetJsonProp(item, "adjustment_justification", "adjustmentJustification", "AdjustmentJustification")
                });
            }
            return result;
        }

        // Handle pre-deserialized List<InformationTypeInput>
        if (raw is IEnumerable<InformationTypeInput> typed)
            return typed.ToList();

        // Handle generic list/collection
        if (raw is System.Collections.IEnumerable enumerable)
        {
            var json = JsonSerializer.Serialize(raw);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<InformationTypeInput>>(json, opts) ?? [];
        }

        throw new InvalidOperationException("information_types must be an array of info type objects.");
    }

    /// <summary>Try multiple property name variations and return the first match.</summary>
    private static string? GetJsonProp(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var prop))
                return prop.GetString();
        }
        return null;
    }

    private static object FormatCategorization(SecurityCategorization sc) => new
    {
        id = sc.Id,
        system_id = sc.RegisteredSystemId,
        system_name = sc.RegisteredSystem?.Name,
        confidentiality_impact = sc.ConfidentialityImpact.ToString(),
        integrity_impact = sc.IntegrityImpact.ToString(),
        availability_impact = sc.AvailabilityImpact.ToString(),
        overall_categorization = sc.OverallCategorization.ToString(),
        fips_199_notation = sc.FormalNotation,
        dod_impact_level = sc.DoDImpactLevel,
        nist_baseline = sc.NistBaseline,
        is_national_security_system = sc.IsNationalSecuritySystem,
        information_type_count = sc.InformationTypes.Count,
        information_types = sc.InformationTypes.Select(it => new
        {
            id = it.Id,
            sp800_60_id = it.Sp80060Id,
            name = it.Name,
            category = it.Category,
            confidentiality = it.ConfidentialityImpact.ToString(),
            integrity = it.IntegrityImpact.ToString(),
            availability = it.AvailabilityImpact.ToString(),
            uses_provisional = it.UsesProvisionalImpactLevels,
            adjustment_justification = it.AdjustmentJustification
        }).ToList(),
        justification = sc.Justification,
        categorized_by = sc.CategorizedBy,
        categorized_at = sc.CategorizedAt.ToString("O")
    };

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, JsonOpts);

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        execution_time_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };
}

// ────────────────────────────────────────────────────────────────────────────
// T039: GetCategorizationTool
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// MCP tool: compliance_get_categorization — Retrieve security categorization for a system.
/// </summary>
public class GetCategorizationTool : BaseTool
{
    private readonly ICategorizationService _categorizationService;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public GetCategorizationTool(
        ICategorizationService categorizationService,
        ILogger<GetCategorizationTool> logger) : base(logger)
    {
        _categorizationService = categorizationService;
    }

    public override string Name => "compliance_get_categorization";

    public override string Description =>
        "Retrieve the FIPS 199 security categorization for a registered system, including all information types and computed fields.";

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
            var categorization = await _categorizationService.GetCategorizationAsync(
                systemId, cancellationToken);

            sw.Stop();

            if (categorization == null)
            {
                return JsonSerializer.Serialize(new
                {
                    status = "success",
                    data = (object?)null,
                    message = $"No categorization found for system '{systemId}'.",
                    metadata = Meta(sw)
                }, JsonOpts);
            }

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    id = categorization.Id,
                    system_id = categorization.RegisteredSystemId,
                    system_name = categorization.RegisteredSystem?.Name,
                    confidentiality_impact = categorization.ConfidentialityImpact.ToString(),
                    integrity_impact = categorization.IntegrityImpact.ToString(),
                    availability_impact = categorization.AvailabilityImpact.ToString(),
                    overall_categorization = categorization.OverallCategorization.ToString(),
                    fips_199_notation = categorization.FormalNotation,
                    dod_impact_level = categorization.DoDImpactLevel,
                    nist_baseline = categorization.NistBaseline,
                    is_national_security_system = categorization.IsNationalSecuritySystem,
                    information_type_count = categorization.InformationTypes.Count,
                    information_types = categorization.InformationTypes.Select(it => new
                    {
                        id = it.Id,
                        sp800_60_id = it.Sp80060Id,
                        name = it.Name,
                        category = it.Category,
                        confidentiality = it.ConfidentialityImpact.ToString(),
                        integrity = it.IntegrityImpact.ToString(),
                        availability = it.AvailabilityImpact.ToString(),
                        uses_provisional = it.UsesProvisionalImpactLevels,
                        adjustment_justification = it.AdjustmentJustification
                    }).ToList(),
                    justification = categorization.Justification,
                    categorized_by = categorization.CategorizedBy,
                    categorized_at = categorization.CategorizedAt.ToString("O")
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_get_categorization failed for '{SystemId}'", systemId);
            return Error("RETRIEVAL_FAILED", ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, JsonOpts);

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        execution_time_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };
}

// ────────────────────────────────────────────────────────────────────────────
// T040: SuggestInfoTypesTool
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// MCP tool: compliance_suggest_info_types — AI-assisted SP 800-60 info type suggestions.
/// </summary>
public class SuggestInfoTypesTool : BaseTool
{
    private readonly ICategorizationService _categorizationService;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public SuggestInfoTypesTool(
        ICategorizationService categorizationService,
        ILogger<SuggestInfoTypesTool> logger) : base(logger)
    {
        _categorizationService = categorizationService;
    }

    public override string Name => "compliance_suggest_info_types";

    public override string Description =>
        "Suggest SP 800-60 information types based on system description and type. Returns ranked list with confidence scores.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true },
        ["description"] = new() { Name = "description", Description = "Additional context for better suggestions", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var systemId = GetArg<string>(arguments, "system_id");
        var description = GetArg<string>(arguments, "description");

        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");

        try
        {
            var suggestions = await _categorizationService.SuggestInfoTypesAsync(
                systemId, description, cancellationToken);

            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    system_id = systemId,
                    suggestion_count = suggestions.Count,
                    suggestions = suggestions.Select(s => new
                    {
                        sp800_60_id = s.Sp80060Id,
                        name = s.Name,
                        category = s.Category,
                        confidence = Math.Round(s.Confidence, 2),
                        rationale = s.Rationale,
                        default_confidentiality = s.DefaultConfidentialityImpact,
                        default_integrity = s.DefaultIntegrityImpact,
                        default_availability = s.DefaultAvailabilityImpact
                    }).ToList()
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("SUGGESTION_FAILED", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_suggest_info_types failed for '{SystemId}'", systemId);
            return Error("SUGGESTION_FAILED", ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, JsonOpts);

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        execution_time_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };
}
