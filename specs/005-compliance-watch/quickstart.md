# Quickstart: Compliance Watch (Feature 005)

**Date**: 2026-02-22  
**Spec**: [spec.md](spec.md)  
**Contracts**: [contracts/tool-responses.md](contracts/tool-responses.md)

## Prerequisites

- Feature 001 (Compliance Engine) operational — NIST controls seeded, assessment infrastructure working
- Feature 002 (Kanban) operational — board and task management available for integration
- Feature 004 (User Context) operational — IUserContext identity propagation in place
- 974 existing tests passing (`dotnet test`)
- Branch: `005-compliance-watch`

## Build & Test

```bash
# Build
dotnet build Ato.Copilot.sln

# Run all tests
dotnet test Ato.Copilot.sln

# Run only Feature 005 tests (once implemented)
dotnet test tests/Ato.Copilot.Tests.Unit --filter "FullyQualifiedName~ComplianceWatch"
dotnet test tests/Ato.Copilot.Tests.Integration --filter "FullyQualifiedName~ComplianceWatch"
```

## Implementation Order

### Phase 1: Core Entities & Infrastructure

1. **Enums** — `AlertStatus`, `AlertType`, `AlertSeverity`, `MonitoringFrequency`, `MonitoringMode`, `NotificationChannel`, `SuppressionType` in `ComplianceModels.cs`
2. **Entities** — `MonitoringConfiguration`, `ComplianceBaseline`, `ComplianceAlert`, `AlertRule`, `SuppressionRule`, `EscalationPath`, `AlertNotification`, `ComplianceSnapshot`, `AlertIdCounter` in `ComplianceModels.cs`
3. **DbContext** — Add 9 DbSets + entity configuration (indexes, converters, relationships) to `AtoCopilotContext`
4. **Interfaces** — `IComplianceWatchService`, `IAlertManager`, `IAlertNotificationService`, `IEscalationService`, `IAlertCorrelationService` in `ComplianceInterfaces.cs`
5. **Configuration** — `MonitoringOptions`, `AlertOptions`, `NotificationOptions`, `EscalationOptions` in `GatewayOptions.cs`

### Phase 2: Core Services

6. **AlertManager** — Alert CRUD, lifecycle transitions, ID generation, role-based access
7. **ComplianceWatchService** — Baseline capture, drift detection, monitoring run execution
8. **AlertNotificationService** — Multi-channel dispatch, rate limiting, quiet hours
9. **AlertCorrelationService** — Sliding window grouping, anomaly detection
10. **EscalationService** — SLA checking, escalation path execution

### Phase 3: Background Services

11. **ComplianceWatchHostedService** — Scheduled monitoring loop (1-min tick, DB-backed schedule)
12. **EscalationHostedService** — SLA expiry monitoring + escalation execution

### Phase 4: Tools

13. **Monitoring tools** — `watch_enable_monitoring`, `watch_disable_monitoring`, `watch_configure_monitoring`, `watch_monitoring_status`
14. **Alert tools** — `watch_show_alerts`, `watch_get_alert`, `watch_acknowledge_alert`, `watch_fix_alert`, `watch_dismiss_alert`
15. **Rules & suppression tools** — `watch_create_rule`, `watch_list_rules`, `watch_suppress_alerts`, `watch_list_suppressions`, `watch_configure_quiet_hours`
16. **Notification & escalation tools** — `watch_configure_notifications`, `watch_configure_escalation`
17. **History & query tools** — `watch_alert_history`, `watch_compliance_trend`, `watch_alert_statistics`
18. **Integration tools** — `watch_create_task_from_alert`, `watch_collect_evidence_from_alert`
19. **Auto-remediation tools** — `watch_create_auto_remediation_rule`, `watch_list_auto_remediation_rules`

### Phase 5: Registration & Integration

20. **Service registration** — Register all new services in `ServiceCollectionExtensions.cs`
21. **MCP exposure** — Add all tools to `ComplianceMcpTools.cs`
22. **Agent registration** — Register tools in `ComplianceAgent` constructor
23. **System prompt** — Update `ComplianceAgent.prompt.txt` with monitoring capabilities

