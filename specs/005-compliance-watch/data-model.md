# Data Model: Compliance Watch (Feature 005)

**Date**: 2026-02-22  
**Spec**: [spec.md](spec.md)  
**Research**: [research.md](research.md)

## Entity Overview

| Entity | Purpose | Storage | Lifecycle |
|--------|---------|---------|-----------|
| MonitoringConfiguration | Per-scope monitoring settings | EF Core table | Long-lived, user-managed |
| ComplianceBaseline | Point-in-time compliant resource snapshot | EF Core table | Created after assessments, replaced on re-baseline |
| ComplianceAlert | Detected compliance issue with lifecycle | EF Core table | Created by engine, transitions through states, retained 2–7 years |
| AlertRule | User-defined or default alerting rule | EF Core table | Long-lived, user-managed |
| SuppressionRule | Temporary or permanent alert suppression | EF Core table | Auto-expires (temp) or permanent with justification |
| EscalationPath | Notification chain for unacknowledged alerts | EF Core table | Long-lived, user-managed |
| AlertNotification | Record of a notification sent per channel | EF Core table | Append-only audit trail |
| ComplianceSnapshot | Periodic compliance state capture | EF Core table | Daily/weekly, retained 90 days / 2 years |
| AlertIdCounter | Date-partitioned sequence for ALT-YYYYMMDDNNNNN | EF Core table | One row per calendar date |

## Enums

### AlertStatus

```csharp
public enum AlertStatus
{
    New,
    Acknowledged,
    InProgress,
    Resolved,
    Dismissed,
    Escalated
}
```

**Valid Transitions**:
| From | To | Condition |
|------|----|-----------|
| New | Acknowledged | Any authenticated user |
| New | Escalated | System (SLA expiry) |
| New | Resolved | System (auto-resolve on compliance check) |
| Acknowledged | InProgress | Assigned user or Compliance Officer |
| Acknowledged | Dismissed | Compliance Officer (justification required) |
| Acknowledged | Escalated | System (SLA expiry) |
| Acknowledged | Resolved | System (auto-resolve) |
| InProgress | Resolved | System (auto-resolve) or user (manual) |
| InProgress | Escalated | System (SLA expiry) |
| Escalated | Acknowledged | Any authenticated user |
| Escalated | InProgress | Assigned user or Compliance Officer |
| Escalated | Resolved | System (auto-resolve) or user |
| Resolved | New | System (drift recurs) |

### AlertType

```csharp
public enum AlertType
{
    Drift,          // Resource config deviated from baseline
    Violation,      // New resource non-compliant
    Degradation,    // Score dropped below threshold
    Anomaly,        // Unusual pattern detected (actor correlation)
    Escalation,     // SLA expired, escalation triggered
    Resolution      // Auto-remediation applied successfully
}
```

### AlertSeverity

```csharp
public enum AlertSeverity
{
    Critical,   // SLA < 1 hour
    High,       // SLA < 4 hours
    Medium,     // SLA < 24 hours
    Low         // SLA < 7 days
}
```

### MonitoringFrequency

```csharp
public enum MonitoringFrequency
{
    FifteenMinutes,
    Hourly,
    Daily,
    Weekly
}
```

### MonitoringMode

```csharp
public enum MonitoringMode
{
    Scheduled,      // Periodic timer-based checks
    EventDriven,    // Triggered by platform events
    Both            // Combined scheduled + event-driven
}
```

### NotificationChannel

```csharp
public enum NotificationChannel
{
    Chat,       // In-app chat (always enabled)
    Email,      // Configurable email delivery
    Webhook     // Configurable webhook POST
}
```

### SuppressionType

```csharp
public enum SuppressionType
{
    Temporary,   // Auto-expires after duration
    Permanent    // Requires justification, visible to auditors
}
```

## Entities

### MonitoringConfiguration

Defines monitoring mode, frequency, and scope for a subscription or resource group.

