using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Interfaces.Auth;
using Ato.Copilot.Core.Interfaces.Kanban;
using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.Core.Models.Kanban;
using TaskStatus = Ato.Copilot.Core.Models.Kanban.TaskStatus;

namespace Ato.Copilot.Agents.Compliance.Tools;

// ═══════════════════════════════════════════════════════════════════════════════
// Base class for all Kanban tools — provides standard MCP envelope formatting
// and scoped IKanbanService resolution (Singleton tools → Scoped service).
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Abstract base for Kanban tools. Holds <see cref="IServiceScopeFactory"/> so
/// each <c>ExecuteAsync</c> call can resolve a scoped <see cref="IKanbanService"/>.
/// </summary>
public abstract class KanbanToolBase : BaseTool
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    protected readonly IServiceScopeFactory ScopeFactory;

    protected KanbanToolBase(IServiceScopeFactory scopeFactory, ILogger logger) : base(logger)
    {
        ScopeFactory = scopeFactory;
    }

    /// <summary>Formats a success response envelope per Constitution Principle VII.</summary>
    protected string Success(object data, int? page = null, int? pageSize = null, int? totalItems = null)
    {
        var response = new Dictionary<string, object?>
        {
            ["status"] = "success",
            ["data"] = data,
            ["metadata"] = new { toolName = Name, executionTimeMs = 0, timestamp = DateTime.UtcNow }
        };

        if (page.HasValue && pageSize.HasValue && totalItems.HasValue)
        {
            var totalPages = pageSize.Value > 0 ? (int)Math.Ceiling((double)totalItems.Value / pageSize.Value) : 0;
            response["pagination"] = new
            {
                page = page.Value,
                pageSize = pageSize.Value,
                totalItems = totalItems.Value,
                totalPages,
                hasNextPage = page.Value < totalPages
            };
        }

        return JsonSerializer.Serialize(response, JsonOpts);
    }

    /// <summary>Formats an error response envelope.</summary>
    protected string Error(string message, string errorCode, string? suggestion = null)
    {
        return JsonSerializer.Serialize(new
        {
            status = "error",
            error = new { message, errorCode, suggestion },
            metadata = new { toolName = Name, executionTimeMs = 0, timestamp = DateTime.UtcNow }
        }, JsonOpts);
    }

    /// <summary>Parse a JSON array argument into a List of strings.</summary>
    protected static List<string> ParseStringArray(Dictionary<string, object?> arguments, string key)
    {
        if (!arguments.TryGetValue(key, out var val) || val == null)
            return new List<string>();

        if (val is JsonElement je && je.ValueKind == JsonValueKind.Array)
            return je.EnumerateArray().Select(e => e.GetString()!).Where(s => s != null).ToList();

        if (val is IEnumerable<string> strList)
            return strList.ToList();

        return new List<string>();
    }

    /// <summary>
    /// Resolves <see cref="IUserContext"/> from a scoped <see cref="IServiceProvider"/>.
    /// Falls back to a default anonymous context if the service is not registered.
    /// </summary>
    /// <param name="serviceProvider">The scoped service provider.</param>
    /// <returns>The resolved <see cref="IUserContext"/> instance.</returns>
    protected static IUserContext ResolveUserContext(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetService<IUserContext>() ?? AnonymousUserContext.Instance;
    }
}

/// <summary>
/// Fallback <see cref="IUserContext"/> for unauthenticated or test scenarios
/// where no <c>IUserContext</c> is registered in DI.
/// </summary>
internal sealed class AnonymousUserContext : IUserContext
{
    public static readonly AnonymousUserContext Instance = new();
    public string UserId => "anonymous";
    public string DisplayName => "anonymous";
    public string Role => "Compliance.Viewer";
    public bool IsAuthenticated => false;
}

