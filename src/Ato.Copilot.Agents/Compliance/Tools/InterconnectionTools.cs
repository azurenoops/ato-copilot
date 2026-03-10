using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Auth;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Tools;

// ═══════════════════════════════════════════════════════════════════════════════
// Feature 021 — Interconnection MCP Tools (US2)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// MCP tool: compliance_add_interconnection — Register a system interconnection.
/// RBAC: PlatformEngineer, Analyst.
/// </summary>
public class AddInterconnectionTool : BaseTool
{
    private readonly IInterconnectionService _service;
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public AddInterconnectionTool(IInterconnectionService service, ILogger<AddInterconnectionTool> logger) : base(logger)
    {
        _service = service;
    }

    public override string Name => "compliance_add_interconnection";

    public override string Description =>
        "Register a system-to-system interconnection that crosses the authorization boundary. " +
        "Clears HasNoExternalInterconnections flag if previously set.";

    public override PimTier RequiredPimTier => PimTier.Write;

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true },
        ["target_system_name"] = new() { Name = "target_system_name", Description = "Name of external system", Type = "string", Required = true },
        ["connection_type"] = new() { Name = "connection_type", Description = "Connection type: direct, vpn, api, federated, wireless, remote_access", Type = "string", Required = true },
        ["data_flow_direction"] = new() { Name = "data_flow_direction", Description = "Data flow: inbound, outbound, bidirectional", Type = "string", Required = true },
        ["data_classification"] = new() { Name = "data_classification", Description = "Data classification: unclassified, cui, secret, top_secret", Type = "string", Required = true },
        ["target_system_owner"] = new() { Name = "target_system_owner", Description = "Organization/POC owning target system", Type = "string", Required = false },
        ["target_system_acronym"] = new() { Name = "target_system_acronym", Description = "Target system abbreviation", Type = "string", Required = false },
        ["data_description"] = new() { Name = "data_description", Description = "Description of data exchanged", Type = "string", Required = false },
        ["protocols"] = new() { Name = "protocols", Description = "JSON array of protocols used", Type = "string", Required = false },
        ["ports"] = new() { Name = "ports", Description = "JSON array of ports used", Type = "string", Required = false },
        ["security_measures"] = new() { Name = "security_measures", Description = "JSON array of security controls", Type = "string", Required = false },
        ["authentication_method"] = new() { Name = "authentication_method", Description = "How systems authenticate to each other", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var systemId = GetArg<string>(arguments, "system_id");
        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");

        var targetName = GetArg<string>(arguments, "target_system_name");
        if (string.IsNullOrWhiteSpace(targetName))
            return Error("INVALID_INPUT", "The 'target_system_name' parameter is required.");

        var connectionTypeStr = GetArg<string>(arguments, "connection_type");
        if (!TryParseEnum<InterconnectionType>(connectionTypeStr, out var connectionType))
            return Error("INVALID_INPUT", $"Invalid connection type '{connectionTypeStr}'. Valid values: direct, vpn, api, federated, wireless, remote_access");

        var flowStr = GetArg<string>(arguments, "data_flow_direction");
        if (!TryParseEnum<DataFlowDirection>(flowStr, out var dataFlow))
            return Error("INVALID_INPUT", $"Invalid data flow direction '{flowStr}'. Valid values: inbound, outbound, bidirectional");

        var classification = GetArg<string>(arguments, "data_classification");
        if (string.IsNullOrWhiteSpace(classification))
            return Error("INVALID_INPUT", "The 'data_classification' parameter is required.");

        var protocols = ParseJsonArray(GetArg<string>(arguments, "protocols"));
        var ports = ParseJsonArray(GetArg<string>(arguments, "ports"));
        var measures = ParseJsonArray(GetArg<string>(arguments, "security_measures"));

        try
        {
            var result = await _service.AddInterconnectionAsync(
                systemId, targetName, connectionType, dataFlow, classification,
                "mcp-user",
                GetArg<string>(arguments, "target_system_owner"),
                GetArg<string>(arguments, "target_system_acronym"),
                GetArg<string>(arguments, "data_description"),
                protocols, ports, measures,
                GetArg<string>(arguments, "authentication_method"),
                cancellationToken);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    interconnectionId = result.InterconnectionId,
                    targetSystemName = result.TargetSystemName,
                    interconnectionStatus = result.Status.ToString(),
                    hasAgreement = result.HasAgreement
                }
            }, s_jsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("NOT_FOUND", ex.Message);
        }
    }

    private static bool TryParseEnum<T>(string? value, out T result) where T : struct, Enum
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        // Normalize: "remote_access" → "RemoteAccess"
        var normalized = string.Join("", value.Split('_').Select(s =>
            char.ToUpperInvariant(s[0]) + s[1..]));
        return Enum.TryParse(normalized, ignoreCase: true, out result);
    }

    private static List<string>? ParseJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<List<string>>(json, s_jsonOpts); }
        catch (JsonException) { return null; }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, s_jsonOpts);
}

