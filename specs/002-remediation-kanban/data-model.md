# Data Model: Remediation Kanban

**Branch**: `002-remediation-kanban` | **Date**: 2026-02-21

This document defines all new entities, their relationships, validation rules, and state
transitions for the Remediation Kanban feature. These entities extend the existing data model
in `Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs` and are added to
`Ato.Copilot.Core/Data/Context/AtoCopilotContext.cs`.

---

## Entity Overview

```text
RemediationBoard ──< RemediationTask >── ComplianceFinding (existing, FK reference)
        │                   │
        │                   ├──< TaskComment
        │                   │
        │                   └──< TaskHistoryEntry
        │
        └── ComplianceAssessment (existing, optional FK reference)

SavedView (ephemeral — IAgentStateManager)
NotificationConfig (ephemeral — IAgentStateManager)
```

---

## Enums

### TaskStatus

```text
Backlog = 0       — New findings awaiting triage
ToDo = 1          — Triaged and ready to work
InProgress = 2    — Currently being remediated
InReview = 3      — Remediation applied, awaiting validation
Blocked = 4       — Cannot proceed due to dependency or issue
Done = 5          — Validated and closed
```

### TaskSeverity

Reuses existing `FindingSeverity` enum (Critical, High, Medium, Low, Informational).
No new enum needed — the spec maps directly: Critical → Critical, High → High, etc.

### HistoryEventType

```text
Created = 0           — Task created (from assessment or manual)
StatusChanged = 1     — Task moved between columns
Assigned = 2          — Task assigned or unassigned
CommentAdded = 3      — Comment posted
CommentEdited = 4     — Comment edited
CommentDeleted = 5    — Comment deleted
RemediationAttempt = 6 — Remediation script executed
ValidationRun = 7     — Validation scan performed
DueDateChanged = 8    — Due date modified
SeverityChanged = 9   — Severity level changed
```

### NotificationEventType

```text
TaskAssigned = 0
StatusChanged = 1
CommentAdded = 2
TaskOverdue = 3
TaskClosed = 4
Mentioned = 5
```

### NotificationChannelType

```text
Email = 0
Teams = 1
Slack = 2
```

---

## Persistent Entities (EF Core)

### 1. RemediationBoard

Represents a Kanban board grouping remediation tasks.

| Field | Type | Constraints | Default | Description |
|-------|------|-------------|---------|-------------|
| `Id` | `string` | PK, GUID format | `Guid.NewGuid().ToString()` | Unique board identifier |
| `Name` | `string` | Required, MaxLength(200) | `""` | Board name (e.g., "Q1 2026 Audit") |
| `SubscriptionId` | `string` | Required, MaxLength(100), Indexed | `""` | Azure subscription ID |
| `AssessmentId` | `string?` | MaxLength(100), FK→ComplianceAssessment.Id (optional) | `null` | Assessment that generated this board |
| `Owner` | `string` | Required, MaxLength(200) | `""` | User who created the board |
| `CreatedAt` | `DateTime` | Required | `DateTime.UtcNow` | UTC creation timestamp |
| `UpdatedAt` | `DateTime` | Required | `DateTime.UtcNow` | UTC last-modified timestamp |
| `IsArchived` | `bool` | Required | `false` | Whether board is archived |
| `NextTaskNumber` | `int` | Required, Min(1) | `1` | Counter for sequential REM-NNN generation |
| `RowVersion` | `Guid` | ConcurrencyToken | `Guid.NewGuid()` | Optimistic concurrency token |
| `Tasks` | `List<RemediationTask>` | Navigation | `new()` | Child tasks |

**Indexes**:
- `SubscriptionId`
- `SubscriptionId, IsArchived` (composite — for "list active boards")

**Relationships**:
- Board 1:N Tasks (cascade delete — archiving a board retains tasks; deleting removes them)
- Board → ComplianceAssessment (optional FK, restrict delete)

---

### 2. RemediationTask

Represents a single remediation work item (Kanban card).

