namespace Ato.Copilot.Core.Constants;

/// <summary>
/// Constants for the Kanban remediation feature.
/// SLA values are overridable via configuration; these are compile-time defaults.
/// </summary>
public static class KanbanConstants
{
    // ─── SLA Defaults ────────────────────────────────────────────────────────

    /// <summary>Default SLA for Critical severity: 24 hours.</summary>
    public const int DefaultCriticalHours = 24;

    /// <summary>Default SLA for High severity: 7 days.</summary>
    public const int DefaultHighDays = 7;

    /// <summary>Default SLA for Medium severity: 30 days.</summary>
    public const int DefaultMediumDays = 30;

    /// <summary>Default SLA for Low severity: 90 days.</summary>
    public const int DefaultLowDays = 90;

    // ─── Task ID Format ──────────────────────────────────────────────────────

    /// <summary>Task ID prefix for human-readable identifiers.</summary>
    public const string TaskIdPrefix = "REM-";

    /// <summary>Format string for task number (e.g., REM-001).</summary>
    public const string TaskIdFormat = "REM-{0:D3}";

    // ─── Limits ──────────────────────────────────────────────────────────────

    /// <summary>Maximum number of tasks per board.</summary>
    public const int MaxTasksPerBoard = 500;

    /// <summary>Maximum comment content length in characters.</summary>
    public const int MaxCommentLength = 4000;

    /// <summary>Comment edit window in hours.</summary>
    public const int CommentEditWindowHours = 24;

    /// <summary>Comment delete window in hours for non-officer users.</summary>
    public const int CommentDeleteWindowHours = 1;

    /// <summary>Default page size for paginated responses.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>Maximum page size for paginated responses.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Maximum boards per user.</summary>
    public const int MaxBoardsPerUser = 50;

    /// <summary>Maximum attachments per task.</summary>
    public const int MaxAttachmentsPerTask = 20;

    /// <summary>Maximum comments per task.</summary>
    public const int MaxCommentsPerTask = 200;

    // ─── Validation Patterns ─────────────────────────────────────────────────

    /// <summary>Regex pattern for valid task numbers (e.g., REM-001, REM-1234).</summary>
    public const string TaskNumberPattern = @"^REM-\d{3,4}$";

    /// <summary>Regex pattern for valid NIST control IDs (e.g., AC-2, AC-2.1).</summary>
    public const string ControlIdPattern = @"^[A-Z]{2}-\d+(\.\d+)?$";

    /// <summary>Regex pattern for @mentions in comments.</summary>
    public const string MentionPattern = @"@[\w.@]+";

    // ─── Default Column Names ────────────────────────────────────────────────

    /// <summary>Default Kanban column names in order.</summary>
    public static readonly string[] DefaultColumns =
    {
        "Backlog",
        "To Do",
        "In Progress",
        "In Review",
        "Blocked",
        "Done"
    };

    // ─── System Messages ─────────────────────────────────────────────────────

    /// <summary>Content replacement for deleted comments (preserves audit trail).</summary>
    public const string DeletedCommentContent = "[deleted]";

    /// <summary>System comment for auto-closed tasks during board update.</summary>
    public const string AutoClosedComment = "Auto-closed: finding resolved in latest assessment";
}
