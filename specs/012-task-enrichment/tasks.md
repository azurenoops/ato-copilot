# Tasks: AI-Powered Task Enrichment — Remediation Scripts & Validation Criteria

**Input**: Design documents from `/specs/012-task-enrichment/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are included — spec.md §Testing Strategy explicitly defines 17+ test cases across 3 new test files plus existing file updates.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Data model changes, new interface, and result models shared across all user stories

- [x] T001 Add `RemediationScriptType` string property to `RemediationTask` in `src/Ato.Copilot.Core/Models/Kanban/KanbanModels.cs`
- [x] T002 [P] Add `TaskEnrichmentResult` model class to `src/Ato.Copilot.Core/Models/Kanban/KanbanModels.cs`
- [x] T003 [P] Add `BoardEnrichmentResult` model class to `src/Ato.Copilot.Core/Models/Kanban/KanbanModels.cs`
- [x] T004 Add EF configuration for `RemediationScriptType` column (`HasMaxLength(20)`) in `src/Ato.Copilot.Core/Data/Context/AtoCopilotContext.cs`
- [x] T005 Create `ITaskEnrichmentService` interface with `EnrichTaskAsync`, `EnrichBoardTasksAsync`, and `GenerateValidationCriteriaAsync` in `src/Ato.Copilot.Core/Interfaces/Kanban/ITaskEnrichmentService.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core `TaskEnrichmentService` implementation — MUST be complete before ANY user story integration

**⚠️ CRITICAL**: All user story phases depend on this service being fully implemented and tested

- [x] T006 Create `TaskEnrichmentService` class with constructor injecting `IRemediationEngine`, `IChatClient?`, `IOptions<AzureOpenAIGatewayOptions>`, `ILogger<TaskEnrichmentService>` in `src/Ato.Copilot.Agents/Compliance/Services/TaskEnrichmentService.cs`
- [x] T007 Implement `EnrichTaskAsync` — skip logic (already has script + force=false), null finding guard, Informational severity static strings (extract as `const` fields: `InformationalRemediationMessage`, `InformationalValidationMessage` per Constitution VI), delegate to `IRemediationEngine.GenerateRemediationScriptAsync`, set `RemediationScript`/`RemediationScriptType`/`ValidationCriteria` in `src/Ato.Copilot.Agents/Compliance/Services/TaskEnrichmentService.cs`
- [x] T008 Implement `GenerateValidationCriteriaAsync` — dedicated AI prompt via `IChatClient` when available, template fallback string when AI unavailable, in `src/Ato.Copilot.Agents/Compliance/Services/TaskEnrichmentService.cs`
- [x] T009 Implement `EnrichBoardTasksAsync` — build findingId→finding lookup, `SemaphoreSlim(5)` bounded concurrency, per-task 30s timeout via linked `CancellationTokenSource`, `IProgress<string>` reporting, aggregate `BoardEnrichmentResult` in `src/Ato.Copilot.Agents/Compliance/Services/TaskEnrichmentService.cs`
- [x] T010 Register `ITaskEnrichmentService` as scoped (`AddScoped<ITaskEnrichmentService, TaskEnrichmentService>()`) in `src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs`

### Tests for Foundational Phase

