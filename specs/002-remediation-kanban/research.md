# Research: Remediation Kanban

**Branch**: `002-remediation-kanban` | **Date**: 2026-02-21

## R-001: Optimistic Concurrency Control (EF Core — SQLite + SQL Server)

**Decision**: Use a `Guid`-based concurrency token (`RowVersion` property) managed in application code, configured with `.IsConcurrencyToken()` in the Fluent API, and auto-regenerated on `SaveChangesAsync` via a `ChangeTracker` override.

**Rationale**: The `[Timestamp]` / `byte[]` approach relies on SQL Server's native `rowversion` column, but SQLite has no equivalent — the EF Core SQLite provider fakes it with `changes()` which is unreliable under pooled connections. A `Guid` token works identically across both providers because the value is managed in the application layer (EF Core change tracker), not the database engine.

**Alternatives considered**:
- `[Timestamp]` with `byte[]` — Works on SQL Server but unreliable on SQLite. Would require provider-conditional model building. Rejected.
- `DateTime LastModified` as `[ConcurrencyCheck]` — Fragile due to precision differences between providers. Rejected.
- `long` auto-increment version — Viable but adds complexity for atomic increments. Not needed at Kanban scale.
- Pessimistic locking (`SELECT FOR UPDATE`) — Not supported by SQLite, unnecessary for Kanban write volumes. Rejected.

**Key details**:
- Apply `ConcurrentEntity` base class to `RemediationBoard` and `RemediationTask` (the entities with concurrent write risk).
- Override `SaveChangesAsync` in `AtoCopilotContext` to regenerate `RowVersion = Guid.NewGuid()` for all modified `ConcurrentEntity` entries.
- Status transitions and assignments: **fail-fast** on `DbUpdateConcurrencyException` with conflict message showing current state.
- Adding comments: **retry (3x)** since comments are additive and don't conflict with other changes.
- Bulk operations: **per-task fail-fast with aggregate report** showing which succeeded and which conflicted.
- SQLite caveat: Set `PRAGMA journal_mode=WAL` for concurrent read support during writes.

---

## R-002: Multi-Channel Notifications (Email / Teams / Slack)

**Decision**: `Channel<NotificationMessage>` (bounded, 500 capacity) + `BackgroundService` dispatcher + per-channel `INotificationChannel` implementations (MailKit for email, `HttpClient` for Teams/Slack). Polly 8 resilience via `Microsoft.Extensions.Http.Resilience`.

**Rationale**: Fire-and-forget via `Task.Run` swallows exceptions and has no backpressure. `Channel<T>` is fully async, zero-allocation on hot path, and built into .NET. `BackgroundService` reads from the channel and dispatches to the appropriate channel implementation.

**Alternatives considered**:
- `Task.Run` / `_ = SendAsync()` — No backpressure, swallows exceptions. Rejected.
- `BlockingCollection<T>` — Synchronous API, not suitable for async I/O. Rejected.
- External queue (Azure Service Bus) — Over-engineered for notification volumes at this scale. Rejected.

**Key details**:

### Email
- Use **MailKit** (>= 4.10.0). `System.Net.Mail.SmtpClient` is `[Obsolete]` since .NET 6.
- MailKit's `SmtpClient` is NOT thread-safe — create one per send via the channel dispatcher.

### Teams
- POST Adaptive Card v1.4 JSON to incoming webhook URL.
- Schema: `{ "type": "message", "attachments": [{ "contentType": "application/vnd.microsoft.card.adaptive", "content": { ... } }] }`.
- Rate limit: ~4 messages/second per webhook. Retry on `429`.
- No extra NuGet — use existing `IHttpClientFactory`.

### Slack
- POST Block Kit JSON to incoming webhook URL.
- Schema: `{ "blocks": [ ... ] }` with optional `"text"` fallback.
- Rate limit: 1 message/second per webhook. Retry on `429` with `Retry-After` header.
- No extra NuGet — use existing `IHttpClientFactory`.

### Resilience
- `Microsoft.Extensions.Http.Resilience` (in-box .NET 9, wraps Polly 8) for retry/circuit-breaker on HTTP channels.
- 3 retries with exponential backoff + jitter for transient failures (429, 5xx).
- Permanent failures (400, 401): log and dead-letter to a database table, no retry.
- 10-second timeout per HTTP call; 30-second connect + 60-second send for SMTP.

### New NuGet packages: 2
- `MailKit` (>= 4.10.0) — SMTP email
- `Microsoft.Extensions.Http.Resilience` (>= 9.0.0) — Polly 8 integration

---

## R-003: CSV Export

**Decision**: Use `System.Text.StringBuilder` with manual CSV formatting. No external library needed at this scale (< 500 rows).

**Rationale**: At Kanban scale (≤ 500 tasks per board), manual CSV generation is simpler than adding a dependency like `CsvHelper`. The output format is straightforward: headers + one row per task with known columns.

**Alternatives considered**:
- `CsvHelper` NuGet — Full-featured CSV library. Overkill for ≤ 500 rows with fixed columns. Would add a dependency for a single export function. Rejected.
- `System.IO.StreamWriter` with manual formatting — Equivalent to StringBuilder approach but with I/O side effects. StringBuilder is more testable.

