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
/// Unit tests for KanbanService.ExecuteTaskRemediationAsync:
/// success→InReview, failure→error comment, no-script error.
/// </summary>
public class KanbanServiceRemediationTests : IDisposable
{
    private readonly AtoCopilotContext _context;
    private readonly KanbanService _service;
    private readonly Mock<IRemediationEngine> _remediationMock = new();
    private readonly Mock<INotificationService> _notificationMock = new();

    public KanbanServiceRemediationTests()
    {
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"KanbanRemediation_{Guid.NewGuid()}")
            .Options;
        _context = new AtoCopilotContext(options);

        _notificationMock.Setup(n => n.EnqueueAsync(It.IsAny<NotificationMessage>()))
            .Returns(Task.CompletedTask);

        _service = new KanbanService(
            _context,
            Mock.Of<ILogger<KanbanService>>(),
            _notificationMock.Object,
            Mock.Of<IAgentStateManager>(),
            Mock.Of<IAtoComplianceEngine>(),
            _remediationMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ExecuteRemediation_Success_MovesToInReview()
    {
        _remediationMock.Setup(r => r.ExecuteRemediationAsync(
            It.IsAny<string>(), true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Remediation applied successfully");

        var task = await SeedTask(TaskStatus.InProgress, "script.ps1");

        var result = await _service.ExecuteTaskRemediationAsync(task.Id, "user1", "User One");

        result.Success.Should().BeTrue();
        result.Details.Should().Contain("Remediation applied");

        var updated = await _context.RemediationTasks.FindAsync(task.Id);
        updated!.Status.Should().Be(TaskStatus.InReview);
    }

    [Fact]
    public async Task ExecuteRemediation_Success_AddsHistoryEntry()
    {
        _remediationMock.Setup(r => r.ExecuteRemediationAsync(
            It.IsAny<string>(), true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync("ok");

        var task = await SeedTask(TaskStatus.InProgress, "script.ps1");

        await _service.ExecuteTaskRemediationAsync(task.Id, "user1", "User One");

        var history = await _context.TaskHistoryEntries
            .Where(h => h.TaskId == task.Id && h.EventType == HistoryEventType.RemediationAttempt)
            .ToListAsync();

        history.Should().ContainSingle(h => h.NewValue == "Success");
    }

    [Fact]
    public async Task ExecuteRemediation_Failure_AddsErrorComment()
    {
        _remediationMock.Setup(r => r.ExecuteRemediationAsync(
            It.IsAny<string>(), true, false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Access denied"));

        var task = await SeedTask(TaskStatus.InProgress, "script.ps1");

        var result = await _service.ExecuteTaskRemediationAsync(task.Id, "user1", "User One");

        result.Success.Should().BeFalse();
        result.Details.Should().Contain("Access denied");

        var comments = await _context.TaskComments
            .Where(c => c.TaskId == task.Id && c.IsSystemComment)
            .ToListAsync();

        comments.Should().ContainSingle(c => c.Content.Contains("Remediation failed"));
    }

    [Fact]
    public async Task ExecuteRemediation_Failure_AddsFailedHistoryEntry()
    {
        _remediationMock.Setup(r => r.ExecuteRemediationAsync(
            It.IsAny<string>(), true, false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("timeout"));

        var task = await SeedTask(TaskStatus.InProgress, "script.ps1");

        await _service.ExecuteTaskRemediationAsync(task.Id, "user1", "User One");

        var history = await _context.TaskHistoryEntries
            .Where(h => h.TaskId == task.Id && h.EventType == HistoryEventType.RemediationAttempt)
            .ToListAsync();

        history.Should().ContainSingle(h => h.NewValue == "Failed");
    }

    [Fact]
    public async Task ExecuteRemediation_NoScript_ThrowsInvalidOperation()
    {
        var task = await SeedTask(TaskStatus.InProgress, remediationScript: null);

        var act = () => _service.ExecuteTaskRemediationAsync(task.Id, "user1", "User One");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no remediation script*");
    }

    [Fact]
    public async Task ExecuteRemediation_TaskNotFound_Throws()
    {
        var act = () => _service.ExecuteTaskRemediationAsync("nonexistent", "user1", "User One");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    private async Task<RemediationTask> SeedTask(TaskStatus status, string? remediationScript = "fix.ps1")
    {
        var board = new RemediationBoard { Name = "Board", SubscriptionId = "sub", Owner = "owner" };
        _context.RemediationBoards.Add(board);

        var task = new RemediationTask
        {
            BoardId = board.Id,
            TaskNumber = "REM-001",
            Title = "Fix", ControlId = "AC-2", ControlFamily = "AC",
            Status = status,
            Severity = FindingSeverity.High,
            CreatedBy = "owner",
            AssigneeId = "user1",
            AssigneeName = "User One",
            RemediationScript = remediationScript,
            AffectedResources = new List<string> { "res1" },
        };
        _context.RemediationTasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }
}