- [x] T011 [P] Test `EnrichTask_GeneratesScript_WhenScriptIsNull` — mock `IRemediationEngine.GenerateRemediationScriptAsync`, verify `task.RemediationScript` and `task.RemediationScriptType` set in `tests/Ato.Copilot.Tests.Unit/Services/TaskEnrichmentServiceTests.cs`
- [x] T012 [P] Test `EnrichTask_GeneratesValidationCriteria_WhenCriteriaIsNull` — verify `task.ValidationCriteria` populated in `tests/Ato.Copilot.Tests.Unit/Services/TaskEnrichmentServiceTests.cs`
- [x] T013 [P] Test `EnrichTask_SkipsScript_WhenAlreadyPresent_AndForceIsFalse` — verify no call to `IRemediationEngine`, verify `Skipped=true` in `tests/Ato.Copilot.Tests.Unit/Services/TaskEnrichmentServiceTests.cs`
- [x] T014 [P] Test `EnrichTask_RegeneratesScript_WhenForceIsTrue` — verify `IRemediationEngine` called even when existing script present in `tests/Ato.Copilot.Tests.Unit/Services/TaskEnrichmentServiceTests.cs`
- [x] T015 [P] Test `EnrichTask_SetsInformationalFallback_WhenSeverityIsInformational` — verify static strings set, no AI call in `tests/Ato.Copilot.Tests.Unit/Services/TaskEnrichmentServiceTests.cs`
- [x] T016 [P] Test `EnrichTask_SkipsEnrichment_WhenFindingIsNull` — verify `Skipped=true`, no AI call in `tests/Ato.Copilot.Tests.Unit/Services/TaskEnrichmentServiceTests.cs`
- [x] T017 [P] Test `EnrichTask_UsesFallback_WhenAiUnavailable` — mock `AgentAIEnabled=false`, verify template-based script returned in `tests/Ato.Copilot.Tests.Unit/Services/TaskEnrichmentServiceTests.cs`
- [x] T018 [P] Test `EnrichTask_HandlesAiFailure_Gracefully` — throw from `GenerateRemediationScriptAsync`, verify fallback used and error logged in `tests/Ato.Copilot.Tests.Unit/Services/TaskEnrichmentServiceTests.cs`
- [x] T019 [P] Test `GenerateValidationCriteria_UsesAi_WhenAvailable` — verify AI prompt sent to `IChatClient` with finding context in `tests/Ato.Copilot.Tests.Unit/Services/TaskEnrichmentServiceTests.cs`
- [x] T020 [P] Test `GenerateValidationCriteria_UsesTemplate_WhenAiUnavailable` — verify template format includes ResourceId, ControlId, ControlFamily in `tests/Ato.Copilot.Tests.Unit/Services/TaskEnrichmentServiceTests.cs`
- [x] T021 [P] Test `EnrichBoard_EnrichesAllTasks_InParallel` — create board with 10 tasks, verify all enriched and `BoardEnrichmentResult` counts correct in `tests/Ato.Copilot.Tests.Unit/Services/TaskEnrichmentServiceTests.cs`
- [x] T022 [P] Test `EnrichBoard_SkipsAlreadyEnrichedTasks` — pre-set some tasks with scripts, verify `TasksSkipped` count in `tests/Ato.Copilot.Tests.Unit/Services/TaskEnrichmentServiceTests.cs`
- [x] T023 [P] Test `EnrichBoard_ReportsProgress` — verify `IProgress<string>` receives "Enriching task {n}/{total}" messages in `tests/Ato.Copilot.Tests.Unit/Services/TaskEnrichmentServiceTests.cs`
- [x] T024 [P] Test `EnrichBoard_ContinuesOnIndividualFailure` — one task's finding throws, verify other tasks still enriched and `TasksFailed=1` in `tests/Ato.Copilot.Tests.Unit/Services/TaskEnrichmentServiceTests.cs`

**Checkpoint**: `TaskEnrichmentService` fully implemented with 14 passing unit tests. All user story integration can now begin.

---

## Phase 3: User Story 1 — Auto-Enrich Tasks at Board Creation (Priority: P1) 🎯 MVP

**Goal**: When `CreateBoardFromAssessmentAsync` is called, every resulting task is automatically enriched with a remediation script and validation criteria via the new `TaskEnrichmentService`.

**Independent Test**: Create a board from a mock assessment with findings. Verify each task has non-null `RemediationScript` and `ValidationCriteria`.

### Implementation for User Story 1