/// <summary>
/// MCP tool: compliance_list_interconnections — List system interconnections with agreement status.
/// RBAC: All roles.
/// </summary>
public class ListInterconnectionsTool : BaseTool
{
    private readonly IInterconnectionService _service;
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public ListInterconnectionsTool(IInterconnectionService service, ILogger<ListInterconnectionsTool> logger) : base(logger)
    {
        _service = service;
    }

    public override string Name => "compliance_list_interconnections";

    public override string Description =>
        "List all system interconnections with agreement status summaries. " +
        "Optionally filter by status.";

    public override PimTier RequiredPimTier => PimTier.Read;

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true },
        ["status_filter"] = new() { Name = "status_filter", Description = "Filter: proposed, active, suspended, terminated", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var systemId = GetArg<string>(arguments, "system_id");
        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");

        InterconnectionStatus? statusFilter = null;
        var filterStr = GetArg<string>(arguments, "status_filter");
        if (!string.IsNullOrWhiteSpace(filterStr))
        {
            if (!Enum.TryParse<InterconnectionStatus>(filterStr, ignoreCase: true, out var parsed))
                return Error("INVALID_INPUT", $"Invalid status filter '{filterStr}'. Valid values: proposed, active, suspended, terminated");
            statusFilter = parsed;
        }

        try
        {
            var results = await _service.ListInterconnectionsAsync(systemId, statusFilter, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    systemId,
                    totalInterconnections = results.Count,
                    interconnections = results.Select(r => new
                    {
                        id = r.InterconnectionId,
                        targetSystemName = r.TargetSystemName,
                        interconnectionStatus = r.Status.ToString(),
                        hasAgreement = r.HasAgreement
                    })
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
/// MCP tool: compliance_update_interconnection — Update interconnection details or status.
/// RBAC: Analyst, SecurityLead.
/// </summary>
public class UpdateInterconnectionTool : BaseTool
{
    private readonly IInterconnectionService _service;
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public UpdateInterconnectionTool(IInterconnectionService service, ILogger<UpdateInterconnectionTool> logger) : base(logger)
    {
        _service = service;
    }

    public override string Name => "compliance_update_interconnection";

    public override string Description =>
        "Update an existing interconnection's details or status. " +
        "Requires status_reason when suspending or terminating.";

    public override PimTier RequiredPimTier => PimTier.Write;

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["interconnection_id"] = new() { Name = "interconnection_id", Description = "SystemInterconnection ID (GUID)", Type = "string", Required = true },
        ["status"] = new() { Name = "status", Description = "New status: proposed, active, suspended, terminated", Type = "string", Required = false },
        ["status_reason"] = new() { Name = "status_reason", Description = "Reason for status change (required for suspended/terminated)", Type = "string", Required = false },
        ["connection_type"] = new() { Name = "connection_type", Description = "Updated connection type", Type = "string", Required = false },
        ["data_classification"] = new() { Name = "data_classification", Description = "Updated data classification", Type = "string", Required = false },
        ["protocols"] = new() { Name = "protocols", Description = "Updated protocols (JSON array)", Type = "string", Required = false },
        ["ports"] = new() { Name = "ports", Description = "Updated ports (JSON array)", Type = "string", Required = false },
        ["security_measures"] = new() { Name = "security_measures", Description = "Updated security controls (JSON array)", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var interconnectionId = GetArg<string>(arguments, "interconnection_id");
        if (string.IsNullOrWhiteSpace(interconnectionId))
            return Error("INVALID_INPUT", "The 'interconnection_id' parameter is required.");

        InterconnectionStatus? status = null;
        var statusStr = GetArg<string>(arguments, "status");
        if (!string.IsNullOrWhiteSpace(statusStr))
        {
            if (!Enum.TryParse<InterconnectionStatus>(statusStr, ignoreCase: true, out var parsed))
                return Error("INVALID_INPUT", $"Invalid status '{statusStr}'. Valid values: proposed, active, suspended, terminated");
            status = parsed;
        }

        InterconnectionType? connectionType = null;
        var ctStr = GetArg<string>(arguments, "connection_type");
        if (!string.IsNullOrWhiteSpace(ctStr))
        {
            var normalized = string.Join("", ctStr.Split('_').Select(s =>
                char.ToUpperInvariant(s[0]) + s[1..]));
            if (!Enum.TryParse<InterconnectionType>(normalized, ignoreCase: true, out var parsedCt))
                return Error("INVALID_INPUT", $"Invalid connection type '{ctStr}'.");
            connectionType = parsedCt;
        }

        var protocols = ParseJsonArray(GetArg<string>(arguments, "protocols"));
        var ports = ParseJsonArray(GetArg<string>(arguments, "ports"));
        var measures = ParseJsonArray(GetArg<string>(arguments, "security_measures"));

        try
        {
            var result = await _service.UpdateInterconnectionAsync(
                interconnectionId,
                interconnectionType: connectionType,
                dataClassification: GetArg<string>(arguments, "data_classification"),
                protocolsUsed: protocols,
                portsUsed: ports,
                securityMeasures: measures,
                status: status,
                statusReason: GetArg<string>(arguments, "status_reason"),
                cancellationToken: cancellationToken);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    interconnectionId = result.InterconnectionId,
                    targetSystemName = result.TargetSystemName,
                    interconnectionStatus = result.Status.ToString(),
                    hasAgreement = result.HasAgreement
                }
            }, s_jsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("VALIDATION_ERROR", ex.Message);
        }
    }

    private static List<string>? ParseJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<List<string>>(json, s_jsonOpts); }
        catch (JsonException) { return null; }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, s_jsonOpts);
}