// ═══════════════════════════════════════════════════════════════════════════════
// Board Tools
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// MCP tool: kanban_create_board — Create a Kanban board (empty or from assessment).
/// </summary>
public class KanbanCreateBoardTool : KanbanToolBase
{
    public KanbanCreateBoardTool(IServiceScopeFactory scopeFactory, ILogger<KanbanCreateBoardTool> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "kanban_create_board";
    public override string Description => "Create a new Kanban remediation board. Optionally auto-populate from an existing compliance assessment.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["name"] = new() { Name = "name", Description = "Board name (e.g., 'Q1 2026 FedRAMP Audit')", Type = "string", Required = true },
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Azure subscription ID", Type = "string" },
        ["assessment_id"] = new() { Name = "assessment_id", Description = "Existing assessment ID to populate tasks from findings", Type = "string" },
        ["owner"] = new() { Name = "owner", Description = "Board owner identity. Defaults to current user.", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var name = GetArg<string>(arguments, "name");
        var subscriptionId = GetArg<string>(arguments, "subscription_id") ?? "";
        var assessmentId = GetArg<string>(arguments, "assessment_id");

        if (string.IsNullOrWhiteSpace(name))
            return Error("Board name is required.", "COMMENT_REQUIRES_TEXT", "Provide a name for the board.");

        try
        {
            using var scope = ScopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IKanbanService>();
            var userContext = ResolveUserContext(scope.ServiceProvider);
            var owner = GetArg<string>(arguments, "owner") ?? userContext.UserId;

            RemediationBoard board;
            if (!string.IsNullOrWhiteSpace(assessmentId))
                board = await svc.CreateBoardFromAssessmentAsync(assessmentId, name, subscriptionId, owner, cancellationToken);
            else
                board = await svc.CreateBoardAsync(name, subscriptionId, owner, cancellationToken);

            var tasksByStatus = board.Tasks.GroupBy(t => t.Status).ToDictionary(g => g.Key.ToString(), g => g.Count());
            var tasksBySeverity = board.Tasks.GroupBy(t => t.Severity).ToDictionary(g => g.Key.ToString(), g => g.Count());

            return Success(new
            {
                boardId = board.Id, name = board.Name, subscriptionId = board.SubscriptionId,
                assessmentId = board.AssessmentId, owner = board.Owner, createdAt = board.CreatedAt,
                taskCount = board.Tasks.Count, tasksByStatus, tasksBySeverity
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return Error(ex.Message, "ASSESSMENT_NOT_FOUND", "Verify the assessment ID is correct.");
        }
    }
}

/// <summary>
/// MCP tool: kanban_board_show — Display board overview with columns and task summaries.
/// </summary>
public class KanbanBoardShowTool : KanbanToolBase
{
    public KanbanBoardShowTool(IServiceScopeFactory scopeFactory, ILogger<KanbanBoardShowTool> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "kanban_board_show";
    public override string Description => "Show a Kanban board overview with task counts and column breakdowns.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["board_id"] = new() { Name = "board_id", Description = "Board ID", Type = "string", Required = true },
        ["include_task_summaries"] = new() { Name = "include_task_summaries", Description = "Include brief task summaries in each column", Type = "boolean" },
        ["page"] = new() { Name = "page", Description = "Page number for task summaries", Type = "integer" },
        ["page_size"] = new() { Name = "page_size", Description = "Tasks per page", Type = "integer" }
    };

    public override async Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var boardId = GetArg<string>(arguments, "board_id");
        var includeTaskSummaries = GetArg<bool?>(arguments, "include_task_summaries") ?? true;
        var page = GetArg<int?>(arguments, "page") ?? 1;
        var pageSize = GetArg<int?>(arguments, "page_size") ?? 25;

        if (string.IsNullOrWhiteSpace(boardId))
            return Error("Board ID is required.", "BOARD_NOT_FOUND");

        using var scope = ScopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IKanbanService>();
        var userContext = ResolveUserContext(scope.ServiceProvider);

        var board = await svc.GetBoardAsync(boardId, cancellationToken);
        if (board == null)
            return Error($"Board '{boardId}' not found.", "BOARD_NOT_FOUND");

        var tasks = await svc.ListTasksAsync(boardId, page: page, pageSize: pageSize, cancellationToken: cancellationToken);

        var columns = new Dictionary<string, object>();
        foreach (TaskStatus status in Enum.GetValues<TaskStatus>())
        {
            var statusTasks = tasks.Items.Where(t => t.Status == status).ToList();
            var allStatusCount = board.Tasks.Count(t => t.Status == status);
            columns[status.ToString()] = new
            {
                count = allStatusCount,
                tasks = includeTaskSummaries
                    ? statusTasks.Select(t => new
                    {
                        taskId = t.Id, taskNumber = t.TaskNumber, title = t.Title,
                        severity = t.Severity.ToString(), assigneeName = t.AssigneeName,
                        dueDate = t.DueDate,
                        isOverdue = t.DueDate < DateTime.UtcNow && t.Status != TaskStatus.Done && t.Status != TaskStatus.Blocked,
                        isAssignedToCurrentUser = userContext.IsAuthenticated && string.Equals(t.AssigneeId, userContext.UserId, StringComparison.OrdinalIgnoreCase)
                    }).ToArray()
                    : Array.Empty<object>()
            };
        }

        var overdueTasks = board.Tasks.Count(t => t.DueDate < DateTime.UtcNow && t.Status != TaskStatus.Done && t.Status != TaskStatus.Blocked);
        var completionPct = board.Tasks.Count > 0
            ? Math.Round(100.0 * board.Tasks.Count(t => t.Status == TaskStatus.Done) / board.Tasks.Count, 2) : 0;
        var severityBreakdown = board.Tasks.GroupBy(t => t.Severity).ToDictionary(g => g.Key.ToString(), g => g.Count());

        return Success(new
        {
            boardId = board.Id, name = board.Name, subscriptionId = board.SubscriptionId,
            owner = board.Owner, isArchived = board.IsArchived,
            createdAt = board.CreatedAt, updatedAt = board.UpdatedAt,
            totalTasks = board.Tasks.Count, overdueTasks, columns,
            severityBreakdown, completionPercentage = completionPct
        }, page, pageSize, tasks.TotalCount);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Task Tools
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// MCP tool: kanban_get_task — Get full details of a single remediation task.
/// </summary>
public class KanbanGetTaskTool : KanbanToolBase
{
    public KanbanGetTaskTool(IServiceScopeFactory scopeFactory, ILogger<KanbanGetTaskTool> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "kanban_get_task";
    public override string Description => "Get the full details of a single remediation task.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["task_id"] = new() { Name = "task_id", Description = "Task ID or task number (e.g., 'REM-001')", Type = "string", Required = true },
        ["board_id"] = new() { Name = "board_id", Description = "Board ID. Required if using task number.", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var taskId = GetArg<string>(arguments, "task_id");
        if (string.IsNullOrWhiteSpace(taskId))
            return Error("Task ID is required.", "TASK_NOT_FOUND");

        using var scope = ScopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IKanbanService>();
        var userContext = ResolveUserContext(scope.ServiceProvider);

        var task = await svc.GetTaskAsync(taskId, cancellationToken);
        if (task == null)
            return Error($"Task '{taskId}' not found.", "TASK_NOT_FOUND");

        return Success(new
        {
            taskId = task.Id, taskNumber = task.TaskNumber, boardId = task.BoardId,
            title = task.Title, description = task.Description,
            controlId = task.ControlId, controlFamily = task.ControlFamily,
            severity = task.Severity.ToString(), status = task.Status.ToString(),
            assigneeId = task.AssigneeId, assigneeName = task.AssigneeName,
            isAssignedToCurrentUser = userContext.IsAuthenticated && string.Equals(task.AssigneeId, userContext.UserId, StringComparison.OrdinalIgnoreCase),
            dueDate = task.DueDate,
            isOverdue = task.DueDate < DateTime.UtcNow && task.Status != TaskStatus.Done && task.Status != TaskStatus.Blocked,
            createdAt = task.CreatedAt, updatedAt = task.UpdatedAt, createdBy = task.CreatedBy,
            affectedResources = task.AffectedResources,
            remediationScript = task.RemediationScript, validationCriteria = task.ValidationCriteria,
            findingId = task.FindingId,
            commentCount = task.Comments?.Count ?? 0, historyCount = task.History?.Count ?? 0
        });
    }
}

/// <summary>
/// MCP tool: kanban_create_task — Create a remediation task manually.
/// </summary>
public class KanbanCreateTaskTool : KanbanToolBase
{
    public KanbanCreateTaskTool(IServiceScopeFactory scopeFactory, ILogger<KanbanCreateTaskTool> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "kanban_create_task";
    public override string Description => "Create a new remediation task on a Kanban board.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["board_id"] = new() { Name = "board_id", Description = "Board to add the task to", Type = "string", Required = true },
        ["title"] = new() { Name = "title", Description = "Task title", Type = "string", Required = true },
        ["control_id"] = new() { Name = "control_id", Description = "NIST control ID (e.g., 'AC-2.1')", Type = "string", Required = true },
        ["description"] = new() { Name = "description", Description = "Detailed finding description", Type = "string" },
        ["severity"] = new() { Name = "severity", Description = "Severity: Critical, High, Medium, Low, Informational", Type = "string" },
        ["assignee_id"] = new() { Name = "assignee_id", Description = "User to assign the task to", Type = "string" },
        ["assignee_name"] = new() { Name = "assignee_name", Description = "Display name for the assignee", Type = "string" },
        ["due_date"] = new() { Name = "due_date", Description = "Due date (ISO 8601)", Type = "string" },
        ["affected_resources"] = new() { Name = "affected_resources", Description = "Azure resource IDs affected", Type = "array" },
        ["remediation_script"] = new() { Name = "remediation_script", Description = "PowerShell/CLI remediation script", Type = "string" },
        ["validation_criteria"] = new() { Name = "validation_criteria", Description = "How to verify the fix", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var boardId = GetArg<string>(arguments, "board_id");
        var title = GetArg<string>(arguments, "title");
        var controlId = GetArg<string>(arguments, "control_id");
        var description = GetArg<string>(arguments, "description");
        var severityStr = GetArg<string>(arguments, "severity");
        var assigneeId = GetArg<string>(arguments, "assignee_id");
        var dueDateStr = GetArg<string>(arguments, "due_date");
        var remediationScript = GetArg<string>(arguments, "remediation_script");
        var validationCriteria = GetArg<string>(arguments, "validation_criteria");

        if (string.IsNullOrWhiteSpace(boardId)) return Error("Board ID is required.", "BOARD_NOT_FOUND");
        if (string.IsNullOrWhiteSpace(title)) return Error("Task title is required.", "COMMENT_REQUIRES_TEXT");
        if (string.IsNullOrWhiteSpace(controlId)) return Error("Control ID is required.", "COMMENT_REQUIRES_TEXT");

        FindingSeverity? severity = null;
        if (!string.IsNullOrWhiteSpace(severityStr) && Enum.TryParse<FindingSeverity>(severityStr, true, out var s))
            severity = s;

        DateTime? dueDate = null;
        if (!string.IsNullOrWhiteSpace(dueDateStr) && DateTime.TryParse(dueDateStr, out var d))
            dueDate = d;

        var affectedResources = ParseStringArray(arguments, "affected_resources");

        try
        {
            using var scope = ScopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IKanbanService>();
            var userContext = ResolveUserContext(scope.ServiceProvider);

            var task = await svc.CreateTaskAsync(
                boardId, title, controlId, userContext.UserId, description, severity, assigneeId, dueDate,
                affectedResources.Count > 0 ? affectedResources : null,
                remediationScript, validationCriteria, cancellationToken);

            return Success(new
            {
                taskId = task.Id, taskNumber = task.TaskNumber, boardId = task.BoardId,
                title = task.Title, controlId = task.ControlId, controlFamily = task.ControlFamily,
                severity = task.Severity.ToString(), status = task.Status.ToString(),
                assigneeId = task.AssigneeId, assigneeName = task.AssigneeName,
                dueDate = task.DueDate, createdAt = task.CreatedAt, createdBy = task.CreatedBy
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        { return Error(ex.Message, "BOARD_NOT_FOUND"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("archived"))
        { return Error(ex.Message, "BOARD_ARCHIVED"); }
    }
}

/// <summary>
/// MCP tool: kanban_assign_task — Assign or unassign a task.
/// </summary>
public class KanbanAssignTaskTool : KanbanToolBase
{
    public KanbanAssignTaskTool(IServiceScopeFactory scopeFactory, ILogger<KanbanAssignTaskTool> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "kanban_assign_task";
    public override string Description => "Assign or unassign a remediation task to a user.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["task_id"] = new() { Name = "task_id", Description = "Task ID or task number", Type = "string", Required = true },
        ["board_id"] = new() { Name = "board_id", Description = "Board ID. Required if using task number.", Type = "string" },
        ["assignee_id"] = new() { Name = "assignee_id", Description = "User ID to assign. Omit or null to unassign.", Type = "string" },
        ["assignee_name"] = new() { Name = "assignee_name", Description = "Display name for the assignee", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var taskId = GetArg<string>(arguments, "task_id");
        var assigneeId = GetArg<string>(arguments, "assignee_id");
        var assigneeName = GetArg<string>(arguments, "assignee_name");

        if (string.IsNullOrWhiteSpace(taskId))
            return Error("Task ID is required.", "TASK_NOT_FOUND");

        try
        {
            using var scope = ScopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IKanbanService>();
            var userContext = ResolveUserContext(scope.ServiceProvider);

            var task = await svc.AssignTaskAsync(
                taskId, userContext.UserId, userContext.DisplayName, userContext.Role, assigneeId, assigneeName, cancellationToken);

            return Success(new
            {
                taskId = task.Id, taskNumber = task.TaskNumber,
                previousAssignee = (string?)null,
                newAssignee = new { assigneeId = task.AssigneeId, assigneeName = task.AssigneeName },
                assignedAt = DateTime.UtcNow, assignedBy = userContext.UserId
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        { return Error(ex.Message, "TASK_NOT_FOUND"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Done"))
        { return Error(ex.Message, "TERMINAL_STATE"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ermission"))
        { return Error(ex.Message, "KANBAN_PERMISSION_DENIED"); }
        catch (DbUpdateConcurrencyException)
        { return Error("Task was modified by another user. Please retry.", "CONCURRENCY_CONFLICT"); }
    }
}

/// <summary>
/// MCP tool: kanban_move_task — Transition a task between Kanban columns.
/// </summary>
public class KanbanMoveTaskTool : KanbanToolBase
{
    public KanbanMoveTaskTool(IServiceScopeFactory scopeFactory, ILogger<KanbanMoveTaskTool> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "kanban_move_task";
    public override string Description => "Move a remediation task to a new Kanban column (status transition).";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["task_id"] = new() { Name = "task_id", Description = "Task ID or task number", Type = "string", Required = true },
        ["board_id"] = new() { Name = "board_id", Description = "Board ID. Required if using task number.", Type = "string" },
        ["target_status"] = new() { Name = "target_status", Description = "Target column: Backlog, ToDo, InProgress, InReview, Blocked, Done", Type = "string", Required = true },
        ["comment"] = new() { Name = "comment", Description = "Reason for the transition. Required for Blocked.", Type = "string" },
        ["skip_validation"] = new() { Name = "skip_validation", Description = "Skip validation on Done. CO only.", Type = "boolean" }
    };

    public override async Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var taskId = GetArg<string>(arguments, "task_id");
        var targetStatusStr = GetArg<string>(arguments, "target_status");
        var comment = GetArg<string>(arguments, "comment");
        var skipValidation = GetArg<bool?>(arguments, "skip_validation") ?? false;

        if (string.IsNullOrWhiteSpace(taskId)) return Error("Task ID is required.", "TASK_NOT_FOUND");
        if (string.IsNullOrWhiteSpace(targetStatusStr)) return Error("Target status is required.", "INVALID_TRANSITION");
        if (!Enum.TryParse<TaskStatus>(targetStatusStr, true, out var targetStatus))
            return Error($"Invalid status '{targetStatusStr}'. Valid: Backlog, ToDo, InProgress, InReview, Blocked, Done.", "INVALID_TRANSITION");

        try
        {
            using var scope = ScopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IKanbanService>();
            var userContext = ResolveUserContext(scope.ServiceProvider);

            var task = await svc.MoveTaskAsync(
                taskId, targetStatus, userContext.UserId, userContext.DisplayName, userContext.Role, comment, skipValidation, cancellationToken);

            return Success(new
            {
                taskId = task.Id, taskNumber = task.TaskNumber,
                previousStatus = (string?)null, newStatus = task.Status.ToString(),
                transitionedAt = DateTime.UtcNow, transitionedBy = userContext.UserId
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        { return Error(ex.Message, "TASK_NOT_FOUND"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("erminal") || ex.Message.Contains("Done state"))
        { return Error(ex.Message, "TERMINAL_STATE"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ransition"))
        { return Error(ex.Message, "INVALID_TRANSITION"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("locker"))
        { return Error(ex.Message, "BLOCKER_COMMENT_REQUIRED"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("esolution"))
        { return Error(ex.Message, "RESOLUTION_COMMENT_REQUIRED"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("alidation"))
        { return Error(ex.Message, "VALIDATION_REQUIRED"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ermission"))
        { return Error(ex.Message, "KANBAN_PERMISSION_DENIED"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("rchived"))
        { return Error(ex.Message, "BOARD_ARCHIVED"); }
        catch (DbUpdateConcurrencyException)
        { return Error("Task was modified by another user. Please retry.", "CONCURRENCY_CONFLICT"); }
    }
}

/// <summary>
/// MCP tool: kanban_task_list — List and filter tasks on a board.
/// </summary>
public class KanbanTaskListTool : KanbanToolBase
{
    public KanbanTaskListTool(IServiceScopeFactory scopeFactory, ILogger<KanbanTaskListTool> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "kanban_task_list";
    public override string Description => "List and filter remediation tasks on a Kanban board.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["board_id"] = new() { Name = "board_id", Description = "Board ID", Type = "string", Required = true },
        ["status"] = new() { Name = "status", Description = "Filter by status", Type = "string" },
        ["severity"] = new() { Name = "severity", Description = "Filter by severity", Type = "string" },
        ["assignee_id"] = new() { Name = "assignee_id", Description = "Filter by assignee", Type = "string" },
        ["control_family"] = new() { Name = "control_family", Description = "Filter by control family (e.g., 'AC')", Type = "string" },
        ["is_overdue"] = new() { Name = "is_overdue", Description = "Filter for overdue tasks only", Type = "boolean" },
        ["sort_by"] = new() { Name = "sort_by", Description = "Sort: severity, dueDate, createdAt, status, controlId", Type = "string" },
        ["sort_order"] = new() { Name = "sort_order", Description = "Sort direction: asc, desc", Type = "string" },
        ["page"] = new() { Name = "page", Description = "Page number", Type = "integer" },
        ["page_size"] = new() { Name = "page_size", Description = "Items per page. Max 100.", Type = "integer" }
    };

    public override async Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var boardId = GetArg<string>(arguments, "board_id");
        var statusStr = GetArg<string>(arguments, "status");
        var severityStr = GetArg<string>(arguments, "severity");
        var assigneeId = GetArg<string>(arguments, "assignee_id");
        var controlFamily = GetArg<string>(arguments, "control_family");
        var isOverdue = GetArg<bool?>(arguments, "is_overdue");
        var sortBy = GetArg<string>(arguments, "sort_by");
        var sortOrder = GetArg<string>(arguments, "sort_order");
        var page = GetArg<int?>(arguments, "page") ?? 1;
        var pageSize = GetArg<int?>(arguments, "page_size") ?? 25;

        if (string.IsNullOrWhiteSpace(boardId)) return Error("Board ID is required.", "BOARD_NOT_FOUND");

        TaskStatus? status = null;
        if (!string.IsNullOrWhiteSpace(statusStr) && Enum.TryParse<TaskStatus>(statusStr, true, out var ps)) status = ps;
        FindingSeverity? severity = null;
        if (!string.IsNullOrWhiteSpace(severityStr) && Enum.TryParse<FindingSeverity>(severityStr, true, out var pss)) severity = pss;

        try
        {
            using var scope = ScopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IKanbanService>();
            var userContext = ResolveUserContext(scope.ServiceProvider);

            var result = await svc.ListTasksAsync(
                boardId, status, severity, assigneeId, controlFamily, isOverdue,
                sortBy, sortOrder, page, pageSize, cancellationToken);
            var board = await svc.GetBoardAsync(boardId, cancellationToken);

            return Success(new
            {
                boardId, boardName = board?.Name,
                tasks = result.Items.Select(t => new
                {
                    taskId = t.Id, taskNumber = t.TaskNumber, title = t.Title,
                    controlId = t.ControlId, controlFamily = t.ControlFamily,
                    severity = t.Severity.ToString(), status = t.Status.ToString(),
                    assigneeName = t.AssigneeName, dueDate = t.DueDate,
                    isOverdue = t.DueDate < DateTime.UtcNow && t.Status != TaskStatus.Done && t.Status != TaskStatus.Blocked,
                    isAssignedToCurrentUser = userContext.IsAuthenticated && string.Equals(t.AssigneeId, userContext.UserId, StringComparison.OrdinalIgnoreCase),
                    commentCount = t.Comments?.Count ?? 0, createdAt = t.CreatedAt
                }),
                appliedFilters = new { status = statusStr, severity = severityStr, assigneeId, controlFamily, isOverdue }
            }, page, pageSize, result.TotalCount);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        { return Error(ex.Message, "BOARD_NOT_FOUND"); }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// History & Validation Tools
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// MCP tool: kanban_task_history — View full audit trail of a task.
/// </summary>
public class KanbanTaskHistoryTool : KanbanToolBase
{
    public KanbanTaskHistoryTool(IServiceScopeFactory scopeFactory, ILogger<KanbanTaskHistoryTool> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "kanban_task_history";
    public override string Description => "View the full audit trail and history of a remediation task.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["task_id"] = new() { Name = "task_id", Description = "Task ID or task number", Type = "string", Required = true },
        ["board_id"] = new() { Name = "board_id", Description = "Board ID", Type = "string" },
        ["event_type"] = new() { Name = "event_type", Description = "Filter by event type", Type = "string" },
        ["page"] = new() { Name = "page", Description = "Page number", Type = "integer" },
        ["page_size"] = new() { Name = "page_size", Description = "Items per page", Type = "integer" }
    };

    public override async Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var taskId = GetArg<string>(arguments, "task_id");
        var eventTypeStr = GetArg<string>(arguments, "event_type");
        var page = GetArg<int?>(arguments, "page") ?? 1;
        var pageSize = GetArg<int?>(arguments, "page_size") ?? 25;

        if (string.IsNullOrWhiteSpace(taskId)) return Error("Task ID is required.", "TASK_NOT_FOUND");

        HistoryEventType? eventType = null;
        if (!string.IsNullOrWhiteSpace(eventTypeStr) && Enum.TryParse<HistoryEventType>(eventTypeStr, true, out var pe)) eventType = pe;

        using var scope = ScopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IKanbanService>();

        var task = await svc.GetTaskAsync(taskId, cancellationToken);
        if (task == null) return Error($"Task '{taskId}' not found.", "TASK_NOT_FOUND");

        var result = await svc.GetTaskHistoryAsync(taskId, eventType, page, pageSize, cancellationToken);

        return Success(new
        {
            taskId = task.Id, taskNumber = task.TaskNumber,
            entries = result.Items.Select(e => new
            {
                entryId = e.Id, eventType = e.EventType.ToString(),
                oldValue = e.OldValue, newValue = e.NewValue,
                actingUserId = e.ActingUserId, actingUserName = e.ActingUserName,
                timestamp = e.Timestamp, details = e.Details
            })
        }, page, pageSize, result.TotalCount);
    }
}

/// <summary>
/// MCP tool: kanban_task_validate — Trigger a validation scan for a task.
/// </summary>
public class KanbanValidateTaskTool : KanbanToolBase
{
    public KanbanValidateTaskTool(IServiceScopeFactory scopeFactory, ILogger<KanbanValidateTaskTool> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "kanban_task_validate";
    public override string Description => "Run a targeted validation scan to verify a remediation fix was applied.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["task_id"] = new() { Name = "task_id", Description = "Task ID or task number", Type = "string", Required = true },
        ["board_id"] = new() { Name = "board_id", Description = "Board ID", Type = "string" },
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Azure subscription ID", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var taskId = GetArg<string>(arguments, "task_id");
        var subscriptionId = GetArg<string>(arguments, "subscription_id");

        if (string.IsNullOrWhiteSpace(taskId)) return Error("Task ID is required.", "TASK_NOT_FOUND");

        try
        {
            using var scope = ScopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IKanbanService>();

            var result = await svc.ValidateTaskAsync(taskId, subscriptionId, cancellationToken);
            var task = await svc.GetTaskAsync(taskId, cancellationToken);

            return Success(new
            {
                taskId = task?.Id, taskNumber = task?.TaskNumber, controlId = task?.ControlId,
                validationResult = result.AllPassed ? "pass" : "fail",
                details = result.Summary,
                resourcesChecked = result.ResourceResults.Count,
                resourcesPassing = result.ResourceResults.Count(r => r.Passed),
                validatedAt = DateTime.UtcNow, canClose = result.CanClose
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        { return Error(ex.Message, "TASK_NOT_FOUND"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Done"))
        { return Error(ex.Message, "TERMINAL_STATE"); }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Comment Tools
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// MCP tool: kanban_add_comment — Add a comment to a task.
/// </summary>
public class KanbanAddCommentTool : KanbanToolBase
{
    public KanbanAddCommentTool(IServiceScopeFactory scopeFactory, ILogger<KanbanAddCommentTool> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "kanban_add_comment";
    public override string Description => "Add a comment to a remediation task. Supports Markdown and @mentions.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["task_id"] = new() { Name = "task_id", Description = "Task ID or task number", Type = "string", Required = true },
        ["board_id"] = new() { Name = "board_id", Description = "Board ID", Type = "string" },
        ["content"] = new() { Name = "content", Description = "Comment text (Markdown)", Type = "string", Required = true },
        ["parent_comment_id"] = new() { Name = "parent_comment_id", Description = "Parent comment ID for threading", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var taskId = GetArg<string>(arguments, "task_id");
        var content = GetArg<string>(arguments, "content");
        var parentCommentId = GetArg<string>(arguments, "parent_comment_id");

        if (string.IsNullOrWhiteSpace(taskId)) return Error("Task ID is required.", "TASK_NOT_FOUND");
        if (string.IsNullOrWhiteSpace(content)) return Error("Comment content is required.", "COMMENT_REQUIRES_TEXT");

        try
        {
            using var scope = ScopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IKanbanService>();
            var userContext = ResolveUserContext(scope.ServiceProvider);

            var comment = await svc.AddCommentAsync(
                taskId, userContext.UserId, userContext.DisplayName, content, userContext.Role, parentCommentId, cancellationToken);
            var task = await svc.GetTaskAsync(taskId, cancellationToken);

            return Success(new
            {
                commentId = comment.Id, taskId = comment.TaskId, taskNumber = task?.TaskNumber,
                authorId = comment.AuthorId, authorName = comment.AuthorName,
                content = comment.Content, createdAt = comment.CreatedAt,
                isSystemComment = comment.IsSystemComment, mentions = comment.Mentions,
                parentCommentId = comment.ParentCommentId
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        { return Error(ex.Message, "TASK_NOT_FOUND"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ermission"))
        { return Error(ex.Message, "KANBAN_PERMISSION_DENIED"); }
    }
}

/// <summary>
/// MCP tool: kanban_task_comments — List comments on a task.
/// </summary>
public class KanbanTaskCommentsTool : KanbanToolBase
{
    public KanbanTaskCommentsTool(IServiceScopeFactory scopeFactory, ILogger<KanbanTaskCommentsTool> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "kanban_task_comments";
    public override string Description => "List all comments on a remediation task.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["task_id"] = new() { Name = "task_id", Description = "Task ID or task number", Type = "string", Required = true },
        ["board_id"] = new() { Name = "board_id", Description = "Board ID", Type = "string" },
        ["include_deleted"] = new() { Name = "include_deleted", Description = "Include soft-deleted comments", Type = "boolean" },
        ["page"] = new() { Name = "page", Description = "Page number", Type = "integer" },
        ["page_size"] = new() { Name = "page_size", Description = "Items per page", Type = "integer" }
    };

    public override async Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var taskId = GetArg<string>(arguments, "task_id");
        var includeDeleted = GetArg<bool?>(arguments, "include_deleted") ?? false;
        var page = GetArg<int?>(arguments, "page") ?? 1;
        var pageSize = GetArg<int?>(arguments, "page_size") ?? 25;

        if (string.IsNullOrWhiteSpace(taskId)) return Error("Task ID is required.", "TASK_NOT_FOUND");

        using var scope = ScopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IKanbanService>();

        var task = await svc.GetTaskAsync(taskId, cancellationToken);
        if (task == null) return Error($"Task '{taskId}' not found.", "TASK_NOT_FOUND");

        var result = await svc.ListCommentsAsync(taskId, includeDeleted, page, pageSize, cancellationToken);

        return Success(new
        {
            taskId = task.Id, taskNumber = task.TaskNumber,
            comments = result.Items.Select(c => new
            {
                commentId = c.Id, authorId = c.AuthorId, authorName = c.AuthorName,
                content = c.Content, createdAt = c.CreatedAt, isEdited = c.IsEdited,
                isSystemComment = c.IsSystemComment, isDeleted = c.IsDeleted,
                parentCommentId = c.ParentCommentId, mentions = c.Mentions
            })
        }, page, pageSize, result.TotalCount);
    }
}

/// <summary>
/// MCP tool: kanban_edit_comment — Edit a comment within the 24h window.
/// </summary>
public class KanbanEditCommentTool : KanbanToolBase
{
    public KanbanEditCommentTool(IServiceScopeFactory scopeFactory, ILogger<KanbanEditCommentTool> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "kanban_edit_comment";
    public override string Description => "Edit an existing comment on a remediation task. Must be within 24 hours of creation.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["comment_id"] = new() { Name = "comment_id", Description = "Comment ID", Type = "string", Required = true },
        ["task_id"] = new() { Name = "task_id", Description = "Task ID or task number", Type = "string", Required = true },
        ["board_id"] = new() { Name = "board_id", Description = "Board ID", Type = "string" },
        ["content"] = new() { Name = "content", Description = "Updated comment text", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var commentId = GetArg<string>(arguments, "comment_id");
        var content = GetArg<string>(arguments, "content");

        if (string.IsNullOrWhiteSpace(commentId)) return Error("Comment ID is required.", "COMMENT_NOT_FOUND");
        if (string.IsNullOrWhiteSpace(content)) return Error("Comment content is required.", "COMMENT_REQUIRES_TEXT");

        try
        {
            using var scope = ScopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IKanbanService>();
            var userContext = ResolveUserContext(scope.ServiceProvider);

            var comment = await svc.EditCommentAsync(commentId, userContext.UserId, content, cancellationToken);
            var task = await svc.GetTaskAsync(comment.TaskId, cancellationToken);

            return Success(new
            {
                commentId = comment.Id, taskId = comment.TaskId, taskNumber = task?.TaskNumber,
                content = comment.Content, editedAt = comment.EditedAt, isEdited = comment.IsEdited
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        { return Error(ex.Message, "COMMENT_NOT_FOUND"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("window") || ex.Message.Contains("24"))
        { return Error(ex.Message, "COMMENT_EDIT_WINDOW_EXPIRED"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Done"))
        { return Error(ex.Message, "TERMINAL_STATE"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("own"))
        { return Error(ex.Message, "KANBAN_PERMISSION_DENIED"); }
    }
}

/// <summary>
/// MCP tool: kanban_delete_comment — Delete a comment (soft delete).
/// </summary>
public class KanbanDeleteCommentTool : KanbanToolBase
{
    public KanbanDeleteCommentTool(IServiceScopeFactory scopeFactory, ILogger<KanbanDeleteCommentTool> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "kanban_delete_comment";
    public override string Description => "Delete a comment on a remediation task. Non-officers must delete within 1 hour.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["comment_id"] = new() { Name = "comment_id", Description = "Comment ID", Type = "string", Required = true },
        ["task_id"] = new() { Name = "task_id", Description = "Task ID or task number", Type = "string", Required = true },
        ["board_id"] = new() { Name = "board_id", Description = "Board ID", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var commentId = GetArg<string>(arguments, "comment_id");

        if (string.IsNullOrWhiteSpace(commentId)) return Error("Comment ID is required.", "COMMENT_NOT_FOUND");

        try
        {
            using var scope = ScopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IKanbanService>();
            var userContext = ResolveUserContext(scope.ServiceProvider);

            var comment = await svc.DeleteCommentAsync(commentId, userContext.UserId, userContext.Role, cancellationToken);
            var task = await svc.GetTaskAsync(comment.TaskId, cancellationToken);

            return Success(new
            {
                commentId = comment.Id, taskId = comment.TaskId, taskNumber = task?.TaskNumber,
                deletedAt = DateTime.UtcNow, deletedBy = userContext.UserId
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        { return Error(ex.Message, "COMMENT_NOT_FOUND"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("window") || ex.Message.Contains("1 hour") || ex.Message.Contains("1h"))
        { return Error(ex.Message, "COMMENT_DELETE_WINDOW_EXPIRED"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Done"))
        { return Error(ex.Message, "TERMINAL_STATE"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("own") || ex.Message.Contains("ermission"))
        { return Error(ex.Message, "KANBAN_PERMISSION_DENIED"); }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Remediation & Evidence Tools
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// MCP tool: kanban_remediate_task — Execute remediation script for a task.
/// </summary>
public class KanbanRemediateTaskTool : KanbanToolBase
{
    public KanbanRemediateTaskTool(IServiceScopeFactory scopeFactory, ILogger<KanbanRemediateTaskTool> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "kanban_remediate_task";
    public override string Description => "Execute the remediation script for a task and move it to InReview on success.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["task_id"] = new() { Name = "task_id", Description = "Task ID or task number", Type = "string", Required = true },
        ["board_id"] = new() { Name = "board_id", Description = "Board ID", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var taskId = GetArg<string>(arguments, "task_id");
        if (string.IsNullOrWhiteSpace(taskId)) return Error("Task ID is required.", "TASK_NOT_FOUND");

        try
        {
            using var scope = ScopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IKanbanService>();
            var userContext = ResolveUserContext(scope.ServiceProvider);

            var result = await svc.ExecuteTaskRemediationAsync(taskId, userContext.UserId, userContext.DisplayName, cancellationToken);

            return Success(new
            {
                taskId = result.Task.Id, taskNumber = result.Task.TaskNumber,
                success = result.Success, newStatus = result.Task.Status.ToString(),
                details = result.Details, executedAt = DateTime.UtcNow
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        { return Error(ex.Message, "TASK_NOT_FOUND"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("script"))
        { return Error(ex.Message, "TASK_NOT_FOUND", "Add a remediationScript to the task first."); }
    }
}

/// <summary>
/// MCP tool: kanban_collect_evidence — Collect evidence scoped to a task.
/// </summary>
public class KanbanCollectEvidenceTool : KanbanToolBase
{
    public KanbanCollectEvidenceTool(IServiceScopeFactory scopeFactory, ILogger<KanbanCollectEvidenceTool> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "kanban_collect_evidence";
    public override string Description => "Collect compliance evidence for a remediation task's control ID and affected resources.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["task_id"] = new() { Name = "task_id", Description = "Task ID or task number", Type = "string", Required = true },
        ["board_id"] = new() { Name = "board_id", Description = "Board ID", Type = "string" },
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Azure subscription ID", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var taskId = GetArg<string>(arguments, "task_id");
        var subscriptionId = GetArg<string>(arguments, "subscription_id");

        if (string.IsNullOrWhiteSpace(taskId)) return Error("Task ID is required.", "TASK_NOT_FOUND");

        try
        {
            using var scope = ScopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IKanbanService>();
            var userContext = ResolveUserContext(scope.ServiceProvider);

            var result = await svc.CollectTaskEvidenceAsync(
                taskId, userContext.UserId, userContext.DisplayName, subscriptionId, cancellationToken);

            return Success(new
            {
                taskId = result.Task.Id, taskNumber = result.Task.TaskNumber,
                controlId = result.Task.ControlId,
                evidenceItems = result.ItemsCollected, summary = result.Summary,
                collectedAt = DateTime.UtcNow
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        { return Error(ex.Message, "TASK_NOT_FOUND"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("subscription"))
        { return Error(ex.Message, "SUBSCRIPTION_NOT_CONFIGURED"); }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Bulk & Export Tools
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// MCP tool: kanban_bulk_update — Bulk assign/move/setDueDate on multiple tasks.
/// </summary>
public class KanbanBulkUpdateTool : KanbanToolBase
{
    public KanbanBulkUpdateTool(IServiceScopeFactory scopeFactory, ILogger<KanbanBulkUpdateTool> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "kanban_bulk_update";
    public override string Description => "Perform bulk operations on multiple remediation tasks (assign, move, change due date).";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["board_id"] = new() { Name = "board_id", Description = "Board ID", Type = "string", Required = true },
        ["task_ids"] = new() { Name = "task_ids", Description = "Task IDs or task numbers", Type = "array", Required = true },
        ["operation"] = new() { Name = "operation", Description = "Operation: assign, move, setDueDate", Type = "string", Required = true },
        ["assignee_id"] = new() { Name = "assignee_id", Description = "Assignee for 'assign'", Type = "string" },
        ["assignee_name"] = new() { Name = "assignee_name", Description = "Display name for 'assign'", Type = "string" },
        ["target_status"] = new() { Name = "target_status", Description = "Target status for 'move'", Type = "string" },
        ["due_date"] = new() { Name = "due_date", Description = "Due date for 'setDueDate' (ISO 8601)", Type = "string" },
        ["comment"] = new() { Name = "comment", Description = "Comment for transitions that require one", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var boardId = GetArg<string>(arguments, "board_id");
        var operation = GetArg<string>(arguments, "operation");
        var assigneeId = GetArg<string>(arguments, "assignee_id");
        var assigneeName = GetArg<string>(arguments, "assignee_name");
        var targetStatusStr = GetArg<string>(arguments, "target_status");
        var dueDateStr = GetArg<string>(arguments, "due_date");
        var comment = GetArg<string>(arguments, "comment");

        if (string.IsNullOrWhiteSpace(boardId)) return Error("Board ID is required.", "BOARD_NOT_FOUND");
        if (string.IsNullOrWhiteSpace(operation)) return Error("Operation is required.", "COMMENT_REQUIRES_TEXT");

        var taskIds = ParseStringArray(arguments, "task_ids");
        if (taskIds.Count == 0) return Error("At least one task ID is required.", "COMMENT_REQUIRES_TEXT");

        try
        {
            using var scope = ScopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IKanbanService>();
            var userContext = ResolveUserContext(scope.ServiceProvider);

            BulkOperationResult result;
            switch (operation.ToLowerInvariant())
            {
                case "assign":
                    result = await svc.BulkAssignAsync(
                        boardId, taskIds, assigneeId ?? "", assigneeName ?? "",
                        userContext.UserId, userContext.DisplayName, userContext.Role, cancellationToken);
                    break;
                case "move":
                    if (string.IsNullOrWhiteSpace(targetStatusStr) || !Enum.TryParse<TaskStatus>(targetStatusStr, true, out var ts))
                        return Error("Valid target status is required for 'move' operation.", "INVALID_TRANSITION");
                    result = await svc.BulkMoveAsync(
                        boardId, taskIds, ts, userContext.UserId, userContext.DisplayName, userContext.Role, comment, cancellationToken);
                    break;
                case "setduedate":
                    if (string.IsNullOrWhiteSpace(dueDateStr) || !DateTime.TryParse(dueDateStr, out var dd))
                        return Error("Valid due date is required for 'setDueDate' operation.", "COMMENT_REQUIRES_TEXT");
                    result = await svc.BulkSetDueDateAsync(boardId, taskIds, dd, userContext.UserId, userContext.DisplayName, cancellationToken);
                    break;
                default:
                    return Error($"Unknown operation '{operation}'. Use 'assign', 'move', or 'setDueDate'.", "COMMENT_REQUIRES_TEXT");
            }

            var status = result.Failed > 0 ? (result.Succeeded > 0 ? "partial" : "error") : "success";
            return JsonSerializer.Serialize(new
            {
                status,
                data = new
                {
                    boardId, operation, targetStatus = targetStatusStr,
                    totalRequested = taskIds.Count, succeeded = result.Succeeded, failed = result.Failed,
                    results = result.Results.Select(r => new
                    {
                        taskId = r.TaskId, taskNumber = r.TaskNumber,
                        status = r.Success ? "success" : "error",
                        errorCode = r.Success ? null : "INVALID_TRANSITION", message = r.Error
                    })
                },
                metadata = new { toolName = Name, executionTimeMs = 0, timestamp = DateTime.UtcNow }
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        { return Error(ex.Message, "BOARD_NOT_FOUND"); }
    }
}

/// <summary>
/// MCP tool: kanban_export — Export board data as CSV or POA&M.
/// </summary>
public class KanbanExportTool : KanbanToolBase
{
    public KanbanExportTool(IServiceScopeFactory scopeFactory, ILogger<KanbanExportTool> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "kanban_export";
    public override string Description => "Export Kanban board data as CSV for reporting or POA&M integration.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["board_id"] = new() { Name = "board_id", Description = "Board ID", Type = "string", Required = true },
        ["format"] = new() { Name = "format", Description = "Export format: csv, poam", Type = "string" },
        ["statuses"] = new() { Name = "statuses", Description = "Filter by statuses (comma-separated)", Type = "string" },
        ["include_history"] = new() { Name = "include_history", Description = "Include full history in export", Type = "boolean" }
    };

    public override async Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var boardId = GetArg<string>(arguments, "board_id");
        var format = GetArg<string>(arguments, "format") ?? "csv";
        var includeHistory = GetArg<bool?>(arguments, "include_history") ?? false;

        if (string.IsNullOrWhiteSpace(boardId)) return Error("Board ID is required.", "BOARD_NOT_FOUND");

        try
        {
            using var scope = ScopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IKanbanService>();
            var userContext = ResolveUserContext(scope.ServiceProvider);

            string csvContent = includeHistory
                ? await svc.ExportBoardHistoryAsync(boardId, userContext.UserId, userContext.Role, cancellationToken)
                : await svc.ExportBoardCsvAsync(boardId, userContext.UserId, userContext.Role, cancellationToken);

            var board = await svc.GetBoardAsync(boardId, cancellationToken);

            return Success(new
            {
                boardId, boardName = board?.Name, format,
                exportedAt = DateTime.UtcNow, taskCount = board?.Tasks.Count ?? 0,
                csvContent, sizeBytes = System.Text.Encoding.UTF8.GetByteCount(csvContent)
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        { return Error(ex.Message, "BOARD_NOT_FOUND"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ermission"))
        { return Error(ex.Message, "KANBAN_PERMISSION_DENIED"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("large") || ex.Message.Contains("500"))
        { return Error(ex.Message, "EXPORT_TOO_LARGE"); }
    }
}

/// <summary>
/// MCP tool: kanban_archive_board — Archive a board (soft delete).
/// </summary>
public class KanbanArchiveBoardTool : KanbanToolBase
{
    public KanbanArchiveBoardTool(IServiceScopeFactory scopeFactory, ILogger<KanbanArchiveBoardTool> logger)
        : base(scopeFactory, logger) { }

    public override string Name => "kanban_archive_board";
    public override string Description => "Archive a Kanban board. Archived boards are read-only.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["board_id"] = new() { Name = "board_id", Description = "Board ID", Type = "string", Required = true },
        ["confirm"] = new() { Name = "confirm", Description = "Confirm archival. Must be true.", Type = "boolean", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var boardId = GetArg<string>(arguments, "board_id");
        var confirm = GetArg<bool?>(arguments, "confirm") ?? false;

        if (string.IsNullOrWhiteSpace(boardId)) return Error("Board ID is required.", "BOARD_NOT_FOUND");
        if (!confirm) return Error("Archival must be confirmed. Set confirm=true.", "BOARD_NOT_FOUND", "Pass confirm=true.");

        try
        {
            using var scope = ScopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IKanbanService>();
            var userContext = ResolveUserContext(scope.ServiceProvider);

            var board = await svc.ArchiveBoardAsync(boardId, userContext.UserId, userContext.DisplayName, cancellationToken);
            var tasksByStatus = board.Tasks.GroupBy(t => t.Status).ToDictionary(g => g.Key.ToString(), g => g.Count());

            return Success(new
            {
                boardId = board.Id, name = board.Name,
                archivedAt = DateTime.UtcNow, archivedBy = userContext.UserId,
                taskCount = board.Tasks.Count, tasksByStatus
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        { return Error(ex.Message, "BOARD_NOT_FOUND"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("remaining") || ex.Message.Contains("TASKS_REMAINING"))
        { return Error(ex.Message, "TASKS_REMAINING"); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ermission"))
        { return Error(ex.Message, "KANBAN_PERMISSION_DENIED"); }
    }
}