- [x] T025 [US1] Add optional `ITaskEnrichmentService?` constructor parameter to `KanbanService` in `src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs`
- [x] T026 [US1] Wire `EnrichBoardTasksAsync` call after task creation and `SaveChangesAsync` in `CreateBoardFromAssessmentAsync` in `src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs`
- [x] T027 [US1] Add structured log for board enrichment result (enriched/skipped/failed counts) in `src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs`

### Tests for User Story 1

- [x] T028 [P] [US1] Test `CreateBoardFromAssessment_CallsEnrichmentService_WhenAvailable` — verify `EnrichBoardTasksAsync` invoked with board and findings in `tests/Ato.Copilot.Tests.Unit/Services/KanbanServiceTests.cs`
- [x] T029 [P] [US1] Test `CreateBoardFromAssessment_SkipsEnrichment_WhenServiceIsNull` — verify no exception when `ITaskEnrichmentService` not injected in `tests/Ato.Copilot.Tests.Unit/Services/KanbanServiceTests.cs`
- [x] T030 [P] [US1] Test `CreateBoardFromAssessment_TasksHaveScripts_AfterEnrichment` — verify each task's `RemediationScript` and `ValidationCriteria` are non-null post-enrichment in `tests/Ato.Copilot.Tests.Unit/Services/KanbanServiceTests.cs`

**Checkpoint**: Board creation now auto-enriches all tasks. This is the MVP — deploy and verify via Docker stack.

---

## Phase 4: User Story 2 — On-Demand Script Generation Tool (Priority: P2)

**Goal**: Users can invoke `kanban_generate_script` to generate or regenerate a remediation script for any task, with support for multiple script types.

**Independent Test**: Call the tool with a task ID and `script_type=PowerShell`. Verify it returns a generated script and persists it.

### Implementation for User Story 2

- [x] T031 [US2] Implement `KanbanGenerateScriptTool` extending `BaseTool` with `Name="kanban_generate_script"`, parameters `task_id` (required), `script_type` (optional), `force` (optional) in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [x] T032 [US2] Implement `ExecuteAsync` — resolve task by ID/TaskNumber, look up linked finding, call `ITaskEnrichmentService.EnrichTaskAsync`, persist, return response envelope per contract in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [x] T033 [US2] Register `KanbanGenerateScriptTool` as singleton and as `BaseTool` in `src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs`
- [x] T034 [US2] Register `kanban_generate_script` via `RegisterTool()` in `ComplianceAgent` constructor in `src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs`

### Tests for User Story 2

- [x] T035 [P] [US2] Test `GenerateScript_ReturnsScript_WhenTaskHasNoScript` — verify script generated and persisted in `tests/Ato.Copilot.Tests.Unit/Tools/KanbanGenerateScriptToolTests.cs`
- [x] T036 [P] [US2] Test `GenerateScript_ReturnsError_WhenTaskNotFound` — verify `TASK_NOT_FOUND` error envelope in `tests/Ato.Copilot.Tests.Unit/Tools/KanbanGenerateScriptToolTests.cs`
- [x] T037 [P] [US2] Test `GenerateScript_ReturnsError_WhenScriptExists_AndForceIsFalse` — verify `SCRIPT_EXISTS` error envelope in `tests/Ato.Copilot.Tests.Unit/Tools/KanbanGenerateScriptToolTests.cs`
- [x] T038 [P] [US2] Test `GenerateScript_RegeneratesScript_WhenForceIsTrue` — verify new script replaces old in `tests/Ato.Copilot.Tests.Unit/Tools/KanbanGenerateScriptToolTests.cs`
- [x] T039 [P] [US2] Test `GenerateScript_UsesPowerShell_WhenScriptTypeSpecified` — verify `RemediationScriptType` set to "PowerShell" in `tests/Ato.Copilot.Tests.Unit/Tools/KanbanGenerateScriptToolTests.cs`
- [x] T040 [P] [US2] Test `GenerateScript_UsesFallback_WhenAiUnavailable` — verify template script returned in `tests/Ato.Copilot.Tests.Unit/Tools/KanbanGenerateScriptToolTests.cs`

