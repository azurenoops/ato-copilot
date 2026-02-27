using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Http.Resilience;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Agents.Compliance.Agents;
using Ato.Copilot.Agents.Compliance.Configuration;
using Ato.Copilot.Agents.Compliance.EvidenceCollectors;
using Ato.Copilot.Agents.Compliance.Scanners;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Agents.Compliance.Services.KnowledgeBase;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Agents.Configuration.Agents;
using Ato.Copilot.Agents.Configuration.Tools;
using Ato.Copilot.Agents.KnowledgeBase.Agents;
using Ato.Copilot.Agents.KnowledgeBase.Configuration;
using Ato.Copilot.Agents.KnowledgeBase.Services;
using Ato.Copilot.Agents.KnowledgeBase.Tools;
using Ato.Copilot.Agents.Compliance.Services.Engines.Remediation;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Auth;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Interfaces.Kanban;
using Polly;

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

        // Bind NIST Controls options with validation
        services.AddOptions<NistControlsOptions>()
            .Bind(configuration.GetSection("Agents:Compliance:NistControls"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Add in-memory cache for NIST catalog caching
        services.AddMemoryCache();

        // Register compliance services (Phase 4)
        services.AddSingleton<INistControlsService, NistControlsService>();
        services.AddSingleton<IAzurePolicyComplianceService, AzurePolicyComplianceService>();
        services.AddSingleton<IDefenderForCloudService, DefenderForCloudService>();
        services.AddSingleton<IAtoComplianceEngine, AtoComplianceEngine>();
        // ─── Remediation Engine v2 (Feature 009 — AtoRemediationEngine) ─────
        services.AddSingleton<IAiRemediationPlanGenerator, AiRemediationPlanGenerator>();
        services.AddSingleton<IRemediationScriptExecutor, RemediationScriptExecutor>();
        services.AddSingleton<INistRemediationStepsService, NistRemediationStepsService>();
        services.AddSingleton<IAzureArmRemediationService, AzureArmRemediationService>();
        services.AddSingleton<IComplianceRemediationService, ComplianceRemediationService>();
        services.AddSingleton<IScriptSanitizationService, ScriptSanitizationService>();
        services.AddSingleton<AtoRemediationEngine>();
        services.AddSingleton<IRemediationEngine>(sp => sp.GetRequiredService<AtoRemediationEngine>());
        services.AddSingleton<IEvidenceStorageService, EvidenceStorageService>();
        services.AddSingleton<IDocumentGenerationService, DocumentGenerationService>();
        services.AddSingleton<ComplianceMonitoringService>();
        services.AddSingleton<IComplianceMonitoringService>(sp => sp.GetRequiredService<ComplianceMonitoringService>());
        services.AddSingleton<IComplianceHistoryService>(sp => sp.GetRequiredService<ComplianceMonitoringService>());
        services.AddSingleton<IAssessmentAuditService>(sp => sp.GetRequiredService<ComplianceMonitoringService>());
        services.AddSingleton<IComplianceStatusService>(sp => sp.GetRequiredService<ComplianceMonitoringService>());

        // ─── Compliance Engine Infrastructure (Feature 008) ──────────────────
        services.AddSingleton<IAzureResourceService, AzureResourceService>();
        services.AddSingleton<IAssessmentPersistenceService, AssessmentPersistenceService>();
        services.AddSingleton<IScannerRegistry, ScannerRegistry>();
        services.AddSingleton<IEvidenceCollectorRegistry, EvidenceCollectorRegistry>();

        // ─── Family-Specific Scanners (Feature 008 — US2) ───────────────────
        services.AddSingleton<IComplianceScanner, AccessControlScanner>();
        services.AddSingleton<IComplianceScanner, AuditScanner>();
        services.AddSingleton<IComplianceScanner, SecurityCommunicationsScanner>();
        services.AddSingleton<IComplianceScanner, SystemIntegrityScanner>();
        services.AddSingleton<IComplianceScanner, ContingencyPlanningScanner>();
        services.AddSingleton<IComplianceScanner, IdentificationAuthScanner>();
        services.AddSingleton<IComplianceScanner, ConfigManagementScanner>();
        services.AddSingleton<IComplianceScanner, IncidentResponseScanner>();
        services.AddSingleton<IComplianceScanner, RiskAssessmentScanner>();
        services.AddSingleton<IComplianceScanner, CertAccreditationScanner>();
        services.AddSingleton<IComplianceScanner, DefaultComplianceScanner>();

        // ─── Family-Specific Evidence Collectors (Feature 008 — US3) ────────
        services.AddSingleton<IEvidenceCollector, AccessControlEvidenceCollector>();
        services.AddSingleton<IEvidenceCollector, AuditEvidenceCollector>();
        services.AddSingleton<IEvidenceCollector, SecurityCommsEvidenceCollector>();
        services.AddSingleton<IEvidenceCollector, SystemIntegrityEvidenceCollector>();
        services.AddSingleton<IEvidenceCollector, ContingencyPlanningEvidenceCollector>();
        services.AddSingleton<IEvidenceCollector, IdentificationAuthEvidenceCollector>();
        services.AddSingleton<IEvidenceCollector, ConfigMgmtEvidenceCollector>();
        services.AddSingleton<IEvidenceCollector, IncidentResponseEvidenceCollector>();
        services.AddSingleton<IEvidenceCollector, RiskAssessmentEvidenceCollector>();
        services.AddSingleton<IEvidenceCollector, CertAccreditationEvidenceCollector>();
        services.AddSingleton<IEvidenceCollector, DefaultEvidenceCollector>();

        // ─── Knowledge Base Stubs (Feature 008) ─────────────────────────────
        services.AddSingleton<IStigValidationService, StigValidationService>();
        services.AddSingleton<IRmfKnowledgeService, RmfKnowledgeService>();
        services.AddSingleton<IStigKnowledgeService, StigKnowledgeService>();
        services.AddSingleton<IDoDInstructionService, DoDInstructionService>();
        services.AddSingleton<IDoDWorkflowService, DoDWorkflowService>();
        services.AddSingleton<IImpactLevelService, ImpactLevelService>();
        services.AddSingleton<IFedRampTemplateService, FedRampTemplateService>();

        // ─── Compliance Watch Services ───────────────────────────────────────
        services.AddSingleton<AlertCorrelationService>();
        services.AddSingleton<IAlertCorrelationService>(sp => sp.GetRequiredService<AlertCorrelationService>());
        services.AddSingleton<AlertManager>();
        services.AddSingleton<IAlertManager>(sp => sp.GetRequiredService<AlertManager>());
        services.AddSingleton<ComplianceWatchService>();
        services.AddSingleton<IComplianceWatchService>(sp => sp.GetRequiredService<ComplianceWatchService>());
        services.AddSingleton<ActivityLogEventSource>();
        services.AddSingleton<IComplianceEventSource>(sp => sp.GetRequiredService<ActivityLogEventSource>());
        services.AddHostedService<ComplianceWatchHostedService>();

        // HttpClient for NistControlsService with Polly resilience
        services.AddHttpClient<NistControlsService>()
            .AddResilienceHandler("nist-catalog", builder =>
            {
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(2),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                });
            });

        // NIST Controls cache warmup background service
        services.AddHostedService<NistControlsCacheWarmupService>();

        // Compliance validation service (validates 11 system-critical control IDs)
        services.AddSingleton<ComplianceValidationService>();

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
        services.AddSingleton<IacComplianceScanTool>();

        // Compliance Watch monitoring tools
        services.AddSingleton<WatchEnableMonitoringTool>();
        services.AddSingleton<WatchDisableMonitoringTool>();
        services.AddSingleton<WatchConfigureMonitoringTool>();
        services.AddSingleton<WatchMonitoringStatusTool>();

        // Compliance Watch alert lifecycle tools (US2)
        services.AddSingleton<WatchShowAlertsTool>();
        services.AddSingleton<WatchGetAlertTool>();
        services.AddSingleton<WatchAcknowledgeAlertTool>();
        services.AddSingleton<WatchFixAlertTool>();
        services.AddSingleton<WatchDismissAlertTool>();

        // Compliance Watch alert rules & suppression tools (US3)
        services.AddSingleton<WatchCreateRuleTool>();
        services.AddSingleton<WatchListRulesTool>();
        services.AddSingleton<WatchSuppressAlertsTool>();
        services.AddSingleton<WatchListSuppressionsTool>();
        services.AddSingleton<WatchConfigureQuietHoursTool>();

        // Compliance Watch notification & escalation tools (US4)
        services.AddSingleton<WatchConfigureNotificationsTool>();
        services.AddSingleton<WatchConfigureEscalationTool>();

        // Compliance Watch dashboard & reporting tools (US5)
        services.AddSingleton<WatchAlertHistoryTool>();
        services.AddSingleton<WatchComplianceTrendTool>();
        services.AddSingleton<WatchAlertStatisticsTool>();

        // Compliance Watch integration tools (US8)
        services.AddSingleton<WatchCreateTaskFromAlertTool>();
        services.AddSingleton<WatchCollectEvidenceFromAlertTool>();

        // Compliance Watch auto-remediation tools (US9)
        services.AddSingleton<WatchCreateAutoRemediationRuleTool>();
        services.AddSingleton<WatchListAutoRemediationRulesTool>();

        // NIST Controls knowledge tools (Feature 007)
        services.AddSingleton<NistControlSearchTool>();
        services.AddSingleton<NistControlExplainerTool>();

        // Compliance Watch notification & escalation services (US4)
        services.AddSingleton<AlertNotificationService>();
        services.AddSingleton<IAlertNotificationService>(sp => sp.GetRequiredService<AlertNotificationService>());
        services.AddSingleton<EscalationHostedService>();
        services.AddSingleton<IEscalationService>(sp => sp.GetRequiredService<EscalationHostedService>());
        services.AddHostedService(sp => sp.GetRequiredService<EscalationHostedService>());

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

        // Compliance Watch tools as BaseTool
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchEnableMonitoringTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchDisableMonitoringTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchConfigureMonitoringTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchMonitoringStatusTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchShowAlertsTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchGetAlertTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchAcknowledgeAlertTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchFixAlertTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchDismissAlertTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchCreateRuleTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchListRulesTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchSuppressAlertsTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchListSuppressionsTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchConfigureQuietHoursTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchConfigureNotificationsTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchConfigureEscalationTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchAlertHistoryTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchComplianceTrendTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchAlertStatisticsTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchCreateTaskFromAlertTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchCollectEvidenceFromAlertTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchCreateAutoRemediationRuleTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WatchListAutoRemediationRulesTool>());

        // NIST Controls knowledge tools as BaseTool (Feature 007)
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<NistControlSearchTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<NistControlExplainerTool>());

        // Register the agent
        services.AddSingleton<ComplianceAgent>();
        services.AddSingleton<BaseAgent>(sp => sp.GetRequiredService<ComplianceAgent>());

        // ─── Kanban Services ─────────────────────────────────────────────────
        services.AddScoped<IKanbanService, KanbanService>();
        services.AddScoped<ITaskEnrichmentService, TaskEnrichmentService>();
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
        services.AddSingleton<KanbanGenerateScriptTool>();
        services.AddSingleton<KanbanGenerateValidationTool>();

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
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanGenerateScriptTool>());
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<KanbanGenerateValidationTool>());

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

    /// <summary>
    /// Register the KnowledgeBase agent, its options, and service implementations.
    /// </summary>
    public static IServiceCollection AddKnowledgeBaseAgent(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind options
        services.Configure<KnowledgeBaseAgentOptions>(
            configuration.GetSection("Agents:KnowledgeBaseAgent"));

        // Register KB tools
        services.AddSingleton<ExplainNistControlTool>();
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<ExplainNistControlTool>());
        services.AddSingleton<SearchNistControlsTool>();
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<SearchNistControlsTool>());
        services.AddSingleton<ExplainStigTool>();
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<ExplainStigTool>());
        services.AddSingleton<SearchStigsTool>();
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<SearchStigsTool>());
        services.AddSingleton<ExplainRmfTool>();
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<ExplainRmfTool>());
        services.AddSingleton<ExplainImpactLevelTool>();
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<ExplainImpactLevelTool>());
        services.AddSingleton<GetFedRampTemplateGuidanceTool>();
        services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<GetFedRampTemplateGuidanceTool>());

        // Register the agent
        services.AddSingleton<KnowledgeBaseAgent>();
        services.AddSingleton<BaseAgent>(sp => sp.GetRequiredService<KnowledgeBaseAgent>());

        return services;
    }
}
