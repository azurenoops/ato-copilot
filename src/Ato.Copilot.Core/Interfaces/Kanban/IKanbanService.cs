using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.Core.Models.Kanban;
using TaskStatus = Ato.Copilot.Core.Models.Kanban.TaskStatus;

namespace Ato.Copilot.Core.Interfaces.Kanban;

/// <summary>
/// Service interface for all Kanban remediation board operations.
/// Consolidates board, task, comment, history, view, and bulk operations.
/// Registered as Scoped — one instance per request for DB context alignment.
/// </summary>
public interface IKanbanService
{
    // ─── Board Operations ────────────────────────────────────────────────────

    /// <summary>Creates a new empty remediation board.</summary>
    Task<RemediationBoard> CreateBoardAsync(
        string name,
        string subscriptionId,
        string owner,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a board from a completed assessment, with one task per finding.</summary>
    Task<RemediationBoard> CreateBoardFromAssessmentAsync(
        string assessmentId,
        string name,
        string subscriptionId,
        string owner,
        CancellationToken cancellationToken = default);

    /// <summary>Updates an existing board with findings from a new assessment run.</summary>
    Task<BoardUpdateResult> UpdateBoardFromAssessmentAsync(
        string boardId,
        string assessmentId,
        string actingUserId,
        string actingUserName,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a board with task count summaries.</summary>
    Task<RemediationBoard?> GetBoardAsync(
        string boardId,
        CancellationToken cancellationToken = default);

    /// <summary>Lists boards for a subscription, with optional archive filter.</summary>
    Task<PagedResult<RemediationBoard>> ListBoardsAsync(
        string subscriptionId,
        bool? isArchived = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default);

    /// <summary>Archives a board (all tasks must be Done).</summary>
    Task<RemediationBoard> ArchiveBoardAsync(
        string boardId,
        string actingUserId,
        string actingUserName,
        CancellationToken cancellationToken = default);

    /// <summary>Exports board tasks as CSV.</summary>
    Task<string> ExportBoardCsvAsync(
        string boardId,
        string actingUserId,
        string actingUserRole,
        CancellationToken cancellationToken = default);

    /// <summary>Exports board history as CSV.</summary>
    Task<string> ExportBoardHistoryAsync(
        string boardId,
        string actingUserId,
        string actingUserRole,
        CancellationToken cancellationToken = default);

    // ─── Task Operations ─────────────────────────────────────────────────────

    /// <summary>Creates a new remediation task on a board.</summary>
    Task<RemediationTask> CreateTaskAsync(
        string boardId,
        string title,
        string controlId,
        string createdBy,
        string? description = null,
        FindingSeverity? severity = null,
        string? assigneeId = null,
        DateTime? dueDate = null,
        List<string>? affectedResources = null,
        string? remediationScript = null,
        string? validationCriteria = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a single task with full details.</summary>
    Task<RemediationTask?> GetTaskAsync(
        string taskId,
        CancellationToken cancellationToken = default);

    /// <summary>Lists tasks with optional filtering, sorting, and pagination.</summary>
    Task<PagedResult<RemediationTask>> ListTasksAsync(
        string boardId,
        TaskStatus? status = null,
        FindingSeverity? severity = null,
        string? assigneeId = null,
        string? controlFamily = null,
        bool? isOverdue = null,
        string? sortBy = null,
        string? sortOrder = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default);

    /// <summary>Moves a task to a new status column.</summary>
    Task<RemediationTask> MoveTaskAsync(
        string taskId,
        TaskStatus targetStatus,
        string actingUserId,
        string actingUserName,
        string actingUserRole,
        string? comment = null,
        bool skipValidation = false,
        CancellationToken cancellationToken = default);

    /// <summary>Assigns or unassigns a task.</summary>
    Task<RemediationTask> AssignTaskAsync(
        string taskId,
        string actingUserId,
        string actingUserName,
        string actingUserRole,
        string? assigneeId = null,
        string? assigneeName = null,
        CancellationToken cancellationToken = default);

    /// <summary>Validates a task by re-scanning affected resources.</summary>
    Task<ValidationResult> ValidateTaskAsync(
        string taskId,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets open tasks for POA&M document generation.</summary>
    Task<List<RemediationTask>> GetOpenTasksForPoamAsync(
        string boardId,
        CancellationToken cancellationToken = default);

    // ─── Remediation Operations ──────────────────────────────────────────────

    /// <summary>Executes the remediation script for a task's affected resources.</summary>
    Task<RemediationExecutionResult> ExecuteTaskRemediationAsync(
        string taskId,
        string actingUserId,
        string actingUserName,
        CancellationToken cancellationToken = default);

    /// <summary>Collects evidence for a task's control ID and affected resources.</summary>
    Task<EvidenceCollectionResult> CollectTaskEvidenceAsync(
        string taskId,
        string actingUserId,
        string actingUserName,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default);

    // ─── Comment Operations ──────────────────────────────────────────────────

    /// <summary>Adds a comment to a task.</summary>
    Task<TaskComment> AddCommentAsync(
        string taskId,
        string authorId,
        string authorName,
        string content,
        string authorRole,
        string? parentCommentId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Edits a comment (within 24h window, author only).</summary>
    Task<TaskComment> EditCommentAsync(
        string commentId,
        string actingUserId,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a comment (within 1h window for non-CO, soft delete).</summary>
    Task<TaskComment> DeleteCommentAsync(
        string commentId,
        string actingUserId,
        string actingUserRole,
        CancellationToken cancellationToken = default);

    /// <summary>Lists comments for a task with optional threading and pagination.</summary>
    Task<PagedResult<TaskComment>> ListCommentsAsync(
        string taskId,
        bool includeDeleted = false,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default);

    // ─── History Operations ──────────────────────────────────────────────────

    /// <summary>Gets history entries for a task.</summary>
    Task<PagedResult<TaskHistoryEntry>> GetTaskHistoryAsync(
        string taskId,
        HistoryEventType? eventType = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default);

    // ─── View Operations ─────────────────────────────────────────────────────

    /// <summary>Saves a named view (filter combination) for a user.</summary>
    Task<SavedView> SaveViewAsync(
        SavedView view,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a saved view by name for a user.</summary>
    Task<SavedView?> GetViewAsync(
        string userId,
        string viewName,
        CancellationToken cancellationToken = default);

    /// <summary>Lists all saved views for a user.</summary>
    Task<List<SavedView>> ListViewsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a saved view.</summary>
    Task DeleteViewAsync(
        string userId,
        string viewName,
        CancellationToken cancellationToken = default);

    // ─── Bulk Operations ─────────────────────────────────────────────────────

    /// <summary>Bulk-assigns multiple tasks.</summary>
    Task<BulkOperationResult> BulkAssignAsync(
        string boardId,
        List<string> taskIds,
        string assigneeId,
        string assigneeName,
        string actingUserId,
        string actingUserName,
        string actingUserRole,
        CancellationToken cancellationToken = default);

    /// <summary>Bulk-moves multiple tasks to a target status.</summary>
    Task<BulkOperationResult> BulkMoveAsync(
        string boardId,
        List<string> taskIds,
        TaskStatus targetStatus,
        string actingUserId,
        string actingUserName,
        string actingUserRole,
        string? comment = null,
        CancellationToken cancellationToken = default);

    /// <summary>Bulk-sets due date for multiple tasks.</summary>
    Task<BulkOperationResult> BulkSetDueDateAsync(
        string boardId,
        List<string> taskIds,
        DateTime dueDate,
        string actingUserId,
        string actingUserName,
        CancellationToken cancellationToken = default);
}

// ─── Result Types ────────────────────────────────────────────────────────────

/// <summary>
/// Paginated result wrapper for list operations.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
public class PagedResult<T>
{
    /// <summary>Items in the current page.</summary>
    public List<T> Items { get; set; } = new();

    /// <summary>Total count of items across all pages.</summary>
    public int TotalCount { get; set; }

    /// <summary>Current page number (1-based).</summary>
    public int Page { get; set; }

    /// <summary>Items per page.</summary>
    public int PageSize { get; set; }

    /// <summary>Total number of pages.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>Whether there are more pages.</summary>
    public bool HasMore => Page < TotalPages;
}

/// <summary>
/// Result of a board update from a new assessment run.
/// </summary>
public class BoardUpdateResult
{
    /// <summary>Number of new tasks added.</summary>
    public int TasksAdded { get; set; }

    /// <summary>Number of tasks auto-closed (finding resolved).</summary>
    public int TasksClosed { get; set; }

    /// <summary>Number of tasks unchanged.</summary>
    public int TasksUnchanged { get; set; }

    /// <summary>The updated board.</summary>
    public RemediationBoard Board { get; set; } = null!;
}

/// <summary>
/// Result of a task validation scan.
/// </summary>
public class ValidationResult
{
    /// <summary>Whether all resources passed validation.</summary>
    public bool AllPassed { get; set; }

    /// <summary>Whether the task can be closed.</summary>
    public bool CanClose { get; set; }

    /// <summary>Per-resource validation results.</summary>
    public List<ResourceValidationResult> ResourceResults { get; set; } = new();

    /// <summary>Summary message.</summary>
    public string Summary { get; set; } = "";
}

/// <summary>
/// Validation result for a single resource.
/// </summary>
public class ResourceValidationResult
{
    /// <summary>Azure resource ID.</summary>
    public string ResourceId { get; set; } = "";

    /// <summary>Whether the resource passed validation.</summary>
    public bool Passed { get; set; }

    /// <summary>Details of the validation check.</summary>
    public string Details { get; set; } = "";
}

/// <summary>
/// Result of a remediation execution on a task.
/// </summary>
public class RemediationExecutionResult
{
    /// <summary>Whether remediation succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>The task after remediation.</summary>
    public RemediationTask Task { get; set; } = null!;

    /// <summary>Execution details or error message.</summary>
    public string Details { get; set; } = "";
}

/// <summary>
/// Result of evidence collection for a task.
/// </summary>
public class EvidenceCollectionResult
{
    /// <summary>Whether evidence was collected successfully.</summary>
    public bool Success { get; set; }

    /// <summary>Number of evidence items collected.</summary>
    public int ItemsCollected { get; set; }

    /// <summary>Summary of collected evidence.</summary>
    public string Summary { get; set; } = "";

    /// <summary>The task after evidence collection.</summary>
    public RemediationTask Task { get; set; } = null!;
}

/// <summary>
/// Result of a bulk operation on multiple tasks.
/// </summary>
public class BulkOperationResult
{
    /// <summary>Number of tasks that succeeded.</summary>
    public int Succeeded { get; set; }

    /// <summary>Number of tasks that failed.</summary>
    public int Failed { get; set; }

    /// <summary>Per-task results with details.</summary>
    public List<TaskOperationResult> Results { get; set; } = new();
}

/// <summary>
/// Result of an individual task operation within a bulk operation.
/// </summary>
public class TaskOperationResult
{
    /// <summary>Task ID.</summary>
    public string TaskId { get; set; } = "";

    /// <summary>Task number (e.g., REM-001).</summary>
    public string TaskNumber { get; set; } = "";

    /// <summary>Whether this task operation succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Error message if failed.</summary>
    public string? Error { get; set; }
}
