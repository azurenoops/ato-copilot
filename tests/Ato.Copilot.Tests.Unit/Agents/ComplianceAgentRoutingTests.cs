// ─────────────────────────────────────────────────────────────────────────────
// Feature 015 · Phase 14 — Compliance Agent RMF Step Routing (US12)
// T167: Unit tests for RMF step routing
// ─────────────────────────────────────────────────────────────────────────────

using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Compliance.Agents;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Agents;

public class ComplianceAgentRoutingTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // GetRmfStepContextSupplement tests — verify correct tool prioritization
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(RmfPhase.Prepare, "compliance_register_system")]
    [InlineData(RmfPhase.Prepare, "compliance_define_boundary")]
    [InlineData(RmfPhase.Prepare, "compliance_assign_rmf_role")]
    [InlineData(RmfPhase.Categorize, "compliance_categorize_system")]
    [InlineData(RmfPhase.Categorize, "compliance_suggest_info_types")]
    [InlineData(RmfPhase.Select, "compliance_select_baseline")]
    [InlineData(RmfPhase.Select, "compliance_tailor_baseline")]
    [InlineData(RmfPhase.Select, "compliance_generate_crm")]
    [InlineData(RmfPhase.Implement, "compliance_write_narrative")]
    [InlineData(RmfPhase.Implement, "compliance_suggest_narrative")]
    [InlineData(RmfPhase.Implement, "compliance_generate_ssp")]
    [InlineData(RmfPhase.Assess, "compliance_assess_control")]
    [InlineData(RmfPhase.Assess, "compliance_take_snapshot")]
    [InlineData(RmfPhase.Assess, "compliance_generate_sar")]
    [InlineData(RmfPhase.Authorize, "compliance_issue_authorization")]
    [InlineData(RmfPhase.Authorize, "compliance_accept_risk")]
    [InlineData(RmfPhase.Authorize, "compliance_bundle_authorization_package")]
    [InlineData(RmfPhase.Monitor, "compliance_create_conmon_plan")]
    [InlineData(RmfPhase.Monitor, "compliance_track_ato_expiration")]
    [InlineData(RmfPhase.Monitor, "compliance_export_emass")]
    public void GetRmfStepContextSupplement_ShouldMentionPrimaryTools(RmfPhase step, string expectedTool)
    {
        var supplement = ComplianceAgent.GetRmfStepContextSupplement(step, "Test System");

        supplement.Should().Contain(expectedTool,
            because: $"RMF step {step} should prioritize {expectedTool}");
    }

    [Theory]
    [InlineData(RmfPhase.Prepare, "Step 0")]
    [InlineData(RmfPhase.Categorize, "Step 1")]
    [InlineData(RmfPhase.Select, "Step 2")]
    [InlineData(RmfPhase.Implement, "Step 3")]
    [InlineData(RmfPhase.Assess, "Step 4")]
    [InlineData(RmfPhase.Authorize, "Step 5")]
    [InlineData(RmfPhase.Monitor, "Step 6")]
    public void GetRmfStepContextSupplement_ShouldIncludeStepNumber(RmfPhase step, string expectedStepLabel)
    {
        var supplement = ComplianceAgent.GetRmfStepContextSupplement(step, "ACME Portal");

        supplement.Should().Contain(expectedStepLabel);
    }

    [Fact]
    public void GetRmfStepContextSupplement_ShouldIncludeSystemName()
    {
        var supplement = ComplianceAgent.GetRmfStepContextSupplement(RmfPhase.Select, "ACME Portal");

        supplement.Should().Contain("ACME Portal");
    }

    [Fact]
    public void GetRmfStepContextSupplement_WithNullSystemName_ShouldUseGenericLabel()
    {
        var supplement = ComplianceAgent.GetRmfStepContextSupplement(RmfPhase.Select, null);

        supplement.Should().Contain("The active system");
        supplement.Should().NotContain("null");
    }

    [Theory]
    [InlineData(RmfPhase.Prepare, "compliance_categorize_system")]
    [InlineData(RmfPhase.Categorize, "compliance_select_baseline")]
    [InlineData(RmfPhase.Select, "compliance_write_narrative")]
    [InlineData(RmfPhase.Implement, "compliance_assess_control")]
    [InlineData(RmfPhase.Assess, "compliance_bundle_authorization_package")]
    [InlineData(RmfPhase.Authorize, "compliance_create_conmon_plan")]
    [InlineData(RmfPhase.Monitor, "compliance_track_ato_expiration")]
    public void GetRmfStepContextSupplement_ShouldSuggestNextStep(RmfPhase step, string expectedNextTool)
    {
        var supplement = ComplianceAgent.GetRmfStepContextSupplement(step, "TestSys");

        supplement.Should().Contain(expectedNextTool,
            because: $"step {step} should suggest the next step's primary tool");
    }

    [Theory]
    [InlineData(RmfPhase.Prepare)]
    [InlineData(RmfPhase.Categorize)]
    [InlineData(RmfPhase.Select)]
    [InlineData(RmfPhase.Implement)]
    [InlineData(RmfPhase.Assess)]
    [InlineData(RmfPhase.Authorize)]
    [InlineData(RmfPhase.Monitor)]
    public void GetRmfStepContextSupplement_ShouldIncludeAdvisoryDisclaimer(RmfPhase step)
    {
        var supplement = ComplianceAgent.GetRmfStepContextSupplement(step, "TestSys");

        supplement.Should().Contain("advisory, not restrictive",
            because: "step context should never block cross-step requests");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetPrioritizedToolsForStep tests — tool list validation
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetPrioritizedToolsForStep_Prepare_ShouldIncludeRegistrationTools()
    {
        var tools = ComplianceAgent.GetPrioritizedToolsForStep(RmfPhase.Prepare);

        tools.Should().Contain("compliance_register_system");
        tools.Should().Contain("compliance_define_boundary");
        tools.Should().Contain("compliance_assign_rmf_role");
        tools.Should().Contain("compliance_list_rmf_roles");
        tools.Should().NotContain("compliance_categorize_system",
            because: "categorization is the next step");
    }

    [Fact]
    public void GetPrioritizedToolsForStep_Select_ShouldIncludeBaselineTools()
    {
        var tools = ComplianceAgent.GetPrioritizedToolsForStep(RmfPhase.Select);

        tools.Should().Contain("compliance_select_baseline");
        tools.Should().Contain("compliance_tailor_baseline");
        tools.Should().Contain("compliance_set_inheritance");
        tools.Should().Contain("compliance_generate_crm");
        tools.Should().NotContain("compliance_write_narrative",
            because: "implementation is the next step");
    }

    [Fact]
    public void GetPrioritizedToolsForStep_Implement_ShouldIncludeSspTools()
    {
        var tools = ComplianceAgent.GetPrioritizedToolsForStep(RmfPhase.Implement);

        tools.Should().Contain("compliance_write_narrative");
        tools.Should().Contain("compliance_suggest_narrative");
        tools.Should().Contain("compliance_batch_populate_narratives");
        tools.Should().Contain("compliance_generate_ssp");
        tools.Should().Contain("compliance_remediate");
    }

    [Fact]
    public void GetPrioritizedToolsForStep_Assess_ShouldIncludeAssessmentTools()
    {
        var tools = ComplianceAgent.GetPrioritizedToolsForStep(RmfPhase.Assess);

        tools.Should().Contain("compliance_assess_control");
        tools.Should().Contain("compliance_take_snapshot");
        tools.Should().Contain("compliance_verify_evidence");
        tools.Should().Contain("compliance_generate_sar");
        tools.Should().NotContain("compliance_issue_authorization",
            because: "authorization is the next step");
    }

    [Fact]
    public void GetPrioritizedToolsForStep_Authorize_ShouldIncludeAuthorizationTools()
    {
        var tools = ComplianceAgent.GetPrioritizedToolsForStep(RmfPhase.Authorize);

        tools.Should().Contain("compliance_issue_authorization");
        tools.Should().Contain("compliance_accept_risk");
        tools.Should().Contain("compliance_create_poam");
        tools.Should().Contain("compliance_generate_rar");
        tools.Should().Contain("compliance_bundle_authorization_package");
    }

    [Fact]
    public void GetPrioritizedToolsForStep_Monitor_ShouldIncludeConMonTools()
    {
        var tools = ComplianceAgent.GetPrioritizedToolsForStep(RmfPhase.Monitor);

        tools.Should().Contain("compliance_create_conmon_plan");
        tools.Should().Contain("compliance_generate_conmon_report");
        tools.Should().Contain("compliance_track_ato_expiration");
        tools.Should().Contain("compliance_export_emass");
        tools.Should().Contain("watch_enable_monitoring");
    }

    [Fact]
    public void GetPrioritizedToolsForStep_AllSteps_ShouldReturnNonEmptyLists()
    {
        foreach (var step in Enum.GetValues<RmfPhase>())
        {
            var tools = ComplianceAgent.GetPrioritizedToolsForStep(step);
            tools.Should().NotBeEmpty(because: $"step {step} should have prioritized tools");
        }
    }

    [Fact]
    public void GetPrioritizedToolsForStep_AllToolNames_ShouldStartWithComplianceOrWatch()
    {
        foreach (var step in Enum.GetValues<RmfPhase>())
        {
            var tools = ComplianceAgent.GetPrioritizedToolsForStep(step);
            foreach (var tool in tools)
            {
                (tool.StartsWith("compliance_") || tool.StartsWith("watch_")).Should().BeTrue(
                    because: $"tool '{tool}' at step {step} should follow naming convention");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // System prompt integration tests — verify dynamic supplement injection
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SystemPrompt_WithoutActiveStep_ShouldNotContainActiveSystemContext()
    {
        var agent = CreateAgent();

        var prompt = agent.GetSystemPrompt();

        prompt.Should().NotContain("Active System Context",
            because: "no system is active in the current async context");
    }

    [Fact]
    public void SystemPrompt_ShouldContainRmfStepToolGroupings()
    {
        var agent = CreateAgent();

        var prompt = agent.GetSystemPrompt();

        prompt.Should().Contain("RMF Step-Aware Tool Prioritization",
            because: "the static prompt should include the RMF step tool map");
        prompt.Should().Contain("Step 0 — Prepare");
        prompt.Should().Contain("Step 6 — Monitor");
    }

    [Fact]
    public void SystemPrompt_ShouldContainCrossStepToolsSection()
    {
        var agent = CreateAgent();

        var prompt = agent.GetSystemPrompt();

        prompt.Should().Contain("Cross-Step Tools",
            because: "the prompt should document always-available tools");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ProcessAsync with RMF step context integration tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_WithSystemId_ShouldResolveRmfStep()
    {
        var dbName = $"RoutingTest_ResolveStep_{Guid.NewGuid()}";
        var dbOptions = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var dbFactory = new InMemoryDbContextFactory(dbOptions);

        // Seed a system at Step 2 (Select)
        await using (var db = dbFactory.CreateDbContext())
        {
            db.RegisteredSystems.Add(new RegisteredSystem
            {
                Id = "sys-routing-001",
                Name = "Routing Test System",
                SystemType = SystemType.MajorApplication,
                MissionCriticality = MissionCriticality.MissionEssential,
                HostingEnvironment = "Azure Government",
                CurrentRmfStep = RmfPhase.Select,
                CreatedBy = "test-user"
            });
            await db.SaveChangesAsync();
        }

        var agent = CreateAgent(dbFactory);
        var context = new AgentConversationContext
        {
            ConversationId = "test-routing-1",
            UserId = "test-user",
            WorkflowState = { ["system_id"] = "sys-routing-001" }
        };

        // ProcessAsync should resolve the RMF step so AI prompt includes it
        var result = await agent.ProcessAsync("What should I do next?", context);

        result.Should().NotBeNull();
        result.AgentName.Should().Be("Compliance Agent");
    }

    [Fact]
    public async Task ProcessAsync_WithNoSystemId_ShouldGracefullyFallback()
    {
        var agent = CreateAgent();
        var context = new AgentConversationContext
        {
            ConversationId = "test-routing-2",
            UserId = "test-user"
            // No system_id in WorkflowState
        };

        var result = await agent.ProcessAsync("Check compliance status", context);

        result.Should().NotBeNull();
        result.AgentName.Should().Be("Compliance Agent");
    }

    [Fact]
    public async Task ProcessAsync_WithInvalidSystemId_ShouldGracefullyFallback()
    {
        var dbName = $"RoutingTest_InvalidSys_{Guid.NewGuid()}";
        var dbOptions = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var dbFactory = new InMemoryDbContextFactory(dbOptions);

        var agent = CreateAgent(dbFactory);
        var context = new AgentConversationContext
        {
            ConversationId = "test-routing-3",
            UserId = "test-user",
            WorkflowState = { ["system_id"] = "nonexistent-system-id" }
        };

        // Should not throw — graceful fallback
        var result = await agent.ProcessAsync("Show me the baseline", context);

        result.Should().NotBeNull();
        result.AgentName.Should().Be("Compliance Agent");
    }

    [Theory]
    [InlineData(RmfPhase.Prepare, "compliance_register_system")]
    [InlineData(RmfPhase.Categorize, "compliance_categorize_system")]
    [InlineData(RmfPhase.Select, "compliance_select_baseline")]
    [InlineData(RmfPhase.Implement, "compliance_write_narrative")]
    [InlineData(RmfPhase.Assess, "compliance_assess_control")]
    [InlineData(RmfPhase.Authorize, "compliance_issue_authorization")]
    [InlineData(RmfPhase.Monitor, "compliance_create_conmon_plan")]
    public void GetPrioritizedToolsForStep_PrimaryTool_ShouldBeFirst(RmfPhase step, string expectedFirstTool)
    {
        var tools = ComplianceAgent.GetPrioritizedToolsForStep(step);

        tools[0].Should().Be(expectedFirstTool,
            because: $"the primary tool for {step} should be listed first");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static ComplianceAgent CreateAgent(IDbContextFactory<AtoCopilotContext>? dbFactory = null)
    {
        var logger = Mock.Of<ILogger<ComplianceAgent>>();
        var scopeFactory = Mock.Of<IServiceScopeFactory>();
        var complianceEngine = Mock.Of<IAtoComplianceEngine>();
        var remediationEngine = Mock.Of<IRemediationEngine>();
        var nistControls = Mock.Of<INistControlsService>();
        var evidence = Mock.Of<IEvidenceStorageService>();
        var monitoring = Mock.Of<IComplianceMonitoringService>();
        var docGen = Mock.Of<IDocumentGenerationService>();
        var audit = Mock.Of<IAssessmentAuditService>();
        var history = Mock.Of<IComplianceHistoryService>();
        var status = Mock.Of<IComplianceStatusService>();
        var watchService = Mock.Of<IComplianceWatchService>();
        var alertManager = Mock.Of<IAlertManager>();

        var factory = dbFactory ?? new InMemoryDbContextFactory(
            new DbContextOptionsBuilder<AtoCopilotContext>()
                .UseInMemoryDatabase($"RoutingTests_{Guid.NewGuid()}")
                .Options);

        return new ComplianceAgent(
            new ComplianceAssessmentTool(complianceEngine, scopeFactory, Mock.Of<ILogger<ComplianceAssessmentTool>>()),
            new ControlFamilyTool(nistControls, Mock.Of<ILogger<ControlFamilyTool>>()),
            new DocumentGenerationTool(docGen, Mock.Of<IDocumentTemplateService>(), scopeFactory, Mock.Of<ILogger<DocumentGenerationTool>>()),
            new EvidenceCollectionTool(evidence, Mock.Of<ILogger<EvidenceCollectionTool>>()),
            new RemediationExecuteTool(remediationEngine, Mock.Of<ILogger<RemediationExecuteTool>>()),
            new ValidateRemediationTool(remediationEngine, Mock.Of<ILogger<ValidateRemediationTool>>()),
            new RemediationPlanTool(remediationEngine, Mock.Of<ILogger<RemediationPlanTool>>()),
            new AssessmentAuditLogTool(audit, Mock.Of<ILogger<AssessmentAuditLogTool>>()),
            new ComplianceHistoryTool(history, Mock.Of<ILogger<ComplianceHistoryTool>>()),
            new ComplianceStatusTool(status, Mock.Of<ILogger<ComplianceStatusTool>>()),
            new ComplianceMonitoringTool(monitoring, Mock.Of<ILogger<ComplianceMonitoringTool>>()),
            new KanbanCreateBoardTool(scopeFactory, Mock.Of<ILogger<KanbanCreateBoardTool>>()),
            new KanbanBoardShowTool(scopeFactory, Mock.Of<ILogger<KanbanBoardShowTool>>()),
            new KanbanGetTaskTool(scopeFactory, Mock.Of<ILogger<KanbanGetTaskTool>>()),
            new KanbanCreateTaskTool(scopeFactory, Mock.Of<ILogger<KanbanCreateTaskTool>>()),
            new KanbanAssignTaskTool(scopeFactory, Mock.Of<ILogger<KanbanAssignTaskTool>>()),
            new KanbanMoveTaskTool(scopeFactory, Mock.Of<ILogger<KanbanMoveTaskTool>>()),
            new KanbanTaskListTool(scopeFactory, Mock.Of<ILogger<KanbanTaskListTool>>()),
            new KanbanTaskHistoryTool(scopeFactory, Mock.Of<ILogger<KanbanTaskHistoryTool>>()),
            new KanbanValidateTaskTool(scopeFactory, Mock.Of<ILogger<KanbanValidateTaskTool>>()),
            new KanbanAddCommentTool(scopeFactory, Mock.Of<ILogger<KanbanAddCommentTool>>()),
            new KanbanTaskCommentsTool(scopeFactory, Mock.Of<ILogger<KanbanTaskCommentsTool>>()),
            new KanbanEditCommentTool(scopeFactory, Mock.Of<ILogger<KanbanEditCommentTool>>()),
            new KanbanDeleteCommentTool(scopeFactory, Mock.Of<ILogger<KanbanDeleteCommentTool>>()),
            new KanbanRemediateTaskTool(scopeFactory, Mock.Of<ILogger<KanbanRemediateTaskTool>>()),
            new KanbanCollectEvidenceTool(scopeFactory, Mock.Of<ILogger<KanbanCollectEvidenceTool>>()),
            new KanbanBulkUpdateTool(scopeFactory, Mock.Of<ILogger<KanbanBulkUpdateTool>>()),
            new KanbanExportTool(scopeFactory, Mock.Of<ILogger<KanbanExportTool>>()),
            new KanbanArchiveBoardTool(scopeFactory, Mock.Of<ILogger<KanbanArchiveBoardTool>>()),
            new KanbanGenerateScriptTool(scopeFactory, Mock.Of<ILogger<KanbanGenerateScriptTool>>()),
            new KanbanGenerateValidationTool(scopeFactory, Mock.Of<ILogger<KanbanGenerateValidationTool>>()),
            new CacStatusTool(scopeFactory, Mock.Of<ILogger<CacStatusTool>>()),
            new CacSignOutTool(scopeFactory, Mock.Of<ILogger<CacSignOutTool>>()),
            new CacSetTimeoutTool(scopeFactory, Mock.Of<ILogger<CacSetTimeoutTool>>()),
            new CacMapCertificateTool(scopeFactory, Mock.Of<ILogger<CacMapCertificateTool>>()),
            new PimListEligibleTool(scopeFactory, Mock.Of<ILogger<PimListEligibleTool>>()),
            new PimActivateRoleTool(scopeFactory, Mock.Of<ILogger<PimActivateRoleTool>>()),
            new PimDeactivateRoleTool(scopeFactory, Mock.Of<ILogger<PimDeactivateRoleTool>>()),
            new PimListActiveTool(scopeFactory, Mock.Of<ILogger<PimListActiveTool>>()),
            new PimExtendRoleTool(scopeFactory, Mock.Of<ILogger<PimExtendRoleTool>>()),
            new PimApproveRequestTool(scopeFactory, Mock.Of<ILogger<PimApproveRequestTool>>()),
            new PimDenyRequestTool(scopeFactory, Mock.Of<ILogger<PimDenyRequestTool>>()),
            new JitRequestAccessTool(scopeFactory, Mock.Of<ILogger<JitRequestAccessTool>>()),
            new JitListSessionsTool(scopeFactory, Mock.Of<ILogger<JitListSessionsTool>>()),
            new JitRevokeAccessTool(scopeFactory, Mock.Of<ILogger<JitRevokeAccessTool>>()),
            new PimHistoryTool(scopeFactory, Mock.Of<ILogger<PimHistoryTool>>()),
            new WatchEnableMonitoringTool(watchService, Mock.Of<ILogger<WatchEnableMonitoringTool>>()),
            new WatchDisableMonitoringTool(watchService, Mock.Of<ILogger<WatchDisableMonitoringTool>>()),
            new WatchConfigureMonitoringTool(watchService, Mock.Of<ILogger<WatchConfigureMonitoringTool>>()),
            new WatchMonitoringStatusTool(watchService, Mock.Of<ILogger<WatchMonitoringStatusTool>>()),
            new WatchShowAlertsTool(alertManager, Mock.Of<ILogger<WatchShowAlertsTool>>()),
            new WatchGetAlertTool(alertManager, Mock.Of<ILogger<WatchGetAlertTool>>()),
            new WatchAcknowledgeAlertTool(alertManager, Mock.Of<ILogger<WatchAcknowledgeAlertTool>>()),
            new WatchFixAlertTool(alertManager, complianceEngine, Mock.Of<ILogger<WatchFixAlertTool>>()),
            new WatchDismissAlertTool(alertManager, Mock.Of<ILogger<WatchDismissAlertTool>>()),
            new WatchCreateRuleTool(watchService, Mock.Of<ILogger<WatchCreateRuleTool>>()),
            new WatchListRulesTool(watchService, Mock.Of<ILogger<WatchListRulesTool>>()),
            new WatchSuppressAlertsTool(watchService, Mock.Of<ILogger<WatchSuppressAlertsTool>>()),
            new WatchListSuppressionsTool(watchService, Mock.Of<ILogger<WatchListSuppressionsTool>>()),
            new WatchConfigureQuietHoursTool(watchService, Mock.Of<ILogger<WatchConfigureQuietHoursTool>>()),
            new WatchConfigureNotificationsTool(Mock.Of<IEscalationService>(), Mock.Of<ILogger<WatchConfigureNotificationsTool>>()),
            new WatchConfigureEscalationTool(Mock.Of<IEscalationService>(), Mock.Of<ILogger<WatchConfigureEscalationTool>>()),
            new WatchAlertHistoryTool(alertManager, Mock.Of<ILogger<WatchAlertHistoryTool>>()),
            new WatchComplianceTrendTool(factory, Mock.Of<ILogger<WatchComplianceTrendTool>>()),
            new WatchAlertStatisticsTool(factory, Mock.Of<ILogger<WatchAlertStatisticsTool>>()),
            new WatchCreateTaskFromAlertTool(alertManager, scopeFactory, Mock.Of<ILogger<WatchCreateTaskFromAlertTool>>()),
            new WatchCollectEvidenceFromAlertTool(alertManager, factory, Mock.Of<ILogger<WatchCollectEvidenceFromAlertTool>>()),
            new WatchCreateAutoRemediationRuleTool(watchService, Mock.Of<ILogger<WatchCreateAutoRemediationRuleTool>>()),
            new WatchListAutoRemediationRulesTool(watchService, Mock.Of<ILogger<WatchListAutoRemediationRulesTool>>()),
            new NistControlSearchTool(nistControls, Mock.Of<ILogger<NistControlSearchTool>>()),
            new NistControlExplainerTool(nistControls, Mock.Of<ILogger<NistControlExplainerTool>>()),
            Enumerable.Empty<BaseTool>(),
            factory,
            scopeFactory,
            Mock.Of<ISystemIdResolver>(),
            logger);
    }

    private class InMemoryDbContextFactory : IDbContextFactory<AtoCopilotContext>
    {
        private readonly DbContextOptions<AtoCopilotContext> _options;

        public InMemoryDbContextFactory(DbContextOptions<AtoCopilotContext> options)
        {
            _options = options;
        }

        public AtoCopilotContext CreateDbContext() => new(_options);

        public Task<AtoCopilotContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
