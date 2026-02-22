# MCP Tool Contracts: Kanban Agent

**Branch**: `002-remediation-kanban` | **Date**: 2026-02-21

All tools are exposed via MCP JSON-RPC 2.0 protocol. Tools are invoked through `tools/call`
with the tool name and JSON arguments. All responses follow the standard envelope schema
per Constitution Principle VII.

## Response Envelope

Reuses the same envelope from `compliance-tools.md`:

```json
{
  "status": "success | error | partial",
  "data": { /* tool-specific payload */ },
  "metadata": {
    "toolName": "kanban_create_board",
    "executionTimeMs": 123,
    "timestamp": "2026-02-21T10:30:00Z"
  }
}
```

Error responses include `error.message`, `error.errorCode`, and `error.suggestion`.

---

## HTTP Status Code Mapping (Kanban-specific)

| MCP Error Code | HTTP Status |
|---------------|-------------|
| `BOARD_NOT_FOUND` | 404 Not Found |
| `TASK_NOT_FOUND` | 404 Not Found |
| `COMMENT_NOT_FOUND` | 404 Not Found |
| `INVALID_TRANSITION` | 409 Conflict |
| `CONCURRENCY_CONFLICT` | 409 Conflict |
| `COMMENT_REQUIRES_TEXT` | 400 Bad Request |
| `COMMENT_EDIT_WINDOW_EXPIRED` | 403 Forbidden |
| `COMMENT_DELETE_WINDOW_EXPIRED` | 403 Forbidden |
| `BLOCKER_COMMENT_REQUIRED` | 400 Bad Request |
| `RESOLUTION_COMMENT_REQUIRED` | 400 Bad Request |
| `VALIDATION_REQUIRED` | 400 Bad Request |
| `TERMINAL_STATE` | 409 Conflict |
| `EXPORT_TOO_LARGE` | 413 Payload Too Large |
| `KANBAN_PERMISSION_DENIED` | 403 Forbidden |
| `BOARD_ARCHIVED` | 409 Conflict |
| `TASKS_REMAINING` | 409 Conflict |
| `SUBSCRIPTION_NOT_CONFIGURED` | 400 Bad Request |
| `ASSESSMENT_NOT_FOUND` | 404 Not Found |

---

## Pagination

Paginated tools in this contract: `kanban_board_show`, `kanban_task_list`,
`kanban_task_history`, `kanban_task_comments`.

Default page size: **25**. Maximum page size: **100**.

---

## Tool: `kanban_create_board`

Create a new Kanban board, optionally from an existing compliance assessment.

### Parameters

```json
{
  "name": "kanban_create_board",
  "description": "Create a new Kanban remediation board. Optionally auto-populate from an existing compliance assessment.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "name": {
        "type": "string",
        "maxLength": 200,
        "description": "Board name (e.g., 'Q1 2026 FedRAMP Audit'). Required."
      },
      "subscriptionId": {
        "type": "string",
        "description": "Azure subscription ID. Falls back to configured default."
      },
      "assessmentId": {
        "type": "string",
        "description": "Existing assessment ID to populate tasks from findings. If omitted, creates an empty board."
      },
      "owner": {
        "type": "string",
        "description": "Board owner identity. Defaults to current user."
      }
    },
    "required": ["name"]
  }
}
```

### Response

```json
{
  "status": "success",
  "data": {
    "boardId": "b1-...",
    "name": "Q1 2026 FedRAMP Audit",
    "subscriptionId": "sub-123",
    "assessmentId": "a1-...",
    "owner": "user@contoso.com",
    "createdAt": "2026-02-21T10:30:00Z",
    "taskCount": 48,
    "tasksByStatus": {
      "Backlog": 48,
      "ToDo": 0,
      "InProgress": 0,
      "InReview": 0,
      "Blocked": 0,
      "Done": 0
    },
    "tasksBySeverity": {
      "Critical": 3,
      "High": 12,
      "Medium": 20,
      "Low": 13
    }
  },
  "metadata": { "toolName": "kanban_create_board", "executionTimeMs": 1502, "timestamp": "..." }
}
```

### Error Codes
- `ASSESSMENT_NOT_FOUND` — `assessmentId` does not exist
- `SUBSCRIPTION_NOT_CONFIGURED` — No subscription specified or configured
- `KANBAN_PERMISSION_DENIED` — User does not have board creation permission

---

## Tool: `kanban_create_task`

Create a remediation task manually (not from an assessment).