// ═══════════════════════════════════════════════════════════════════════════════
// Feature 021 — ISA / Agreement MCP Tools (US3)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// MCP tool: compliance_generate_isa — Generate an ISA from interconnection data.
/// RBAC: SecurityLead only.
/// </summary>
public class GenerateIsaTool : BaseTool
{
    private readonly IInterconnectionService _service;
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public GenerateIsaTool(IInterconnectionService service, ILogger<GenerateIsaTool> logger) : base(logger)
    {
        _service = service;
    }

    public override string Name => "compliance_generate_isa";

    public override string Description =>
        "Generate an AI-drafted Interconnection Security Agreement (ISA) from interconnection data using NIST SP 800-47 structure.";

    public override PimTier RequiredPimTier => PimTier.Write;

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["interconnection_id"] = new() { Name = "interconnection_id", Description = "SystemInterconnection ID (GUID)", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var interconnectionId = GetArg<string>(arguments, "interconnection_id");
        if (string.IsNullOrWhiteSpace(interconnectionId))
            return Error("INVALID_INPUT", "The 'interconnection_id' parameter is required.");

        try
        {
            var result = await _service.GenerateIsaAsync(interconnectionId, "mcp-user", cancellationToken);
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    agreementId = result.AgreementId,
                    title = result.Title,
                    agreementType = result.AgreementType.ToString(),
                    narrativeDocument = result.NarrativeDocument
                }
            }, s_jsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("VALIDATION_ERROR", ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, s_jsonOpts);
}

/// <summary>
/// MCP tool: compliance_register_agreement — Register an ISA/MOU/SLA agreement.
/// RBAC: SecurityLead only.
/// </summary>
public class RegisterAgreementTool : BaseTool
{
    private readonly IInterconnectionService _service;
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public RegisterAgreementTool(IInterconnectionService service, ILogger<RegisterAgreementTool> logger) : base(logger)
    {
        _service = service;
    }

    public override string Name => "compliance_register_agreement";

    public override string Description =>
        "Register a pre-existing ISA, MOU, or SLA agreement for a system interconnection.";