```csharp
public class MonitoringConfiguration
{
    public Guid Id { get; set; }
    public string SubscriptionId { get; set; } = string.Empty;
    public string? ResourceGroupName { get; set; }          // null = entire subscription
    public MonitoringMode Mode { get; set; }
    public MonitoringFrequency Frequency { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset NextRunAt { get; set; }            // For scheduled monitoring
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? LastEventCheckAt { get; set; }    // High-water mark for event-driven
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

**Indexes**: `(NextRunAt, IsEnabled)` for background service poll, `(SubscriptionId, ResourceGroupName)` unique for scope identity.

**Validation**:
- `SubscriptionId` required, non-empty
- `Frequency` must be a valid enum value
- `NextRunAt` computed from `Frequency` on creation and after each run

---

### ComplianceBaseline

A point-in-time snapshot of a resource's compliant configuration, captured after successful assessment or remediation.

```csharp
public class ComplianceBaseline
{
    public Guid Id { get; set; }
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;       // Azure resource ID
    public string ResourceType { get; set; } = string.Empty;
    public string ConfigurationHash { get; set; } = string.Empty; // SHA-256 of configuration
    public string ConfigurationSnapshot { get; set; } = string.Empty; // JSON of relevant config properties
    public string? PolicyComplianceState { get; set; }            // JSON of policy compliance at baseline
    public Guid? AssessmentId { get; set; }                       // FK to assessment that established baseline
    public DateTimeOffset CapturedAt { get; set; }
    public bool IsActive { get; set; } = true;                    // false when superseded by newer baseline
}
```

**Indexes**: `(ResourceId, IsActive)` for drift comparison (only active baselines queried), `(SubscriptionId, CapturedAt)`.

**Relationships**: Optional FK to `ComplianceAssessment.Id` (existing entity).

**Validation**:
- `ResourceId` required, non-empty
- `ConfigurationHash` required, 64-character hex string
- Only one `IsActive = true` baseline per `ResourceId` at any time

---

### ComplianceAlert

A detected compliance issue with full lifecycle tracking.

```csharp
public class ComplianceAlert
{
    public Guid Id { get; set; }
    public string AlertId { get; set; } = string.Empty;          // Human-readable: ALT-YYYYMMDDNNNNN
    public AlertType Type { get; set; }
    public AlertSeverity Severity { get; set; }
    public AlertStatus Status { get; set; } = AlertStatus.New;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public List<string> AffectedResources { get; set; } = new(); // JSON-serialized
    public string? ControlId { get; set; }                        // e.g., "SC-8"
    public string? ControlFamily { get; set; }                    // e.g., "SC"
    public string? ChangeDetails { get; set; }                    // JSON: { property, oldValue, newValue }
    public string? ActorId { get; set; }                          // Who made the detected change
    public string? RecommendedAction { get; set; }
    public string? AssignedTo { get; set; }                       // User assigned to resolve
    public string? DismissalJustification { get; set; }           // Required when dismissed
    public string? DismissedBy { get; set; }
    public Guid? GroupedAlertId { get; set; }                     // FK to parent if this is a child in a correlated group
    public bool IsGrouped { get; set; }                           // true if this alert is a correlation parent
    public int ChildAlertCount { get; set; }                      // Number of correlated child alerts
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset? EscalatedAt { get; set; }
    public DateTimeOffset SlaDeadline { get; set; }               // Computed from Severity SLA

    // Navigation
    public ComplianceAlert? GroupedAlert { get; set; }
    public ICollection<ComplianceAlert> ChildAlerts { get; set; } = new List<ComplianceAlert>();
    public ICollection<AlertNotification> Notifications { get; set; } = new List<AlertNotification>();
}
```

**Indexes**: `(Status, Severity)`, `(SubscriptionId, CreatedAt)`, `(ControlFamily)`, `(AssignedTo, Status)`, `(AlertId)` unique, `(GroupedAlertId)`, `(SlaDeadline, Status)` for escalation checks.

**JSON Columns** (via ValueConverter):
- `AffectedResources` → `List<string>` ↔ JSON
- `ChangeDetails` → stored as raw JSON string

**Validation**:
- `AlertId` required, unique, matches pattern `ALT-\d{8}\d{5}`
- `Title` required, max 500 chars
- `Severity` + `Status` must follow valid transition rules
- `DismissalJustification` required when `Status == Dismissed`

---

### AlertRule

A user-defined or default rule that specifies alert conditions, severity overrides, and recipients.

```csharp
public class AlertRule
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SubscriptionId { get; set; }                   // null = all subscriptions
    public string? ResourceGroupName { get; set; }                // null = entire subscription
    public string? ResourceType { get; set; }                     // Filter by resource type
    public string? ResourceId { get; set; }                       // Specific resource
    public string? ControlFamily { get; set; }                    // e.g., "AC"
    public string? ControlId { get; set; }                        // e.g., "AC-2"
    public string? TriggerCondition { get; set; }                 // JSON expression for custom triggers
    public AlertSeverity? SeverityOverride { get; set; }          // Override default severity
    public List<string> RecipientOverrides { get; set; } = new(); // JSON-serialized
    public bool IsDefault { get; set; }                           // Pre-created default rule
    public bool IsEnabled { get; set; } = true;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