### Phase 6: Testing & Polish

24. **Unit tests** — Services, tools, state transitions, correlation logic
25. **Integration tests** — MCP tool endpoints, background service behavior
26. **Performance validation** — Query pagination, bounded results, cancellation support

## Key Architectural Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Monitoring timer | Single BackgroundService, 1-min tick, DB schedule | Durable across restart, scales to many scopes |
| State machine | Enum + transition map | Matches existing patterns, no external dependency |
| Alert IDs | DB-backed date-partitioned counter | Concurrent-safe, human-readable, durable |
| Correlation | In-memory sliding windows | Transient 5-min windows don't need persistence |
| Event-driven | Activity Log polling (2-min interval) | Works in Azure Government, no infrastructure |
| Notification dispatch | Extended bounded channel | Reuses existing pattern, rate-limited |
| Rate limiting | .NET SlidingWindowRateLimiter | Built into .NET 9, no additional dependency |

## Service Lifetimes

| Service | Lifetime | Reason |
|---------|----------|--------|
| ComplianceWatchService | Singleton | Uses IDbContextFactory, stateless |
| AlertManager | Singleton | Uses IDbContextFactory, stateless |
| AlertNotificationService | Singleton | Bounded channel, background dispatch |
| AlertCorrelationService | Singleton | In-memory correlation windows |
| EscalationService | Singleton | Uses IDbContextFactory, stateless |
| ComplianceWatchHostedService | Hosted | BackgroundService for scheduled monitoring |
| EscalationHostedService | Hosted | BackgroundService for SLA monitoring |
| IComplianceEventSource | Singleton | Polling service, stateless |

## File Touchpoints

| File | Change Type | Description |
|------|------------|-------------|
| `Core/Models/Compliance/ComplianceModels.cs` | Extend | +7 enums, +9 entities |
| `Core/Interfaces/Compliance/ComplianceInterfaces.cs` | Extend | +5 interfaces |
| `Core/Configuration/GatewayOptions.cs` | Extend | +4 options classes |
| `Core/Data/Context/AtoCopilotContext.cs` | Extend | +9 DbSets, entity config |
| `Agents/Compliance/Services/ComplianceWatchService.cs` | New | Core monitoring engine |
| `Agents/Compliance/Services/AlertManager.cs` | New | Alert lifecycle management |
| `Agents/Compliance/Services/AlertNotificationService.cs` | New | Multi-channel dispatch |
| `Agents/Compliance/Services/AlertCorrelationService.cs` | New | Grouping + anomaly |
| `Agents/Compliance/Services/ComplianceWatchHostedService.cs` | New | Scheduled monitoring |
| `Agents/Compliance/Services/EscalationHostedService.cs` | New | SLA monitoring |
| `Agents/Compliance/Tools/ComplianceWatchTools.cs` | New | ~20 BaseTool implementations |
| `Agents/Extensions/ServiceCollectionExtensions.cs` | Extend | New service registrations |
| `Agents/Compliance/Agents/ComplianceAgent.cs` | Extend | RegisterTool() for new tools |
| `Agents/Compliance/Prompts/ComplianceAgent.prompt.txt` | Extend | Monitoring capabilities |
| `Mcp/Tools/ComplianceMcpTools.cs` | Extend | ~20 MCP method wrappers |
| `Tests.Unit/Services/ComplianceWatchServiceTests.cs` | New | Unit tests |
| `Tests.Unit/Services/AlertManagerTests.cs` | New | Unit tests |
| `Tests.Unit/Services/AlertNotificationServiceTests.cs` | New | Unit tests |
| `Tests.Unit/Services/AlertCorrelationServiceTests.cs` | New | Unit tests |
| `Tests.Unit/Services/EscalationServiceTests.cs` | New | Unit tests |
| `Tests.Unit/Tools/ComplianceWatchToolTests.cs` | New | Unit tests |
| `Tests.Integration/Tools/ComplianceWatchIntegrationTests.cs` | New | Integration tests |