| Field | Type | Constraints | Default | Description |
|-------|------|-------------|---------|-------------|
| `Id` | `string` | PK, GUID format | `Guid.NewGuid().ToString()` | Unique task identifier |
| `TaskNumber` | `string` | Required, MaxLength(10), Indexed | `""` | Human-readable ID (REM-001) |
| `BoardId` | `string` | Required, FK→RemediationBoard.Id, Indexed | `""` | Parent board |
| `Title` | `string` | Required, MaxLength(500) | `""` | Task title (e.g., "AC-2.1: Enable MFA") |
| `Description` | `string` | MaxLength(4000) | `""` | Detailed finding description |
| `ControlId` | `string` | Required, MaxLength(20), Indexed | `""` | NIST control reference (e.g., AC-2.1) |
| `ControlFamily` | `string` | MaxLength(5) | `""` | Two-letter family (e.g., "AC") |
| `Severity` | `FindingSeverity` | Required | `FindingSeverity.Medium` | Task severity level |
| `Status` | `TaskStatus` | Required, Indexed | `TaskStatus.Backlog` | Current Kanban column |
| `AssigneeId` | `string?` | MaxLength(200), Indexed | `null` | Assigned user identifier |
| `AssigneeName` | `string?` | MaxLength(200) | `null` | Assigned user display name |
| `DueDate` | `DateTime` | Required | `DateTime.UtcNow.AddDays(30)` | SLA-derived or manual due date |
| `CreatedAt` | `DateTime` | Required | `DateTime.UtcNow` | UTC creation timestamp |
| `UpdatedAt` | `DateTime` | Required | `DateTime.UtcNow` | UTC last-modified timestamp |
| `AffectedResources` | `List<string>` | JSON-serialized | `new()` | Azure resource IDs |
| `RemediationScript` | `string?` | MaxLength(8000) | `null` | PowerShell/CLI script if available |
| `ValidationCriteria` | `string?` | MaxLength(2000) | `null` | How to verify the fix |
| `FindingId` | `string?` | MaxLength(100) | `null` | FK→ComplianceFinding.Id (traceability) |
| `CreatedBy` | `string` | Required, MaxLength(200) | `""` | User who created the task |
| `LastOverdueNotifiedAt` | `DateTime?` | | `null` | Prevents repeat overdue notifications |
| `RowVersion` | `Guid` | ConcurrencyToken | `Guid.NewGuid()` | Optimistic concurrency token |
| `Comments` | `List<TaskComment>` | Navigation | `new()` | Child comments |
| `History` | `List<TaskHistoryEntry>` | Navigation | `new()` | Child history entries |

**Indexes**:
- `BoardId`
- `Status`
- `AssigneeId`
- `ControlId`
- `DueDate`
- `BoardId, Status` (composite — for board column view)
- `BoardId, ControlFamily` (composite — for family filtering)

**Relationships**:
- Task → Board (required FK, cascade delete)
- Task 1:N Comments (cascade delete)
- Task 1:N History (cascade delete)
- Task → ComplianceFinding (optional FK, restrict delete)

**Validation Rules**:
- `TaskNumber` must match pattern `^REM-\d{3,4}$`
- `ControlId` must match pattern `^[A-Z]{2}-\d+(\.\d+)?$`
- `ControlFamily` derived from first two characters of `ControlId`
- `DueDate` must be in the future at creation (can be past after creation — overdue)
- `Severity` determines default `DueDate`: Critical = +24h, High = +7d, Medium = +30d, Low = +90d

---

### 3. TaskComment

A threaded comment on a remediation task.

| Field | Type | Constraints | Default | Description |
|-------|------|-------------|---------|-------------|
| `Id` | `string` | PK, GUID format | `Guid.NewGuid().ToString()` | Unique comment identifier |
| `TaskId` | `string` | Required, FK→RemediationTask.Id, Indexed | `""` | Parent task |
| `AuthorId` | `string` | Required, MaxLength(200) | `""` | Comment author user ID |
| `AuthorName` | `string` | Required, MaxLength(200) | `""` | Comment author display name |
| `Content` | `string` | Required, MaxLength(4000) | `""` | Comment text (Markdown) |
| `CreatedAt` | `DateTime` | Required | `DateTime.UtcNow` | UTC creation timestamp |
| `EditedAt` | `DateTime?` | | `null` | UTC timestamp of last edit |
| `IsEdited` | `bool` | Required | `false` | Whether comment has been edited |
| `IsDeleted` | `bool` | Required | `false` | Soft delete flag (preserves audit trail) |
| `IsSystemComment` | `bool` | Required | `false` | True for auto-generated comments (validation results, status changes) |
| `ParentCommentId` | `string?` | MaxLength(100) | `null` | Parent comment ID for threading (single-level) |
| `Mentions` | `List<string>` | JSON-serialized | `new()` | @mentioned user IDs |

