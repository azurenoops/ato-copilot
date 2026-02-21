using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Agents.Compliance.Tools;

/// <summary>
/// Tool for running NIST 800-53 compliance assessments
/// </summary>
public class ComplianceAssessmentTool : BaseTool
{
    private readonly IAtoComplianceEngine _complianceEngine;

    public ComplianceAssessmentTool(IAtoComplianceEngine complianceEngine, ILogger<ComplianceAssessmentTool> logger) : base(logger)
    {
        _complianceEngine = complianceEngine;
    }

    public override string Name => "compliance_assess";
    public override string Description => "Run a NIST 800-53 compliance assessment against Azure resources. Supports scan types: quick, policy, full.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Azure subscription ID", Type = "string", Required = true },
        ["framework"] = new() { Name = "framework", Description = "Compliance framework", Type = "string" },
        ["control_families"] = new() { Name = "control_families", Description = "Control families to assess", Type = "string" },
        ["resource_types"] = new() { Name = "resource_types", Description = "Resource types to assess", Type = "string" },
        ["scan_type"] = new() { Name = "scan_type", Description = "Scan type: quick, policy, full", Type = "string" },
        ["include_passed"] = new() { Name = "include_passed", Description = "Include passed controls", Type = "boolean" }
    };

    public override async Task<string> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var subscriptionId = GetArg<string>(arguments, "subscription_id");
        var framework = GetArg<string>(arguments, "framework");
        var controlFamilies = GetArg<string>(arguments, "control_families");
        var resourceTypes = GetArg<string>(arguments, "resource_types");
        var scanType = GetArg<string>(arguments, "scan_type") ?? "quick";
        var includePassed = GetArg<bool?>(arguments, "include_passed") ?? false;

        Logger.LogInformation("Running compliance assessment | Sub: {Sub} | Type: {Type}", subscriptionId, scanType);

        var result = await _complianceEngine.RunAssessmentAsync(
            subscriptionId ?? "", framework, controlFamilies, resourceTypes, scanType, includePassed, cancellationToken);

        return $"## Compliance Assessment Results\n\n" +
               $"**Subscription**: {result.SubscriptionId}\n" +
               $"**Framework**: {result.Framework}\n" +
               $"**Scan Type**: {result.ScanType}\n" +
               $"**Assessed**: {result.AssessedAt:yyyy-MM-dd HH:mm:ss UTC}\n\n" +
               $"### Score: {result.ComplianceScore:F1}%\n\n" +
               $"| Metric | Count |\n|--------|-------|\n" +
               $"| Total Controls | {result.TotalControls} |\n" +
               $"| ✅ Passed | {result.PassedControls} |\n" +
               $"| ❌ Failed | {result.FailedControls} |\n" +
               $"| ⚪ Not Assessed | {result.NotAssessedControls} |\n\n" +
               $"### Findings ({result.Findings.Count})\n\n" +
               string.Join("\n", result.Findings.Take(10).Select(f =>
                   $"- **{f.Severity}** [{f.ControlId}] {f.Title}"));
    }
}

/// <summary>
/// Tool for NIST control family details
/// </summary>
public class ControlFamilyTool : BaseTool
{
    private readonly INistControlsService _nistService;

    public ControlFamilyTool(INistControlsService nistService, ILogger<ControlFamilyTool> logger) : base(logger)
    {
        _nistService = nistService;
    }

    public override string Name => "compliance_get_control_family";
    public override string Description => "Get detailed information about a NIST 800-53 control family.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["family_id"] = new() { Name = "family_id", Description = "Control family (e.g., AC, AU, IA)", Type = "string", Required = true },
        ["include_controls"] = new() { Name = "include_controls", Description = "Include individual controls", Type = "boolean" }
    };

    public override async Task<string> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var familyId = GetArg<string>(arguments, "family_id") ?? "AC";
        var includeControls = GetArg<bool?>(arguments, "include_controls") ?? true;

        var controls = await _nistService.GetControlFamilyAsync(familyId, includeControls, cancellationToken);

        return $"## NIST 800-53 Control Family: {familyId}\n\n" +
               $"**Controls Found**: {controls.Count}\n\n" +
               string.Join("\n", controls.Select(c => $"- **{c.Id}** - {c.Title}\n  {c.Description}"));
    }
}

/// <summary>
/// Tool for generating compliance documents (SSP, POA&M, SAR)
/// </summary>
public class DocumentGenerationTool : BaseTool
{
    private readonly IDocumentGenerationService _documentService;

    public DocumentGenerationTool(IDocumentGenerationService documentService, ILogger<DocumentGenerationTool> logger) : base(logger)
    {
        _documentService = documentService;
    }

