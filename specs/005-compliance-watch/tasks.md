# Tasks: Compliance Watch

**Input**: Design documents from `/specs/005-compliance-watch/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/tool-responses.md, quickstart.md

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Exact file paths included in all descriptions

## Phase 1: Setup

**Purpose**: Enums, configuration options, and configuration binding

- [X] T001 Add 7 enums (AlertStatus, AlertType, AlertSeverity, MonitoringFrequency, MonitoringMode, NotificationChannel, SuppressionType) per data-model.md to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T002 Add 5 configuration options classes (MonitoringOptions, AlertOptions, NotificationOptions, EscalationOptions, RetentionPolicyOptions) to src/Ato.Copilot.Core/Configuration/GatewayOptions.cs
- [X] T003 Bind MonitoringOptions, AlertOptions, NotificationOptions, EscalationOptions, and RetentionPolicyOptions from configuration sections in src/Ato.Copilot.Core/Extensions/CoreServiceExtensions.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core alert entity, alert ID generation, alert lifecycle manager — ALL user stories depend on this

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Add ComplianceAlert entity (with self-referential GroupedAlertId FK, AffectedResources JSON column, all lifecycle timestamps) and AlertIdCounter entity per data-model.md to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T005 [P] Add IAlertManager interface (CreateAlertAsync, TransitionAlertAsync, GetAlertAsync, GetAlertsAsync with pagination, GenerateAlertIdAsync, DismissAlertAsync with justification) to src/Ato.Copilot.Core/Interfaces/Compliance/ComplianceInterfaces.cs
- [X] T006 [P] Add ComplianceAlerts and AlertIdCounters DbSets with entity configuration (indexes on Status+Severity, SubscriptionId+CreatedAt, ControlFamily, AlertId unique, SlaDeadline+Status; ValueConverter for AffectedResources List‹string›; self-referential FK with restricted cascade) to src/Ato.Copilot.Core/Data/Context/AtoCopilotContext.cs
- [X] T007 Implement AlertManager service (alert CRUD, lifecycle state machine via Dictionary‹AlertStatus, HashSet‹AlertStatus›› transition map, alert ID generation via AlertIdCounter with serializable transaction, role-based transition validation — only Compliance Officer can dismiss with justification, Auditor role has read-only access, auto-resolve on compliance check, SLA deadline computation from severity) in src/Ato.Copilot.Agents/Compliance/Services/AlertManager.cs
- [X] T008 Register AlertManager as Singleton with IAlertManager interface forwarding in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs
- [X] T009 [P] Unit tests for AlertManager — lifecycle state machine transitions (all valid + all invalid), alert ID generation (sequential within date, rollover on new date), role-based access (CO dismiss allowed, PE dismiss denied, Auditor read-only), SLA deadline computation per severity, boundary tests (empty justification, null parameters, max pagination) in tests/Ato.Copilot.Tests.Unit/Services/AlertManagerTests.cs

**Checkpoint**: AlertManager functional — alert CRUD, lifecycle transitions, and ID generation all working and tested

---

## Phase 3: User Story 1 — Scheduled Compliance Monitoring & Drift Detection (Priority: P1) 🎯 MVP

**Goal**: Enable configurable scheduled monitoring that captures compliant baselines, detects drift from those baselines, and creates alerts when violations are found

**Independent Test**: Enable hourly monitoring for a subscription. Run an assessment to capture a baseline. Simulate a configuration change that violates a control. Wait for the next scheduled check. Verify a drift alert is created with correct severity, control mapping, change details, and recommended action.

- [X] T010 [US1] Add MonitoringConfiguration entity (Id, SubscriptionId, ResourceGroupName, Mode, Frequency, IsEnabled, NextRunAt, LastRunAt, LastEventCheckAt, CreatedBy, timestamps) and ComplianceBaseline entity (Id, SubscriptionId, ResourceId, ResourceType, ConfigurationHash, ConfigurationSnapshot, PolicyComplianceState, AssessmentId FK, CapturedAt, IsActive) per data-model.md to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T011 [P] [US1] Add IComplianceWatchService interface (EnableMonitoringAsync, DisableMonitoringAsync, ConfigureMonitoringAsync, GetMonitoringStatusAsync, CaptureBaselineAsync, RunMonitoringCheckAsync, DetectDriftAsync) to src/Ato.Copilot.Core/Interfaces/Compliance/ComplianceInterfaces.cs
- [X] T012 [P] [US1] Add MonitoringConfigurations and ComplianceBaselines DbSets with entity configuration (unique index on SubscriptionId+ResourceGroupName, index on NextRunAt+IsEnabled, index on ResourceId+IsActive) to src/Ato.Copilot.Core/Data/Context/AtoCopilotContext.cs
- [X] T013 [P] [US1] Implement ComplianceWatchService (enable/disable/configure monitoring with NextRunAt computation from frequency, capture baseline after assessment via SHA-256 config hash including PolicyComplianceState, drift detection by comparing current resource config against active baseline for baseline drift AND comparing current PolicyComplianceState for compliance state drift AND polling secure scores via IAtoComplianceEngine to detect DEGRADATION when score drops below threshold, create DRIFT/VIOLATION/DEGRADATION alerts via IAlertManager with control mapping and change details, auto-resolve alerts when resource returns to baseline) in src/Ato.Copilot.Agents/Compliance/Services/ComplianceWatchService.cs
- [X] T014 [P] [US1] Implement ComplianceWatchHostedService as BackgroundService (1-minute tick interval via PeriodicTimer, query MonitoringConfigurations where NextRunAt ≤ UtcNow and IsEnabled, execute RunMonitoringCheckAsync per scope, advance NextRunAt by configured frequency, structured logging of run duration and alert counts, exponential backoff on connection failures with meta-alert creation per FR-042) in src/Ato.Copilot.Agents/Compliance/Services/ComplianceWatchHostedService.cs
- [X] T015 [P] [US1] Implement WatchEnableMonitoringTool (including permissions pre-check — validate required Azure permissions on enable and return structured capabilities report listing which monitoring features are available vs degraded per FR-043), WatchDisableMonitoringTool, WatchConfigureMonitoringTool, and WatchMonitoringStatusTool extending BaseTool with parameters and response contracts per contracts/tool-responses.md in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceWatchTools.cs
- [X] T016 [US1] Add MCP method wrappers for watch_enable_monitoring, watch_disable_monitoring, watch_configure_monitoring, and watch_monitoring_status with [Description] attributes and parameter mapping to src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs
- [X] T017 [US1] Register ComplianceWatchService as Singleton with IComplianceWatchService forwarding, register ComplianceWatchHostedService as hosted service, and register 4 monitoring tools as Singleton+BaseTool in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs; add tools to ComplianceAgent constructor and RegisterTool() calls in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs
- [X] T018 [P] [US1] Unit tests for ComplianceWatchService (baseline capture + hash comparison, drift detection positive/negative, frequency NextRunAt computation, auto-resolve on return to baseline, DEGRADATION alert on score threshold) and ComplianceWatchHostedService (tick fires on schedule, skips disabled configs, backoff on failure) in tests/Ato.Copilot.Tests.Unit/Services/ComplianceWatchServiceTests.cs; unit tests for 4 monitoring tools (enable/disable/configure/status — positive + error paths, permissions pre-check) in tests/Ato.Copilot.Tests.Unit/Tools/ComplianceWatchToolTests.cs

**Checkpoint**: Scheduled monitoring operational — baselines captured, drift detected, alerts created on schedule, all tested

---

## Phase 4: User Story 2 — Alert Lifecycle & Management (Priority: P1) 🎯 MVP

**Goal**: Users can view, filter, acknowledge, fix, and dismiss alerts through MCP tools with role-based access control

**Independent Test**: Create a test alert. View it via watch_show_alerts. Acknowledge it. Move it to IN_PROGRESS via watch_fix_alert. Verify all state transitions are logged and the alert reaches RESOLVED.

- [X] T019 [US2] Implement WatchShowAlertsTool (paginated list with severity/status/controlFamily/days filters), WatchGetAlertTool (full details with notifications and child alerts), WatchAcknowledgeAlertTool (transition to Acknowledged, pause escalation), WatchFixAlertTool (execute remediation via IAtoComplianceEngine, re-validate, transition to Resolved), and WatchDismissAlertTool (Compliance Officer only, require justification parameter, deny Platform Engineers with actionable error) extending BaseTool per contracts/tool-responses.md in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceWatchTools.cs
- [X] T020 [US2] Add MCP method wrappers for watch_show_alerts, watch_get_alert, watch_acknowledge_alert, watch_fix_alert, and watch_dismiss_alert to src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs
- [X] T021 [US2] Register 5 alert management tools as Singleton+BaseTool in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs; add tools to ComplianceAgent constructor and RegisterTool() calls in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs
- [X] T022 [US2] Update system prompt with monitoring and alert management capabilities (scheduled monitoring, drift detection, alert lifecycle, available watch_* commands) in src/Ato.Copilot.Agents/Compliance/Prompts/ComplianceAgent.prompt.txt
- [X] T023 [US2] Enforce Auditor read-only access (FR-031) — Auditors can invoke watch_show_alerts, watch_get_alert, watch_alert_history, watch_compliance_trend, watch_alert_statistics but MUST be denied access to write operations (acknowledge, dismiss, fix, create rule, suppress). Add role validation to all write tools returning INSUFFICIENT_PERMISSIONS with actionable error. Add negative integration tests for Auditor attempting write operations in tests/Ato.Copilot.Tests.Integration/Tools/ComplianceWatchIntegrationTests.cs
- [X] T024 [P] [US2] Unit tests for 5 alert management tools (show with filters, get details, acknowledge state transition, fix + remediation, dismiss CO-only + PE denied + Auditor denied, pagination boundaries, invalid alertId) in tests/Ato.Copilot.Tests.Unit/Tools/ComplianceWatchToolTests.cs; integration tests for alert lifecycle end-to-end (create → acknowledge → fix → resolve, dismiss flow, escalation trigger) in tests/Ato.Copilot.Tests.Integration/Tools/ComplianceWatchIntegrationTests.cs

**Checkpoint**: MVP complete — monitoring detects drift, creates alerts, users can view/acknowledge/fix/dismiss alerts through chat, role access enforced, all tested

---

## Phase 5: User Story 3 — Alert Rules & Suppression Configuration (Priority: P2)

**Goal**: Compliance Officers can create custom alert rules with severity overrides, configure temporary/permanent suppression, and set quiet hours

**Independent Test**: Create a custom rule via watch_create_rule for encryption changes with Critical severity. Trigger a change matching the rule. Verify alert fires with overridden severity. Create a suppression rule and verify subsequent alerts are suppressed.

- [X] T025 [US3] Add AlertRule entity (Name, scope fields, ControlFamily, ControlId, TriggerCondition JSON, SeverityOverride, RecipientOverrides JSON, IsDefault, IsEnabled) and SuppressionRule entity (scope fields, Type, Justification, ExpiresAt, IsActive, QuietHoursStart, QuietHoursEnd) per data-model.md to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T026 [US3] Add AlertRules and SuppressionRules DbSets with entity configuration (indexes on SubscriptionId+ControlFamily+IsEnabled, IsDefault, IsActive+ExpiresAt; ValueConverter for RecipientOverrides List‹string›) to src/Ato.Copilot.Core/Data/Context/AtoCopilotContext.cs
- [X] T027 [US3] Add alert rule matching (evaluate rules by scope+trigger, apply severity overrides, evaluate secure score threshold rules for DEGRADATION alerts per FR-004), suppression checking (match active suppressions by scope, check temporary expiration, respect quiet hours — hold non-Critical during quiet window, deliver Critical immediately per FR-019), and default rule seeding on first enable (AC High, SC-encryption Critical, AU-logging Critical, IA-MFA Critical per FR-016) to src/Ato.Copilot.Agents/Compliance/Services/ComplianceWatchService.cs
- [X] T028 [P] [US3] Implement WatchCreateRuleTool, WatchListRulesTool, WatchSuppressAlertsTool, WatchListSuppressionsTool, and WatchConfigureQuietHoursTool extending BaseTool per contracts/tool-responses.md in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceWatchTools.cs
- [X] T029 [US3] Add MCP method wrappers for watch_create_rule, watch_list_rules, watch_suppress_alerts, watch_list_suppressions, and watch_configure_quiet_hours to src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs
- [X] T030 [US3] Register 5 rules/suppression tools as Singleton+BaseTool in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs; add tools to ComplianceAgent constructor and RegisterTool() calls in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs
- [X] T031 [P] [US3] Unit tests for rule matching (scope matching, severity override, default rule seeding, secure score threshold evaluation), suppression logic (temporary expiration, permanent justification required, quiet hours hold/release, Critical bypass), and 5 rules/suppression tools (positive + error paths) in tests/Ato.Copilot.Tests.Unit/Tools/ComplianceWatchToolTests.cs

**Checkpoint**: Alert rules and suppression functional — custom rules apply severity overrides, suppressions mute alerts, quiet hours hold non-Critical notifications, all tested

---

## Phase 6: User Story 4 — Notification Channels & Escalation Paths (Priority: P2)

**Goal**: Multi-channel alert delivery (chat, email, webhook) with rate limiting, and automatic escalation when alerts exceed SLA

**Independent Test**: Configure email notification for Critical alerts. Trigger a Critical alert. Verify notification record created. Configure escalation path. Let an alert exceed SLA. Verify escalation occurs (additional recipients notified, alert status ESCALATED).

- [X] T032 [US4] Add EscalationPath entity (Name, TriggerSeverity, EscalationDelayMinutes, Recipients JSON, Channel, RepeatIntervalMinutes, MaxEscalations, WebhookUrl, IsEnabled) and AlertNotification entity (AlertId FK, Channel, Recipient, Subject, Body, IsDelivered, DeliveryError, SentAt, DeliveredAt) per data-model.md to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T033 [P] [US4] Add IAlertNotificationService interface (SendNotificationAsync, SendDigestAsync, GetNotificationsForAlertAsync) and IEscalationService interface (CheckEscalationsAsync, ConfigureEscalationPathAsync, GetEscalationPathsAsync) to src/Ato.Copilot.Core/Interfaces/Compliance/ComplianceInterfaces.cs
- [X] T034 [P] [US4] Add EscalationPaths and AlertNotifications DbSets with entity configuration (indexes on TriggerSeverity+IsEnabled, AlertId+Channel, SentAt; AlertNotification.AlertId FK to ComplianceAlert with cascade delete) to src/Ato.Copilot.Core/Data/Context/AtoCopilotContext.cs
- [X] T035 [US4] Implement AlertNotificationService (multi-channel dispatch — chat always enabled, email immediate for Critical/High + daily digest for Medium/Low, webhook with HMAC-SHA256 signed JSON payload per contracts/tool-responses.md Webhook Signing Specification; rate limiting via System.Threading.RateLimiting.SlidingWindowRateLimiter at max 10/minute/channel; quiet hours checking via SuppressionRule; AlertNotification record creation for audit; bounded channel for async dispatch) in src/Ato.Copilot.Agents/Compliance/Services/AlertNotificationService.cs
- [X] T036 [P] [US4] Implement EscalationHostedService as BackgroundService (periodic tick, query ComplianceAlerts where SlaDeadline ≤ UtcNow and Status is New/Acknowledged, match EscalationPaths by severity, transition alert to Escalated, send escalation notifications to configured recipients, respect MaxEscalations and RepeatIntervalMinutes) in src/Ato.Copilot.Agents/Compliance/Services/EscalationHostedService.cs
- [X] T037 [P] [US4] Implement WatchConfigureNotificationsTool (validate Security Lead role can configure escalation per FR-030) and WatchConfigureEscalationTool (Security Lead and Compliance Officer can configure escalation paths, Platform Engineers denied with actionable error per FR-030) extending BaseTool per contracts/tool-responses.md in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceWatchTools.cs
- [X] T038 [US4] Add MCP method wrappers for watch_configure_notifications and watch_configure_escalation to src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs
- [X] T039 [US4] Register AlertNotificationService as Singleton with IAlertNotificationService forwarding, register EscalationHostedService as hosted service, register 2 notification/escalation tools as Singleton+BaseTool in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs; add tools to ComplianceAgent constructor in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs
- [X] T040 [P] [US4] Unit tests for AlertNotificationService (multi-channel dispatch, rate limiting at boundary 10/min, quiet hours hold/bypass, webhook HMAC signing, digest aggregation, delivery error recording) in tests/Ato.Copilot.Tests.Unit/Services/AlertNotificationServiceTests.cs; unit tests for EscalationHostedService (SLA expiry detection, escalation path matching, MaxEscalations cap, repeat interval) in tests/Ato.Copilot.Tests.Unit/Services/EscalationServiceTests.cs; unit tests for notification/escalation tools (Security Lead access, PE denied) in tests/Ato.Copilot.Tests.Unit/Tools/ComplianceWatchToolTests.cs

**Checkpoint**: Multi-channel notifications operational — rate-limited dispatch, escalation on SLA expiry, notification audit trail, all tested

---

## Phase 7: User Story 5 — Event-Driven & Real-Time Monitoring (Priority: P2)

**Goal**: Detect compliance-relevant platform changes within 5 minutes via Activity Log polling and trigger immediate targeted compliance checks

**Independent Test**: Enable event-driven monitoring for a resource group. Create a new resource in that group. Verify within 5 minutes a compliance check runs and creates an alert if non-compliant.

- [X] T041 [US5] Add IComplianceEventSource interface (GetRecentEventsAsync returning list of ComplianceEvent records with EventType, ResourceId, ActorId, Timestamp) to src/Ato.Copilot.Core/Interfaces/Compliance/ComplianceInterfaces.cs
- [X] T042 [US5] Implement ActivityLogEventSource (poll Azure Activity Log via ArmClient at configurable interval default 2 minutes, filter for resource write/delete/policy/role events including policy assignment changes for policy drift detection per FR-004, track high-water mark LastEventCheckAt from MonitoringConfiguration, support dual-cloud Azure Government/Commercial) in src/Ato.Copilot.Agents/Compliance/Services/ActivityLogEventSource.cs
- [X] T043 [US5] Add event-driven monitoring support to ComplianceWatchHostedService (poll IComplianceEventSource for scopes with Mode=EventDriven or Both, trigger targeted compliance check on affected resources, detect policy drift via policy assignment change events per FR-004, advance LastEventCheckAt high-water mark) in src/Ato.Copilot.Agents/Compliance/Services/ComplianceWatchHostedService.cs
- [X] T044 [US5] Register ActivityLogEventSource as Singleton with IComplianceEventSource interface forwarding in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs
- [X] T045 [P] [US5] Unit tests for ActivityLogEventSource (event filtering, high-water mark advancement, dual-cloud URL construction) and event-driven hosted service flow (event triggers check, policy drift detection, fallback to scheduled when Activity Log unavailable per edge case) in tests/Ato.Copilot.Tests.Unit/Services/ComplianceWatchServiceTests.cs

**Checkpoint**: Event-driven monitoring operational — Activity Log changes trigger compliance checks within 5 minutes, policy drift detected, all tested

---

## Phase 8: User Story 6 — Alert Correlation & Noise Reduction (Priority: P3)

**Goal**: Group related alerts to reduce noise — same-resource grouping (5-min window), same-control grouping, actor anomaly detection

**Independent Test**: Simulate 5 changes to the same resource within 5 minutes. Verify one grouped alert created (not 5). Simulate one actor making 12 security changes in 5 minutes. Verify an ANOMALY alert is created.

- [X] T046 [US6] Add IAlertCorrelationService interface (CorrelateAlertAsync returning grouped alert or new alert, GetCorrelationWindowAsync, FinalizeExpiredWindowsAsync) to src/Ato.Copilot.Core/Interfaces/Compliance/ComplianceInterfaces.cs
- [X] T047 [US6] Implement AlertCorrelationService (in-memory ConcurrentDictionary‹string, CorrelationWindow› with correlation keys: "resource:{resourceId}", "control:{controlId}:{subscriptionId}", "actor:{actorId}"; 5-minute sliding window with expiry reset on new matches; anomaly detection at 10+ actor events; periodic sweep timer to finalize expired windows; update parent alert ChildAlertCount and IsGrouped; rate-limit detection at 50+ alerts in 5 minutes creating summary alert) in src/Ato.Copilot.Agents/Compliance/Services/AlertCorrelationService.cs
- [X] T048 [US6] Integrate IAlertCorrelationService into alert creation flow — call CorrelateAlertAsync before persisting new alerts in AlertManager, merge into existing grouped alert or create new parent in src/Ato.Copilot.Agents/Compliance/Services/AlertManager.cs
- [X] T049 [US6] Register AlertCorrelationService as Singleton with IAlertCorrelationService interface forwarding in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs
- [X] T050 [P] [US6] Unit tests for AlertCorrelationService (same-resource grouping within 5-min window, same-control grouping, actor anomaly at 10+ threshold, window expiry and finalization, alert storm detection at 50+ alerts, boundary: exactly 10 actor events, exactly 5-min window edge) in tests/Ato.Copilot.Tests.Unit/Services/AlertCorrelationServiceTests.cs

**Checkpoint**: Alert correlation active — duplicate notifications reduced, anomaly patterns detected, alert storms summarized, all tested

---

## Phase 9: User Story 7 — Compliance Dashboard Queries & Historical Reporting (Priority: P3)

**Goal**: Natural-language queries for alert history, compliance trends, and statistics; daily/weekly compliance snapshots for trend analysis

**Independent Test**: Accumulate alerts over several days. Ask "What drifted this week?" and verify accurate summary. Ask "Show alert statistics this month" and verify counts by severity and type.

- [X] T051 [US7] Add ComplianceSnapshot entity (SubscriptionId, ComplianceScore, TotalControls, PassedControls, FailedControls, TotalResources, CompliantResources, NonCompliantResources, ActiveAlertCount, CriticalAlertCount, HighAlertCount, ControlFamilyBreakdown JSON, CapturedAt, IsWeeklySnapshot) per data-model.md to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T052 [US7] Add ComplianceSnapshots DbSet with entity configuration (indexes on SubscriptionId+CapturedAt, IsWeeklySnapshot+CapturedAt for retention cleanup) to src/Ato.Copilot.Core/Data/Context/AtoCopilotContext.cs
- [X] T053 [US7] Add daily and weekly snapshot capture logic (capture daily snapshot at midnight UTC, promote to weekly on Sundays via IsWeeklySnapshot flag, query latest assessment + active alert counts to populate snapshot fields) to src/Ato.Copilot.Agents/Compliance/Services/ComplianceWatchHostedService.cs
- [X] T054 [P] [US7] Implement WatchAlertHistoryTool (query parsing via keyword/pattern matching for known query types — "drifted" maps to type=Drift filter, "dismissed" maps to status=Dismissed + actor filter, control family names map to controlFamily filter — with fallback to structured parameter filters for unrecognized queries; paginated results), WatchComplianceTrendTool (trend from ComplianceSnapshot data with direction indicators), and WatchAlertStatisticsTool (counts by severity, type, status; average resolution time; escalation count; auto-resolved count) extending BaseTool per contracts/tool-responses.md in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceWatchTools.cs
- [X] T055 [US7] Add MCP method wrappers for watch_alert_history, watch_compliance_trend, and watch_alert_statistics to src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs
- [X] T056 [US7] Register 3 query tools as Singleton+BaseTool in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs; add tools to ComplianceAgent constructor and RegisterTool() calls in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs
- [X] T057 [P] [US7] Unit tests for snapshot capture (daily creation, weekly promotion on Sundays, field population from assessment), history tool (keyword-to-filter mapping: "drifted"→Drift, "dismissed by John"→actor filter, fallback to structured params), trend tool (direction indicators, empty snapshot handling), statistics tool (aggregation accuracy, zero-alert period) in tests/Ato.Copilot.Tests.Unit/Tools/ComplianceWatchToolTests.cs

**Checkpoint**: Historical queries operational — trend analysis, drift summaries, and statistics available via chat, all tested

---

## Phase 10: User Story 8 — Integration with Existing Compliance Features (Priority: P3)

**Goal**: Alerts integrate with Kanban (create/auto-close tasks), evidence collection, compliance reports, and POA&M documents

**Independent Test**: Create a task from an alert via watch_create_task_from_alert. Verify Kanban task created with alert details. Resolve the alert and verify the task is auto-closed.

- [X] T058 [US8] Implement WatchCreateTaskFromAlertTool (create RemediationTask via IKanbanService with alert title, description, severity, control mapping pre-populated; link task to alert ID; error if task already exists for alert) and WatchCollectEvidenceFromAlertTool (capture alert details, timeline, and remediation steps as ComplianceEvidence via existing evidence service) extending BaseTool per contracts/tool-responses.md in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceWatchTools.cs
- [X] T059 [US8] Add auto-close linked Kanban task when alert transitions to Resolved (query RemediationTasks by linked alert ID, move to Done via IKanbanService) and capture alert state changes as AuditLogEntry evidence events (log transition actor, timestamp, from/to status) in src/Ato.Copilot.Agents/Compliance/Services/AlertManager.cs
- [X] T060 [US8] Add MCP method wrappers for watch_create_task_from_alert and watch_collect_evidence_from_alert to src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs
- [X] T061 [US8] Register 2 integration tools as Singleton+BaseTool in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs; add tools to ComplianceAgent constructor and RegisterTool() calls in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs
- [X] T062 [US8] Integrate monitoring data into compliance reports and POA&M documents (FR-038) — extend existing report generation service to include active alert counts, monitoring statistics, open alerts as findings with severity/control mapping/remediation status, and monitoring coverage summary in src/Ato.Copilot.Agents/Compliance/Services/ (relevant report service)
- [X] T063 [P] [US8] Unit tests for task-from-alert creation (pre-populated fields, duplicate prevention), auto-close on resolve (linked task moved to Done, no-op if no linked task), evidence capture (state change logged with actor/timestamp), report/POA&M integration (alert counts in report, open alerts as findings) in tests/Ato.Copilot.Tests.Unit/Tools/ComplianceWatchToolTests.cs

**Checkpoint**: Alert-to-Kanban integration operational — tasks created from alerts, auto-closed on resolution, state changes captured as evidence, monitoring data in reports, all tested

---

## Phase 11: User Story 9 — Automated Response (Priority: P3)

**Goal**: Opt-in auto-remediation rules for trusted, low-risk violations with safety guardrails (AC, IA, SC control families blocked)

**Independent Test**: Create an auto-remediation rule for missing tags. Trigger a missing-tag violation. Verify tags are automatically applied and remediation is logged as AuditLogEntry.

- [X] T064 [US9] Add AutoRemediationRule entity per data-model.md (Name, scope fields, ControlFamily, ControlId, Action, ApprovalMode, IsEnabled, ExecutionCount, LastExecutedAt) to src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs and add AutoRemediationRules DbSet with entity configuration (indexes on SubscriptionId+ControlFamily+IsEnabled, IsEnabled) to src/Ato.Copilot.Core/Data/Context/AtoCopilotContext.cs
- [X] T065 [US9] Implement auto-remediation engine in ComplianceWatchService (match alerts against AutoRemediationRules by scope+trigger, validate control family NOT in blocked set AC/IA/SC, create RemediationPlan with DryRun=true first, if dry-run succeeds execute via IAtoComplianceEngine.RemediateAsync, create RESOLUTION alert on success, move original alert back to New on failure with notification, create AuditLogEntry with action AutoRemediation recording rule+resource+outcome) in src/Ato.Copilot.Agents/Compliance/Services/ComplianceWatchService.cs
- [X] T066 [P] [US9] Implement WatchCreateAutoRemediationRuleTool (validate blocked families, create rule with approval mode) and WatchListAutoRemediationRulesTool (list rules with execution history) extending BaseTool per contracts/tool-responses.md in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceWatchTools.cs
- [X] T067 [US9] Add MCP method wrappers for watch_create_auto_remediation_rule and watch_list_auto_remediation_rules to src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs
- [X] T068 [US9] Register 2 auto-remediation tools as Singleton+BaseTool in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs; add tools to ComplianceAgent constructor and RegisterTool() calls in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs
- [X] T069 [P] [US9] Unit tests for auto-remediation engine (rule matching by scope, blocked family rejection for AC/IA/SC, dry-run-first flow, RESOLUTION alert on success, revert to New on failure, audit log entry creation) and tools (create rule validation, list with execution history) in tests/Ato.Copilot.Tests.Unit/Tools/ComplianceWatchToolTests.cs

**Checkpoint**: Auto-remediation operational — rules match violations, blocked families rejected, dry-run validation, execution logged for audit, all tested

---

## Phase 12: Polish & Cross-Cutting Concerns

**Purpose**: Retention, prompt finalization, documentation, build validation

- [X] T070 [P] Add snapshot retention cleanup (delete daily snapshots older than 90 days, weekly snapshots older than 2 years) and alert retention cleanup (configurable 2–7 years via RetentionPolicyOptions) to src/Ato.Copilot.Agents/Compliance/Services/RetentionCleanupHostedService.cs
- [X] T071 [P] Finalize ComplianceAgent.prompt.txt with complete Compliance Watch capabilities — this supersedes the incremental update in T022. Include all 23 watch_* tools, monitoring modes, alert lifecycle, rules, suppression, notifications, escalation, correlation, history, integration, auto-remediation in src/Ato.Copilot.Agents/Compliance/Prompts/ComplianceAgent.prompt.txt
- [X] T072 [P] Create user-facing documentation at docs/compliance-watch.md covering: monitoring setup (scheduled + event-driven), alert lifecycle and management, rules and suppression configuration, notification channels, escalation paths, quiet hours, historical queries, Kanban integration, auto-remediation safety guardrails, and troubleshooting (Constitution Quality Gate: "New features MUST update relevant /docs/*.md")
- [X] T073 Verify build with zero warnings: dotnet build Ato.Copilot.sln
- [X] T074 Run quickstart.md validation (build, test, verify monitoring + alert lifecycle + rule matching end-to-end)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**
- **US1 (Phase 3)**: Depends on Foundational — core monitoring engine
- **US2 (Phase 4)**: Depends on Foundational — alert management tools (functionally paired with US1 for MVP)
- **US3 (Phase 5)**: Depends on Foundational — rules/suppression extend monitoring
- **US4 (Phase 6)**: Depends on Foundational — notification/escalation are independent of monitoring engine
- **US5 (Phase 7)**: Depends on **US1** — extends ComplianceWatchHostedService with event-driven mode
- **US6 (Phase 8)**: Depends on Foundational — modifies AlertManager creation flow
- **US7 (Phase 9)**: Depends on Foundational — queries existing alert data
- **US8 (Phase 10)**: Depends on Foundational — integrates alerts with Kanban/evidence
- **US9 (Phase 11)**: Depends on **US1** — uses ComplianceWatchService for remediation execution
- **Polish (Phase 12)**: Depends on all desired user stories being complete

### User Story Independence

| Story | Can Start After | Independent? | Notes |
|-------|----------------|--------------|-------|
| US1 (P1) | Foundational | Yes | Core monitoring — no other story dependencies |
| US2 (P1) | Foundational | Yes | Alert tools use AlertManager from Foundational |
| US3 (P2) | Foundational | Yes | Rules/suppression are configuration entities |
| US4 (P2) | Foundational | Yes | Notification/escalation are standalone services |
| US5 (P2) | US1 | Partially | Extends ComplianceWatchHostedService from US1 |
| US6 (P3) | Foundational | Yes | Modifies AlertManager alert creation flow |
| US7 (P3) | Foundational | Yes | Queries alert data using AlertManager |
| US8 (P3) | Foundational | Yes | Uses IKanbanService (existing) + AlertManager |
| US9 (P3) | US1 | Partially | Uses ComplianceWatchService from US1 |

### Parallel Opportunities

After Foundational completes, these stories can proceed in parallel:
- **Track A**: US1 → US5 → US9 (monitoring pipeline)
- **Track B**: US2 → US6 (alert management pipeline)
- **Track C**: US3 (rules/suppression — standalone)
- **Track D**: US4 (notifications/escalation — standalone)
- **Track E**: US7 + US8 (queries and integration — standalone)

---

## Parallel Example: Phase 3 (User Story 1)

```bash
# After T010 completes, launch parallel group:
Task T011: "Add IComplianceWatchService interface"          # ComplianceInterfaces.cs
Task T012: "Add MonitoringConfigurations DbSets"            # AtoCopilotContext.cs

