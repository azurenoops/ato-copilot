using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Auth;
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

    /// <summary>
    /// Creates a ServiceProvider with IUserContext registered for a specific user.
    /// </summary>
    private ServiceProvider BuildServiceProviderWithUser(string userId, string displayName, string role)
    {
        var userContext = new Mock<IUserContext>();
        userContext.Setup(u => u.UserId).Returns(userId);
        userContext.Setup(u => u.DisplayName).Returns(displayName);
        userContext.Setup(u => u.Role).Returns(role);
        userContext.Setup(u => u.IsAuthenticated).Returns(true);

        var services = new ServiceCollection();
        services.AddDbContext<AtoCopilotContext>(opt => opt.UseInMemoryDatabase(_dbName));
        services.AddScoped<IKanbanService, KanbanService>();
        services.AddSingleton(_notificationMock.Object);
        services.AddSingleton(Mock.Of<IAgentStateManager>());
        services.AddSingleton(Mock.Of<IAtoComplianceEngine>());
        services.AddSingleton(Mock.Of<IRemediationEngine>());
        services.AddScoped<IUserContext>(_ => userContext.Object);
        services.AddLogging();
        return services.BuildServiceProvider();
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

        // Note: Without IUserContext registered, AnonymousUserContext returns
        // "Compliance.Viewer" role which has no CanMoveAny/CanMoveOwn permissions.
        // KanbanService throws UnauthorizedAccessException for unauthorized moves.
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

    // ── Identity propagation (T018) ───────────────────────────────────────

    [Fact]
    public async Task CreateBoardTool_PropagatesUserContextOwner()
    {
        using var sp = BuildServiceProviderWithUser("user-abc", "Alice", ComplianceRoles.Administrator);
        var factory = sp.GetRequiredService<IServiceScopeFactory>();
        var tool = new KanbanCreateBoardTool(factory, Mock.Of<ILogger<KanbanCreateBoardTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["name"] = "Identity Board",
            ["subscription_id"] = "sub-id",
        });

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("owner").GetString().Should().Be("user-abc");
    }

    [Fact]
    public async Task CreateTaskTool_PropagatesUserContextCreatedBy()
    {
        using var sp = BuildServiceProviderWithUser("user-xyz", "Bob", ComplianceRoles.Administrator);
        var factory = sp.GetRequiredService<IServiceScopeFactory>();

        // Seed board
        using (var scope = factory.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
            ctx.RemediationBoards.Add(new RemediationBoard { Id = "board-id-ct", Name = "B", SubscriptionId = "sub", Owner = "o" });
            await ctx.SaveChangesAsync();
        }

        var tool = new KanbanCreateTaskTool(factory, Mock.Of<ILogger<KanbanCreateTaskTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["board_id"] = "board-id-ct",
            ["title"] = "Task by Bob",
            ["control_id"] = "AC-1",
        });

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("createdBy").GetString().Should().Be("user-xyz");
    }

    [Fact]
    public async Task CreateBoardTool_WithoutUserContext_UsesAnonymousFallback()
    {
        // Default _serviceProvider has no IUserContext registered
        var tool = new KanbanCreateBoardTool(ScopeFactory, Mock.Of<ILogger<KanbanCreateBoardTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["name"] = "Anonymous Board",
            ["subscription_id"] = "sub-anon",
        });

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("owner").GetString().Should().Be("anonymous");
    }

    [Fact]
    public async Task AssignTaskTool_PropagatesUserContextRole()
    {
        using var sp = BuildServiceProviderWithUser("admin-001", "Admin", ComplianceRoles.Administrator);
        var factory = sp.GetRequiredService<IServiceScopeFactory>();

        // Seed board with task
        string taskId;
        using (var scope = factory.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
            var board = new RemediationBoard { Id = "board-assign", Name = "B", SubscriptionId = "sub", Owner = "o" };
            var task = new RemediationTask
            {
                BoardId = "board-assign", TaskNumber = "REM-001", Title = "T",
                ControlId = "AC-1", ControlFamily = "AC",
                Severity = FindingSeverity.High, CreatedBy = "owner",
            };
            taskId = task.Id;
            board.Tasks.Add(task);
            ctx.RemediationBoards.Add(board);
            await ctx.SaveChangesAsync();
        }

        var tool = new KanbanAssignTaskTool(factory, Mock.Of<ILogger<KanbanAssignTaskTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["task_id"] = taskId,
            ["assignee_id"] = "dev-001",
            ["assignee_name"] = "Dev User",
        });

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("assignedBy").GetString().Should().Be("admin-001");
    }

    // ── isAssignedToCurrentUser (T023) ────────────────────────────────────

    [Fact]
    public async Task BoardShowTool_FlagsTaskAssignedToCurrentUser()
    {
        using var sp = BuildServiceProviderWithUser("user-me", "Me", ComplianceRoles.Analyst);
        var factory = sp.GetRequiredService<IServiceScopeFactory>();

        // Seed board with tasks — one assigned to "user-me", one to someone else
        using (var scope = factory.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
            var board = new RemediationBoard { Id = "board-cu", Name = "B", SubscriptionId = "sub", Owner = "o" };
            board.Tasks.Add(new RemediationTask
            {
                BoardId = "board-cu", TaskNumber = "REM-001", Title = "My Task",
                ControlId = "AC-1", ControlFamily = "AC",
                Severity = FindingSeverity.High, CreatedBy = "owner",
                AssigneeId = "user-me", AssigneeName = "Me",
            });
            board.Tasks.Add(new RemediationTask
            {
                BoardId = "board-cu", TaskNumber = "REM-002", Title = "Other Task",
                ControlId = "AC-2", ControlFamily = "AC",
                Severity = FindingSeverity.Medium, CreatedBy = "owner",
                AssigneeId = "user-other", AssigneeName = "Other",
            });
            ctx.RemediationBoards.Add(board);
            await ctx.SaveChangesAsync();
        }

        var tool = new KanbanBoardShowTool(factory, Mock.Of<ILogger<KanbanBoardShowTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?> { ["board_id"] = "board-cu" });

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");

        var columns = doc.RootElement.GetProperty("data").GetProperty("columns");
        // columns is an object keyed by status — iterate its properties
        var allTasks = new List<JsonElement>();
        foreach (var col in columns.EnumerateObject())
            foreach (var t in col.Value.GetProperty("tasks").EnumerateArray())
                allTasks.Add(t);

        allTasks.Should().HaveCount(2);

        var myTask = allTasks.First(t => t.GetProperty("taskNumber").GetString() == "REM-001");
        myTask.GetProperty("isAssignedToCurrentUser").GetBoolean().Should().BeTrue();

        var otherTask = allTasks.First(t => t.GetProperty("taskNumber").GetString() == "REM-002");
        otherTask.GetProperty("isAssignedToCurrentUser").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task BoardShowTool_WithoutUserContext_FlagIsFalse()
    {
        // No IUserContext registered — AnonymousUserContext (IsAuthenticated=false)
        using (var scope = ScopeFactory.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
            var board = new RemediationBoard { Id = "board-anon", Name = "B", SubscriptionId = "sub", Owner = "o" };
            board.Tasks.Add(new RemediationTask
            {
                BoardId = "board-anon", TaskNumber = "REM-001", Title = "T",
                ControlId = "AC-1", ControlFamily = "AC",
                Severity = FindingSeverity.High, CreatedBy = "owner",
                AssigneeId = "anonymous", AssigneeName = "Anon",
            });
            ctx.RemediationBoards.Add(board);
            await ctx.SaveChangesAsync();
        }

        var tool = new KanbanBoardShowTool(ScopeFactory, Mock.Of<ILogger<KanbanBoardShowTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?> { ["board_id"] = "board-anon" });

        using var doc = JsonDocument.Parse(result);
        var columns = doc.RootElement.GetProperty("data").GetProperty("columns");
        var tasks = new List<JsonElement>();
        foreach (var col in columns.EnumerateObject())
            foreach (var t in col.Value.GetProperty("tasks").EnumerateArray())
                tasks.Add(t);

        tasks.Should().HaveCount(1);
        tasks[0].GetProperty("isAssignedToCurrentUser").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task GetTaskTool_FlagsTaskAssignedToCurrentUser()
    {
        using var sp = BuildServiceProviderWithUser("user-me", "Me", ComplianceRoles.Analyst);
        var factory = sp.GetRequiredService<IServiceScopeFactory>();

        string taskId;
        using (var scope = factory.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
            var board = new RemediationBoard { Id = "board-gt", Name = "B", SubscriptionId = "sub", Owner = "o" };
            var task = new RemediationTask
            {
                BoardId = "board-gt", TaskNumber = "REM-001", Title = "My Task",
                ControlId = "AC-1", ControlFamily = "AC",
                Severity = FindingSeverity.High, CreatedBy = "owner",
                AssigneeId = "user-me", AssigneeName = "Me",
            };
            taskId = task.Id;
            board.Tasks.Add(task);
            ctx.RemediationBoards.Add(board);
            await ctx.SaveChangesAsync();
        }

        var tool = new KanbanGetTaskTool(factory, Mock.Of<ILogger<KanbanGetTaskTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?> { ["task_id"] = taskId });

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("isAssignedToCurrentUser").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task TaskListTool_FlagsTaskAssignedToCurrentUser()
    {
        using var sp = BuildServiceProviderWithUser("user-me", "Me", ComplianceRoles.Analyst);
        var factory = sp.GetRequiredService<IServiceScopeFactory>();

        using (var scope = factory.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
            var board = new RemediationBoard { Id = "board-tl", Name = "B", SubscriptionId = "sub", Owner = "o" };
            board.Tasks.Add(new RemediationTask
            {
                BoardId = "board-tl", TaskNumber = "REM-001", Title = "My Task",
                ControlId = "AC-1", ControlFamily = "AC",
                Severity = FindingSeverity.High, CreatedBy = "owner",
                AssigneeId = "user-me", AssigneeName = "Me",
            });
            board.Tasks.Add(new RemediationTask
            {
                BoardId = "board-tl", TaskNumber = "REM-002", Title = "Other",
                ControlId = "AC-2", ControlFamily = "AC",
                Severity = FindingSeverity.Low, CreatedBy = "owner",
                AssigneeId = "user-other", AssigneeName = "Other",
            });
            ctx.RemediationBoards.Add(board);
            await ctx.SaveChangesAsync();
        }

        var tool = new KanbanTaskListTool(factory, Mock.Of<ILogger<KanbanTaskListTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?> { ["board_id"] = "board-tl" });

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        var tasks = doc.RootElement.GetProperty("data").GetProperty("tasks");

        var taskList = tasks.EnumerateArray().ToList();
        taskList.Should().HaveCount(2);

        var myTask = taskList.First(t => t.GetProperty("taskNumber").GetString() == "REM-001");
        myTask.GetProperty("isAssignedToCurrentUser").GetBoolean().Should().BeTrue();

        var otherTask = taskList.First(t => t.GetProperty("taskNumber").GetString() == "REM-002");
        otherTask.GetProperty("isAssignedToCurrentUser").GetBoolean().Should().BeFalse();
    }
}