### Parameters

```json
{
  "name": "kanban_create_task",
  "description": "Create a new remediation task on a Kanban board.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "boardId": {
        "type": "string",
        "description": "Board to add the task to. Required."
      },
      "title": {
        "type": "string",
        "maxLength": 500,
        "description": "Task title. Required."
      },
      "controlId": {
        "type": "string",
        "description": "NIST control ID (e.g., 'AC-2.1'). Required."
      },
      "description": {
        "type": "string",
        "maxLength": 4000,
        "description": "Detailed finding description."
      },
      "severity": {
        "type": "string",
        "enum": ["Critical", "High", "Medium", "Low", "Informational"],
        "default": "Medium",
        "description": "Severity level. Case-insensitive."
      },
      "assigneeId": {
        "type": "string",
        "description": "User to assign the task to."
      },
      "assigneeName": {
        "type": "string",
        "description": "Display name for the assignee."
      },
      "dueDate": {
        "type": "string",
        "format": "date-time",
        "description": "Due date (ISO 8601). Defaults to SLA-derived offset based on severity."
      },
      "affectedResources": {
        "type": "array",
        "items": { "type": "string" },
        "description": "Azure resource IDs affected by this finding."
      },
      "remediationScript": {
        "type": "string",
        "description": "PowerShell/CLI remediation script."
      },
      "validationCriteria": {
        "type": "string",
        "description": "Description of how to verify the fix."
      }
    },
    "required": ["boardId", "title", "controlId"]
  }
}
```

### Response

```json
{
  "status": "success",
  "data": {
    "taskId": "t1-...",
    "taskNumber": "REM-001",
    "boardId": "b1-...",
    "title": "AC-2.1: Enable MFA for Admin Accounts",
    "controlId": "AC-2.1",
    "controlFamily": "AC",
    "severity": "Critical",
    "status": "Backlog",
    "assigneeId": "user@contoso.com",
    "assigneeName": "John Doe",
    "dueDate": "2026-02-22T10:30:00Z",
    "createdAt": "2026-02-21T10:30:00Z",
    "createdBy": "admin@contoso.com"
  },
  "metadata": { "toolName": "kanban_create_task", "executionTimeMs": 45, "timestamp": "..." }
}
```

### Error Codes
- `BOARD_NOT_FOUND` — `boardId` does not exist
- `BOARD_ARCHIVED` — Board is archived, no new tasks allowed
- `KANBAN_PERMISSION_DENIED` — User does not have task creation permission

---

## Tool: `kanban_move_task`

Transition a task between Kanban columns (status change).

### Parameters

```json
{
  "name": "kanban_move_task",
  "description": "Move a remediation task to a new Kanban column (status transition).",
  "inputSchema": {
    "type": "object",
    "properties": {
      "taskId": {
        "type": "string",
        "description": "Task ID or task number (e.g., 'REM-001'). Required."
      },
      "boardId": {
        "type": "string",
        "description": "Board ID. Required if using task number."
      },
      "targetStatus": {
        "type": "string",
        "enum": ["Backlog", "ToDo", "InProgress", "InReview", "Blocked", "Done"],
        "description": "Target Kanban column. Case-insensitive. Required."
      },
      "comment": {
        "type": "string",
        "maxLength": 4000,
        "description": "Reason for the transition. Required when moving to/from Blocked."
      },
      "skipValidation": {
        "type": "boolean",
        "default": false,
        "description": "Skip validation when moving to Done. Compliance Officer only."
      }
    },
    "required": ["taskId", "targetStatus"]
  }
}
```

### Response

```json
{
  "status": "success",
  "data": {
    "taskId": "t1-...",
    "taskNumber": "REM-001",
    "previousStatus": "InProgress",
    "newStatus": "InReview",
    "transitionedAt": "2026-02-21T11:00:00Z",
    "transitionedBy": "engineer@contoso.com",
    "historyEntryId": "h1-...",
    "validation": {
      "triggered": true,
      "result": "pending"
    }
  },
  "metadata": { "toolName": "kanban_move_task", "executionTimeMs": 210, "timestamp": "..." }
}
```