# After T011+T012 complete, launch parallel group:
Task T013: "Implement ComplianceWatchService"               # ComplianceWatchService.cs
Task T014: "Implement ComplianceWatchHostedService"         # ComplianceWatchHostedService.cs
Task T015: "Implement 4 monitoring tools"                   # ComplianceWatchTools.cs
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2)

1. Complete Phase 1: Setup (T001–T003)
2. Complete Phase 2: Foundational (T004–T009)
3. Complete Phase 3: User Story 1 — Monitoring & Drift (T010–T018)
4. Complete Phase 4: User Story 2 — Alert Lifecycle (T019–T024)
5. **STOP and VALIDATE**: Monitoring detects drift, users can manage alerts, all tests pass
6. Deploy/demo MVP

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. US1 + US2 → **MVP** (monitoring + alerts) → Deploy
3. US3 → Rules & suppression → Deploy
4. US4 → Notifications & escalation → Deploy
5. US5 → Event-driven monitoring → Deploy
6. US6 → Correlation & noise reduction → Deploy
7. US7 → Historical queries → Deploy
8. US8 → Kanban/evidence integration → Deploy
9. US9 → Auto-remediation → Deploy
10. Polish → Final validation

### Suggested MVP Scope

User Stories 1 + 2 (Phases 1–4, tasks T001–T024) deliver the minimum viable product:
- Configurable scheduled monitoring with drift detection
- Alert creation with human-readable IDs and full lifecycle
- User-facing tools for viewing, acknowledging, fixing, and dismissing alerts
- Role-based access (Compliance Officer vs Platform Engineer vs Auditor)
- Unit and integration tests for all services and tools

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks in same group
- [US#] label maps task to specific user story for traceability
- Each user story is independently completable and testable (except US5/US9 depend on US1)
- Each phase includes a test task covering the services and tools introduced in that phase (Constitution Principle III)
- Commit after each task or logical group
- All tools follow BaseTool pattern — Name, Description, Parameters, ExecuteCoreAsync
- All services are Singleton using IDbContextFactory for DB access
- Target: 80%+ unit test coverage (tests created alongside each service/tool implementation)
- All 4 roles validated: Compliance Officer (full access), Platform Engineer (restricted), Security Lead (escalation), Auditor (read-only)
