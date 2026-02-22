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
/// Unit tests for KanbanService.CreateTaskAsync: validation, SLA mapping,
/// sequential IDs, RBAC.
/// </summary>
public class KanbanServiceTaskCreationTests : IDisposable
{
    private readonly AtoCopilotContext _context;
    private readonly KanbanService _service;

    public KanbanServiceTaskCreationTests()
    {
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"KanbanTaskCreation_{Guid.NewGuid()}")
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
    public async Task CreateTask_WithValidInput_CreatesTask()
    {
        var board = await SeedBoard();

        var task = await _service.CreateTaskAsync(
            board.Id, "Test Task", "AC-2", "user1");

        task.Should().NotBeNull();
        task.Title.Should().Be("Test Task");
        task.ControlId.Should().Be("AC-2");
        task.Status.Should().Be(TaskStatus.Backlog);
    }

    [Fact]
    public async Task CreateTask_SetsControlFamily()
    {
        var board = await SeedBoard();

        var task = await _service.CreateTaskAsync(
            board.Id, "Task", "SC-7", "user1");

        task.ControlFamily.Should().Be("SC");
    }

    [Fact]
    public async Task CreateTask_SequentialTaskNumbers()
    {
        var board = await SeedBoard();

        var t1 = await _service.CreateTaskAsync(board.Id, "T1", "AC-1", "u1");
        var t2 = await _service.CreateTaskAsync(board.Id, "T2", "AC-2", "u1");
        var t3 = await _service.CreateTaskAsync(board.Id, "T3", "AC-3", "u1");

        t1.TaskNumber.Should().Be("REM-001");
        t2.TaskNumber.Should().Be("REM-002");
        t3.TaskNumber.Should().Be("REM-003");
    }

    [Fact]
    public async Task CreateTask_SetsDefaultSeverityToMedium()
    {
        var board = await SeedBoard();

        var task = await _service.CreateTaskAsync(
            board.Id, "Task", "AC-2", "u1");

        task.Severity.Should().Be(FindingSeverity.Medium);
    }

    [Fact]
    public async Task CreateTask_CriticalSeverity_DueDateWithin24Hours()
    {
        var board = await SeedBoard();
        var before = DateTime.UtcNow;

        var task = await _service.CreateTaskAsync(
            board.Id, "Task", "AC-2", "u1", severity: FindingSeverity.Critical);

        task.DueDate.Should().BeCloseTo(before.AddHours(24), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CreateTask_InvalidControlId_Throws()
    {
        var board = await SeedBoard();

        var act = () => _service.CreateTaskAsync(
            board.Id, "Task", "invalid", "u1");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid control ID*");
    }

    [Fact]
    public async Task CreateTask_BoardNotFound_Throws()
    {
        var act = () => _service.CreateTaskAsync(
            "nonexistent", "Task", "AC-2", "u1");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task CreateTask_CreatesHistoryEntry()
    {
        var board = await SeedBoard();

        var task = await _service.CreateTaskAsync(
            board.Id, "Task", "AC-2", "u1");

        var history = await _context.TaskHistoryEntries
            .Where(h => h.TaskId == task.Id && h.EventType == HistoryEventType.Created)
            .ToListAsync();

        history.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateTask_WithCustomDueDate_UsesProvided()
    {
        var board = await SeedBoard();
        var customDate = DateTime.UtcNow.AddDays(14);

        var task = await _service.CreateTaskAsync(
            board.Id, "Task", "AC-2", "u1", dueDate: customDate);

        task.DueDate.Should().BeCloseTo(customDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateTask_WithAffectedResources_Sets()
    {
        var board = await SeedBoard();
        var resources = new List<string> { "/sub/rg/vm1", "/sub/rg/vm2" };

        var task = await _service.CreateTaskAsync(
            board.Id, "Task", "AC-2", "u1", affectedResources: resources);

        task.AffectedResources.Should().BeEquivalentTo(resources);
    }

    private async Task<RemediationBoard> SeedBoard()
    {
        var board = new RemediationBoard
        {
            Name = "Test Board",
            SubscriptionId = "sub",
            Owner = "owner",
        };
        _context.RemediationBoards.Add(board);
        await _context.SaveChangesAsync();
        return board;
    }
}
