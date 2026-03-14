# Tasks: Azure AI Foundry Agent Integration

**Input**: Design documents from `/specs/028-foundry-agents/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/foundry-provisioning.md, quickstart.md

**Tests**: Tests ARE included — spec SC-007 requires ≥ 6 new unit tests covering the Foundry processing flow; SC-009 requires fallback integration tests.

**Organization**: Tasks grouped by user story. P1 stories (US1, US2, US4) form the MVP; P2 stories (US3, US5, US6) add the full Foundry processing loop and resilience.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story (US1–US6) this task belongs to
- Exact file paths from plan.md project structure

---

## Phase 1: Setup

**Purpose**: NuGet dependency and unified configuration model

- [x] T001 Add `Azure.AI.Agents.Persistent` v1.1.0 NuGet package to `src/Ato.Copilot.Core/Ato.Copilot.Core.csproj` (FR-015)
- [x] T002 [P] Define `AiProvider` enum (`OpenAi=0`, `Foundry=1`) in `src/Ato.Copilot.Core/Configuration/GatewayOptions.cs`
- [x] T003 [P] Define `AzureAiOptions` class with all properties per data-model.md (Enabled, Provider, Endpoint, DeploymentName, ApiKey, UseManagedIdentity, CloudEnvironment, MaxCompletionTokens, MaxToolIterations, ConversationWindowSize, Temperature, FoundryProjectEndpoint, RunTimeoutSeconds, SystemPromptTemplate) and computed properties (`IsFoundry`, `IsConfigured`) in `src/Ato.Copilot.Core/Configuration/GatewayOptions.cs`
- [x] T004 Add `AzureAi` configuration section to `src/Ato.Copilot.Mcp/appsettings.json` with sensible defaults per quickstart.md
- [x] T005 Register `IOptions<AzureAiOptions>` binding from `AzureAi` config section in `src/Ato.Copilot.Core/Extensions/CoreServiceExtensions.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: BaseAgent constructor and DI wiring that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T006 Add `PersistentAgentsClient?` and `AzureAiOptions?` constructor parameters to `BaseAgent` in `src/Ato.Copilot.Agents/Common/BaseAgent.cs` — store as `private protected` fields `_foundryClient`, `_azureAiOptions` (FR-004)
- [x] T007 Add `ConcurrentDictionary<string, string> _threadMap` field and `string? _foundryAgentId` field to `BaseAgent` in `src/Ato.Copilot.Agents/Common/BaseAgent.cs` (FR-011)
- [x] T008 [P] Update `ComplianceAgent` constructor to accept optional `PersistentAgentsClient?` and `IOptions<AzureAiOptions>?`, unwrap `.Value` and pass to `base()` in `src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs` (FR-008)
- [x] T009 [P] Update `ConfigurationAgent` constructor to accept optional `PersistentAgentsClient?` and `IOptions<AzureAiOptions>?`, unwrap `.Value` and pass to `base()` in `src/Ato.Copilot.Agents/Configuration/Agents/ConfigurationAgent.cs` (FR-008)
- [x] T010 [P] Update `KnowledgeBaseAgent` constructor to accept optional `PersistentAgentsClient?` and `IOptions<AzureAiOptions>?`, unwrap `.Value` and pass to `base()` in `src/Ato.Copilot.Agents/KnowledgeBase/Agents/KnowledgeBaseAgent.cs` (FR-008)
- [x] T010a Update agent DI registrations in `src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs` — resolve optional `PersistentAgentsClient?` via `sp.GetService<PersistentAgentsClient>()` and `IOptions<AzureAiOptions>` via `sp.GetRequiredService<IOptions<AzureAiOptions>>()`, pass to agent constructors

**Checkpoint**: Build passes (`dotnet build Ato.Copilot.sln`), all existing tests pass (`dotnet test`) — zero regressions (FR-016)

---