### Error Codes
- `TASK_NOT_FOUND` — Task ID or number does not exist
- `INVALID_TRANSITION` — Transition not allowed (e.g., Done → ToDo)
- `TERMINAL_STATE` — Task is in Done state and cannot be moved
- `BLOCKER_COMMENT_REQUIRED` — Moving to Blocked requires a comment
- `RESOLUTION_COMMENT_REQUIRED` — Moving from Blocked requires a comment
- `VALIDATION_REQUIRED` — Moving to Done requires validation pass (unless `skipValidation`)
- `CONCURRENCY_CONFLICT` — Task was modified by another user; retry
- `KANBAN_PERMISSION_DENIED` — User cannot move this task
- `BOARD_ARCHIVED` — Board is archived, no transitions allowed

---

## Tool: `kanban_assign_task`

Assign or unassign a task.

### Parameters

```json
{
  "name": "kanban_assign_task",
  "description": "Assign or unassign a remediation task to a user.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "taskId": {
        "type": "string",
        "description": "Task ID or task number (e.g., 'REM-001'). Required."
      },
      "boardId": {
        "type": "string",
        "description": "Board ID. Required if using task number."
      },
      "assigneeId": {
        "type": "string",
        "description": "User ID to assign. Omit or set null to unassign."
      },
      "assigneeName": {
        "type": "string",
        "description": "Display name for the assignee."
      }
    },
    "required": ["taskId"]
  }
}
```

### Response

```json
{
  "status": "success",
  "data": {
    "taskId": "t1-...",
    "taskNumber": "REM-001",
    "previousAssignee": null,
    "newAssignee": {
      "assigneeId": "engineer@contoso.com",
      "assigneeName": "Jane Smith"
    },
    "assignedAt": "2026-02-21T11:15:00Z",
    "assignedBy": "lead@contoso.com"
  },
  "metadata": { "toolName": "kanban_assign_task", "executionTimeMs": 35, "timestamp": "..." }
}
```

### Error Codes
- `TASK_NOT_FOUND` — Task ID or number does not exist
- `TERMINAL_STATE` — Cannot reassign a Done task
- `CONCURRENCY_CONFLICT` — Task was modified by another user; retry
- `KANBAN_PERMISSION_DENIED` — User cannot assign this task
- `BOARD_ARCHIVED` — Board is archived

---

## Tool: `kanban_add_comment`

Add a comment to a remediation task.

### Parameters

```json
{
  "name": "kanban_add_comment",
  "description": "Add a comment to a remediation task. Supports Markdown and @mentions.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "taskId": {
        "type": "string",
        "description": "Task ID or task number. Required."
      },
      "boardId": {
        "type": "string",
        "description": "Board ID. Required if using task number."
      },
      "content": {
        "type": "string",
        "maxLength": 4000,
        "description": "Comment text (Markdown). Required."
      },
      "parentCommentId": {
        "type": "string",
        "description": "Parent comment ID for threading (single-level only)."
      }
    },
    "required": ["taskId", "content"]
  }
}
```

### Response

```json
{
  "status": "success",
  "data": {
    "commentId": "c1-...",
    "taskId": "t1-...",
    "taskNumber": "REM-001",
    "authorId": "user@contoso.com",
    "authorName": "John Doe",
    "content": "Applied the remediation script. MFA is now enforced.",
    "createdAt": "2026-02-21T11:30:00Z",
    "isSystemComment": false,
    "mentions": ["lead@contoso.com"],
    "parentCommentId": null
  },
  "metadata": { "toolName": "kanban_add_comment", "executionTimeMs": 28, "timestamp": "..." }
}
```

### Error Codes
- `TASK_NOT_FOUND` — Task does not exist
- `COMMENT_REQUIRES_TEXT` — Content is empty or whitespace
- `COMMENT_NOT_FOUND` — `parentCommentId` does not exist
- `KANBAN_PERMISSION_DENIED` — User cannot comment on this task

---

## Tool: `kanban_board_show`

Display board details with task counts and column breakdowns.

### Parameters

```json
{
  "name": "kanban_board_show",
  "description": "Show a Kanban board overview with task counts and column breakdowns.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "boardId": {
        "type": "string",
        "description": "Board ID. Required."
      },
      "includeTaskSummaries": {
        "type": "boolean",
        "default": true,
        "description": "Include brief task summaries in each column."
      },
      "page": {
        "type": "integer",
        "default": 1,
        "description": "Page number for task summaries."
      },
      "pageSize": {
        "type": "integer",
        "default": 25,
        "description": "Tasks per page."
      }
    },
    "required": ["boardId"]
  }
}
```

### Response