    public override PimTier RequiredPimTier => PimTier.Write;

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["interconnection_id"] = new() { Name = "interconnection_id", Description = "SystemInterconnection ID", Type = "string", Required = true },
        ["agreement_type"] = new() { Name = "agreement_type", Description = "Agreement type: isa, mou, sla", Type = "string", Required = true },
        ["title"] = new() { Name = "title", Description = "Agreement title", Type = "string", Required = true },
        ["document_reference"] = new() { Name = "document_reference", Description = "URL or path to agreement document", Type = "string", Required = false },
        ["status"] = new() { Name = "status", Description = "Initial status: draft, pending_signature, signed", Type = "string", Required = false },
        ["effective_date"] = new() { Name = "effective_date", Description = "ISO 8601 effective date", Type = "string", Required = false },
        ["expiration_date"] = new() { Name = "expiration_date", Description = "ISO 8601 expiration date", Type = "string", Required = false },
        ["signed_by_local"] = new() { Name = "signed_by_local", Description = "Local signatory name/title", Type = "string", Required = false },
        ["signed_by_remote"] = new() { Name = "signed_by_remote", Description = "Remote signatory name/title", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var interconnectionId = GetArg<string>(arguments, "interconnection_id");
        if (string.IsNullOrWhiteSpace(interconnectionId))
            return Error("INVALID_INPUT", "The 'interconnection_id' parameter is required.");

        var typeStr = GetArg<string>(arguments, "agreement_type");
        if (!Enum.TryParse<AgreementType>(typeStr, ignoreCase: true, out var agreementType))
            return Error("INVALID_INPUT", $"Invalid agreement type '{typeStr}'. Valid values: isa, mou, sla");

        var title = GetArg<string>(arguments, "title");
        if (string.IsNullOrWhiteSpace(title))
            return Error("INVALID_INPUT", "The 'title' parameter is required.");

        var status = AgreementStatus.Draft;
        var statusStr = GetArg<string>(arguments, "status");
        if (!string.IsNullOrWhiteSpace(statusStr))
        {
            var normalized = string.Join("", statusStr.Split('_').Select(s =>
                char.ToUpperInvariant(s[0]) + s[1..]));
            if (!Enum.TryParse<AgreementStatus>(normalized, ignoreCase: true, out status))
                return Error("INVALID_INPUT", $"Invalid status '{statusStr}'.");
        }

        DateTime? effectiveDate = TryParseDate(GetArg<string>(arguments, "effective_date"));
        DateTime? expirationDate = TryParseDate(GetArg<string>(arguments, "expiration_date"));

        try
        {
            var agreement = await _service.RegisterAgreementAsync(
                interconnectionId, agreementType, title, "mcp-user",
                GetArg<string>(arguments, "document_reference"),
                status, effectiveDate, expirationDate,
                GetArg<string>(arguments, "signed_by_local"),
                GetArg<string>(arguments, "signed_by_remote"),
                cancellationToken);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    agreementId = agreement.Id,
                    title = agreement.Title,
                    agreementType = agreement.AgreementType.ToString(),
                    agreementStatus = agreement.Status.ToString(),
                    expirationDate = agreement.ExpirationDate
                }
            }, s_jsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("VALIDATION_ERROR", ex.Message);
        }
    }

    private static DateTime? TryParseDate(string? s) =>
        DateTime.TryParse(s, out var d) ? d : null;

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, s_jsonOpts);
}

/// <summary>
/// MCP tool: compliance_update_agreement — Update an agreement's status or metadata.
/// RBAC: SecurityLead only.
/// </summary>
public class UpdateAgreementTool : BaseTool
{
    private readonly IInterconnectionService _service;
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public UpdateAgreementTool(IInterconnectionService service, ILogger<UpdateAgreementTool> logger) : base(logger)
    {
        _service = service;
    }

    public override string Name => "compliance_update_agreement";

    public override string Description =>
        "Update an existing agreement's status, metadata, or signatories. Terminated agreements can only have review_notes updated.";