**Indexes**: `(SubscriptionId, ControlFamily, IsEnabled)`, `(IsDefault)`.

**Default Rules** (seeded on first enable):
| Control Family | Default Severity | Description |
|---------------|-----------------|-------------|
| AC | High | Access control changes |
| SC (encryption) | Critical | Encryption setting changes |
| AU (logging) | Critical | Audit logging disabled |
| IA (MFA) | Critical | MFA configuration changes |

---

### SuppressionRule

Temporary or permanent rule that mutes alerts for a defined scope.

```csharp
public class SuppressionRule
{
    public Guid Id { get; set; }
    public string? SubscriptionId { get; set; }
    public string? ResourceGroupName { get; set; }
    public string? ResourceId { get; set; }
    public string? ControlFamily { get; set; }
    public string? ControlId { get; set; }
    public SuppressionType Type { get; set; }
    public string? Justification { get; set; }                    // Required for permanent
    public DateTimeOffset? ExpiresAt { get; set; }                // Required for temporary
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    // Quiet Hours
    public TimeOnly? QuietHoursStart { get; set; }                // e.g., 22:00
    public TimeOnly? QuietHoursEnd { get; set; }                  // e.g., 06:00
}
```

**Indexes**: `(IsActive, ExpiresAt)` for cleanup, `(SubscriptionId, ResourceId)` for matching.

**Validation**:
- Permanent type requires `Justification` non-empty
- Temporary type requires `ExpiresAt` in the future
- Quiet hours: both start and end must be set, or neither

---

### EscalationPath

Chain of notification actions triggered when an alert is not acknowledged within SLA.

