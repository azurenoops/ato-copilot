# MCP Tool Response Contracts: Kanban User Context Changes

**Feature**: `004-kanban-user-context` | **Date**: 2026-02-22

## Overview

This document defines the changes to MCP tool response contracts introduced by the user context propagation feature. All changes are **additive** — no existing fields are removed or renamed.

---

## Contract Change 1: Task Summary (Board Show)

**Tool**: `kanban_board_show`  
**Change**: Add `isAssignedToCurrentUser` field to each task summary in column arrays.

### Before
```json
{
  "status": "Success",
  "data": {
    "columns": {
      "Backlog": [
        {
          "taskNumber": "REM-001",
          "title": "AC-1: Access Control Policy",
          "severity": "High",
          "assigneeName": "Jane Doe",
          "dueDate": "2026-03-01T00:00:00Z",
          "isOverdue": false,
          "commentCount": 3
        }
      ]
    }
  }
}
```

### After
```json
{
  "status": "Success",
  "data": {
    "columns": {
      "Backlog": [
        {
          "taskNumber": "REM-001",
          "title": "AC-1: Access Control Policy",
          "severity": "High",
          "assigneeName": "Jane Doe",
          "dueDate": "2026-03-01T00:00:00Z",
          "isOverdue": false,
          "commentCount": 3,
          "isAssignedToCurrentUser": true
        }
      ]
    }
  }
}
```

---

## Contract Change 2: Task Detail (Get Task)

**Tool**: `kanban_get_task`  
**Change**: Add `isAssignedToCurrentUser` field to the task detail response.

### After (new field only)
```json
{
  "status": "Success",
  "data": {
    "taskNumber": "REM-001",
    "title": "AC-1: Access Control Policy",
    "assigneeId": "jane.doe",
    "assigneeName": "Jane Doe",
    "isAssignedToCurrentUser": true
  }
}
```

---

## Contract Change 3: Task List

**Tool**: `kanban_task_list`  
**Change**: Add `isAssignedToCurrentUser` field to each task in the list.

### After (new field only)
```json
{
  "status": "Success",
  "data": {
    "tasks": [
      {
        "taskNumber": "REM-001",
        "assigneeName": "Jane Doe",
        "isAssignedToCurrentUser": true
      },
      {
        "taskNumber": "REM-002",
        "assigneeName": "John Smith",
        "isAssignedToCurrentUser": false
      }
    ]
  }
}
```

---

## Contract Change 4: Identity Attribution in Mutation Responses

**Tools**: `kanban_assign_task`, `kanban_move_task`, `kanban_archive_board`  
**Change**: The `assignedBy`, `transitionedBy`, and `archivedBy` fields now reflect the authenticated user's identity instead of the hardcoded `"system"`.

### Before
```json
{
  "status": "Success",
  "data": {
    "assignedBy": "system"
  }
}
```

### After
```json
{
  "status": "Success",
  "data": {
    "assignedBy": "jane.doe"
  }
}
```

---

## Backward Compatibility

| Aspect | Impact |
|--------|--------|
| New fields | `isAssignedToCurrentUser` is additive — existing consumers that don't parse it are unaffected |
| Identity attribution | `assignedBy`/`transitionedBy`/`archivedBy` values change from `"system"` to real user IDs — consumers parsing these values should expect dynamic values |
| Error responses | No changes to error codes or error response structure |
| Unauthenticated requests | All tools continue to function; `isAssignedToCurrentUser` is `false`, identity defaults to `"anonymous"` |
