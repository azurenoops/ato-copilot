using System.ComponentModel;
using Ato.Copilot.Agents.Compliance.Tools;

namespace Ato.Copilot.Mcp.Tools;

/// <summary>
/// MCP tools for compliance operations. Wraps Agent Framework compliance tools
/// for exposure via the MCP protocol (GitHub Copilot, Claude Desktop, etc.)
/// </summary>
public class ComplianceMcpTools
{
    private readonly ComplianceAssessmentTool _assessmentTool;
    private readonly ControlFamilyTool _controlFamilyTool;
    private readonly DocumentGenerationTool _documentGenerationTool;
    private readonly EvidenceCollectionTool _evidenceCollectionTool;
    private readonly RemediationExecuteTool _remediationTool;
    private readonly ValidateRemediationTool _validateRemediationTool;
    private readonly RemediationPlanTool _remediationPlanTool;
    private readonly AssessmentAuditLogTool _auditLogTool;
    private readonly ComplianceHistoryTool _historyTool;
    private readonly ComplianceStatusTool _statusTool;
    private readonly ComplianceMonitoringTool _monitoringTool;

    public ComplianceMcpTools(
        ComplianceAssessmentTool assessmentTool,
        ControlFamilyTool controlFamilyTool,
        DocumentGenerationTool documentGenerationTool,
        EvidenceCollectionTool evidenceCollectionTool,
        RemediationExecuteTool remediationTool,
        ValidateRemediationTool validateRemediationTool,
        RemediationPlanTool remediationPlanTool,
        AssessmentAuditLogTool auditLogTool,
        ComplianceHistoryTool historyTool,
        ComplianceStatusTool statusTool,
        ComplianceMonitoringTool monitoringTool)
    {
        _assessmentTool = assessmentTool;
        _controlFamilyTool = controlFamilyTool;
        _documentGenerationTool = documentGenerationTool;
        _evidenceCollectionTool = evidenceCollectionTool;
        _remediationTool = remediationTool;
        _validateRemediationTool = validateRemediationTool;
        _remediationPlanTool = remediationPlanTool;
        _auditLogTool = auditLogTool;
        _historyTool = historyTool;
        _statusTool = statusTool;
        _monitoringTool = monitoringTool;
    }

    [Description("Run a NIST 800-53 compliance assessment. Scan types: quick, policy, full.")]
    public async Task<string> RunComplianceAssessmentAsync(
        string? subscriptionId = null, string? framework = null,
        string? controlFamilies = null, string? resourceTypes = null,
        string? scanType = null, bool includePassed = false,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["subscription_id"] = subscriptionId, ["framework"] = framework,
            ["control_families"] = controlFamilies, ["resource_types"] = resourceTypes,
            ["scan_type"] = scanType, ["include_passed"] = includePassed
        };
        return await _assessmentTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Get detailed information about a NIST 800-53 control family.")]
    public async Task<string> GetControlFamilyInfoAsync(
        string familyId, bool includeControls = true, CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?> { ["family_id"] = familyId, ["include_controls"] = includeControls };
        return await _controlFamilyTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Generate compliance documentation (SSP, POA&M, SAR).")]
    public async Task<string> GenerateComplianceDocumentAsync(
        string documentType, string? subscriptionId = null,
        string? framework = null, string? systemName = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["document_type"] = documentType, ["subscription_id"] = subscriptionId,
            ["framework"] = framework, ["system_name"] = systemName
        };
        return await _documentGenerationTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Collect compliance evidence from Azure resources.")]
    public async Task<string> CollectComplianceEvidenceAsync(
        string controlId, string? subscriptionId = null,
        string? resourceGroup = null, CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["control_id"] = controlId, ["subscription_id"] = subscriptionId, ["resource_group"] = resourceGroup
        };
        return await _evidenceCollectionTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Remediate a compliance finding with guided or automated fixes.")]
    public async Task<string> RemediateComplianceFindingAsync(
        string findingId, bool applyRemediation = false, bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["finding_id"] = findingId, ["apply_remediation"] = applyRemediation, ["dry_run"] = dryRun
        };
        return await _remediationTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Validate that a remediation was successfully applied.")]
    public async Task<string> ValidateRemediationAsync(
        string findingId, string? executionId = null, string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["finding_id"] = findingId, ["execution_id"] = executionId, ["subscription_id"] = subscriptionId
        };
        return await _validateRemediationTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Generate a prioritized remediation plan for compliance findings.")]
    public async Task<string> GenerateRemediationPlanAsync(
        string? subscriptionId = null, string? resourceGroupName = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["subscription_id"] = subscriptionId, ["resource_group_name"] = resourceGroupName
        };
        return await _remediationPlanTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Get the audit trail of compliance assessments.")]
    public async Task<string> GetAssessmentAuditLogAsync(
        string? subscriptionId = null, int days = 7, CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?> { ["subscription_id"] = subscriptionId, ["days"] = days };
        return await _auditLogTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Get compliance history and trends over time.")]
    public async Task<string> GetComplianceHistoryAsync(
        string? subscriptionId = null, int days = 30, CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?> { ["subscription_id"] = subscriptionId, ["days"] = days };
        return await _historyTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Get current compliance status summary.")]
    public async Task<string> GetComplianceStatusAsync(
        string? subscriptionId = null, string? framework = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?> { ["subscription_id"] = subscriptionId, ["framework"] = framework };
        return await _statusTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Query continuous compliance monitoring status, alerts, and trends.")]
    public async Task<string> GetComplianceMonitoringAsync(
        string action, string? subscriptionId = null, int days = 30,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["action"] = action, ["subscription_id"] = subscriptionId, ["days"] = days
        };
        return await _monitoringTool.ExecuteAsync(args, cancellationToken);
    }
}