**Checkpoint**: Users can on-demand generate scripts for any task via `kanban_generate_script`.

---

## Phase 5: User Story 3 — On-Demand Validation Criteria Generation Tool (Priority: P2)

**Goal**: Users can invoke `kanban_generate_validation` to generate or regenerate validation criteria for any task.

**Independent Test**: Call the tool with a task ID. Verify it returns validation criteria and persists it.

### Implementation for User Story 3

- [x] T041 [US3] Implement `KanbanGenerateValidationTool` extending `BaseTool` with `Name="kanban_generate_validation"`, parameters `task_id` (required), `force` (optional) in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [x] T042 [US3] Implement `ExecuteAsync` — resolve task, look up finding, call `ITaskEnrichmentService.GenerateValidationCriteriaAsync`, persist, return response envelope per contract in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [x] T043 [US3] Register `KanbanGenerateValidationTool` as singleton and as `BaseTool` in `src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs`
- [x] T044 [US3] Register `kanban_generate_validation` via `RegisterTool()` in `ComplianceAgent` constructor in `src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs`

### Tests for User Story 3

- [x] T045 [P] [US3] Test `GenerateValidation_ReturnsCriteria_WhenTaskHasNone` — verify criteria generated and persisted in `tests/Ato.Copilot.Tests.Unit/Tools/KanbanGenerateValidationToolTests.cs`
- [x] T046 [P] [US3] Test `GenerateValidation_ReturnsError_WhenTaskNotFound` — verify `TASK_NOT_FOUND` error envelope in `tests/Ato.Copilot.Tests.Unit/Tools/KanbanGenerateValidationToolTests.cs`
- [x] T047 [P] [US3] Test `GenerateValidation_RegeneratesCriteria_WhenForceIsTrue` — verify new criteria replaces old in `tests/Ato.Copilot.Tests.Unit/Tools/KanbanGenerateValidationToolTests.cs`
- [x] T047b [P] [US3] Test `GenerateValidation_ReturnsError_WhenCriteriaExists_AndForceIsFalse` — verify `CRITERIA_EXISTS` error envelope in `tests/Ato.Copilot.Tests.Unit/Tools/KanbanGenerateValidationToolTests.cs`
- [x] T048 [P] [US3] Test `GenerateValidation_UsesFallback_WhenAiUnavailable` — verify template criteria returned in `tests/Ato.Copilot.Tests.Unit/Tools/KanbanGenerateValidationToolTests.cs`

**Checkpoint**: Users can on-demand generate validation criteria for any task via `kanban_generate_validation`.

---

## Phase 6: User Story 5 — Board Update Enrichment (Priority: P2)

**Goal**: New tasks added during `UpdateBoardFromAssessmentAsync` receive the same enrichment as initial board creation. Existing task scripts are not overwritten.

**Independent Test**: Update a board with a new assessment containing additional findings. Verify new tasks are enriched while existing tasks retain their scripts.

### Implementation for User Story 5

- [x] T049 [US5] Wire enrichment for newly created tasks in `UpdateBoardFromAssessmentAsync` — collect new tasks, call `EnrichBoardTasksAsync` with only new tasks and their findings, persist in `src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs`

### Tests for User Story 5

- [x] T050 [P] [US5] Test `UpdateBoard_EnrichesNewTasks_WhenNewFindingsAdded` — verify newly created tasks have `RemediationScript` set in `tests/Ato.Copilot.Tests.Unit/Services/KanbanServiceTests.cs`
- [x] T051 [P] [US5] Test `UpdateBoard_DoesNotOverwriteExistingScripts` — pre-set scripts on existing tasks, verify they remain unchanged in `tests/Ato.Copilot.Tests.Unit/Services/KanbanServiceTests.cs`

**Checkpoint**: Board updates now enrich only new tasks without affecting existing ones.

---