## Phase 3: User Story 1 — Foundry Agent Client Wiring (Priority: P1) 🎯 MVP

**Goal**: Register `PersistentAgentsClient` as a singleton from `AzureAi:FoundryProjectEndpoint` with `DefaultAzureCredential` and Gov cloud support

**Independent Test**: Verify `PersistentAgentsClient` resolves from DI when valid config present; resolves to null when config missing

### Tests for US1

- [x] T011 [P] [US1] Unit test: `RegisterFoundryClient` registers singleton when `AzureAi:FoundryProjectEndpoint` is set — in `tests/Ato.Copilot.Tests.Unit/Extensions/CoreServiceExtensionsAiTests.cs`
- [x] T012 [P] [US1] Unit test: `RegisterFoundryClient` skips registration when `FoundryProjectEndpoint` is empty — in `tests/Ato.Copilot.Tests.Unit/Extensions/CoreServiceExtensionsAiTests.cs`
- [x] T013 [P] [US1] Unit test: `AzureAiOptions` defaults, binding from config section, `IsConfigured` / `IsFoundry` computed properties — in `tests/Ato.Copilot.Tests.Unit/Configuration/AzureAiOptionsTests.cs`
- [x] T013a [P] [US1] Unit test: `RegisterFoundryClient` with `CloudEnvironment=AzureUSGovernment` uses Gov authority host for `DefaultAzureCredential` — in `tests/Ato.Copilot.Tests.Unit/Extensions/CoreServiceExtensionsAiTests.cs` (FR-002)

### Implementation for US1

- [x] T014 [US1] Implement `RegisterFoundryClient` in `src/Ato.Copilot.Core/Extensions/CoreServiceExtensions.cs` — read `AzureAi:FoundryProjectEndpoint`, construct `PersistentAgentsClient` with `DefaultAzureCredential` using Gov/Commercial authority host from `AzureAi:CloudEnvironment`; skip silently when endpoint is empty (FR-001, FR-003)
- [x] T015 [US1] Call `RegisterFoundryClient(services, configuration)` from `AddAtoCopilotCore` in `src/Ato.Copilot.Core/Extensions/CoreServiceExtensions.cs`
- [x] T016 [US1] Add structured log event (Information) when `PersistentAgentsClient` is registered, including endpoint and cloud environment (FR-021)

**Checkpoint**: US1 independently testable — `PersistentAgentsClient` resolves from DI with valid config

---

## Phase 4: User Story 4 — AI Provider Selection (Priority: P1)

**Goal**: `AiProvider` enum controls routing in `TryProcessWithBackendAsync` — Foundry, OpenAi, or disabled

**Independent Test**: Set `AzureAi:Provider` to each value and `Enabled` to true/false, verify correct processing path is invoked

### Tests for US4

- [x] T017 [P] [US4] Unit test: `Provider=OpenAi` with no `IChatClient` returns null from `TryProcessWithBackendAsync` — in `tests/Ato.Copilot.Tests.Unit/Agents/FoundryAgentTests.cs`
- [x] T018 [P] [US4] Unit test: `Provider=Foundry` calls `TryProcessWithFoundryAsync` before `IChatClient` fallback — in `tests/Ato.Copilot.Tests.Unit/Agents/FoundryAgentTests.cs`
- [x] T019 [P] [US4] Unit test: `Enabled=false` skips both AI paths — in `tests/Ato.Copilot.Tests.Unit/Common/BaseAgentAiProcessingTests.cs`
- [x] T019a [P] [US4] Unit test: `Provider=Foundry` with null `PersistentAgentsClient` returns null from `TryProcessWithBackendAsync` and logs warning — in `tests/Ato.Copilot.Tests.Unit/Agents/FoundryAgentTests.cs` (US4.4)

### Implementation for US4