    public override string Name => "compliance_generate_document";
    public override string Description => "Generate compliance documentation (SSP, POA&M, SAR).";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["document_type"] = new() { Name = "document_type", Description = "Document type: ssp, poam, sar", Type = "string", Required = true },
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Azure subscription for evidence", Type = "string" },
        ["framework"] = new() { Name = "framework", Description = "Compliance framework", Type = "string" },
        ["system_name"] = new() { Name = "system_name", Description = "System name for document", Type = "string" }
    };

    public override async Task<string> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var documentType = GetArg<string>(arguments, "document_type") ?? "ssp";
        var subscriptionId = GetArg<string>(arguments, "subscription_id");
        var framework = GetArg<string>(arguments, "framework");
        var systemName = GetArg<string>(arguments, "system_name");

        var doc = await _documentService.GenerateDocumentAsync(documentType, subscriptionId, framework, systemName, cancellationToken);
        return doc.Content;
    }
}

/// <summary>
/// Tool for collecting compliance evidence
/// </summary>
public class EvidenceCollectionTool : BaseTool
{
    private readonly IEvidenceStorageService _evidenceService;

    public EvidenceCollectionTool(IEvidenceStorageService evidenceService, ILogger<EvidenceCollectionTool> logger) : base(logger)
    {
        _evidenceService = evidenceService;
    }

    public override string Name => "compliance_collect_evidence";
    public override string Description => "Collect compliance evidence from Azure resources for audit documentation.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["control_id"] = new() { Name = "control_id", Description = "NIST control ID", Type = "string", Required = true },
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Azure subscription ID", Type = "string", Required = true },
        ["resource_group"] = new() { Name = "resource_group", Description = "Resource group filter", Type = "string" }
    };

    public override async Task<string> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var controlId = GetArg<string>(arguments, "control_id") ?? "";
        var subscriptionId = GetArg<string>(arguments, "subscription_id") ?? "";
        var resourceGroup = GetArg<string>(arguments, "resource_group");

        var evidence = await _evidenceService.CollectEvidenceAsync(controlId, subscriptionId, resourceGroup, cancellationToken);
        return $"## Evidence Collected\n\n**Control**: {evidence.ControlId}\n**Type**: {evidence.EvidenceType}\n\n{evidence.Content}";
    }
}

/// <summary>
/// Tool for remediating compliance findings
/// </summary>
public class RemediationExecuteTool : BaseTool
{
    private readonly IRemediationEngine _remediationEngine;

    public RemediationExecuteTool(IRemediationEngine remediationEngine, ILogger<RemediationExecuteTool> logger) : base(logger)
    {
        _remediationEngine = remediationEngine;
    }

    public override string Name => "compliance_remediate";
    public override string Description => "Remediate a compliance finding with guided or automated fixes.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["finding_id"] = new() { Name = "finding_id", Description = "Finding ID to remediate", Type = "string", Required = true },
        ["apply_remediation"] = new() { Name = "apply_remediation", Description = "Apply fix automatically", Type = "boolean" },
        ["dry_run"] = new() { Name = "dry_run", Description = "Preview without applying", Type = "boolean" }
    };

    public override async Task<string> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var findingId = GetArg<string>(arguments, "finding_id") ?? "";
        var applyRemediation = GetArg<bool?>(arguments, "apply_remediation") ?? false;
        var dryRun = GetArg<bool?>(arguments, "dry_run") ?? true;

        return await _remediationEngine.ExecuteRemediationAsync(findingId, applyRemediation, dryRun, cancellationToken);
    }
}

/// <summary>
/// Tool for validating remediations
/// </summary>
public class ValidateRemediationTool : BaseTool
{
    private readonly IRemediationEngine _remediationEngine;

    public ValidateRemediationTool(IRemediationEngine remediationEngine, ILogger<ValidateRemediationTool> logger) : base(logger)
    {
        _remediationEngine = remediationEngine;
    }

    public override string Name => "compliance_validate_remediation";
    public override string Description => "Validate that a remediation was successfully applied.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["finding_id"] = new() { Name = "finding_id", Description = "Finding ID to validate", Type = "string", Required = true },
        ["execution_id"] = new() { Name = "execution_id", Description = "Execution ID to validate", Type = "string" },
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Azure subscription", Type = "string" }
    };

    public override async Task<string> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var findingId = GetArg<string>(arguments, "finding_id") ?? "";
        var executionId = GetArg<string>(arguments, "execution_id");
        var subscriptionId = GetArg<string>(arguments, "subscription_id");

        return await _remediationEngine.ValidateRemediationAsync(findingId, executionId, subscriptionId, cancellationToken);
    }
}

/// <summary>
/// Tool for generating remediation plans
/// </summary>
public class RemediationPlanTool : BaseTool
{
    private readonly IRemediationEngine _remediationEngine;

    public RemediationPlanTool(IRemediationEngine remediationEngine, ILogger<RemediationPlanTool> logger) : base(logger)
    {
        _remediationEngine = remediationEngine;
    }