```json
{
  "status": "success",
  "data": {
    "boardId": "b1-...",
    "name": "Q1 2026 FedRAMP Audit",
    "subscriptionId": "sub-123",
    "owner": "user@contoso.com",
    "isArchived": false,
    "createdAt": "2026-02-21T10:30:00Z",
    "updatedAt": "2026-02-21T11:30:00Z",
    "totalTasks": 48,
    "overdueTasks": 3,
    "columns": {
      "Backlog": {
        "count": 20,
        "tasks": [
          {
            "taskId": "t1-...",
            "taskNumber": "REM-001",
            "title": "AC-2.1: Enable MFA",
            "severity": "Critical",
            "assigneeName": null,
            "dueDate": "2026-02-22T10:30:00Z",
            "isOverdue": true
          }
        ]
      },
      "ToDo": { "count": 10, "tasks": [ ... ] },
      "InProgress": { "count": 8, "tasks": [ ... ] },
      "InReview": { "count": 5, "tasks": [ ... ] },
      "Blocked": { "count": 2, "tasks": [ ... ] },
      "Done": { "count": 3, "tasks": [ ... ] }
    },
    "severityBreakdown": {
      "Critical": 3,
      "High": 12,
      "Medium": 20,
      "Low": 13
    },
    "completionPercentage": 6.25
  },
  "metadata": { "toolName": "kanban_board_show", "executionTimeMs": 89, "timestamp": "..." },
  "pagination": { "page": 1, "pageSize": 25, "totalItems": 48, "totalPages": 2, "hasNextPage": true }
}
```

### Error Codes
- `BOARD_NOT_FOUND` — Board does not exist

---

## Tool: `kanban_task_list`

List and filter tasks on a board.

### Parameters

```json
{
  "name": "kanban_task_list",
  "description": "List and filter remediation tasks on a Kanban board.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "boardId": {
        "type": "string",
        "description": "Board ID. Required."
      },
      "status": {
        "type": "string",
        "enum": ["Backlog", "ToDo", "InProgress", "InReview", "Blocked", "Done"],
        "description": "Filter by status. Case-insensitive."
      },
      "severity": {
        "type": "string",
        "enum": ["Critical", "High", "Medium", "Low", "Informational"],
        "description": "Filter by severity. Case-insensitive."
      },
      "assigneeId": {
        "type": "string",
        "description": "Filter by assignee. Use 'unassigned' for unassigned tasks."
      },
      "controlFamily": {
        "type": "string",
        "description": "Filter by control family (e.g., 'AC'). Case-insensitive."
      },
      "isOverdue": {
        "type": "boolean",
        "description": "Filter for overdue tasks only."
      },
      "sortBy": {
        "type": "string",
        "enum": ["severity", "dueDate", "createdAt", "status", "controlId"],
        "default": "severity",
        "description": "Sort field."
      },
      "sortOrder": {
        "type": "string",
        "enum": ["asc", "desc"],
        "default": "desc",
        "description": "Sort direction."
      },
      "page": {
        "type": "integer",
        "default": 1,
        "description": "Page number."
      },
      "pageSize": {
        "type": "integer",
        "default": 25,
        "description": "Items per page. Max 100."
      }
    },
    "required": ["boardId"]
  }
}
```

### Response

```json
{
  "status": "success",
  "data": {
    "boardId": "b1-...",
    "boardName": "Q1 2026 FedRAMP Audit",
    "tasks": [
      {
        "taskId": "t1-...",
        "taskNumber": "REM-001",
        "title": "AC-2.1: Enable MFA for Admin Accounts",
        "controlId": "AC-2.1",
        "controlFamily": "AC",
        "severity": "Critical",
        "status": "Backlog",
        "assigneeName": null,
        "dueDate": "2026-02-22T10:30:00Z",
        "isOverdue": true,
        "commentCount": 2,
        "createdAt": "2026-02-21T10:30:00Z"
      }
    ],
    "appliedFilters": {
      "status": null,
      "severity": "Critical",
      "assigneeId": null,
      "controlFamily": null,
      "isOverdue": null
    }
  },
  "metadata": { "toolName": "kanban_task_list", "executionTimeMs": 56, "timestamp": "..." },
  "pagination": { "page": 1, "pageSize": 25, "totalItems": 3, "totalPages": 1, "hasNextPage": false }
}
```

### Error Codes
- `BOARD_NOT_FOUND` — Board does not exist

---

## Tool: `kanban_task_history`

View the audit trail / history for a task.

### Parameters

