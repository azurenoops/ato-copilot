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
/// Unit tests for KanbanService.ValidateTaskAsync: re-scanning resources,
/// pass/fail results, history entries, system comments.
/// </summary>
public class KanbanServiceValidationTests : IDisposable
{
    private readonly AtoCopilotContext _context;
    private readonly KanbanService _service;
    private readonly Mock<IRemediationEngine> _remediationMock = new();

    public KanbanServiceValidationTests()
    {
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"KanbanValidation_{Guid.NewGuid()}")
            .Options;
        _context = new AtoCopilotContext(options);

        _service = new KanbanService(
            _context,
            Mock.Of<ILogger<KanbanService>>(),
            Mock.Of<INotificationService>(),
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
    public async Task ValidateTask_AllPassed_ReturnsCanCloseTrue()
    {
        _remediationMock.Setup(r => r.ValidateRemediationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("All resources compliant");

        var task = await SeedTask(new List<string> { "res1", "res2" });

        var result = await _service.ValidateTaskAsync(task.Id);

        result.AllPassed.Should().BeTrue();
        result.CanClose.Should().BeTrue();
        result.ResourceResults.Should().HaveCount(2);
        result.ResourceResults.Should().AllSatisfy(r => r.Passed.Should().BeTrue());
    }

    [Fact]
    public async Task ValidateTask_SomeFailed_ReturnsCanCloseFalse()
    {
        var calls = 0;
        _remediationMock.Setup(r => r.ValidateRemediationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => calls++ == 0 ? "Resource is compliant" : "VIOLATION: resource is out of policy");

        var task = await SeedTask(new List<string> { "res1", "res2" });

        var result = await _service.ValidateTaskAsync(task.Id);

        result.AllPassed.Should().BeFalse();
        result.CanClose.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateTask_AddsValidationRunHistoryEntry()
    {
        _remediationMock.Setup(r => r.ValidateRemediationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("pass");

        var task = await SeedTask(new List<string> { "res1" });

        await _service.ValidateTaskAsync(task.Id);

        var history = await _context.TaskHistoryEntries
            .Where(h => h.TaskId == task.Id && h.EventType == HistoryEventType.ValidationRun)
            .ToListAsync();

        history.Should().HaveCount(1);
    }

    [Fact]
    public async Task ValidateTask_AddsSystemComment()
    {
        _remediationMock.Setup(r => r.ValidateRemediationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("pass");

        var task = await SeedTask(new List<string> { "res1" });

        await _service.ValidateTaskAsync(task.Id);

        var comments = await _context.TaskComments
            .Where(c => c.TaskId == task.Id && c.IsSystemComment)
            .ToListAsync();

        comments.Should().HaveCount(1);
        comments[0].Content.Should().Contain("Validation Results");
    }

    [Fact]
    public async Task ValidateTask_ExceptionOnResource_ReportsFailure()
    {
        _remediationMock.Setup(r => r.ValidateRemediationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection timeout"));

        var task = await SeedTask(new List<string> { "res1" });

        var result = await _service.ValidateTaskAsync(task.Id);

        result.AllPassed.Should().BeFalse();
        result.ResourceResults[0].Passed.Should().BeFalse();
        result.ResourceResults[0].Details.Should().Contain("Connection timeout");
    }

    [Fact]
    public async Task ValidateTask_TaskNotFound_Throws()
    {
        var act = () => _service.ValidateTaskAsync("nonexistent");

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
            Status = TaskStatus.InReview,
            AffectedResources = resources,
            Severity = FindingSeverity.High,
            CreatedBy = "owner",
        };
        _context.RemediationTasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }
}
