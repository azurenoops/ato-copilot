using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Agents.Compliance.Tools;

namespace Ato.Copilot.Agents.Compliance.Agents;

/// <summary>
/// Compliance Agent - handles all NIST 800-53, FedRAMP, and ATO compliance operations.
/// Extends BaseAgent per Constitution Principle II.
/// </summary>
public class ComplianceAgent : BaseAgent
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

    public ComplianceAgent(
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
        ComplianceMonitoringTool monitoringTool,
        ILogger<ComplianceAgent> logger)
        : base(logger)
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

        // Register all tools per Constitution Principle II
        RegisterTool(_assessmentTool);
        RegisterTool(_controlFamilyTool);
        RegisterTool(_documentGenerationTool);
        RegisterTool(_evidenceCollectionTool);
        RegisterTool(_remediationTool);
        RegisterTool(_validateRemediationTool);
        RegisterTool(_remediationPlanTool);
        RegisterTool(_auditLogTool);
        RegisterTool(_historyTool);
        RegisterTool(_statusTool);
        RegisterTool(_monitoringTool);
    }

    public override string AgentId => "compliance-agent";
    public override string AgentName => "Compliance Agent";
    public override string Description => "Handles NIST 800-53, FedRAMP, and ATO compliance assessments, remediation, and documentation";

    public override string GetSystemPrompt()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Ato.Copilot.Agents.Compliance.Prompts.ComplianceAgent.prompt.txt";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            Logger.LogWarning("System prompt resource not found: {Resource}", resourceName);
            return "You are a compliance agent for Azure Government NIST 800-53 assessments.";
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public override async Task<AgentResponse> ProcessAsync(
        string message,
        AgentConversationContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        Logger.LogInformation("ComplianceAgent processing: {Message}", message[..Math.Min(100, message.Length)]);

        try
        {
            // Analyze intent and route to appropriate tool
            var toolResult = await RouteToToolAsync(message, context, cancellationToken);

            stopwatch.Stop();
            return new AgentResponse
            {
                Success = true,
                Response = toolResult,
                AgentName = AgentName,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Error in ComplianceAgent processing");
            return new AgentResponse
            {
                Success = false,
                Response = $"Error processing compliance request: {ex.Message}",
                AgentName = AgentName,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private async Task<string> RouteToToolAsync(
        string message,
        AgentConversationContext context,
        CancellationToken cancellationToken)
    {
        var lowerMessage = message.ToLowerInvariant();

        // Route based on intent keywords
        if (ContainsAny(lowerMessage, "assess", "scan", "audit", "check compliance", "run assessment"))
        {
            return await _assessmentTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["subscription_id"] = GetContextValue(context, "subscription_id"),
                ["scan_type"] = "quick"
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "control family", "nist control", "control details"))
        {
            var family = ExtractControlFamily(lowerMessage);
            return await _controlFamilyTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["family_id"] = family
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "generate ssp", "generate document", "poam", "poa&m", "sar", "system security plan"))
        {
            var docType = ExtractDocumentType(lowerMessage);
            return await _documentGenerationTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["document_type"] = docType,
                ["subscription_id"] = GetContextValue(context, "subscription_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "collect evidence", "evidence collection", "gather evidence"))
        {
            return await _evidenceCollectionTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["subscription_id"] = GetContextValue(context, "subscription_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "remediate", "fix finding", "apply fix"))
        {
            return await _remediationTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["dry_run"] = true
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "remediation plan", "plan remediation"))
        {
            return await _remediationPlanTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["subscription_id"] = GetContextValue(context, "subscription_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "compliance status", "current status", "posture"))
        {
            return await _statusTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["subscription_id"] = GetContextValue(context, "subscription_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "compliance history", "trend", "historical"))
        {
            return await _historyTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["subscription_id"] = GetContextValue(context, "subscription_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "audit log", "audit trail"))
        {
            return await _auditLogTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["subscription_id"] = GetContextValue(context, "subscription_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "monitor", "alert", "continuous"))
        {
            return await _monitoringTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["action"] = "status",
                ["subscription_id"] = GetContextValue(context, "subscription_id")
            }, cancellationToken);
        }

        // Default: return compliance status
        return await _statusTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscription_id"] = GetContextValue(context, "subscription_id")
        }, cancellationToken);
    }

    private static bool ContainsAny(string text, params string[] keywords) =>
        keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static string? GetContextValue(AgentConversationContext context, string key) =>
        context.WorkflowState.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static string ExtractControlFamily(string message)
    {
        var families = new[] { "AC", "AU", "AT", "CM", "CP", "IA", "IR", "MA", "MP", "PE", "PL", "PM", "PS", "RA", "SA", "SC", "SI", "SR" };
        foreach (var family in families)
        {
            if (message.Contains(family, StringComparison.OrdinalIgnoreCase))
                return family;
        }
        return "AC";
    }

    private static string ExtractDocumentType(string message)
    {
        if (message.Contains("ssp", StringComparison.OrdinalIgnoreCase)) return "ssp";
        if (message.Contains("poam", StringComparison.OrdinalIgnoreCase) || message.Contains("poa&m", StringComparison.OrdinalIgnoreCase)) return "poam";
        if (message.Contains("sar", StringComparison.OrdinalIgnoreCase)) return "sar";
        return "ssp";
    }
}