```json
{
  "name": "kanban_task_history",
  "description": "View the full audit trail and history of a remediation task.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "taskId": {
        "type": "string",
        "description": "Task ID or task number. Required."
      },
      "boardId": {
        "type": "string",
        "description": "Board ID. Required if using task number."
      },
      "eventType": {
        "type": "string",
        "enum": ["Created", "StatusChanged", "Assigned", "CommentAdded", "CommentEdited", "CommentDeleted", "RemediationAttempt", "ValidationRun", "DueDateChanged", "SeverityChanged"],
        "description": "Filter by event type."
      },
      "page": {
        "type": "integer",
        "default": 1
      },
      "pageSize": {
        "type": "integer",
        "default": 25
      }
    },
    "required": ["taskId"]
  }
}
```

### Response

```json
{
  "status": "success",
  "data": {
    "taskId": "t1-...",
    "taskNumber": "REM-001",
    "entries": [
      {
        "entryId": "h1-...",
        "eventType": "StatusChanged",
        "oldValue": "Backlog",
        "newValue": "ToDo",
        "actingUserId": "admin@contoso.com",
        "actingUserName": "Admin User",
        "timestamp": "2026-02-21T10:45:00Z",
        "details": null
      },
      {
        "entryId": "h2-...",
        "eventType": "Assigned",
        "oldValue": null,
        "newValue": "engineer@contoso.com",
        "actingUserId": "admin@contoso.com",
        "actingUserName": "Admin User",
        "timestamp": "2026-02-21T10:46:00Z",
        "details": "Assigned to Jane Smith"
      }
    ]
  },
  "metadata": { "toolName": "kanban_task_history", "executionTimeMs": 42, "timestamp": "..." },
  "pagination": { "page": 1, "pageSize": 25, "totalItems": 2, "totalPages": 1, "hasNextPage": false }
}
```

### Error Codes
- `TASK_NOT_FOUND` — Task does not exist
- `KANBAN_PERMISSION_DENIED` — User cannot view this task's history

---

## Tool: `kanban_task_comments`

List comments on a task.

### Parameters

```json
{
  "name": "kanban_task_comments",
  "description": "List all comments on a remediation task.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "taskId": {
        "type": "string",
        "description": "Task ID or task number. Required."
      },
      "boardId": {
        "type": "string",
        "description": "Board ID. Required if using task number."
      },
      "includeDeleted": {
        "type": "boolean",
        "default": false,
        "description": "Include soft-deleted comments (shown as '[deleted]')."
      },
      "page": { "type": "integer", "default": 1 },
      "pageSize": { "type": "integer", "default": 25 }
    },
    "required": ["taskId"]
  }
}
```

### Response

```json
{
  "status": "success",
  "data": {
    "taskId": "t1-...",
    "taskNumber": "REM-001",
    "comments": [
      {
        "commentId": "c1-...",
        "authorId": "user@contoso.com",
        "authorName": "John Doe",
        "content": "Remediation script applied successfully.",
        "createdAt": "2026-02-21T11:30:00Z",
        "isEdited": false,
        "isSystemComment": false,
        "isDeleted": false,
        "parentCommentId": null,
        "mentions": []
      }
    ]
  },
  "metadata": { "toolName": "kanban_task_comments", "executionTimeMs": 30, "timestamp": "..." },
  "pagination": { "page": 1, "pageSize": 25, "totalItems": 1, "totalPages": 1, "hasNextPage": false }
}
```

### Error Codes
- `TASK_NOT_FOUND` — Task does not exist

---

## Tool: `kanban_task_validate`

Trigger a validation scan for a remediation task.

### Parameters

```json
{
  "name": "kanban_task_validate",
  "description": "Run a targeted validation scan to verify a remediation fix was applied.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "taskId": {
        "type": "string",
        "description": "Task ID or task number. Required."
      },
      "boardId": {
        "type": "string",
        "description": "Board ID. Required if using task number."
      },
      "subscriptionId": {
        "type": "string",
        "description": "Azure subscription ID. Falls back to board's subscription."
      }
    },
    "required": ["taskId"]
  }
}
```

### Response

```json
{
  "status": "success",
  "data": {
    "taskId": "t1-...",
    "taskNumber": "REM-001",
    "controlId": "AC-2.1",
    "validationResult": "pass",
    "details": "MFA is now enabled for all admin accounts in the subscription.",
    "resourcesChecked": 5,
    "resourcesPassing": 5,
    "validatedAt": "2026-02-21T12:00:00Z",
    "canClose": true,
    "historyEntryId": "h3-..."
  },
  "metadata": { "toolName": "kanban_task_validate", "executionTimeMs": 3450, "timestamp": "..." }
}
```

