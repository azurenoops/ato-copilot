using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Agents.Compliance.Agents;
using Ato.Copilot.Agents.Compliance.Configuration;
using Ato.Copilot.Agents.Compliance.Tools;

namespace Ato.Copilot.Agents.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the compliance agent and all its tools
    /// </summary>
    public static IServiceCollection AddComplianceAgent(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind compliance agent options
        services.Configure<ComplianceAgentOptions>(configuration.GetSection("Agents:Compliance"));

        // Register compliance tools
        services.AddSingleton<ComplianceAssessmentTool>();
        services.AddSingleton<ControlFamilyTool>();
        services.AddSingleton<DocumentGenerationTool>();
        services.AddSingleton<EvidenceCollectionTool>();
        services.AddSingleton<RemediationExecuteTool>();
        services.AddSingleton<ValidateRemediationTool>();
        services.AddSingleton<RemediationPlanTool>();
        services.AddSingleton<AssessmentAuditLogTool>();
        services.AddSingleton<ComplianceHistoryTool>();
        services.AddSingleton<ComplianceStatusTool>();
        services.AddSingleton<ComplianceMonitoringTool>();

        // Register tools as BaseTool collection
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<ComplianceAssessmentTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<ControlFamilyTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<DocumentGenerationTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<EvidenceCollectionTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<RemediationExecuteTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<ValidateRemediationTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<RemediationPlanTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<AssessmentAuditLogTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<ComplianceHistoryTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<ComplianceStatusTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<ComplianceMonitoringTool>());

        // Register the agent
        services.AddSingleton<ComplianceAgent>();
        services.AddSingleton<BaseAgent>(sp => sp.GetRequiredService<ComplianceAgent>());

        return services;
    }
}