## Phase 7: User Story 4 — Lazy Enrichment on Task Detail View (Priority: P3)

**Goal**: `kanban_get_task` lazily enriches tasks that have null `RemediationScript` on first view, so legacy pre-feature tasks benefit without re-assessment.

**Independent Test**: Create a task with null `RemediationScript`. Call `kanban_get_task`. Verify the returned task has a populated script and it was persisted.

### Implementation for User Story 4

- [x] T052 [US4] Add lazy enrichment logic to `KanbanGetTaskTool.ExecuteAsync` — check for null `RemediationScript`, resolve `ITaskEnrichmentService` from scoped `IServiceProvider` (via `GetService` for optional resolution), look up finding by `task.FindingId`, call `EnrichTaskAsync`, persist in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [x] T053 [US4] Add `remediationScriptType` field to `kanban_get_task` response data in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`

### Tests for User Story 4

- [x] T054 [P] [US4] Test `GetTask_LazilyEnriches_WhenScriptIsNull` — verify `RemediationScript` populated in response and persisted in `tests/Ato.Copilot.Tests.Unit/Tools/KanbanGetTaskToolTests.cs`
- [x] T055 [P] [US4] Test `GetTask_SkipsEnrichment_WhenScriptAlreadyExists` — verify no enrichment call, existing script returned as-is in `tests/Ato.Copilot.Tests.Unit/Tools/KanbanGetTaskToolTests.cs`
- [x] T056 [P] [US4] Test `GetTask_SkipsEnrichment_WhenServiceNotRegistered` — verify graceful no-op when `ITaskEnrichmentService` is null in `tests/Ato.Copilot.Tests.Unit/Tools/KanbanGetTaskToolTests.cs`
- [x] T057 [P] [US4] Test `GetTask_UsesFallback_WhenAiUnavailable` — verify template script returned when AI is disabled in `tests/Ato.Copilot.Tests.Unit/Tools/KanbanGetTaskToolTests.cs`

**Checkpoint**: Legacy tasks are now auto-enriched on first view. All user stories complete.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: System prompt update, documentation, final build validation

- [x] T058 [P] Add `kanban_generate_script` and `kanban_generate_validation` tool descriptions to system prompt in `src/Ato.Copilot.Agents/Compliance/Prompts/ComplianceAgent.prompt.txt`
- [x] T059 [P] Verify `dotnet build Ato.Copilot.sln` produces zero warnings in modified files
- [x] T060 Run full test suite `dotnet test Ato.Copilot.sln` — verify all existing 2311 + new tests pass
- [x] T061 Docker rebuild — `docker compose down && docker compose up --build -d` — verify 3 services healthy
- [x] T062 Run quickstart.md validation — create board from assessment, verify tasks show scripts and validation criteria, verify `kanban_generate_script` and `kanban_generate_validation` tools appear in MCP registry. Include timing assertions: board enrichment <60s for 30 tasks, individual tool calls <5s.
- [x] T063 Review `/docs/` directory for any files requiring update (e.g., tool registry, feature list). Update if applicable or document as N/A per Constitution Quality Gate I.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (models + interface) — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Phase 2 — MVP delivery
- **US2 (Phase 4)**: Depends on Phase 2 — can run parallel to US1
- **US3 (Phase 5)**: Depends on Phase 2 — can run parallel to US1/US2
- **US5 (Phase 6)**: Depends on Phase 2 — can run parallel to US1/US2/US3
- **US4 (Phase 7)**: Depends on Phase 2 — can run parallel to all other user stories
- **Polish (Phase 8)**: Depends on all user story phases being complete

### User Story Dependencies

- **US1 (P1)**: Requires `KanbanService` modification — modifies `CreateBoardFromAssessmentAsync`. Independent of tools.
- **US2 (P2)**: New tool only. No dependency on US1 modifications. Can implement in parallel.
- **US3 (P2)**: New tool only. No dependency on US1/US2. Can implement in parallel.
- **US5 (P2)**: Modifies `UpdateBoardFromAssessmentAsync` in `KanbanService.cs`. Same file as US1 but different method — can be done sequentially after US1 (both modify KanbanService).
- **US4 (P3)**: Modifies `KanbanGetTaskTool` in `KanbanTools.cs`. Independent of US1 (different file). Can run parallel to US2/US3 (same file — coordinate).

### Within Each User Story

- Implementation tasks before tests (tests reference concrete classes)
- Models/interface before service implementation
- Service before tools
- Core implementation before DI registration
- DI registration before agent constructor registration

### Parallel Opportunities

Within Phase 1:
- T002 and T003 can run in parallel (both append to same file, different classes)

Within Phase 2 (tests only — implementation is sequential):
- T011–T024 all run in parallel (same test file, independent test methods)

Across User Stories (after Phase 2):
- US1 (Phase 3) + US2 (Phase 4) + US3 (Phase 5) + US4 (Phase 7) can all start in parallel
- US5 (Phase 6) should follow US1 (both modify KanbanService.cs)

---

## Parallel Example: Post-Foundational User Stories

```
# After Phase 2 completes, launch user stories in parallel:

# Developer A: US1 (KanbanService.cs)
Task T025: Add ITaskEnrichmentService? to KanbanService constructor
Task T026: Wire EnrichBoardTasksAsync in CreateBoardFromAssessmentAsync
Task T027: Add structured log
Tasks T028-T030: Unit tests

# Developer B: US2 + US3 (KanbanTools.cs — new tool classes)
Task T031-T034: KanbanGenerateScriptTool implementation + registration
Task T041-T044: KanbanGenerateValidationTool implementation + registration
Tasks T035-T040, T045-T048: Unit tests

# Once US1 done → Developer A continues to US5 (same KanbanService.cs)
Task T049: Wire enrichment in UpdateBoardFromAssessmentAsync
Tasks T050-T051: Unit tests

# Developer C: US4 (KanbanTools.cs — modify existing GetTask)
Task T052-T053: Lazy enrichment in KanbanGetTaskTool
Tasks T054-T057: Unit tests
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (5 tasks — data model + interface)
2. Complete Phase 2: Foundational (19 tasks — service + 14 tests)
3. Complete Phase 3: US1 — Auto-Enrich at Board Creation (6 tasks)
4. **STOP and VALIDATE**: Build, test, Docker deploy, create board, verify scripts
5. Deploy as MVP — boards now auto-enrich

### Incremental Delivery

1. Setup + Foundational → TaskEnrichmentService ready
2. Add US1 → Board creation enrichment → Deploy/Demo (**MVP!**)
3. Add US2 + US3 → On-demand tools → Deploy/Demo
4. Add US5 → Board update enrichment → Deploy/Demo
5. Add US4 → Lazy enrichment → Deploy/Demo (full feature)
6. Polish → System prompt, build validation, quickstart

### Task Count Summary

| Phase | Tasks | Tests | Total |
|-------|-------|-------|-------|
| Phase 1: Setup | 5 | 0 | 5 |
| Phase 2: Foundational | 5 | 14 | 19 |
| Phase 3: US1 (P1) | 3 | 3 | 6 |
| Phase 4: US2 (P2) | 4 | 6 | 10 |
| Phase 5: US3 (P2) | 4 | 5 | 9 |
| Phase 6: US5 (P2) | 1 | 2 | 3 |
| Phase 7: US4 (P3) | 2 | 4 | 6 |
| Phase 8: Polish | 6 | 0 | 6 |
| **Total** | **30** | **34** | **64** |

---

## Notes

- [P] tasks = different files, no dependencies — safe to parallelize
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable after Phase 2
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- US5 should follow US1 (both modify `KanbanService.cs`)
- US2 + US3 can be interleaved (both add new tool classes to `KanbanTools.cs`, non-conflicting)