### Error Codes
- `TASK_NOT_FOUND` — Task does not exist
- `TERMINAL_STATE` — Task is already Done
- `SUBSCRIPTION_NOT_CONFIGURED` — No subscription available

---

## Tool: `kanban_bulk_update`

Perform bulk operations on multiple tasks.

### Parameters

```json
{
  "name": "kanban_bulk_update",
  "description": "Perform bulk operations on multiple remediation tasks (assign, move, change due date).",
  "inputSchema": {
    "type": "object",
    "properties": {
      "boardId": {
        "type": "string",
        "description": "Board ID. Required."
      },
      "taskIds": {
        "type": "array",
        "items": { "type": "string" },
        "description": "Task IDs or task numbers. Required."
      },
      "operation": {
        "type": "string",
        "enum": ["assign", "move", "setDueDate"],
        "description": "Bulk operation type. Required."
      },
      "assigneeId": {
        "type": "string",
        "description": "Assignee for 'assign' operation."
      },
      "assigneeName": {
        "type": "string",
        "description": "Display name for 'assign' operation."
      },
      "targetStatus": {
        "type": "string",
        "enum": ["Backlog", "ToDo", "InProgress", "InReview", "Blocked", "Done"],
        "description": "Target status for 'move' operation."
      },
      "dueDate": {
        "type": "string",
        "format": "date-time",
        "description": "Due date for 'setDueDate' operation."
      },
      "comment": {
        "type": "string",
        "description": "Comment for transitions that require one."
      }
    },
    "required": ["boardId", "taskIds", "operation"]
  }
}
```

### Response

```json
{
  "status": "partial",
  "data": {
    "boardId": "b1-...",
    "operation": "move",
    "targetStatus": "ToDo",
    "totalRequested": 5,
    "succeeded": 4,
    "failed": 1,
    "results": [
      { "taskId": "t1-...", "taskNumber": "REM-001", "status": "success" },
      { "taskId": "t2-...", "taskNumber": "REM-002", "status": "success" },
      { "taskId": "t3-...", "taskNumber": "REM-003", "status": "success" },
      { "taskId": "t4-...", "taskNumber": "REM-004", "status": "success" },
      { "taskId": "t5-...", "taskNumber": "REM-005", "status": "error", "errorCode": "INVALID_TRANSITION", "message": "Cannot move from Done" }
    ]
  },
  "metadata": { "toolName": "kanban_bulk_update", "executionTimeMs": 340, "timestamp": "..." }
}
```

### Error Codes
- `BOARD_NOT_FOUND` — Board does not exist
- Per-task errors are returned in `results` array (partial success)

---

## Tool: `kanban_export`

Export board data as CSV.

### Parameters

```json
{
  "name": "kanban_export",
  "description": "Export Kanban board data as CSV for reporting or POA&M integration.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "boardId": {
        "type": "string",
        "description": "Board ID. Required."
      },
      "format": {
        "type": "string",
        "enum": ["csv", "poam"],
        "default": "csv",
        "description": "Export format. 'poam' generates POA&M-compatible output."
      },
      "statuses": {
        "type": "array",
        "items": { "type": "string" },
        "description": "Filter by statuses. All if omitted."
      },
      "includeHistory": {
        "type": "boolean",
        "default": false,
        "description": "Include full history in export."
      }
    },
    "required": ["boardId"]
  }
}
```

### Response

```json
{
  "status": "success",
  "data": {
    "boardId": "b1-...",
    "boardName": "Q1 2026 FedRAMP Audit",
    "format": "csv",
    "exportedAt": "2026-02-21T12:30:00Z",
    "taskCount": 48,
    "csvContent": "TaskNumber,Title,ControlId,Severity,Status,Assignee,DueDate,IsOverdue\nREM-001,AC-2.1: Enable MFA,AC-2.1,Critical,Backlog,,2026-02-22T10:30:00Z,true\n...",
    "sizeBytes": 4500
  },
  "metadata": { "toolName": "kanban_export", "executionTimeMs": 120, "timestamp": "..." }
}
```

### Error Codes
- `BOARD_NOT_FOUND` — Board does not exist
- `EXPORT_TOO_LARGE` — Board has > 500 tasks with history; reduce scope

