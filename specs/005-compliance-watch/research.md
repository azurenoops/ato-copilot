# Research: Compliance Watch (Feature 005)

**Date**: 2026-02-22  
**Status**: Complete  
**Spec**: [spec.md](spec.md)

## Research Topics

### 1. Background Monitoring Architecture

**Context**: The spec requires scheduled monitoring at configurable intervals (15min, hourly, daily, weekly) with per-scope independent schedules. The codebase already has `OverdueScanHostedService` and `RetentionCleanupHostedService` as `BackgroundService` examples.

**Decision**: Implement a single `ComplianceWatchHostedService : BackgroundService` with a fast tick interval (1 minute) that checks a database-backed schedule table to determine which scopes need a monitoring run. Each `MonitoringConfiguration` entity stores its `NextRunAt` timestamp. The service queries for configurations where `NextRunAt <= UtcNow`, executes checks, and advances `NextRunAt` by the configured interval.

**Rationale**: A single service with a fast tick is simpler than spawning one `BackgroundService` per scope (which would explode with scale). The database-backed schedule ensures durability across restarts — if the server restarts, overdue scans run immediately. This matches the existing `OverdueScanHostedService` pattern (periodic tick → query DB → act). One-minute tick provides sub-interval accuracy without busy-spinning.

**Alternatives Considered**:
- **Per-scope BackgroundService**: Rejected — unbounded service count, registration complexity, no persistence across restart.
- **Quartz.NET / Hangfire**: Rejected — heavyweight dependency for a simple timer pattern. The project already uses raw `BackgroundService` and doesn't need job persistence, retries, or dashboards beyond what EF provides.
- **System.Threading.Timer per scope**: Rejected — no persistence, lost on restart, hard to coordinate shutdown.

---

### 2. Alert Lifecycle State Machine

**Context**: Alerts follow NEW → ACKNOWLEDGED → IN_PROGRESS → RESOLVED / DISMISSED / ESCALATED. Role-based transition constraints (only Compliance Officer can dismiss). The codebase uses enums extensively (e.g., `FindingStatus`, `AssessmentStatus`, `RemediationStatus`).

**Decision**: Use an `AlertStatus` enum + a `TransitionAlert()` method on `IAlertManager` that validates transitions via a static transition map (`Dictionary<AlertStatus, HashSet<AlertStatus>>`). Role checks are enforced at the service layer before calling `TransitionAlert()`. No external state machine library.

**Rationale**: The project already uses this pattern — `RemediationStatus` has defined transitions enforced in service code, `FindingStatus` has similar validation. A formal state machine library (Stateless, etc.) adds a dependency for a 6-state machine that's easily expressed as a transition map. The existing codebase proves this approach works at this scale.

**Valid Transitions**:
```
NEW → ACKNOWLEDGED, ESCALATED, RESOLVED (auto-resolve)
ACKNOWLEDGED → IN_PROGRESS, DISMISSED, ESCALATED, RESOLVED (auto-resolve)
IN_PROGRESS → RESOLVED, ESCALATED
ESCALATED → ACKNOWLEDGED, IN_PROGRESS, RESOLVED
DISMISSED → (terminal)
RESOLVED → NEW (re-opened if drift recurs)
```

**Role Constraints**:
- Anyone: NEW → ACKNOWLEDGED
- Assigned user or Compliance Officer: ACKNOWLEDGED → IN_PROGRESS
- System only: → RESOLVED (auto-resolve), → ESCALATED (SLA expiry)
- Compliance Officer only: → DISMISSED (requires justification)

**Alternatives Considered**:
- **Stateless library**: Rejected — additional NuGet dependency for a simple 6-state machine.
- **Event-sourced state**: Rejected — over-engineering; the project uses mutable EF entities with audit logging, not event sourcing.

---

### 3. EF Core Entity Design for Alert System

**Context**: 8 new entities needed. Existing codebase stores JSON lists via `ValueConverter<List<string>, string>`. Existing entities use owned types for value objects (`ScanSummary`, `DocumentMetadata`). The `AtoCopilotContext` supports both SQLite and SQL Server.

**Decision**: Create separate tables for each of the 8 key entities. Use JSON columns (via `ValueConverter`) for variable-length properties: `AffectedResources` (List<string>), `ChangeDetails` (JSON object), `RecipientOverrides` (List<string>). Use owned types for embedded value objects (e.g., `ChangeDetail` with OldValue/NewValue). Create composite indexes for common query patterns.

**Indexing Strategy**:
- `ComplianceAlert`: Indexes on `(Status, Severity)`, `(SubscriptionId, CreatedAt)`, `(ControlFamily)`, `(AssignedTo)`, `(CreatedAt)` for historical queries
- `AlertNotification`: Index on `(AlertId, Channel)`, `(SentAt)`
- `MonitoringConfiguration`: Index on `(NextRunAt, IsEnabled)` for the background service poll
- `ComplianceBaseline`: Index on `(ResourceId, CapturedAt)` for drift comparison
- `ComplianceSnapshot`: Index on `(SubscriptionId, CapturedAt)` for trend queries