- [x] T020 [US4] Implement `TryProcessWithBackendAsync` dispatch in `src/Ato.Copilot.Agents/Common/BaseAgent.cs` — check `_azureAiOptions.Provider`: if `Foundry` call `TryProcessWithFoundryAsync` first then fallback; if `OpenAi` call `TryProcessWithAiAsync`; guard on `_azureAiOptions is { Enabled: true }` (FR-007, FR-019)
- [x] T021 [US4] Add structured log event (Warning) when fallback is triggered from Foundry → IChatClient or IChatClient → deterministic (FR-021)

**Checkpoint**: US4 independently testable — provider routing works for all enum values

---

## Phase 5: User Story 2 — Foundry Agent Provisioning (Priority: P1)

**Goal**: Each agent creates or reuses a Foundry agent at startup with correct prompt, model, and tool definitions

**Independent Test**: Start with valid Foundry config, verify 3 Foundry agents are provisioned with matching instructions and tools

### Tests for US2

- [x] T022 [P] [US2] Unit test: `ProvisionFoundryAgentAsync` with null client skips gracefully — in `tests/Ato.Copilot.Tests.Unit/Agents/FoundryAgentTests.cs`
- [x] T023 [P] [US2] Unit test: `ProvisionFoundryAgentAsync` with `Provider=OpenAi` skips provisioning — in `tests/Ato.Copilot.Tests.Unit/Agents/FoundryAgentTests.cs`
- [x] T024 [P] [US2] Unit test: `BuildFoundryToolDefinitions` returns empty list when no tools registered — in `tests/Ato.Copilot.Tests.Unit/Agents/FoundryAgentTests.cs`

### Implementation for US2

- [x] T025 [US2] Implement `BuildFoundryToolDefinitions` in `src/Ato.Copilot.Agents/Common/BaseAgent.cs` — convert each `BaseTool` to `FunctionToolDefinition` using `BuildToolJsonSchema()` with name, description, and `BinaryData.FromString(schema)` (FR-010, research R-003)
- [x] T026 [US2] Implement `ProvisionFoundryAgentAsync` in `src/Ato.Copilot.Agents/Common/BaseAgent.cs` — guard on `_azureAiOptions is { IsFoundry: true }`, list agents by name via `Administration.GetAgentsAsync()`, create or update, store `_foundryAgentId`; on failure set `_foundryAgentId = null` and log warning (FR-009, FR-018, research R-007)
- [x] T027 [US2] Add Foundry provisioning call in each concrete agent's initialization: `ComplianceAgent`, `ConfigurationAgent`, `KnowledgeBaseAgent` — call `ProvisionFoundryAgentAsync` when `IsFoundry` (FR-009)
- [x] T028 [US2] Add structured log events: agent provisioned (Information), agent updated (Information), provisioning failed (Warning) in `src/Ato.Copilot.Agents/Common/BaseAgent.cs` (FR-021)

**Checkpoint**: US2 independently testable — Foundry agents are provisioned with correct prompt and tools

---

## Phase 6: User Story 3 — Foundry Thread & Run Processing (Priority: P2)

**Goal**: Process user messages through Foundry thread/run API with RequiresAction tool dispatch and run polling

**Independent Test**: Send a message with `Provider=Foundry`, verify thread created, run executed, tool calls dispatched, natural language response returned

### Tests for US3

- [x] T029 [P] [US3] Unit test: `TryProcessWithFoundryAsync` with no `_foundryAgentId` returns null — in `tests/Ato.Copilot.Tests.Unit/Agents/FoundryAgentTests.cs`
- [x] T030 [P] [US3] Unit test: `TryProcessWithFoundryAsync` with no `_foundryClient` returns null — in `tests/Ato.Copilot.Tests.Unit/Agents/FoundryAgentTests.cs`

### Implementation for US3

