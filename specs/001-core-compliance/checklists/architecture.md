# Architecture Checklist: Core Compliance Capabilities

**Purpose**: Validate completeness, clarity, and consistency of architectural decisions, DI patterns, service boundaries, and data model
**Created**: 2026-02-21
**Feature**: [plan.md](../plan.md), [data-model.md](../data-model.md)

## Service Boundaries & Dependencies

- [x] CHK001 Are the 9 service interface responsibilities clearly separated with no overlapping concerns between services? [Clarity, Spec §FR-004]
- [x] CHK002 Is `IAtoComplianceEngine`'s orchestration responsibility defined — does it hold scan logic or only delegate to `INistControlsService`, `IAzurePolicyComplianceService`, and `IDefenderForCloudService`? [Clarity, Gap]
- [x] CHK003 Is the service lifetime (singleton vs scoped vs transient) specified for each of the 9 service implementations? [Completeness, Gap]
- [x] CHK004 Is the dependency graph between services acyclic — can any circular dependency arise from `AtoComplianceEngine` depending on services that depend on shared state? [Consistency, Gap]
- [x] CHK005 Are thread-safety requirements documented for services accessed concurrently (`ArmClient` singleton, `IAgentStateManager` ConcurrentDictionary)? [Completeness, Gap]

## Data Model Integrity

- [x] CHK006 Are all foreign key relationships between entities defined with cascade/restrict delete behavior? [Completeness, Spec §Data Model §Relationships]
- [x] CHK007 Is `RemediationStep` defined as an owned entity or a separate table? ("owned collection" vs FK relationship) [Ambiguity, Spec §Data Model §6-7]
- [x] CHK008 Is the `NistControl` self-referential relationship (parent → enhancements) defined with appropriate depth limit? [Clarity, Spec §Data Model §3]
- [x] CHK009 Are EF Core value conversions specified for `List<string>` properties (`AffectedResources`, `AffectedControls`, `AzurePolicyDefinitionIds`, `Baselines`)? [Completeness, Gap]
- [x] CHK010 Are database index requirements defined for frequently queried fields (`ControlId`, `AssessmentId`, `SubscriptionId`, `Timestamp`)? [Completeness, Gap]

## State Management

- [x] CHK011 Is the boundary between EF Core persistence and `IAgentStateManager` in-memory state clearly defined — which entities go where? [Clarity, Spec §Data Model §8]
- [x] CHK012 Are requirements defined for what happens to in-memory `ConfigurationSettings` when the server restarts? [Completeness, Gap]
- [x] CHK013 Is `IConversationStateManager` lifecycle defined — when does conversation state expire or get cleaned up? [Completeness, Gap]
- [x] CHK014 Are concurrency requirements defined for multiple users sharing the same `IAgentStateManager` state (config overwrite conflicts)? [Coverage, Gap]

## Agent Architecture

- [x] CHK015 Is the agent routing logic (intent → agent dispatch) specified with conflict resolution when an intent could match both agents? [Clarity, Spec §Contracts §Routing]
- [x] CHK016 Is the `BaseAgent` contract sufficient for `ConfigurationAgent` — does it need additional hooks for state management that `ComplianceAgent` doesn't use? [Consistency, Gap]
- [x] CHK017 Is the agent handoff protocol defined — does it re-route the same message or create a new tool invocation? [Clarity, Spec §FR-012]
- [x] CHK018 Are requirements defined for agent-to-agent communication (does ConfigurationAgent need to notify ComplianceAgent when subscription changes)? [Completeness, Gap]

## DbContext & Migrations

- [x] CHK019 Is the migration strategy defined for production upgrades (auto-migrate at startup vs. separate migration step)? [Clarity, Spec §R-006]
- [x] CHK020 Is the `AtoCopilotContext` provider-agnostic approach validated — do all queries work on both SQLite and SQL Server? [Consistency, Spec §R-006]
- [x] CHK021 Is seed data strategy defined for the `NistControl` table — migrate all 1,189 controls or lazy-load? [Clarity, Gap]
- [x] CHK022 Are requirements defined for database connection resiliency (retry policies for transient SQL failures)? [Completeness, Gap]

## Performance Architecture

- [x] CHK023 Is the `CancellationTokenSource` 60-second timeout applied at engine level or per-scan level (does a combined scan get 60s total or 60s per sub-scan)? [Ambiguity, Spec §FR-015]
- [x] CHK024 Are requirements defined for Resource Graph query batching (multiple Kusto queries in sequence vs parallel)? [Clarity, Gap]
- [x] CHK025 Is the pagination strategy (SkipToken for Resource Graph, paging for Policy) defined with buffer sizes? [Completeness, Spec §R-001]
- [x] CHK026 Are requirements defined for memory management during bulk operations (streaming vs buffering 1000-row pages)? [Completeness, Gap]

## Notes

- Check items off as completed: `[x]`
- Items tagged `[Gap]` indicate undocumented architectural decisions
- Items tagged `[Ambiguity]` indicate conflicting or unclear specifications
- Resolve architectural gaps before Phase 2 (Foundational) implementation to avoid rework