**Rationale**: Separate tables follow the existing pattern (separate `RemediationTask`, `TaskComment`, `TaskHistoryEntry` tables for Kanban). JSON columns for variable-length arrays follow the existing `ValueConverter<List<string>, string>` pattern. Separate tables enable efficient indexing and avoid the performance pitfalls of deeply nested JSON queries on SQLite.

**Alternatives Considered**:
- **Single alert table with JSON blobs**: Rejected — notification history, suppression rules, and escalation paths need independent querying and lifecycle.
- **EF Core 9 complex type mapping**: Would work for embedded objects but doesn't support collections as owned entities in SQLite; JSON converters are simpler and proven.

---

### 4. Notification Dispatch Architecture

**Context**: Existing `NotificationService` uses `Channel.CreateBounded<NotificationMessage>(500)` with a background dispatch loop that currently only logs. Need to extend to support chat, email, webhook with per-channel rate limiting and batching (daily digest for Medium/Low email).

**Decision**: Extend the existing `NotificationService` pattern. Add an `IAlertNotificationService` interface with channel-aware dispatch. Use the existing bounded channel pattern but with `AlertNotification` messages. Implement channel dispatchers as strategy delegates (chat → always inline, email → queue for immediate or digest batch, webhook → HTTP POST with HMAC signing). Rate limiting via a per-channel sliding window counter (in-memory `ConcurrentDictionary<string, SlidingWindowRateLimiter>`). Daily digest handled by a separate timer in the background service that flushes accumulated Medium/Low notifications.

**Rate Limiting Implementation**:
- Track message count per channel per minute using `System.Threading.RateLimiting.SlidingWindowRateLimiter` (built into .NET 9)
- When rate exceeded, buffer messages and deliver when window slides
- Critical alerts bypass rate limiting for chat channel but still count toward limits

**Rationale**: Extending the existing channel-based pattern maintains consistency. .NET 9's built-in `SlidingWindowRateLimiter` provides the rate limiting without external dependencies. The existing `NotificationService` already has the background dispatch loop infrastructure — this extends rather than replaces it.

**Alternatives Considered**:
- **Separate Channel<T> per notification channel**: Rejected — multiplies background processors and complicates shutdown.
- **External message queue (RabbitMQ, Azure Service Bus)**: Rejected — heavyweight infrastructure dependency for a feature that operates within a single process. Can be added later if scale demands.

---

### 5. Alert Correlation & Grouping

**Context**: Spec requires three correlation modes: same-resource (5-min window), same-control (group by control), same-actor (anomaly detection for many changes by one actor in 5 min). Rate limiting at 50+ in 5 min triggers alert storm summary.

**Decision**: Implement an `IAlertCorrelationService` with an in-memory sliding window using `ConcurrentDictionary<string, CorrelationWindow>` keyed by correlation key (resource ID, control ID, or actor ID). When an alert is created, check if an open correlation window exists for its key. If yes, merge into the grouped alert. If no, start a new window. Windows close after 5 minutes of inactivity (no new matching alerts). Use a single `System.Threading.Timer` to periodically sweep and finalize expired windows.

**Correlation Keys**:
- Same-resource: `"resource:{resourceId}"`
- Same-control: `"control:{controlId}:{subscriptionId}"`
- Same-actor: `"actor:{actorId}"`