**Indexes**:
- `TaskId`
- `TaskId, CreatedAt` (composite — for chronological display)

**Relationships**:
- Comment → Task (required FK, cascade delete)
- Comment → ParentComment (optional self-reference for single-level threading)

**Validation Rules**:
- `Content` must be non-empty and ≤ 4000 characters
- Edit window: 24 hours from `CreatedAt` (enforced in service layer)
- Delete window: 1 hour from `CreatedAt` for non-officer users (enforced in service layer)
- Comments on "Done" tasks: no edit/delete allowed (enforced in service layer)
- `IsDeleted` is a soft delete — content is replaced with "[deleted]" but the row persists

---

### 4. TaskHistoryEntry

An immutable record of a change to a remediation task.

| Field | Type | Constraints | Default | Description |
|-------|------|-------------|---------|-------------|
| `Id` | `string` | PK, GUID format | `Guid.NewGuid().ToString()` | Unique history entry identifier |
| `TaskId` | `string` | Required, FK→RemediationTask.Id, Indexed | `""` | Parent task |
| `EventType` | `HistoryEventType` | Required | | Type of change |
| `OldValue` | `string?` | MaxLength(500) | `null` | Previous value (e.g., old status) |
| `NewValue` | `string?` | MaxLength(500) | `null` | New value (e.g., new status) |
| `ActingUserId` | `string` | Required, MaxLength(200) | `""` | User who made the change |
| `ActingUserName` | `string` | Required, MaxLength(200) | `""` | Display name of acting user |
| `Timestamp` | `DateTime` | Required | `DateTime.UtcNow` | UTC timestamp of the change |
| `Details` | `string?` | MaxLength(4000) | `null` | Additional context (blocker reason, validation results, error messages) |

**Indexes**:
- `TaskId`
- `TaskId, Timestamp` (composite — for chronological display)

**Relationships**:
- History → Task (required FK, cascade delete)

**Immutability**:
- No UPDATE operations on this table. Entries are INSERT-only.
- Service layer enforces immutability — no `EditHistory` method exists.
- `DELETE` only via cascade when parent task is deleted.

---

## Ephemeral Models (IAgentStateManager)

### 5. SavedView

A named filter combination stored per user in `IAgentStateManager`.

| Field | Type | Description |
|-------|------|-------------|
| `Name` | `string` | View name (e.g., "My Critical Items") |
| `OwnerId` | `string` | User who created the view |
| `BoardId` | `string` | Board this view applies to |
| `Filters` | `ViewFilters` | Filter criteria |
| `CreatedAt` | `DateTime` | When the view was saved |

### ViewFilters

| Field | Type | Description |
|-------|------|-------------|
| `AssigneeId` | `string?` | Filter by assignee |
| `Severities` | `List<FindingSeverity>?` | Filter by severity levels |
| `ControlFamilies` | `List<string>?` | Filter by control families |
| `Statuses` | `List<TaskStatus>?` | Filter by Kanban columns |
| `DueDateFrom` | `DateTime?` | Filter by due date range start |
| `DueDateTo` | `DateTime?` | Filter by due date range end |
| `CreatedFrom` | `DateTime?` | Filter by creation date range start |
| `CreatedTo` | `DateTime?` | Filter by creation date range end |
| `IsOverdue` | `bool?` | Filter for overdue tasks only |

### 6. NotificationConfig

Per-user notification preferences stored in `IAgentStateManager`.

| Field | Type | Description |
|-------|------|-------------|
| `UserId` | `string` | User identifier |
| `ChannelType` | `NotificationChannelType` | Email, Teams, or Slack |
| `ChannelAddress` | `string` | Email address or webhook URL |
| `EnabledEvents` | `List<NotificationEventType>` | Which events trigger notifications |
| `IsEnabled` | `bool` | Master enable/disable toggle |

---

## State Transitions

### TaskStatus State Machine

