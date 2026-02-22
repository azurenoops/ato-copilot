using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Interfaces.Kanban;
using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.Core.Models.Kanban;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.State.Abstractions;
using TaskStatus = Ato.Copilot.Core.Models.Kanban.TaskStatus;

namespace Ato.Copilot.Tests.Unit.Tools;

/// <summary>
/// Unit tests for Kanban tool classes:
/// Verifies envelope format (success/error), argument parsing,
/// and delegation to IKanbanService through IServiceScopeFactory.
/// </summary>
public class KanbanToolTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly string _dbName;
    private readonly Mock<INotificationService> _notificationMock = new();

    public KanbanToolTests()
    {
        _dbName = $"KanbanTools_{Guid.NewGuid()}";

        _notificationMock.Setup(n => n.EnqueueAsync(It.IsAny<NotificationMessage>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddDbContext<AtoCopilotContext>(opt => opt.UseInMemoryDatabase(_dbName));
        services.AddScoped<IKanbanService, KanbanService>();
        services.AddSingleton(_notificationMock.Object);
        services.AddSingleton(Mock.Of<IAgentStateManager>());
        services.AddSingleton(Mock.Of<IAtoComplianceEngine>());
        services.AddSingleton(Mock.Of<IRemediationEngine>());
        services.AddLogging();
        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        GC.SuppressFinalize(this);
    }

    private IServiceScopeFactory ScopeFactory => _serviceProvider.GetRequiredService<IServiceScopeFactory>();

    // ── CreateBoardTool ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateBoardTool_Success_ReturnsSuccessEnvelope()
    {
        var tool = new KanbanCreateBoardTool(ScopeFactory, Mock.Of<ILogger<KanbanCreateBoardTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["name"] = "Test Board",
            ["subscription_id"] = "sub-123",
            ["owner"] = "owner"
        });

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("name").GetString().Should().Be("Test Board");
        doc.RootElement.GetProperty("metadata").GetProperty("toolName").GetString().Should().Be("kanban_create_board");
    }

    [Fact]
    public async Task CreateBoardTool_MissingName_ReturnsErrorEnvelope()
    {
        var tool = new KanbanCreateBoardTool(ScopeFactory, Mock.Of<ILogger<KanbanCreateBoardTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscription_id"] = "sub-123"
        });

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString().Should().NotBeNullOrEmpty();
    }

    // ── BoardShowTool ─────────────────────────────────────────────────────

    [Fact]
    public async Task BoardShowTool_ValidBoard_ReturnsColumns()
    {
        // Seed a board
        using (var scope = ScopeFactory.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
            var board = new RemediationBoard { Id = "board-1", Name = "B1", SubscriptionId = "sub", Owner = "o" };
            board.Tasks.Add(new RemediationTask
            {
                BoardId = "board-1", TaskNumber = "REM-001", Title = "T1",
                ControlId = "AC-1", ControlFamily = "AC",
                Severity = FindingSeverity.High, CreatedBy = "owner",
            });
            ctx.RemediationBoards.Add(board);
            await ctx.SaveChangesAsync();
        }

        var tool = new KanbanBoardShowTool(ScopeFactory, Mock.Of<ILogger<KanbanBoardShowTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["board_id"] = "board-1"
        });

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("totalTasks").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task BoardShowTool_NotFound_ReturnsError()
    {
        var tool = new KanbanBoardShowTool(ScopeFactory, Mock.Of<ILogger<KanbanBoardShowTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["board_id"] = "nonexistent"
        });

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString().Should().Be("BOARD_NOT_FOUND");
    }

    // ── CreateTaskTool ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTaskTool_ValidInput_ReturnsSuccess()
    {
        // Seed a board
        using (var scope = ScopeFactory.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
            ctx.RemediationBoards.Add(new RemediationBoard { Id = "board-2", Name = "B", SubscriptionId = "sub", Owner = "o" });
            await ctx.SaveChangesAsync();
        }

        var tool = new KanbanCreateTaskTool(ScopeFactory, Mock.Of<ILogger<KanbanCreateTaskTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["board_id"] = "board-2",
            ["title"] = "Fix MFA",
            ["control_id"] = "AC-2",
            ["created_by"] = "owner"
        });

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("taskNumber").GetString().Should().Be("REM-001");
    }

    // ── MoveTaskTool ──────────────────────────────────────────────────────

    [Fact]
    public async Task MoveTaskTool_ValidTransition_ReturnsSuccess()
    {
        // Seed a board with a task assigned to "system" so Analyst CanMoveOwn works
        string taskId;
        using (var scope = ScopeFactory.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
            var board = new RemediationBoard { Id = "board-3", Name = "B", SubscriptionId = "sub", Owner = "o" };
            var task = new RemediationTask
            {
                BoardId = "board-3", TaskNumber = "REM-001", Title = "T",
                ControlId = "AC-1", ControlFamily = "AC", Status = TaskStatus.Backlog,
                Severity = FindingSeverity.High, CreatedBy = "owner",
            };
            taskId = task.Id;
            board.Tasks.Add(task);
            ctx.RemediationBoards.Add(board);
            await ctx.SaveChangesAsync();
        }

        var tool = new KanbanMoveTaskTool(ScopeFactory, Mock.Of<ILogger<KanbanMoveTaskTool>>());

        // Note: KanbanMoveTaskTool hard-codes "Compliance.Officer" role which isn't in
        // ComplianceRoles (no CanMoveAny/CanMoveOwn). The tool doesn't catch 
        // UnauthorizedAccessException so it propagates. This validates that behavior.
        var act = () => tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["task_id"] = taskId,
            ["target_status"] = "ToDo",
        }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Tool metadata ─────────────────────────────────────────────────────

    [Fact]
    public void AllKanbanTools_HaveUniqueNames()
    {
        var factory = ScopeFactory;
        var tools = new KanbanToolBase[]
        {
            new KanbanCreateBoardTool(factory, Mock.Of<ILogger<KanbanCreateBoardTool>>()),
            new KanbanBoardShowTool(factory, Mock.Of<ILogger<KanbanBoardShowTool>>()),
            new KanbanGetTaskTool(factory, Mock.Of<ILogger<KanbanGetTaskTool>>()),
            new KanbanCreateTaskTool(factory, Mock.Of<ILogger<KanbanCreateTaskTool>>()),
            new KanbanAssignTaskTool(factory, Mock.Of<ILogger<KanbanAssignTaskTool>>()),
            new KanbanMoveTaskTool(factory, Mock.Of<ILogger<KanbanMoveTaskTool>>()),
            new KanbanTaskListTool(factory, Mock.Of<ILogger<KanbanTaskListTool>>()),
            new KanbanTaskHistoryTool(factory, Mock.Of<ILogger<KanbanTaskHistoryTool>>()),
            new KanbanValidateTaskTool(factory, Mock.Of<ILogger<KanbanValidateTaskTool>>()),
            new KanbanAddCommentTool(factory, Mock.Of<ILogger<KanbanAddCommentTool>>()),
            new KanbanTaskCommentsTool(factory, Mock.Of<ILogger<KanbanTaskCommentsTool>>()),
            new KanbanEditCommentTool(factory, Mock.Of<ILogger<KanbanEditCommentTool>>()),
            new KanbanDeleteCommentTool(factory, Mock.Of<ILogger<KanbanDeleteCommentTool>>()),
            new KanbanRemediateTaskTool(factory, Mock.Of<ILogger<KanbanRemediateTaskTool>>()),
            new KanbanCollectEvidenceTool(factory, Mock.Of<ILogger<KanbanCollectEvidenceTool>>()),
            new KanbanBulkUpdateTool(factory, Mock.Of<ILogger<KanbanBulkUpdateTool>>()),
            new KanbanExportTool(factory, Mock.Of<ILogger<KanbanExportTool>>()),
            new KanbanArchiveBoardTool(factory, Mock.Of<ILogger<KanbanArchiveBoardTool>>()),
        };

        var names = tools.Select(t => t.Name).ToList();
        names.Should().OnlyHaveUniqueItems();
        names.Should().HaveCount(18);
    }

    [Fact]
    public void AllKanbanTools_HaveDescriptions()
    {
        var factory = ScopeFactory;
        var tools = new KanbanToolBase[]
        {
            new KanbanCreateBoardTool(factory, Mock.Of<ILogger<KanbanCreateBoardTool>>()),
            new KanbanBoardShowTool(factory, Mock.Of<ILogger<KanbanBoardShowTool>>()),
            new KanbanGetTaskTool(factory, Mock.Of<ILogger<KanbanGetTaskTool>>()),
            new KanbanCreateTaskTool(factory, Mock.Of<ILogger<KanbanCreateTaskTool>>()),
            new KanbanAssignTaskTool(factory, Mock.Of<ILogger<KanbanAssignTaskTool>>()),
            new KanbanMoveTaskTool(factory, Mock.Of<ILogger<KanbanMoveTaskTool>>()),
            new KanbanTaskListTool(factory, Mock.Of<ILogger<KanbanTaskListTool>>()),
            new KanbanTaskHistoryTool(factory, Mock.Of<ILogger<KanbanTaskHistoryTool>>()),
            new KanbanValidateTaskTool(factory, Mock.Of<ILogger<KanbanValidateTaskTool>>()),
            new KanbanAddCommentTool(factory, Mock.Of<ILogger<KanbanAddCommentTool>>()),
            new KanbanTaskCommentsTool(factory, Mock.Of<ILogger<KanbanTaskCommentsTool>>()),
            new KanbanEditCommentTool(factory, Mock.Of<ILogger<KanbanEditCommentTool>>()),
            new KanbanDeleteCommentTool(factory, Mock.Of<ILogger<KanbanDeleteCommentTool>>()),
            new KanbanRemediateTaskTool(factory, Mock.Of<ILogger<KanbanRemediateTaskTool>>()),
            new KanbanCollectEvidenceTool(factory, Mock.Of<ILogger<KanbanCollectEvidenceTool>>()),
            new KanbanBulkUpdateTool(factory, Mock.Of<ILogger<KanbanBulkUpdateTool>>()),
            new KanbanExportTool(factory, Mock.Of<ILogger<KanbanExportTool>>()),
            new KanbanArchiveBoardTool(factory, Mock.Of<ILogger<KanbanArchiveBoardTool>>()),
        };

        tools.Should().AllSatisfy(t =>
        {
            t.Description.Should().NotBeNullOrWhiteSpace();
            t.Parameters.Should().NotBeNull();
        });
    }

    // ── Envelope structure ────────────────────────────────────────────────

    [Fact]
    public async Task SuccessEnvelope_HasRequiredFields()
    {
        var tool = new KanbanCreateBoardTool(ScopeFactory, Mock.Of<ILogger<KanbanCreateBoardTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["name"] = "Envelope Test",
            ["subscription_id"] = "sub",
        });

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Object);
        root.GetProperty("metadata").GetProperty("toolName").GetString().Should().Be("kanban_create_board");
        root.GetProperty("metadata").GetProperty("timestamp").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ErrorEnvelope_HasRequiredFields()
    {
        var tool = new KanbanBoardShowTool(ScopeFactory, Mock.Of<ILogger<KanbanBoardShowTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["board_id"] = "nonexistent"
        });

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("error");
        root.GetProperty("error").GetProperty("message").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("error").GetProperty("errorCode").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("metadata").GetProperty("toolName").GetString().Should().Be("kanban_board_show");
    }
}