- [x] T031 [US3] Implement `TryProcessWithFoundryAsync` in `src/Ato.Copilot.Agents/Common/BaseAgent.cs` — create or reuse thread from `_threadMap`, add user message via `CreateMessageAsync`, create run via `CreateRunAsync` with `_foundryAgentId` (FR-005)
- [x] T032 [US3] Implement run polling loop in `TryProcessWithFoundryAsync` — poll `GetRunAsync` with 1-second interval, enforce `AzureAi:RunTimeoutSeconds` timeout via `Stopwatch`, cancel run via `CancelRunAsync` on timeout (FR-006, FR-020)
- [x] T033 [US3] Implement `RequiresAction` tool dispatch in the polling loop — cast to `SubmitToolOutputsAction`, iterate `RequiredFunctionToolCall`, match to `BaseTool` by name, execute locally via `ExecuteAsync`, submit results via `SubmitToolOutputsToRunAsync`; handle unknown tools with error output (FR-013, research R-005)
- [x] T034 [US3] Implement run completion handling — on `Completed` status, read last assistant message via `GetMessagesAsync(Descending)`, return as `AgentResponse.Response`; on `Failed`/`Cancelled`/`Expired`, return null to trigger fallback (FR-014)
- [x] T035 [US3] Implement max tool iterations guard — track tool-call rounds, cancel run when `MaxToolIterations` exceeded, return summary in format: "Completed {N} of {M} tool calls before reaching the maximum iteration limit. Last tool executed: {toolName}." followed by last successful output if available (FR-006, US3.5)
- [x] T036 [US3] Add structured log events: run created (Information), tool dispatched (Debug), run completed (Information), run failed/timed out (Warning) in `src/Ato.Copilot.Agents/Common/BaseAgent.cs` (FR-021)

**Checkpoint**: US3 independently testable — full Foundry thread/run loop works end-to-end

---

## Phase 7: User Story 5 — Thread-to-Conversation Mapping (Priority: P2)

**Goal**: Map `ConversationId` → Foundry `ThreadId` for cross-message context continuity

**Independent Test**: Send multiple messages in same conversation, verify same Foundry thread reused; new conversation creates new thread

### Tests for US5

- [x] T037 [P] [US5] Unit test: Thread mapping stores and retrieves same thread for same conversation — in `tests/Ato.Copilot.Tests.Unit/Agents/FoundryAgentTests.cs`
- [x] T038 [P] [US5] Unit test: Different conversations get different threads — in `tests/Ato.Copilot.Tests.Unit/Agents/FoundryAgentTests.cs`
- [x] T038a [P] [US5] Unit test: Provider switch mid-conversation creates new Foundry thread for existing `ConversationId` after restart — in `tests/Ato.Copilot.Tests.Unit/Agents/FoundryAgentTests.cs` (US5.3)

### Implementation for US5

- [x] T039 [US5] Integrate thread lookup/creation into `TryProcessWithFoundryAsync` in `src/Ato.Copilot.Agents/Common/BaseAgent.cs` — `_threadMap.TryGetValue(ConversationId)` for reuse, `_threadMap[ConversationId] = threadId` on creation (FR-011)

**Checkpoint**: US5 independently testable — conversation context maintained across messages

---

## Phase 8: User Story 6 — Graceful Fallback & Degradation (Priority: P2)

**Goal**: Automatic fallback chain: Foundry → IChatClient → deterministic routing; zero regressions when Foundry unavailable

**Independent Test**: Run with no Foundry config and verify all agents function identically to pre-feature behavior

### Tests for US6

- [x] T040 [P] [US6] Integration test: Foundry fails → falls back to IChatClient → then deterministic — in `tests/Ato.Copilot.Tests.Integration/Agents/FoundryFallbackTests.cs`
- [x] T041 [P] [US6] Integration test: `Provider=OpenAi` skips Foundry, routes to IChatClient — in `tests/Ato.Copilot.Tests.Integration/Agents/FoundryFallbackTests.cs`
- [x] T042 [P] [US6] Integration test: Default provider (no config) returns null → deterministic — in `tests/Ato.Copilot.Tests.Integration/Agents/FoundryFallbackTests.cs`
- [x] T043 [P] [US6] Integration test: Foundry exception during run (including context-window overflow) → fallback triggered gracefully — in `tests/Ato.Copilot.Tests.Integration/Agents/FoundryFallbackTests.cs`