```text
                    ┌──────────────────────────────────────────┐
                    │                                          │
                    ▼                                          │
  ┌─────────┐   ┌──────┐   ┌────────────┐   ┌──────────┐   ┌──────┐
  │ Backlog │──►│ ToDo │──►│ InProgress │──►│ InReview │──►│ Done │
  └─────────┘   └──────┘   └────────────┘   └──────────┘   └──────┘
       │            │            │                │
       │            │            │                │
       │            ▼            ▼                │
       │       ┌─────────┐                        │
       └──────►│ Blocked │◄───────────────────────┘
               └─────────┘
                    │
                    │ (resolution comment required)
                    ▼
            Backlog, ToDo, InProgress, InReview
```

### Transition Rules

| From | To | Conditions | Side Effects |
|------|----|-----------|-------------|
| Backlog | ToDo | None | History entry |
| Backlog | InProgress | None | History entry; auto-assign if unassigned |
| Backlog | Blocked | Comment required | History entry; comment logged |
| ToDo | InProgress | None | History entry; auto-assign if unassigned |
| ToDo | Blocked | Comment required | History entry; comment logged |
| ToDo | Backlog | None | History entry |
| InProgress | InReview | None | History entry; trigger validation |
| InProgress | Blocked | Comment required | History entry; comment logged |
| InProgress | ToDo | None | History entry |
| InReview | Done | Validation pass OR CO explicit skip | History entry; close task |
| InReview | Blocked | Comment required | History entry; comment logged |
| InReview | InProgress | None | History entry |
| Blocked | Backlog | Resolution comment required | History entry |
| Blocked | ToDo | Resolution comment required | History entry |
| Blocked | InProgress | Resolution comment required | History entry |
| Blocked | InReview | Resolution comment required | History entry |
| Done | (none) | Terminal state — no outward transitions | — |

### Role Permissions Matrix

| Action | Compliance Officer | Security Lead | Platform Engineer | Auditor |
|--------|-------------------|---------------|-------------------|---------|
| Create board | ✅ | ✅ | ❌ | ❌ |
| Create task | ✅ | ✅ | ❌ | ❌ |
| Assign any task | ✅ | ✅ | ❌ | ❌ |
| Self-assign (unassigned) | ✅ | ❌ | ✅ | ❌ |
| Unassign self | ✅ | ❌ | ✅ | ❌ |
| Move own task | ✅ | ✅ | ✅ | ❌ |
| Move any task | ✅ | ✅ | ❌ | ❌ |
| Close without validation | ✅ | ❌ | ❌ | ❌ |
| Add comment | ✅ | ✅ | ✅ | ❌ |
| Edit own comment | ✅ | ✅ | ✅ | ❌ |
| Delete own comment | ✅ | ✅ | ✅ | ❌ |
| Delete any comment | ✅ | ❌ | ❌ | ❌ |
| View all tasks | ✅ | ✅ | Team only | ✅ |
| View history | ✅ | ✅ | Own tasks | ✅ |
| Export audit trail | ✅ | ✅ | ❌ | ✅ |
| Archive board | ✅ | ❌ | ❌ | ❌ |
| Export CSV | ✅ | ✅ | ❌ | ✅ |

---

## Default SLA Configuration

| Severity | Default Due Date Offset | Configurable Key |
|----------|------------------------|------------------|
| Critical | +24 hours | `Kanban:Sla:CriticalHours` |
| High | +7 days | `Kanban:Sla:HighDays` |
| Medium | +30 days | `Kanban:Sla:MediumDays` |
| Low | +90 days | `Kanban:Sla:LowDays` |

---

## EF Core Configuration Notes

- `AffectedResources` and `Mentions`: stored as JSON via `ValueConverter<List<string>, string>` (reuses existing converter pattern from `AtoCopilotContext`).
- `RowVersion` on `RemediationBoard` and `RemediationTask`: configured with `.IsConcurrencyToken()` (Guid-based, per research R-001).
- `SaveChangesAsync` override regenerates `RowVersion = Guid.NewGuid()` for all modified `ConcurrentEntity` entries.
- `TaskComment.IsDeleted` is a soft-delete — the row persists for audit trail but `Content` is replaced with `"[deleted]"`.
- `TaskHistoryEntry` rows are INSERT-only — the service layer exposes no edit or delete methods.