    public override PimTier RequiredPimTier => PimTier.Write;

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["agreement_id"] = new() { Name = "agreement_id", Description = "InterconnectionAgreement ID (GUID)", Type = "string", Required = true },
        ["status"] = new() { Name = "status", Description = "New status: draft, pending_signature, signed, expired, terminated", Type = "string", Required = false },
        ["effective_date"] = new() { Name = "effective_date", Description = "Updated ISO 8601 effective date", Type = "string", Required = false },
        ["expiration_date"] = new() { Name = "expiration_date", Description = "Updated ISO 8601 expiration date", Type = "string", Required = false },
        ["signed_by_local"] = new() { Name = "signed_by_local", Description = "Updated local signatory", Type = "string", Required = false },
        ["signed_by_local_date"] = new() { Name = "signed_by_local_date", Description = "Updated ISO 8601 local signature date", Type = "string", Required = false },
        ["signed_by_remote"] = new() { Name = "signed_by_remote", Description = "Updated remote signatory", Type = "string", Required = false },
        ["signed_by_remote_date"] = new() { Name = "signed_by_remote_date", Description = "Updated ISO 8601 remote signature date", Type = "string", Required = false },
        ["review_notes"] = new() { Name = "review_notes", Description = "Review or renewal notes", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var agreementId = GetArg<string>(arguments, "agreement_id");
        if (string.IsNullOrWhiteSpace(agreementId))
            return Error("INVALID_INPUT", "The 'agreement_id' parameter is required.");

        AgreementStatus? status = null;
        var statusStr = GetArg<string>(arguments, "status");
        if (!string.IsNullOrWhiteSpace(statusStr))
        {
            var normalized = string.Join("", statusStr.Split('_').Select(s =>
                char.ToUpperInvariant(s[0]) + s[1..]));
            if (!Enum.TryParse<AgreementStatus>(normalized, ignoreCase: true, out var parsed))
                return Error("INVALID_INPUT", $"Invalid status '{statusStr}'. Valid values: draft, pending_signature, signed, expired, terminated");
            status = parsed;
        }

        try
        {
            var agreement = await _service.UpdateAgreementAsync(
                agreementId, status,
                TryParseDate(GetArg<string>(arguments, "effective_date")),
                TryParseDate(GetArg<string>(arguments, "expiration_date")),
                GetArg<string>(arguments, "signed_by_local"),
                TryParseDate(GetArg<string>(arguments, "signed_by_local_date")),
                GetArg<string>(arguments, "signed_by_remote"),
                TryParseDate(GetArg<string>(arguments, "signed_by_remote_date")),
                GetArg<string>(arguments, "review_notes"),
                cancellationToken);

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    agreementId = agreement.Id,
                    title = agreement.Title,
                    agreementType = agreement.AgreementType.ToString(),
                    agreementStatus = agreement.Status.ToString(),
                    expirationDate = agreement.ExpirationDate
                }
            }, s_jsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("VALIDATION_ERROR", ex.Message);
        }
    }

    private static DateTime? TryParseDate(string? s) =>
        DateTime.TryParse(s, out var d) ? d : null;

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, s_jsonOpts);
}

/// <summary>
/// MCP tool: compliance_certify_no_interconnections — Certify a system has no external interconnections.
/// RBAC: Analyst, SecurityLead.
/// </summary>
public class CertifyNoInterconnectionsTool : BaseTool
{
    private readonly IInterconnectionService _service;
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public CertifyNoInterconnectionsTool(IInterconnectionService service, ILogger<CertifyNoInterconnectionsTool> logger) : base(logger)
    {
        _service = service;
    }

    public override string Name => "compliance_certify_no_interconnections";

    public override string Description =>
        "Certify that a system has no external interconnections, satisfying Gate 4 without requiring interconnection records.";

    public override PimTier RequiredPimTier => PimTier.Write;

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true },
        ["certify"] = new() { Name = "certify", Description = "true to certify no interconnections, false to revoke", Type = "boolean", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var systemId = GetArg<string>(arguments, "system_id");
        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");

        var certify = GetArg<bool?>(arguments, "certify") ?? true;

        try
        {
            await _service.CertifyNoInterconnectionsAsync(systemId, certify, cancellationToken);
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    systemId,
                    hasNoExternalInterconnections = certify,
                    interconnectionGateSatisfied = certify
                }
            }, s_jsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("VALIDATION_ERROR", ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, s_jsonOpts);
}

/// <summary>
/// MCP tool: compliance_validate_agreements — Validate all active interconnections have signed, current agreements.
/// RBAC: Auditor, SecurityLead.
/// </summary>
public class ValidateAgreementsTool : BaseTool
{
    private readonly IInterconnectionService _service;
    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public ValidateAgreementsTool(IInterconnectionService service, ILogger<ValidateAgreementsTool> logger) : base(logger)
    {
        _service = service;
    }

    public override string Name => "compliance_validate_agreements";

    public override string Description =>
        "Validate that all active system interconnections have signed, current agreements. Supports HasNoExternalInterconnections bypass.";

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
            var result = await _service.ValidateAgreementsAsync(systemId, cancellationToken);
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    totalInterconnections = result.TotalInterconnections,
                    compliantCount = result.CompliantCount,
                    expiringWithin90DaysCount = result.ExpiringWithin90DaysCount,
                    missingAgreementCount = result.MissingAgreementCount,
                    expiredAgreementCount = result.ExpiredAgreementCount,
                    isFullyCompliant = result.IsFullyCompliant,
                    items = result.Items.Select(i => new
                    {
                        interconnectionId = i.InterconnectionId,
                        targetSystemName = i.TargetSystemName,
                        validationStatus = i.ValidationStatus,
                        agreementTitle = i.AgreementTitle,
                        expirationDate = i.ExpirationDate,
                        notes = i.Notes
                    })
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