**Window Lifecycle**:
1. First alert for a key → create `CorrelationWindow` with 5-minute expiry, create the parent grouped alert in DB
2. Subsequent alerts within window → add to the group (update grouped alert's child list), reset expiry
3. Expiry reached → finalize (no more additions), window removed from memory

**Anomaly Detection**: If `actor:{actorId}` window accumulates 10+ security-related changes, promote to ANOMALY alert type regardless of individual severities.

**Rationale**: In-memory windows are appropriate because correlation is transient (5-minute windows don't need persistence). The project already uses `ConcurrentDictionary` for state management (see `InMemoryStateManagers`). Periodic sweep is simpler than per-window timers and bounds resource usage.

**Alternatives Considered**:
- **Database-backed windows**: Rejected — 5-minute transient windows don't justify DB round-trips on every alert creation. If the server restarts, new windows start fresh (acceptable — missed correlation during restart is a minor edge case).
- **Rx.NET observable streams**: Rejected — adds a dependency and programming model unfamiliar to the codebase.

---

### 6. Event-Driven Monitoring Integration

**Context**: Spec requires near-real-time (within 5 minutes) detection of platform events. The system uses Azure.ResourceManager for cloud interaction. Must support Azure Government.

**Decision**: Abstract event detection behind an `IComplianceEventSource` interface with a polling implementation that queries Azure Activity Log via the `Azure.ResourceManager` Activity Log API at a configurable interval (default: 2 minutes). The polling implementation tracks a high-water mark (`LastProcessedEventTimestamp`) in the database to avoid reprocessing. When qualifying events are detected (resource write, policy change, role assignment change), the service triggers an immediate targeted compliance check on the affected scope.

**Interface**:
```csharp
public interface IComplianceEventSource
{
    Task<IReadOnlyList<ComplianceEvent>> GetRecentEventsAsync(
        string subscriptionId, DateTimeOffset since, CancellationToken ct);
}
```

**Rationale**: Activity Log polling is the most portable approach — it works identically in Azure Government and Azure Commercial, requires no additional infrastructure (Event Grid, Event Hubs), and the 2-minute poll interval comfortably achieves the 5-minute SLA. The abstraction allows future replacement with Event Grid push without changing consumers. The existing `ArmClient` singleton already handles dual-cloud authentication.

**Alternatives Considered**:
- **Azure Event Grid subscriptions**: Better latency but requires infrastructure provisioning (Event Grid topic, subscription, webhook endpoint), which conflicts with the simplicity of the current stdio/HTTP dual-mode deployment. Future enhancement candidate.
- **Azure Monitor Action Groups**: Rejected — designed for Azure Monitor alerting, not for feeding events into custom processing. Would require managing alert rules in Azure alongside the application's own rules.
- **Change feed / Resource Graph change queries**: Azure Resource Graph changes API is another option but has availability limitations in Azure Government.

---

### 7. Alert ID Generation

**Context**: Spec requires human-readable IDs in format `ALT-YYYYMMDDNNNNN` (e.g., `ALT-2026022200001`). Must handle concurrent alert creation.

**Decision**: Use a database-backed date-partitioned counter. Store a `AlertIdCounter` row with `(Date, LastSequence)`. When generating an ID, use a transaction to atomically increment the sequence for today's date. The `NNNNN` portion is zero-padded to 5 digits (supports up to 99,999 alerts per day). The `AlertId` property on `ComplianceAlert` is the formatted string, while `Id` remains a GUID primary key for internal references.

**Implementation**:
```csharp
// In AlertManager.GenerateAlertIdAsync():
// SELECT LastSequence FROM AlertIdCounters WHERE Date = @today
// UPDATE SET LastSequence = LastSequence + 1 (or INSERT if first of day)
// Return $"ALT-{today:yyyyMMdd}{sequence:D5}"
```

**Rationale**: Database-backed ensures durability and handles concurrent alert creation through database-level locking. Daily reset keeps numbers manageable. 5-digit sequence supports high-volume days. This is simpler than a distributed ID generator and appropriate for single-instance deployment.

**Alternatives Considered**:
- **In-memory atomic counter**: Rejected — resets on restart, could produce duplicates.
- **GUID-only**: Rejected — spec explicitly requires human-readable `ALT-YYYYMMDDNNNNN` format.
- **Snowflake/ULID**: Rejected — not human-readable in the required format.

---

### 8. Auto-Remediation Safety

**Context**: Spec requires opt-in auto-remediation with blocked control families (AC, IA, SC), audit logging, and failure handling. Existing `RemediationExecuteTool` already executes remediation via `IAtoComplianceEngine`.

**Decision**: Implement auto-remediation as an extension of the existing remediation flow. `AutoRemediationRule` entities define scope, trigger, and action. When an alert matches an auto-remediation rule, the system:
1. Validates the control family is NOT in the blocked list (AC, IA, SC)
2. Creates a `RemediationPlan` with `DryRun = true` first
3. If dry-run succeeds, executes the remediation (delegates to `IAtoComplianceEngine.RemediateAsync()`)
4. Creates an `AuditLogEntry` with action `AutoRemediation` recording the rule, resource, and outcome
5. If remediation fails, moves the alert back to NEW and sends a notification indicating manual intervention needed

**Blocked Control Families**:
```csharp
private static readonly HashSet<string> BlockedFamilies = new() { "AC", "IA", "SC" };
```

**Rationale**: Reusing the existing `IAtoComplianceEngine` remediation path ensures consistency and audit coverage. Dry-run-first adds a safety layer. The blocked list is hardcoded per spec (not configurable — these are always dangerous to auto-remediate). Failure → NEW status ensures the alert resurfaces for human attention.

**Alternatives Considered**:
- **Approval workflow**: The spec mentions an "approval mode" option but keeps it simple — auto-apply or require approval. Human approval for high-risk families is enforced by blocking auto-remediation entirely for AC/IA/SC, which is simpler and safer than building an approval queue.
- **Separate remediation engine**: Rejected — the existing engine handles all remediation types. Auto-remediation is a trigger mechanism, not a new engine.
