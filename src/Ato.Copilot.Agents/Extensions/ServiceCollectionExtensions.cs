using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Agents.Compliance.Agents;
using Ato.Copilot.Agents.Compliance.Configuration;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Agents.Configuration.Agents;
using Ato.Copilot.Agents.Configuration.Tools;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Auth;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Interfaces.Kanban;

namespace Ato.Copilot.Agents.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the compliance agent and all its tools.
    /// </summary>
    public static IServiceCollection AddComplianceAgent(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind compliance agent options
        services.Configure<ComplianceAgentOptions>(configuration.GetSection("Agents:Compliance"));

        // Register compliance services (Phase 4)
        services.AddSingleton<INistControlsService, NistControlsService>();
        services.AddSingleton<IAzurePolicyComplianceService, AzurePolicyComplianceService>();
        services.AddSingleton<IDefenderForCloudService, DefenderForCloudService>();
        services.AddSingleton<IAtoComplianceEngine, AtoComplianceEngine>();
        services.AddSingleton<IRemediationEngine, RemediationEngine>();
        services.AddSingleton<IEvidenceStorageService, EvidenceStorageService>();
        services.AddSingleton<IDocumentGenerationService, DocumentGenerationService>();
        services.AddSingleton<ComplianceMonitoringService>();
        services.AddSingleton<IComplianceMonitoringService>(sp => sp.GetRequiredService<ComplianceMonitoringService>());
        services.AddSingleton<IComplianceHistoryService>(sp => sp.GetRequiredService<ComplianceMonitoringService>());
        services.AddSingleton<IAssessmentAuditService>(sp => sp.GetRequiredService<ComplianceMonitoringService>());
        services.AddSingleton<IComplianceStatusService>(sp => sp.GetRequiredService<ComplianceMonitoringService>());

        // HttpClient for NistControlsService online catalog fetch
        services.AddHttpClient<NistControlsService>();

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
        services.AddSingleton<ComplianceChatTool>();

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

        // ─── Kanban Services ─────────────────────────────────────────────────
        services.AddScoped<IKanbanService, KanbanService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddHostedService<OverdueScanHostedService>();
        services.AddHostedService<SessionCleanupHostedService>();

        // ─── Kanban Tools (Singleton — uses IServiceScopeFactory for scoped IKanbanService)
        services.AddSingleton<KanbanCreateBoardTool>();
        services.AddSingleton<KanbanBoardShowTool>();
        services.AddSingleton<KanbanGetTaskTool>();
        services.AddSingleton<KanbanCreateTaskTool>();
        services.AddSingleton<KanbanAssignTaskTool>();
        services.AddSingleton<KanbanMoveTaskTool>();
        services.AddSingleton<KanbanTaskListTool>();
        services.AddSingleton<KanbanTaskHistoryTool>();
        services.AddSingleton<KanbanValidateTaskTool>();
        services.AddSingleton<KanbanAddCommentTool>();
        services.AddSingleton<KanbanTaskCommentsTool>();
        services.AddSingleton<KanbanEditCommentTool>();
        services.AddSingleton<KanbanDeleteCommentTool>();
        services.AddSingleton<KanbanRemediateTaskTool>();
        services.AddSingleton<KanbanCollectEvidenceTool>();
        services.AddSingleton<KanbanBulkUpdateTool>();
        services.AddSingleton<KanbanExportTool>();
        services.AddSingleton<KanbanArchiveBoardTool>();

        // Register Kanban tools as BaseTool collection
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanCreateBoardTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanBoardShowTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanGetTaskTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanCreateTaskTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanAssignTaskTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanMoveTaskTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanTaskListTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanTaskHistoryTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanValidateTaskTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanAddCommentTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanTaskCommentsTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanEditCommentTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanDeleteCommentTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanRemediateTaskTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanCollectEvidenceTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanBulkUpdateTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanExportTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanArchiveBoardTool>());

        // ─── CAC/Auth Services ───────────────────────────────────────────────
        services.AddScoped<ICacSessionService, CacSessionService>();
        services.AddScoped<IPimService>(sp => new PimService(
            sp.GetRequiredService<IDbContextFactory<AtoCopilotContext>>(),
            sp.GetRequiredService<IOptions<PimServiceOptions>>(),
            sp.GetRequiredService<ILogger<PimService>>(),
            sp.GetRequiredService<INotificationService>()));
        services.AddScoped<IJitVmAccessService, JitVmAccessService>();
        services.AddScoped<ICertificateRoleResolver, CertificateRoleResolver>();

        // ─── Data Retention Cleanup ──────────────────────────────────────────
        var retentionConfig = configuration.GetSection(RetentionPolicyOptions.SectionName);
        var enableCleanup = retentionConfig.GetValue("EnableAutomaticCleanup", true);
        if (enableCleanup)
        {
            services.AddHostedService<RetentionCleanupHostedService>();
        }

        // ─── Auth/PIM Tools (Singleton — uses IServiceScopeFactory for scoped services)
        services.AddSingleton<CacStatusTool>();
        services.AddSingleton<CacSignOutTool>();
        services.AddSingleton<CacSetTimeoutTool>();
        services.AddSingleton<CacMapCertificateTool>();
        services.AddSingleton<PimListEligibleTool>();
        services.AddSingleton<PimActivateRoleTool>();
        services.AddSingleton<PimDeactivateRoleTool>();
        services.AddSingleton<PimListActiveTool>();
        services.AddSingleton<PimExtendRoleTool>();
        services.AddSingleton<PimApproveRequestTool>();
        services.AddSingleton<PimDenyRequestTool>();
        services.AddSingleton<JitRequestAccessTool>();
        services.AddSingleton<JitListSessionsTool>();
        services.AddSingleton<JitRevokeAccessTool>();
        services.AddSingleton<PimHistoryTool>();

        // Register Auth/PIM tools as BaseTool collection
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<CacStatusTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<CacSignOutTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<CacSetTimeoutTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<CacMapCertificateTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<PimListEligibleTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<PimActivateRoleTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<PimDeactivateRoleTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<PimListActiveTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<PimExtendRoleTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<PimApproveRequestTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<PimDenyRequestTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<JitRequestAccessTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<JitListSessionsTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<JitRevokeAccessTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<PimHistoryTool>());

        return services;
    }

    /// <summary>
    /// Register the configuration agent and its tool.
    /// </summary>
    public static IServiceCollection AddConfigurationAgent(this IServiceCollection services)
    {
        // Register the configuration tool
        services.AddSingleton<ConfigurationTool>();
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<ConfigurationTool>());

        // Register the agent
        services.AddSingleton<ConfigurationAgent>();
        services.AddSingleton<BaseAgent>(sp => sp.GetRequiredService<ConfigurationAgent>());

        return services;
    }
}
