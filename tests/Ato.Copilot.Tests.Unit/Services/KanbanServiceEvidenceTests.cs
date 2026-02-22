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
using TaskStatus = Ato.Copilot.Core.Models.Kanban.TaskStatus;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for KanbanService.CollectTaskEvidenceAsync:
/// success path, no resources error, history entry, system comment.
/// </summary>
public class KanbanServiceEvidenceTests : IDisposable
{
    private readonly AtoCopilotContext _context;
    private readonly KanbanService _service;

    public KanbanServiceEvidenceTests()
    {
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"KanbanEvidence_{Guid.NewGuid()}")
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
    public async Task CollectEvidence_Success_ReturnsResult()
    {
        var task = await SeedTask(new List<string> { "res1", "res2" });

        var result = await _service.CollectTaskEvidenceAsync(task.Id, "user1", "User One");

        result.Success.Should().BeTrue();
        result.ItemsCollected.Should().Be(2);
        result.Summary.Should().Contain("AC-2");
        result.Summary.Should().Contain("2 resources");
    }

    [Fact]
    public async Task CollectEvidence_AddsHistoryEntry()
    {
        var task = await SeedTask(new List<string> { "res1" });

        await _service.CollectTaskEvidenceAsync(task.Id, "user1", "User One");

        var history = await _context.TaskHistoryEntries
            .Where(h => h.TaskId == task.Id && h.EventType == HistoryEventType.ValidationRun)
            .ToListAsync();

        history.Should().HaveCount(1);
        history[0].NewValue.Should().Be("EvidenceCollected");
        history[0].ActingUserId.Should().Be("user1");
    }

    [Fact]
    public async Task CollectEvidence_AddsSystemComment()
    {
        var task = await SeedTask(new List<string> { "res1" });

        await _service.CollectTaskEvidenceAsync(task.Id, "user1", "User One");

        var comments = await _context.TaskComments
            .Where(c => c.TaskId == task.Id && c.IsSystemComment)
            .ToListAsync();

        comments.Should().HaveCount(1);
        comments[0].Content.Should().Contain("Evidence Collection");
    }

    [Fact]
    public async Task CollectEvidence_NoResources_Throws()
    {
        var task = await SeedTask(new List<string>());

        var act = () => _service.CollectTaskEvidenceAsync(task.Id, "user1", "User One");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no affected resources*");
    }

    [Fact]
    public async Task CollectEvidence_TaskNotFound_Throws()
    {
        var act = () => _service.CollectTaskEvidenceAsync("nonexistent", "user1", "User One");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    private async Task<RemediationTask> SeedTask(List<string> resources)
    {
        var board = new RemediationBoard { Name = "Board", SubscriptionId = "sub", Owner = "owner" };
        _context.RemediationBoards.Add(board);

        var task = new RemediationTask
        {
            BoardId = board.Id,
            TaskNumber = "REM-001",
            Title = "Test", ControlId = "AC-2", ControlFamily = "AC",
            Status = TaskStatus.InProgress,
            AffectedResources = resources,
            Severity = FindingSeverity.High,
            CreatedBy = "owner",
        };
        _context.RemediationTasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }
}