---

## Tool: `kanban_archive_board`

Archive a Kanban board (soft delete — tasks are retained but read-only).

### Parameters

```json
{
  "name": "kanban_archive_board",
  "description": "Archive a Kanban board. Archived boards are read-only.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "boardId": {
        "type": "string",
        "description": "Board ID. Required."
      },
      "confirm": {
        "type": "boolean",
        "default": false,
        "description": "Confirm archival. Must be true."
      }
    },
    "required": ["boardId", "confirm"]
  }
}
```

### Response

```json
{
  "status": "success",
  "data": {
    "boardId": "b1-...",
    "name": "Q1 2026 FedRAMP Audit",
    "archivedAt": "2026-02-21T13:00:00Z",
    "archivedBy": "admin@contoso.com",
    "taskCount": 48,
    "tasksByStatus": {
      "Backlog": 0,
      "ToDo": 0,
      "InProgress": 0,
      "InReview": 0,
      "Blocked": 0,
      "Done": 48
    }
  },
  "metadata": { "toolName": "kanban_archive_board", "executionTimeMs": 65, "timestamp": "..." }
}
```

### Error Codes
- `BOARD_NOT_FOUND` — Board does not exist
- `TASKS_REMAINING` — Board has uncompleted tasks (not all Done); archive blocked
- `KANBAN_PERMISSION_DENIED` — Only Compliance Officer can archive

---

## Tool: `kanban_get_task`

Get full details of a single remediation task including all fields.

### Parameters

```json
{
  "name": "kanban_get_task",
  "description": "Get the full details of a single remediation task.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "taskId": {
        "type": "string",
        "description": "Task ID or task number (e.g., 'REM-001'). Required."
      },
      "boardId": {
        "type": "string",
        "description": "Board ID. Required if using task number."
      }
    },
    "required": ["taskId"]
  }
}
```

### Response

```json
{
  "status": "success",
  "data": {
    "taskId": "t1-...",
    "taskNumber": "REM-001",
    "boardId": "b1-...",
    "title": "AC-2.1: Enable MFA for Admin Accounts",
    "description": "Admin accounts lack MFA enforcement...",
    "controlId": "AC-2.1",
    "controlFamily": "AC",
    "severity": "Critical",
    "status": "InProgress",
    "assigneeId": "engineer@contoso.com",
    "assigneeName": "Jane Smith",
    "dueDate": "2026-02-22T10:30:00Z",
    "isOverdue": true,
    "createdAt": "2026-02-21T10:30:00Z",
    "updatedAt": "2026-02-21T11:00:00Z",
    "createdBy": "admin@contoso.com",
    "affectedResources": ["/subscriptions/.../resourceGroups/.../providers/..."],
    "remediationScript": "Set-AzPolicy ...",
    "validationCriteria": "All admin accounts must have MFA enabled.",
    "findingId": "f1-...",
    "commentCount": 3,
    "historyCount": 7
  },
  "metadata": { "toolName": "kanban_get_task", "executionTimeMs": 25, "timestamp": "..." }
}
```

### Error Codes
- `TASK_NOT_FOUND` — Task ID or number does not exist

---

## Tool: `kanban_edit_comment`

Edit an existing comment within the 24-hour edit window.

### Parameters

```json
{
  "name": "kanban_edit_comment",
  "description": "Edit an existing comment on a remediation task. Must be within 24 hours of creation.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "commentId": {
        "type": "string",
        "description": "Comment ID. Required."
      },
      "taskId": {
        "type": "string",
        "description": "Task ID or task number. Required."
      },
      "boardId": {
        "type": "string",
        "description": "Board ID. Required if using task number."
      },
      "content": {
        "type": "string",
        "maxLength": 4000,
        "description": "Updated comment text (Markdown). Required."
      }
    },
    "required": ["commentId", "taskId", "content"]
  }
}
```

### Response

```json
{
  "status": "success",
  "data": {
    "commentId": "c1-...",
    "taskId": "t1-...",
    "taskNumber": "REM-001",
    "content": "Updated remediation notes: MFA now enforced for all admin accounts.",
    "editedAt": "2026-02-21T12:00:00Z",
    "isEdited": true
  },
  "metadata": { "toolName": "kanban_edit_comment", "executionTimeMs": 22, "timestamp": "..." }
}
```