```csharp
public class EscalationPath
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AlertSeverity TriggerSeverity { get; set; }            // Which severity level triggers this path
    public int EscalationDelayMinutes { get; set; }               // Minutes after SLA before escalating
    public List<string> Recipients { get; set; } = new();         // JSON-serialized: user IDs or roles
    public NotificationChannel Channel { get; set; }              // Preferred notification channel
    public int RepeatIntervalMinutes { get; set; }                // How often to re-notify if still unacknowledged
    public int MaxEscalations { get; set; } = 3;                  // Stop after N escalation attempts
    public string? WebhookUrl { get; set; }                       // External integration URL
    public bool IsEnabled { get; set; } = true;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

**Indexes**: `(TriggerSeverity, IsEnabled)`.

**Validation**:
- `Recipients` must have at least one entry
- `EscalationDelayMinutes` must be > 0
- `RepeatIntervalMinutes` must be >= 5

---

### AlertNotification

Record of a notification sent through a specific channel for a specific alert.

```csharp
public class AlertNotification
{
    public Guid Id { get; set; }
    public Guid AlertId { get; set; }                             // FK to ComplianceAlert
    public NotificationChannel Channel { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public bool IsDelivered { get; set; }
    public string? DeliveryError { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }

    // Navigation
    public ComplianceAlert Alert { get; set; } = null!;
}
```

**Indexes**: `(AlertId, Channel)`, `(SentAt)`.

---

### ComplianceSnapshot

Periodic capture of overall compliance state for trend analysis.

```csharp
public class ComplianceSnapshot
{
    public Guid Id { get; set; }
    public string SubscriptionId { get; set; } = string.Empty;
    public double ComplianceScore { get; set; }                   // 0.0 – 100.0
    public int TotalControls { get; set; }
    public int PassedControls { get; set; }
    public int FailedControls { get; set; }
    public int TotalResources { get; set; }
    public int CompliantResources { get; set; }
    public int NonCompliantResources { get; set; }
    public int ActiveAlertCount { get; set; }
    public int CriticalAlertCount { get; set; }
    public int HighAlertCount { get; set; }
    public string? ControlFamilyBreakdown { get; set; }           // JSON: { family: { passed, failed } }
    public DateTimeOffset CapturedAt { get; set; }
    public bool IsWeeklySnapshot { get; set; }                    // Weekly snapshots retained longer
}
```

**Indexes**: `(SubscriptionId, CapturedAt)`, `(IsWeeklySnapshot, CapturedAt)` for retention cleanup.

**Retention**:
- Daily snapshots retained 90 days
- Weekly snapshots retained 2 years

---

### AlertIdCounter

Database-backed sequence for generating human-readable alert IDs.

```csharp
public class AlertIdCounter
{
    public DateOnly Date { get; set; }                            // PK: calendar date
    public int LastSequence { get; set; }                         // Atomically incremented
}
```

**Concurrency**: Increment within a serializable transaction to prevent duplicates.

---

### AutoRemediationRule

Opt-in rule that defines automatic remediation for trusted, low-risk violations.

```csharp
public class AutoRemediationRule
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SubscriptionId { get; set; }                   // null = all subscriptions
    public string? ResourceGroupName { get; set; }                // null = entire subscription
    public string? ControlFamily { get; set; }                    // Target control family (AC, IA, SC blocked)
    public string? ControlId { get; set; }                        // Target specific control
    public string Action { get; set; } = string.Empty;            // Remediation action description
    public string ApprovalMode { get; set; } = "require-approval"; // "auto" or "require-approval"
    public bool IsEnabled { get; set; } = true;
    public int ExecutionCount { get; set; }                       // Total times executed
    public DateTimeOffset? LastExecutedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

**Indexes**: `(SubscriptionId, ControlFamily, IsEnabled)`, `(IsEnabled)`.

**Validation**:
- `Name` required, max 200 chars
- `Action` required, non-empty
- `ControlFamily` MUST NOT be "AC", "IA", or "SC" (blocked families — always require human approval)
- `ApprovalMode` must be "auto" or "require-approval"

## Relationships

```
MonitoringConfiguration (1) ──── scope ────→ ComplianceBaseline (*)
ComplianceBaseline (*) ──── reference ────→ ComplianceAssessment (1) [optional FK]
MonitoringConfiguration (1) ──── scope ────→ ComplianceAlert (*)
ComplianceAlert (1) ──── parent ────→ ComplianceAlert (*) [self-ref: grouped alerts]
ComplianceAlert (1) ──── notifications ────→ AlertNotification (*)
AlertRule (*) ──── scope ────→ MonitoringConfiguration (logical, not FK)
SuppressionRule (*) ──── scope ────→ MonitoringConfiguration (logical, not FK)
EscalationPath (*) ──── triggers ────→ ComplianceAlert (logical, matched by severity)
AutoRemediationRule (*) ──── scope ────→ MonitoringConfiguration (logical, not FK)
```

## DbContext Changes

Add to `AtoCopilotContext`:

```csharp
public DbSet<MonitoringConfiguration> MonitoringConfigurations { get; set; }
public DbSet<ComplianceBaseline> ComplianceBaselines { get; set; }
public DbSet<ComplianceAlert> ComplianceAlerts { get; set; }
public DbSet<AlertRule> AlertRules { get; set; }
public DbSet<SuppressionRule> SuppressionRules { get; set; }
public DbSet<EscalationPath> EscalationPaths { get; set; }
public DbSet<AlertNotification> AlertNotifications { get; set; }
public DbSet<ComplianceSnapshot> ComplianceSnapshots { get; set; }
public DbSet<AlertIdCounter> AlertIdCounters { get; set; }
public DbSet<AutoRemediationRule> AutoRemediationRules { get; set; }
```

### EF Configuration Notes

- `ComplianceAlert.AffectedResources` — `ValueConverter<List<string>, string>` (same pattern as existing `AuditLogEntry.AffectedResources`)
- `AlertRule.RecipientOverrides` — `ValueConverter<List<string>, string>`
- `EscalationPath.Recipients` — `ValueConverter<List<string>, string>`
- `ComplianceAlert` self-referential: `GroupedAlertId` FK to `ComplianceAlert.Id`, cascade delete restricted
- `AlertNotification.AlertId` FK to `ComplianceAlert.Id`, cascade delete
- `AlertIdCounter.Date` as PK (no GUID for this entity)
- `MonitoringConfiguration` unique constraint on `(SubscriptionId, ResourceGroupName)` with null-safe handling
- `AutoRemediationRule` indexes on `(SubscriptionId, ControlFamily, IsEnabled)` and `(IsEnabled)`