    public override string Name => "compliance_generate_plan";
    public override string Description => "Generate a prioritized remediation plan for compliance findings.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Azure subscription", Type = "string" },
        ["resource_group_name"] = new() { Name = "resource_group_name", Description = "Resource group filter", Type = "string" }
    };

    public override async Task<string> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var subscriptionId = GetArg<string>(arguments, "subscription_id");
        var resourceGroupName = GetArg<string>(arguments, "resource_group_name");

        var plan = await _remediationEngine.GeneratePlanAsync(subscriptionId ?? "", resourceGroupName, cancellationToken);

        return $"## Remediation Plan\n\n" +
               $"**Total Findings**: {plan.TotalFindings}\n" +
               $"**Auto-Remediable**: {plan.AutoRemediableCount}\n\n" +
               $"### Steps\n\n" +
               string.Join("\n", plan.Steps.Select((s, i) =>
                   $"{i + 1}. [{s.ControlId}] {s.Description} (Priority: {s.Priority}, Effort: {s.Effort})"));
    }
}

/// <summary>
/// Tool for viewing assessment audit logs
/// </summary>
public class AssessmentAuditLogTool : BaseTool
{
    private readonly IAssessmentAuditService _auditService;

    public AssessmentAuditLogTool(IAssessmentAuditService auditService, ILogger<AssessmentAuditLogTool> logger) : base(logger)
    {
        _auditService = auditService;
    }

    public override string Name => "compliance_audit_log";
    public override string Description => "Get the audit trail of compliance assessments.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Azure subscription", Type = "string" },
        ["days"] = new() { Name = "days", Description = "Number of days to look back", Type = "integer" }
    };

    public override async Task<string> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var subscriptionId = GetArg<string>(arguments, "subscription_id");
        var days = GetArg<int?>(arguments, "days") ?? 7;

        return await _auditService.GetAuditLogAsync(subscriptionId, days, cancellationToken);
    }
}

/// <summary>
/// Tool for viewing compliance history
/// </summary>
public class ComplianceHistoryTool : BaseTool
{
    private readonly IComplianceHistoryService _historyService;

    public ComplianceHistoryTool(IComplianceHistoryService historyService, ILogger<ComplianceHistoryTool> logger) : base(logger)
    {
        _historyService = historyService;
    }

    public override string Name => "compliance_history";
    public override string Description => "Get compliance history and trends over time.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Azure subscription", Type = "string" },
        ["days"] = new() { Name = "days", Description = "Number of days to look back", Type = "integer" }
    };

    public override async Task<string> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var subscriptionId = GetArg<string>(arguments, "subscription_id");
        var days = GetArg<int?>(arguments, "days") ?? 30;

        return await _historyService.GetHistoryAsync(subscriptionId, days, cancellationToken);
    }
}

/// <summary>
/// Tool for getting current compliance status
/// </summary>
public class ComplianceStatusTool : BaseTool
{
    private readonly IComplianceStatusService _statusService;

    public ComplianceStatusTool(IComplianceStatusService statusService, ILogger<ComplianceStatusTool> logger) : base(logger)
    {
        _statusService = statusService;
    }

    public override string Name => "compliance_status";
    public override string Description => "Get current compliance status and posture summary.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Azure subscription", Type = "string" },
        ["framework"] = new() { Name = "framework", Description = "Compliance framework", Type = "string" }
    };

    public override async Task<string> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var subscriptionId = GetArg<string>(arguments, "subscription_id");
        var framework = GetArg<string>(arguments, "framework");

        return await _statusService.GetStatusAsync(subscriptionId, framework, cancellationToken);
    }
}

/// <summary>
/// Tool for continuous compliance monitoring
/// </summary>
public class ComplianceMonitoringTool : BaseTool
{
    private readonly IComplianceMonitoringService _monitoringService;

    public ComplianceMonitoringTool(IComplianceMonitoringService monitoringService, ILogger<ComplianceMonitoringTool> logger) : base(logger)
    {
        _monitoringService = monitoringService;
    }

    public override string Name => "compliance_monitoring";
    public override string Description => "Query continuous compliance monitoring status, alerts, and trends.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["action"] = new() { Name = "action", Description = "Action: status, scan, alerts, acknowledge, trend, history", Type = "string", Required = true },
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Azure subscription", Type = "string" },
        ["days"] = new() { Name = "days", Description = "Days to look back", Type = "integer" }
    };

    public override async Task<string> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var action = GetArg<string>(arguments, "action") ?? "status";
        var subscriptionId = GetArg<string>(arguments, "subscription_id");
        var days = GetArg<int?>(arguments, "days") ?? 30;

        return action switch
        {
            "status" => await _monitoringService.GetStatusAsync(subscriptionId, cancellationToken),
            "scan" => await _monitoringService.TriggerScanAsync(subscriptionId, cancellationToken),
            "alerts" => await _monitoringService.GetAlertsAsync(subscriptionId, days, cancellationToken),
            "trend" => await _monitoringService.GetTrendAsync(subscriptionId, days, cancellationToken),
            _ => await _monitoringService.GetStatusAsync(subscriptionId, cancellationToken)
        };
    }
}
