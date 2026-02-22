# Data Model: Kanban User Context & Comment Permission Enhancement

**Feature**: `004-kanban-user-context` | **Date**: 2026-02-22

## New Entities

### IUserContext (Interface)

Represents the authenticated user's identity available to all tool invocations.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| UserId | string | Yes | Unique identifier from authentication claims (`oid` or `sub`). Defaults to `"anonymous"` when no auth context is present. |
| DisplayName | string | Yes | Human-readable display name from `Identity.Name`. Falls back to `UserId` if the name claim is missing. |
| Role | string | Yes | Highest-privilege compliance role from claim roles. One of: `Compliance.Administrator`, `Compliance.SecurityLead`, `Compliance.Analyst`, `Compliance.Auditor`, `Compliance.PlatformEngineer`, `Compliance.Viewer`. Defaults to `Compliance.Viewer`. |
| IsAuthenticated | boolean | Yes | Whether the user has been authenticated. `false` when no auth context is present. |

**Relationships**:
- Consumed by all Kanban tool classes to replace hardcoded `"system"` / `"System"` / `"Compliance.Officer"` identity strings.
- Used by board show, task list, and get-task tools to compute `isAssignedToCurrentUser`.

**Validation Rules**:
- `UserId` must never be null or empty — defaults to `"anonymous"`.
- `DisplayName` must never be null or empty — defaults to `UserId`.
- `Role` must be one of the 6 values defined in `ComplianceRoles` constants — defaults to `Compliance.Viewer`.

**State Transitions**: N/A (stateless — computed per request from ambient claims).

---

### HttpUserContext (Implementation)

Concrete implementation of `IUserContext` for ASP.NET Core HTTP requests.

| Dependency | Purpose |
|------------|---------|
| IHttpContextAccessor | Reads `HttpContext.User` claims lazily to support cross-scope resolution |

**Behavior**:
- Properties are read lazily from `IHttpContextAccessor.HttpContext.User` on first access.
- Results are cached for the lifetime of the request to avoid repeated claim parsing.
- When `HttpContext` is null (e.g., stdio mode, background services), all properties return their fallback defaults.

---

## Modified Entities

### KanbanPermissionsHelper — Role Permission Matrix

**Change**: Add `CanDeleteAnyComment` to `SecurityLead` role.

| Role | Current Permissions | Added Permission |
|------|--------------------|--------------------|
| `Compliance.SecurityLead` | CreateBoard, CreateTask, AssignAny, MoveOwn, MoveAny, Comment, Export | **+ CanDeleteAnyComment** |

All other roles unchanged.

---

### Task Response — Tool Output Extension

**Change**: Add `isAssignedToCurrentUser` computed field to task representations.

| Field | Type | Description | Computation |
|-------|------|-------------|-------------|
| isAssignedToCurrentUser | boolean | Whether the task is assigned to the requesting user | `task.AssigneeId == userContext.UserId` |

Affected tool responses:
- `kanban_board_show` — each task summary in the column arrays
- `kanban_get_task` — the task detail response
- `kanban_task_list` — each task in the list response

**Edge cases**:
- Task has no assignee (`AssigneeId` is null/empty) → `false`
- User is not authenticated (`userContext.IsAuthenticated` is false) → `false`
- User ID format mismatch → exact string comparison, `false` if no match

---

## Entity Relationship Summary

```
HttpContext.User (claims)
    │
    ▼
IUserContext (interface in Core)
    │
    ├──► KanbanToolBase.ExecuteCoreAsync() → resolves from scope
    │       │
    │       ├──► Replaces "system" userId in service calls
    │       ├──► Replaces "System" displayName in service calls
    │       ├──► Replaces "Compliance.Officer" role in service calls
    │       └──► Computes isAssignedToCurrentUser in responses
    │
    └──► HttpUserContext (implementation in Mcp)
            │
            └──► IHttpContextAccessor (reads ambient HttpContext)
```
