using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Interfaces.Kanban;
using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.Core.Models.Kanban;
using Ato.Copilot.State.Abstractions;
using TaskStatus = Ato.Copilot.Core.Models.Kanban.TaskStatus;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for KanbanService.UpdateBoardFromAssessmentAsync:
/// diff logic — new findings add tasks, resolved findings close tasks, unchanged tasks stay.
/// </summary>
public class KanbanServiceBoardUpdateTests : IDisposable
{
    private readonly AtoCopilotContext _context;
    private readonly KanbanService _service;

    public KanbanServiceBoardUpdateTests()
    {
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"KanbanBoardUpdate_{Guid.NewGuid()}")
            .Options;
        _context = new AtoCopilotContext(options);

        _service = new KanbanService(
            _context,
            Mock.Of<ILogger<KanbanService>>(),
            Mock.Of<INotificationService>(),
            Mock.Of<IAgentStateManager>(),
            Mock.Of<IAtoComplianceEngine>(),
            Mock.Of<IRemediationEngine>());
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task UpdateBoard_NewFinding_AddsTask()
    {
        var (board, assessment) = await SeedBoardAndAssessment(
            existingFindings: new[] { ("f1", "AC-1", FindingStatus.Open) },
            newFindings: new[] { ("f1", "AC-1", FindingStatus.Open), ("f2", "SC-1", FindingStatus.Open) });

        var result = await _service.UpdateBoardFromAssessmentAsync(
            board.Id, assessment.Id, "user1", "User One");

        result.TasksAdded.Should().Be(1);
        result.TasksUnchanged.Should().Be(1);
        result.TasksClosed.Should().Be(0);
    }

    [Fact]
    public async Task UpdateBoard_ResolvedFinding_ClosesTask()
    {
        var (board, assessment) = await SeedBoardAndAssessment(
            existingFindings: new[] { ("f1", "AC-1", FindingStatus.Open) },
            newFindings: new[] { ("f1", "AC-1", FindingStatus.Remediated) });

        var result = await _service.UpdateBoardFromAssessmentAsync(
            board.Id, assessment.Id, "user1", "User One");

        result.TasksClosed.Should().Be(1);

        var closedTask = await _context.RemediationTasks
            .FirstAsync(t => t.FindingId == "f1" && t.BoardId == board.Id);
        closedTask.Status.Should().Be(TaskStatus.Done);
    }

    [Fact]
    public async Task UpdateBoard_ClosedTask_AddsAutoCloseComment()
    {
        var (board, assessment) = await SeedBoardAndAssessment(
            existingFindings: new[] { ("f1", "AC-1", FindingStatus.Open) },
            newFindings: new[] { ("f1", "AC-1", FindingStatus.Remediated) });

        await _service.UpdateBoardFromAssessmentAsync(board.Id, assessment.Id, "user1", "User One");

        var task = await _context.RemediationTasks
            .Include(t => t.Comments)
            .FirstAsync(t => t.FindingId == "f1" && t.BoardId == board.Id);
        task.Comments.Should().Contain(c => c.Content == KanbanConstants.AutoClosedComment);
    }

    [Fact]
    public async Task UpdateBoard_FalsePositive_ClosesTask()
    {
        var (board, assessment) = await SeedBoardAndAssessment(
            existingFindings: new[] { ("f1", "AC-1", FindingStatus.Open) },
            newFindings: new[] { ("f1", "AC-1", FindingStatus.FalsePositive) });

        var result = await _service.UpdateBoardFromAssessmentAsync(
            board.Id, assessment.Id, "user1", "User One");

        result.TasksClosed.Should().Be(1);
    }

    [Fact]
    public async Task UpdateBoard_NoChanges_AllUnchanged()
    {
        var (board, assessment) = await SeedBoardAndAssessment(
            existingFindings: new[] { ("f1", "AC-1", FindingStatus.Open) },
            newFindings: new[] { ("f1", "AC-1", FindingStatus.Open) });

        var result = await _service.UpdateBoardFromAssessmentAsync(
            board.Id, assessment.Id, "user1", "User One");

        result.TasksAdded.Should().Be(0);
        result.TasksClosed.Should().Be(0);
        result.TasksUnchanged.Should().Be(1);
    }

    [Fact]
    public async Task UpdateBoard_UpdatesAssessmentId()
    {
        var (board, assessment) = await SeedBoardAndAssessment(
            existingFindings: new[] { ("f1", "AC-1", FindingStatus.Open) },
            newFindings: new[] { ("f1", "AC-1", FindingStatus.Open) });

        await _service.UpdateBoardFromAssessmentAsync(board.Id, assessment.Id, "user1", "User One");

        var updated = await _context.RemediationBoards.FindAsync(board.Id);
        updated!.AssessmentId.Should().Be(assessment.Id);
    }

    [Fact]
    public async Task UpdateBoard_BoardNotFound_Throws()
    {
        var assessment = new ComplianceAssessment { SubscriptionId = "sub" };
        _context.Assessments.Add(assessment);
        await _context.SaveChangesAsync();

        var act = () => _service.UpdateBoardFromAssessmentAsync("nonexistent", assessment.Id, "u1", "U1");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Board*not found*");
    }

    [Fact]
    public async Task UpdateBoard_AssessmentNotFound_Throws()
    {
        var board = new RemediationBoard { Name = "B", SubscriptionId = "sub", Owner = "o" };
        _context.RemediationBoards.Add(board);
        await _context.SaveChangesAsync();

        var act = () => _service.UpdateBoardFromAssessmentAsync(board.Id, "nonexistent", "u1", "U1");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Assessment*not found*");
    }

    private async Task<(RemediationBoard board, ComplianceAssessment assessment)> SeedBoardAndAssessment(
        (string id, string controlId, FindingStatus status)[] existingFindings,
        (string id, string controlId, FindingStatus status)[] newFindings)
    {
        var board = new RemediationBoard { Name = "Board", SubscriptionId = "sub", Owner = "owner" };

        // Create existing tasks for existing findings
        foreach (var (id, controlId, _) in existingFindings)
        {
            board.Tasks.Add(new RemediationTask
            {
                BoardId = board.Id,
                TaskNumber = $"REM-{board.NextTaskNumber:D3}",
                Title = $"{controlId}: Finding",
                ControlId = controlId,
                ControlFamily = controlId[..2],
                FindingId = id,
                Severity = FindingSeverity.High,
                CreatedBy = "owner",
            });
            board.NextTaskNumber++;
        }

        _context.RemediationBoards.Add(board);

        // Create the "new" assessment with its findings
        var assessment = new ComplianceAssessment { SubscriptionId = "sub" };
        foreach (var (id, controlId, status) in newFindings)
        {
            assessment.Findings.Add(new ComplianceFinding
            {
                Id = id,
                ControlId = controlId,
                ControlFamily = controlId[..2],
                Title = $"Finding for {controlId}",
                Status = status,
                Severity = FindingSeverity.High,
            });
        }

        _context.Assessments.Add(assessment);
        await _context.SaveChangesAsync();

        return (board, assessment);
    }
}