**Key details**:
- Escape fields containing commas, quotes, or newlines per RFC 4180.
- Columns: TaskId, Title, ControlId, Severity, Status, Assignee, DueDate, CreatedDate, LastUpdated, Description.
- Return as `string` from `IKanbanService.ExportBoardCsvAsync()`; the MCP tool wraps it in the response envelope.

---

## R-004: Task ID Sequencing

**Decision**: Store a `NextTaskNumber` integer on `RemediationBoard`. Increment atomically on task creation, protected by the board's concurrency token.

**Rationale**: Database sequences are SQL Server-specific. Auto-increment columns require `MAX(TaskNumber) + 1` queries which are race-prone. Storing the counter on the parent board row and protecting it with the same `Guid RowVersion` concurrency token is simpler, cross-provider, and consistent with the optimistic concurrency strategy.

**Alternatives considered**:
- Database sequence (`CREATE SEQUENCE`) — SQL Server only, not available on SQLite. Rejected.
- `MAX(TaskNumber) + 1` query — Race condition under concurrent task creation. Rejected.
- GUID-based task IDs (no sequential number) — Loses the user-friendly `REM-001` format. Rejected.

**Key details**:
- On task creation: read board → `taskNumber = board.NextTaskNumber` → create task with `$"REM-{taskNumber:D3}"` → increment `board.NextTaskNumber` → save (both board + task in one transaction).
- On `DbUpdateConcurrencyException`: reload board, get new number, retry.
- Format: `REM-001` through `REM-999`. If a board exceeds 999 tasks, switch to `REM-{n:D4}` (4-digit).

---

## R-005: Overdue Task Detection (Background Service)

**Decision**: Use `IHostedService` / `BackgroundService` with a periodic timer. The service creates a DI scope per tick and queries for overdue tasks.

**Rationale**: Standard .NET pattern for background work. `PeriodicTimer` (introduced .NET 6) provides a clean async timer without Timer callback threading issues.

**Alternatives considered**:
- `Timer` callback — Threading complexity, exception handling challenges. Rejected.
- External scheduler (Hangfire, Quartz.NET) — Overkill for a single periodic check. Rejected.
- On-demand check only (user runs "show overdue") — Missing proactive notifications. Rejected.

**Key details**:
- Check interval: configurable, default 5 minutes.
- Per tick: query `RemediationTasks` where `DueDate < DateTime.UtcNow && Status != Done && Status != Blocked`.
- For each newly-overdue task (not previously notified): enqueue a notification and add a system comment.
- Use `IServiceProvider.CreateScope()` to get a scoped `AtoCopilotContext` and `IKanbanService`.
- Track "already notified" via a `LastOverdueNotifiedAt` timestamp on the task to avoid repeat notifications.

---

## R-006: EF Core Migration Strategy

**Decision**: Single migration adding all four new entity tables in one step.

**Rationale**: All four entities (RemediationBoard, RemediationTask, TaskComment, TaskHistoryEntry) are a cohesive unit — none is useful without the others. A single migration keeps the migration history clean and avoids intermediate states.

**Alternatives considered**:
- One migration per entity — Creates unnecessary intermediate schema states. Boards without tasks, tasks without comments. Rejected.
- Manual SQL scripts — Loses EF Core migration tooling benefits (rollback, model snapshot). Rejected.

**Key details**:
- Migration name: `AddKanbanEntities`.
- Adds tables: `RemediationBoards`, `RemediationTasks`, `TaskComments`, `TaskHistoryEntries`.
- Indexes: `RemediationTasks` indexed on `BoardId`, `Status`, `AssigneeId`, `ControlId`, `DueDate`.
- Foreign keys: Task → Board (cascade delete); Comment → Task (cascade delete); History → Task (cascade delete).
- Existing tables are untouched — no modifications to `Assessments`, `Findings`, etc.
- `db.Database.MigrateAsync()` at startup applies automatically (existing pattern).

---

## R-007: Status Transition Rules Engine

**Decision**: Encode status transitions as a static dictionary of `(fromStatus, toStatus) → TransitionRule` where `TransitionRule` specifies required conditions (comment, role, validation).

**Rationale**: A data-driven transition table is more maintainable and testable than a large `switch` statement. New transitions or rule changes are a single dictionary update.

**Alternatives considered**:
- Large `switch`/`if-else` chain — Hard to test individual transitions, easy to miss edge cases. Rejected.
- State machine library (Stateless NuGet) — Adds a dependency for 6 states and ~20 transitions. Overkill. Rejected.
- Database-stored transitions — Over-engineered for fixed Kanban columns. Rejected.

**Key details**:

```
Allowed transitions:
  Backlog → ToDo, InProgress, Blocked
  ToDo → InProgress, Blocked, Backlog
  InProgress → InReview, Blocked, ToDo
  InReview → Done, Blocked, InProgress
  Blocked → Backlog, ToDo, InProgress, InReview (all require resolution comment)
  Done → (no outward transitions — terminal state)

Rules:
  → Blocked: requires blocker comment (any role)
  → Done: requires validation pass OR Compliance Officer explicit skip
  → InReview: triggers validation workflow
  Blocked →: requires resolution comment
```

- Implemented as `static readonly Dictionary<(TaskStatus From, TaskStatus To), TransitionRule>` in a `StatusTransitionEngine` helper class.
- `TransitionRule` record: `{ RequiresComment, RequiresValidation, RequiredRole?, AllowSkipValidation }`.
