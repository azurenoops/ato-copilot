using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Interfaces.Kanban;
using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.Core.Models.Kanban;
using Ato.Copilot.State.Abstractions;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// FeatureSpec: 012-task-enrichment
/// Unit tests for KanbanService integration with ITaskEnrichmentService:
/// T028-T030 (US1: CreateBoardFromAssessmentAsync enrichment)
/// T050-T051 (US5: UpdateBoardFromAssessmentAsync enrichment for new tasks only)
/// </summary>
public class KanbanServiceEnrichmentTests : IDisposable
{
    private readonly AtoCopilotContext _context;
    private readonly Mock<ITaskEnrichmentService> _enrichmentServiceMock = new();

    public KanbanServiceEnrichmentTests()
    {
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"KanbanEnrichment_{Guid.NewGuid()}")
            .Options;
        _context = new AtoCopilotContext(options);
    }

    private KanbanService CreateService(ITaskEnrichmentService? enrichmentService = null)
    {
        return new KanbanService(
            _context,
            Mock.Of<ILogger<KanbanService>>(),
            Mock.Of<INotificationService>(),
            Mock.Of<IAgentStateManager>(),
            Mock.Of<IAtoComplianceEngine>(),
            Mock.Of<IRemediationEngine>(),
            enrichmentService);
    }

    private async Task<ComplianceAssessment> SeedAssessment(params (string id, string controlId, FindingSeverity severity)[] findings)
    {
        var assessment = new ComplianceAssessment
        {
            Id = Guid.NewGuid().ToString(),
            SubscriptionId = "sub-1",
            AssessedAt = DateTime.UtcNow,
            Findings = findings.Select(f => new ComplianceFinding
            {
                Id = f.id,
                ControlId = f.controlId,
                ControlFamily = f.controlId.Split('-')[0],
                Title = $"Finding for {f.controlId}",
                Description = $"Non-compliance for {f.controlId}",
                Severity = f.severity,
                Status = FindingStatus.Open,
                ResourceId = "/subscriptions/sub-1/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm1",
                ResourceType = "Microsoft.Compute/virtualMachines",
                Source = "PolicyInsights"
            }).ToList()
        };

        _context.Assessments.Add(assessment);
        await _context.SaveChangesAsync();
        return assessment;
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T028: CreateBoardFromAssessment calls EnrichmentService when available
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateBoard_CallsEnrichmentService_WhenAvailable()
    {
        var assessment = await SeedAssessment(
            ("f1", "AC-2", FindingSeverity.High),
            ("f2", "SC-1", FindingSeverity.Medium));

        _enrichmentServiceMock
            .Setup(e => e.EnrichBoardTasksAsync(
                It.IsAny<RemediationBoard>(),
                It.IsAny<IReadOnlyList<ComplianceFinding>>(),
                It.IsAny<IProgress<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BoardEnrichmentResult
            {
                TasksEnriched = 2,
                TotalTasks = 2
            });

        var service = CreateService(_enrichmentServiceMock.Object);
        var board = await service.CreateBoardFromAssessmentAsync(
            assessment.Id, "Test Board", "sub-1", "owner");

        _enrichmentServiceMock.Verify(
            e => e.EnrichBoardTasksAsync(
                It.Is<RemediationBoard>(b => b.Tasks.Count == 2),
                It.Is<IReadOnlyList<ComplianceFinding>>(f => f.Count == 2),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T029: CreateBoardFromAssessment skips enrichment when service is null
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateBoard_SkipsEnrichment_WhenServiceIsNull()
    {
        var assessment = await SeedAssessment(("f1", "AC-2", FindingSeverity.High));

        var service = CreateService(null); // no enrichment service
        var board = await service.CreateBoardFromAssessmentAsync(
            assessment.Id, "Test Board", "sub-1", "owner");

        board.Tasks.Should().HaveCount(1);
        // No exception thrown — graceful no-op
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T030: Tasks have scripts after enrichment (integration with mock service)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateBoard_TasksHaveScripts_AfterEnrichment()
    {
        var assessment = await SeedAssessment(("f1", "AC-2", FindingSeverity.High));

        _enrichmentServiceMock
            .Setup(e => e.EnrichBoardTasksAsync(
                It.IsAny<RemediationBoard>(),
                It.IsAny<IReadOnlyList<ComplianceFinding>>(),
                It.IsAny<IProgress<string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<RemediationBoard, IReadOnlyList<ComplianceFinding>, IProgress<string>?, CancellationToken>(
                (board, findings, progress, ct) =>
                {
                    foreach (var task in board.Tasks)
                    {
                        task.RemediationScript = "az vm update --name vm1";
                        task.RemediationScriptType = "AzureCli";
                        task.ValidationCriteria = "1. Verify VM compliance";
                    }
                })
            .ReturnsAsync(new BoardEnrichmentResult { TasksEnriched = 1, TotalTasks = 1 });

        var service = CreateService(_enrichmentServiceMock.Object);
        var result = await service.CreateBoardFromAssessmentAsync(
            assessment.Id, "Test Board", "sub-1", "owner");

        result.Tasks.Should().HaveCount(1);
        result.Tasks[0].RemediationScript.Should().Be("az vm update --name vm1");
        result.Tasks[0].RemediationScriptType.Should().Be("AzureCli");
        result.Tasks[0].ValidationCriteria.Should().Be("1. Verify VM compliance");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T030b: Enrichment failure doesn't prevent board creation
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateBoard_SurvivesEnrichmentFailure()
    {
        var assessment = await SeedAssessment(("f1", "AC-2", FindingSeverity.High));

        _enrichmentServiceMock
            .Setup(e => e.EnrichBoardTasksAsync(
                It.IsAny<RemediationBoard>(),
                It.IsAny<IReadOnlyList<ComplianceFinding>>(),
                It.IsAny<IProgress<string>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("AI service down"));

        var service = CreateService(_enrichmentServiceMock.Object);
        var board = await service.CreateBoardFromAssessmentAsync(
            assessment.Id, "Test Board", "sub-1", "owner");

        // Board still created despite enrichment failure
        board.Tasks.Should().HaveCount(1);
        board.Id.Should().NotBeNullOrEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T050: UpdateBoard enriches new tasks when new findings are added
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateBoard_EnrichesNewTasks_WhenNewFindingsAdded()
    {
        // Seed initial board with 1 task
        var assessment1 = await SeedAssessment(("f1", "AC-2", FindingSeverity.High));
        var serviceWithoutEnrichment = CreateService(null);
        var board = await serviceWithoutEnrichment.CreateBoardFromAssessmentAsync(
            assessment1.Id, "Test Board", "sub-1", "owner");

        // Give the existing task a script (simulating prior enrichment)
        board.Tasks[0].RemediationScript = "existing script";
        await _context.SaveChangesAsync();

        // Seed new assessment with the same finding + an additional one (use unique IDs)
        var assessment2 = await SeedAssessment(
            ("f1-v2", "AC-2", FindingSeverity.High),
            ("f3", "IA-5", FindingSeverity.Critical));

        // Patch: update the existing task's FindingId to match the new assessment's finding 
        board.Tasks[0].FindingId = "f1-v2";
        await _context.SaveChangesAsync();

        _enrichmentServiceMock
            .Setup(e => e.EnrichBoardTasksAsync(
                It.IsAny<RemediationBoard>(),
                It.IsAny<IReadOnlyList<ComplianceFinding>>(),
                It.IsAny<IProgress<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BoardEnrichmentResult { TasksEnriched = 1, TotalTasks = 1 });

        var service = CreateService(_enrichmentServiceMock.Object);
        var result = await service.UpdateBoardFromAssessmentAsync(
            board.Id, assessment2.Id, "user1", "User One");

        result.TasksAdded.Should().Be(1);

        // Enrichment was called with the new task(s) only
        _enrichmentServiceMock.Verify(
            e => e.EnrichBoardTasksAsync(
                It.Is<RemediationBoard>(b => b.Tasks.Count == 1), // only the new task
                It.IsAny<IReadOnlyList<ComplianceFinding>>(),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // T051: UpdateBoard does not overwrite existing scripts
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateBoard_DoesNotOverwriteExistingScripts()
    {
        var assessment = await SeedAssessment(("f1", "AC-2", FindingSeverity.High));
        var service = CreateService(null);
        var board = await service.CreateBoardFromAssessmentAsync(
            assessment.Id, "Test Board", "sub-1", "owner");

        // Pre-set script on existing task
        board.Tasks[0].RemediationScript = "Custom user script";
        board.Tasks[0].RemediationScriptType = "PowerShell";
        await _context.SaveChangesAsync();

        // Update board with same finding under new assessment (unique ID to avoid EF tracking conflict)
        var assessment2 = await SeedAssessment(("f1-v2", "AC-2", FindingSeverity.High));

        // Patch existing task to match new finding ID so it's recognized as "unchanged"
        board.Tasks[0].FindingId = "f1-v2";
        await _context.SaveChangesAsync();

        var result = await service.UpdateBoardFromAssessmentAsync(
            board.Id, assessment2.Id, "user1", "User One");

        // Reload and verify existing script was not touched
        var existingTask = await _context.RemediationTasks
            .FirstAsync(t => t.BoardId == board.Id && t.ControlId == "AC-2");
        existingTask.RemediationScript.Should().Be("Custom user script");
        existingTask.RemediationScriptType.Should().Be("PowerShell");
    }
}