### Implementation for US6

- [x] T044 [US6] Add try/catch error handling around Foundry dispatch in `TryProcessWithBackendAsync` in `src/Ato.Copilot.Agents/Common/BaseAgent.cs` — catch Foundry exceptions, log warning with error details, fall back to `TryProcessWithAiAsync`; if that also returns null, return null for deterministic routing (FR-012)
- [x] T045 [US6] Ensure provisioning failure sets `_foundryAgentId = null` and logs warning — subsequent messages use fallback chain without errors in `src/Ato.Copilot.Agents/Common/BaseAgent.cs`
- [x] T046 [US6] Add structured log events for fallback chain: include exception type, message, and originating method in Warning logs; emit distinct events for Foundry → IChatClient vs IChatClient → deterministic transitions (FR-021)

**Checkpoint**: US6 independently testable — system functions identically with or without Foundry

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Config cleanup, Docker, documentation, and final validation

- [x] T047 [P] Add `AzureAi` environment variables to `docker-compose.mcp.yml` and `.env` per quickstart.md
- [x] T048 [P] Update user documentation in `docs/` — add Foundry configuration and `AiProvider` selection guide to `docs/getting-started/engineer.md` and architecture notes to `docs/architecture/overview.md`
- [x] T049 Add Feature 028 entry to `CHANGELOG.md`
- [x] T050 Run `dotnet build Ato.Copilot.sln` — verify 0 errors, 0 warnings in modified files
- [x] T051 Run `dotnet test Ato.Copilot.sln` — verify all unit tests pass (target: 4146+), all integration tests pass (target: 220+), 0 failures; confirms FR-016 (existing tests pass) and FR-017 (IChatClient path unchanged)
- [x] T052 Run `docker compose -f docker-compose.mcp.yml build && docker compose -f docker-compose.mcp.yml up -d` — verify all containers healthy
- [x] T053 Run quickstart.md validation — verify startup logs show Foundry agent provisioning (or graceful skip when unconfigured)
- [x] T053a Add XML documentation comments (`<summary>`, `<param>`, `<returns>`) to all new public types and members: `AiProvider`, `AzureAiOptions`, `TryProcessWithFoundryAsync`, `TryProcessWithBackendAsync`, `ProvisionFoundryAgentAsync`, `BuildFoundryToolDefinitions` (Constitution VI)

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1: Setup ──────────────────────────── No dependencies
Phase 2: Foundational ───────────────────── Depends on Phase 1
Phase 3: US1 Client Wiring (P1) ─────────── Depends on Phase 2
Phase 4: US4 Provider Selection (P1) ────── Depends on Phase 2 (can run ∥ Phase 3)
Phase 5: US2 Agent Provisioning (P1) ────── Depends on Phase 3 (US1)
Phase 6: US3 Thread & Run Processing (P2) ─ Depends on Phase 4 (US4) + Phase 5 (US2)
Phase 7: US5 Thread Mapping (P2) ────────── Depends on Phase 6 (US3)
Phase 8: US6 Fallback & Degradation (P2) ── Depends on Phase 4 (US4) (can run ∥ Phase 6)
Phase 9: Polish ─────────────────────────── Depends on all user stories
```

### User Story Dependencies

- **US1 (P1)**: Depends on Foundational (Phase 2) — provides `PersistentAgentsClient` registration
- **US4 (P1)**: Depends on Foundational (Phase 2) — provides `AiProvider` routing; can run **parallel to US1**
- **US2 (P1)**: Depends on **US1** — needs `PersistentAgentsClient` to provision agents
- **US3 (P2)**: Depends on **US2 + US4** — needs provisioned agents + provider routing
- **US5 (P2)**: Depends on **US3** — thread mapping is part of the run processing loop
- **US6 (P2)**: Depends on **US4** — fallback chain lives in `TryProcessWithBackendAsync`; can run **parallel to US3**

### Within Each User Story

1. Tests written first (verify they fail before implementation)
2. Models/infrastructure before services
3. Services before integration points
4. Structured logging added alongside implementation
5. Checkpoint validation before proceeding

### Parallel Opportunities

**Phase 1** (all [P] tasks):
- T002 `AiProvider` enum + T003 `AzureAiOptions` class

**Phase 2** (all [P] tasks):
- T008 ComplianceAgent + T009 ConfigurationAgent + T010 KnowledgeBaseAgent constructor updates

**Phase 3–4** (independent P1 stories):
- US1 (Client Wiring) can run **parallel** to US4 (Provider Selection)

**Phase 6–8** (after US2 complete):
- US6 (Fallback) can run **parallel** to US3 (Thread & Run Processing)

---

## Parallel Example: Phase 2 (Foundational)

```bash
# After T006+T007 complete (BaseAgent constructor + thread map fields):