### Error Codes
- `COMMENT_NOT_FOUND` — Comment does not exist
- `COMMENT_EDIT_WINDOW_EXPIRED` — 24-hour edit window has passed
- `TERMINAL_STATE` — Task is Done; comments are protected
- `KANBAN_PERMISSION_DENIED` — User does not own this comment

---

## Tool: `kanban_delete_comment`

Delete a comment (soft delete — replaces content with "[deleted]").

### Parameters

```json
{
  "name": "kanban_delete_comment",
  "description": "Delete a comment on a remediation task. Non-officers must delete within 1 hour.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "commentId": {
        "type": "string",
        "description": "Comment ID. Required."
      },
      "taskId": {
        "type": "string",
        "description": "Task ID or task number. Required."
      },
      "boardId": {
        "type": "string",
        "description": "Board ID. Required if using task number."
      }
    },
    "required": ["commentId", "taskId"]
  }
}
```

### Response

```json
{
  "status": "success",
  "data": {
    "commentId": "c1-...",
    "taskId": "t1-...",
    "taskNumber": "REM-001",
    "deletedAt": "2026-02-21T12:05:00Z",
    "deletedBy": "user@contoso.com"
  },
  "metadata": { "toolName": "kanban_delete_comment", "executionTimeMs": 18, "timestamp": "..." }
}
```

### Error Codes
- `COMMENT_NOT_FOUND` — Comment does not exist
- `COMMENT_DELETE_WINDOW_EXPIRED` — 1-hour delete window has passed (non-CO only)
- `TERMINAL_STATE` — Task is Done; comments are protected
- `KANBAN_PERMISSION_DENIED` — User does not own this comment and is not a Compliance Officer

---

## Tool: `kanban_collect_evidence`

Collect compliance evidence scoped to a specific remediation task's control and resources.

### Parameters

```json
{
  "name": "kanban_collect_evidence",
  "description": "Collect compliance evidence for a remediation task's control ID and affected resources.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "taskId": {
        "type": "string",
        "description": "Task ID or task number. Required."
      },
      "boardId": {
        "type": "string",
        "description": "Board ID. Required if using task number."
      },
      "subscriptionId": {
        "type": "string",
        "description": "Azure subscription ID. Falls back to board's subscription."
      }
    },
    "required": ["taskId"]
  }
}
```

### Response

```json
{
  "status": "success",
  "data": {
    "taskId": "t1-...",
    "taskNumber": "REM-001",
    "controlId": "AC-2.1",
    "evidenceItems": 5,
    "summary": "Collected 5 evidence items for AC-2.1 across 3 resources.",
    "collectedAt": "2026-02-21T12:15:00Z"
  },
  "metadata": { "toolName": "kanban_collect_evidence", "executionTimeMs": 2100, "timestamp": "..." }
}
```

### Error Codes
- `TASK_NOT_FOUND` — Task does not exist
- `SUBSCRIPTION_NOT_CONFIGURED` — No subscription available

---

## Tool Summary

| Tool | Description | Roles | Paginated |
|------|-------------|-------|-----------|
| `kanban_create_board` | Create board (empty or from assessment) | CO, SL | No |
| `kanban_create_task` | Create task manually | CO, SL | No |
| `kanban_get_task` | Get full task details | ALL | No |
| `kanban_move_task` | Transition task status | CO, SL, PE (own) | No |
| `kanban_assign_task` | Assign/unassign task | CO, SL, PE (self) | No |
| `kanban_add_comment` | Add comment to task | CO, SL, PE | No |
| `kanban_edit_comment` | Edit own comment (24h window) | CO, SL, PE | No |
| `kanban_delete_comment` | Delete comment (1h/CO any) | CO, SL, PE | No |
| `kanban_board_show` | Show board overview | ALL | Yes |
| `kanban_task_list` | Filter and list tasks | ALL (PE: team only) | Yes |
| `kanban_task_history` | View task audit trail | CO, SL, AU, PE (own) | Yes |
| `kanban_task_comments` | List task comments | ALL | Yes |
| `kanban_task_validate` | Trigger validation scan | CO, SL, PE | No |
| `kanban_collect_evidence` | Collect evidence for task | CO, SL, PE | No |
| `kanban_bulk_update` | Bulk assign/move/date | CO, SL | No |
| `kanban_export` | Export as CSV/POA&M | CO, SL, AU | No |
| `kanban_archive_board` | Archive board | CO | No |

**Role abbreviations**: CO = Compliance Officer, SL = Security Lead, PE = Platform Engineer, AU = Auditor
