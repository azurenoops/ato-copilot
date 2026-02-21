using System.Diagnostics;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Models.Compliance;

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
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplianceAgent"/> class.
    /// </summary>
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
        IDbContextFactory<AtoCopilotContext> dbFactory,
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
        _dbFactory = dbFactory;

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

    /// <inheritdoc />
    public override string AgentId => "compliance-agent";
    /// <inheritdoc />
    public override string AgentName => "Compliance Agent";
    /// <inheritdoc />
    public override string Description => "Handles NIST 800-53, FedRAMP, and ATO compliance assessments, remediation, and documentation";

    /// <inheritdoc />
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

    /// <summary>
    /// Processes a compliance request, routing to the appropriate tool and logging the action.
    /// </summary>
    public override async Task<AgentResponse> ProcessAsync(
        string message,
        AgentConversationContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        Logger.LogInformation("ComplianceAgent processing: {Message}", message[..Math.Min(100, message.Length)]);
        var actionType = ClassifyIntent(message);

        try
        {
            // Analyze intent and route to appropriate tool
            var toolResult = await RouteToToolAsync(message, context, cancellationToken);

            stopwatch.Stop();

            // Log successful action to audit trail
            await LogAuditEntryAsync(actionType, GetContextValue(context, "subscription_id"),
                AuditOutcome.Success, $"Processed: {message[..Math.Min(200, message.Length)]}",
                stopwatch.Elapsed, cancellationToken);

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

            // Log failed action to audit trail
            await LogAuditEntryAsync(actionType, GetContextValue(context, "subscription_id"),
                AuditOutcome.Failure, $"Error: {ex.Message}", stopwatch.Elapsed, cancellationToken);

            return new AgentResponse
            {
                Success = false,
                Response = $"Error processing compliance request: {ex.Message}",
                AgentName = AgentName,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>Routes a user message to the appropriate compliance tool based on intent analysis.</summary>
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

    /// <summary>Returns true if the text contains any of the specified keywords (case-insensitive).</summary>
    private static bool ContainsAny(string text, params string[] keywords) =>
        keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    /// <summary>Retrieves a context value from the agent conversation workflow state.</summary>
    private static string? GetContextValue(AgentConversationContext context, string key) =>
        context.WorkflowState.TryGetValue(key, out var value) ? value?.ToString() : null;

    /// <summary>Extracts the NIST control family abbreviation from the user message.</summary>
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

    /// <summary>Extracts the document type (ssp, poam, sar) from the user message.</summary>
    private static string ExtractDocumentType(string message)
    {
        if (message.Contains("ssp", StringComparison.OrdinalIgnoreCase)) return "ssp";
        if (message.Contains("poam", StringComparison.OrdinalIgnoreCase) || message.Contains("poa&m", StringComparison.OrdinalIgnoreCase)) return "poam";
        if (message.Contains("sar", StringComparison.OrdinalIgnoreCase)) return "sar";
        return "ssp";
    }

    /// <summary>
    /// Classifies the user message intent for audit logging.
    /// </summary>
    private static string ClassifyIntent(string message)
    {
        var lower = message.ToLowerInvariant();
        if (ContainsAny(lower, "assess", "scan")) return "Assessment";
        if (ContainsAny(lower, "remediat", "fix")) return "Remediation";
        if (ContainsAny(lower, "evidence", "collect")) return "EvidenceCollection";
        if (ContainsAny(lower, "document", "ssp", "sar", "poam")) return "DocumentGeneration";
        if (ContainsAny(lower, "monitor", "alert")) return "Monitoring";
        if (ContainsAny(lower, "audit", "log")) return "AuditQuery";
        if (ContainsAny(lower, "history", "trend")) return "HistoryQuery";
        if (ContainsAny(lower, "status", "posture")) return "StatusQuery";
        if (ContainsAny(lower, "control", "nist")) return "ControlQuery";
        return "GeneralQuery";
    }

    /// <summary>
    /// Persists an audit log entry to the database.
    /// </summary>
    private async Task LogAuditEntryAsync(
        string action,
        string? subscriptionId,
        AuditOutcome outcome,
        string details,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            db.AuditLogs.Add(new AuditLogEntry
            {
                UserId = "system",
                UserRole = "Agent",
                Action = action,
                SubscriptionId = subscriptionId,
                Outcome = outcome,
                Details = details,
                Duration = duration
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Audit logging should never fail the main operation
            Logger.LogWarning(ex, "Failed to persist audit log entry for action {Action}", action);
        }
    }
}