# Launch all 3 concrete agent constructor updates together [P]:
Task T008: Update ComplianceAgent constructor (ComplianceAgent.cs)
Task T009: Update ConfigurationAgent constructor (ConfigurationAgent.cs)
Task T010: Update KnowledgeBaseAgent constructor (KnowledgeBaseAgent.cs)
# Then T010a: Update DI registrations (ServiceCollectionExtensions.cs)
```

---

## Implementation Strategy

### MVP First (P1 Stories: US1 + US4 + US2)

1. Complete Phase 1: Setup (T001–T005)
2. Complete Phase 2: Foundational (T006–T010)
3. Complete Phase 3: US1 — Client Wiring (T011–T016)
4. Complete Phase 4: US4 — Provider Selection (T017–T021)
5. Complete Phase 5: US2 — Agent Provisioning (T022–T028)
6. **STOP and VALIDATE**: All P1 stories functional, all existing tests pass
7. Deploy/demo MVP — agents can be provisioned and routing works

### Full Feature (P2 Stories)

8. Complete Phase 6: US3 — Thread & Run Processing (T029–T036)
9. Complete Phase 7: US5 — Thread Mapping (T037–T039)
10. Complete Phase 8: US6 — Fallback & Degradation (T040–T046)
11. Complete Phase 9: Polish (T047–T053)
12. **FINAL VALIDATION**: All tests pass, Docker healthy, quickstart verified

### Suggested MVP Scope

**MVP = US1 + US4 + US2** (Phases 1–5, Tasks T001–T028)
- `PersistentAgentsClient` registered from config
- `AiProvider` routing dispatches correctly
- Foundry agents provisioned at startup
- All existing tests still pass
- Foundation ready for P2 stories to add the full processing loop

---

## Notes

- Total tasks: **59** (53 original + T010a, T013a, T019a, T038a, T053a, E6 note on T043)
- Tasks per user story: US1=7, US2=7, US3=8, US4=6, US5=4, US6=7, Setup=5, Foundational=6, Polish=8
- Parallel opportunities: 8 identified (Phases 1, 2, 3∥4, 6∥8)
- Independent test criteria for each story documented at phase level
- All tasks follow checklist format: `- [ ] [TaskID] [P?] [Story?] Description with file path`
- Format validation: ✅ All 59 tasks have checkbox, ID, labels, and file paths
- Per research R-001: Use `PersistentAgentsClient` (not `AgentsClient`), from `Azure.AI.Agents.Persistent` package
- Per research R-004: `DefaultAzureCredential` only — no API key support in Foundry SDK
- Per research R-005: RequiresAction uses `SubmitToolOutputsAction` → `RequiredFunctionToolCall` type hierarchy
- Per data-model.md: No database changes — all state is config, in-memory, or Foundry server-side
