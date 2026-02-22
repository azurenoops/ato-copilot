# Tasks: Kanban User Context & Comment Permission Enhancement

**Input**: Design documents from `/specs/004-kanban-user-context/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/tool-responses.md, quickstart.md

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: No new project setup needed — this feature modifies existing projects. Setup phase registers the new `IUserContext` abstraction in DI and ensures the interface is available across projects.

- [X] T001 Create `IUserContext` interface with `UserId`, `DisplayName`, `Role`, `IsAuthenticated` properties in `src/Ato.Copilot.Core/Interfaces/Auth/IUserContext.cs`
- [X] T002 Create `HttpUserContext` implementation using `IHttpContextAccessor` with lazy claim extraction, caching, and fallback defaults in `src/Ato.Copilot.Mcp/Models/HttpUserContext.cs`
- [X] T003 Register `IHttpContextAccessor` and `IUserContext`/`HttpUserContext` as scoped service in `src/Ato.Copilot.Mcp/Extensions/McpServiceExtensions.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before user story tasks can begin — specifically, the `IUserContext` resolution pattern inside Kanban tools must work before any tool can be modified.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Add `IUserContext` resolution via `scope.ServiceProvider.GetRequiredService<IUserContext>()` to `KanbanToolBase` helper method or establish the pattern in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs` (KanbanToolBase class only)
- [X] T005 [P] Create unit tests for `HttpUserContext` — authenticated user extraction, missing claims fallback, null HttpContext fallback, role priority resolution — in `tests/Ato.Copilot.Tests.Unit/Middleware/UserContextTests.cs`

**Checkpoint**: `IUserContext` is registered, resolvable from tool scopes, and tested. Tool modifications can now begin.

---

## Phase 3: User Story 2 — User Identity Propagation Through Tools (Priority: P1)

**Goal**: Replace all 18 hardcoded `"system"` user-ID strings, 9 `"System"` display-name strings, and 8 `"Compliance.Officer"` role strings across 12 tool methods with `userContext.UserId`, `userContext.DisplayName`, and `userContext.Role`.

**Independent Test**: Invoke any Kanban tool with a known user identity in request context and verify the service receives that identity instead of `"system"`.

> **Note**: US2 is implemented before US1 because US1 (isAssignedToCurrentUser flag) depends on the IUserContext being resolved inside tool methods — which is established by the US2 identity propagation work.

### Implementation for User Story 2

- [X] T006 [US2] Replace hardcoded identity in `KanbanCreateBoardTool.ExecuteCoreAsync` — resolve `IUserContext` from scope, replace `"system"` owner with `userContext.UserId` in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [X] T007 [P] [US2] Replace hardcoded identity in `KanbanCreateTaskTool.ExecuteCoreAsync` — replace `"system"` createdBy with `userContext.UserId` in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [X] T008 [P] [US2] Replace hardcoded identity in `KanbanAssignTaskTool.ExecuteCoreAsync` — replace `"system"`, `"System"`, `"Compliance.Officer"` with `userContext.UserId`, `userContext.DisplayName`, `userContext.Role` in service call and response in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [X] T009 [P] [US2] Replace hardcoded identity in `KanbanMoveTaskTool.ExecuteCoreAsync` — replace `"system"`, `"System"`, `"Compliance.Officer"` with userContext properties in service call and response in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [X] T010 [P] [US2] Replace hardcoded identity in `KanbanAddCommentTool.ExecuteCoreAsync` — replace `"system"`, `"System"`, `"Compliance.Officer"` with userContext properties in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [X] T011 [P] [US2] Replace hardcoded identity in `KanbanEditCommentTool.ExecuteCoreAsync` — replace `"system"` with `userContext.UserId` in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [X] T012 [P] [US2] Replace hardcoded identity in `KanbanDeleteCommentTool.ExecuteCoreAsync` — replace `"system"`, `"Compliance.Officer"` with `userContext.UserId`, `userContext.Role` in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [X] T013 [P] [US2] Replace hardcoded identity in `KanbanRemediateTaskTool.ExecuteCoreAsync` — replace `"system"`, `"System"` with `userContext.UserId`, `userContext.DisplayName` in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [X] T014 [P] [US2] Replace hardcoded identity in `KanbanCollectEvidenceTool.ExecuteCoreAsync` — replace `"system"`, `"System"` with `userContext.UserId`, `userContext.DisplayName` in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [X] T015 [P] [US2] Replace hardcoded identity in `KanbanBulkUpdateTool.ExecuteCoreAsync` — replace 3 sets of `"system"`, `"System"`, `"Compliance.Officer"` in assign/move/setDueDate branches in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [X] T016 [P] [US2] Replace hardcoded identity in `KanbanExportTool.ExecuteCoreAsync` — replace `"system"`, `"Compliance.Officer"` in 2 export method calls in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [X] T017 [P] [US2] Replace hardcoded identity in `KanbanArchiveBoardTool.ExecuteCoreAsync` — replace `"system"`, `"System"` in service call and `"system"` in response in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [X] T018 [US2] Update unit tests for user identity propagation — verify all 13 modified tools pass `userContext` properties instead of hardcoded strings in `tests/Ato.Copilot.Tests.Unit/Tools/KanbanToolTests.cs`
- [X] T019 [US2] Update integration tests for user identity propagation — verify audit trail and history entries reflect real user identity in `tests/Ato.Copilot.Tests.Integration/KanbanIntegrationTests.cs`

**Checkpoint**: All 13 tool methods pass real user identity to service layer. Zero hardcoded `"system"` / `"System"` / `"Compliance.Officer"` strings remain. Existing tests updated and passing.

---

## Phase 4: User Story 1 — Current User Task Highlighting (Priority: P1)

**Goal**: Add `isAssignedToCurrentUser` computed boolean flag to every task representation in board show, task list, and get-task tool responses.

**Independent Test**: Authenticate as user X, assign tasks to X and others, view board, verify X's tasks have `isAssignedToCurrentUser: true` and others have `false`.

### Implementation for User Story 1

- [X] T020 [US1] Add `isAssignedToCurrentUser` computed flag to `KanbanBoardShowTool.ExecuteCoreAsync` — resolve `IUserContext`, compare `task.AssigneeId == userContext.UserId` for each task in column arrays in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [X] T021 [P] [US1] Add `isAssignedToCurrentUser` computed flag to `KanbanGetTaskTool.ExecuteCoreAsync` — add flag to task detail response in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [X] T022 [P] [US1] Add `isAssignedToCurrentUser` computed flag to `KanbanTaskListTool.ExecuteCoreAsync` — add flag to each task in list response in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`
- [X] T023 [US1] Add unit tests for `isAssignedToCurrentUser` flag — test true when assigned to current user, false when assigned to others, false when no assignee, false when no auth context in `tests/Ato.Copilot.Tests.Unit/Tools/KanbanToolTests.cs`
- [X] T024 [US1] Add integration tests for `isAssignedToCurrentUser` — verify flag in board show, get task, and task list responses with authenticated user context in `tests/Ato.Copilot.Tests.Integration/KanbanIntegrationTests.cs`

**Checkpoint**: All three read tools include `isAssignedToCurrentUser` flag. Flag correctly computed for authenticated and unauthenticated requests.

---

## Phase 5: User Story 3 — SecurityLead Comment Deletion (Priority: P2)

**Goal**: Grant the SecurityLead role the `CanDeleteAnyComment` permission so Security Leads can moderate comments like Administrators.

**Independent Test**: Authenticate as SecurityLead, delete another user's comment, verify success.

### Implementation for User Story 3

- [X] T025 [US3] Add `KanbanPermissions.CanDeleteAnyComment` to SecurityLead permission set in `src/Ato.Copilot.Agents/Compliance/Services/KanbanPermissionsHelper.cs`
- [X] T026 [US3] Add unit tests for SecurityLead comment deletion — test SecurityLead can delete any comment, bypasses time window, Analyst still blocked in `tests/Ato.Copilot.Tests.Unit/Services/KanbanPermissionsHelperTests.cs`
- [X] T027 [US3] Add integration test for SecurityLead comment deletion — end-to-end verify SecurityLead deletes another user's comment in `tests/Ato.Copilot.Tests.Integration/KanbanIntegrationTests.cs`

**Checkpoint**: SecurityLead can delete any comment. Analyst/Auditor/Viewer still blocked. All permission matrix tests pass.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Validation, documentation, and final quality checks across all stories.

- [X] T028 [P] Add XML documentation comments to `IUserContext` and `HttpUserContext` per Constitution VI in `src/Ato.Copilot.Core/Interfaces/Auth/IUserContext.cs` and `src/Ato.Copilot.Mcp/Models/HttpUserContext.cs`
- [X] T029 [P] Verify zero hardcoded `"system"` / `"System"` / `"Compliance.Officer"` strings remain in `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs` — grep audit
- [X] T030 [P] Update `McpServer.cs` — replace hardcoded `"mcp-user"` ConversationState.UserId with `IUserContext.UserId` in `src/Ato.Copilot.Mcp/Server/McpServer.cs`
- [X] T031 Run full `dotnet build Ato.Copilot.sln` — verify zero warnings
- [X] T032 Run full `dotnet test Ato.Copilot.sln` — verify all tests pass (existing 950 + new tests)
- [X] T033 Run quickstart.md validation — verify build and test commands from `specs/004-kanban-user-context/quickstart.md` succeed

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — can start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — BLOCKS all user stories
- **Phase 3 (US2 — Identity Propagation)**: Depends on Phase 2 — establishes IUserContext pattern in tools
- **Phase 4 (US1 — Current User Flag)**: Depends on Phase 3 — requires IUserContext already resolved in tools
- **Phase 5 (US3 — SecurityLead Permission)**: Depends on Phase 2 only — can run in parallel with Phase 3/4
- **Phase 6 (Polish)**: Depends on all user story phases being complete

### User Story Dependencies

- **US2 (P1)**: Must complete first — establishes the IUserContext resolution pattern in all tool methods
- **US1 (P1)**: Depends on US2 — adds `isAssignedToCurrentUser` flag using the IUserContext already resolved
- **US3 (P2)**: Independent of US1/US2 — only modifies KanbanPermissionsHelper (can run in parallel with US2/US1)

### Within Each User Story

- Tool modifications (same file KanbanTools.cs) marked [P] can be done in parallel because they modify different classes within the file
- Tests should be updated after implementation tasks
- Integration tests run after unit tests

### Parallel Opportunities

- T007–T017 (US2 tool modifications) are all [P] — each modifies a different tool class within KanbanTools.cs
- T021–T022 (US1 flag addition) are [P] — different tool classes
- T025–T027 (US3 permission) can run in parallel with Phase 3/4
- T028–T030 (Polish) are all [P] — different files

---

## Parallel Example: User Story 2 (Identity Propagation)

```text
# After T006 establishes the pattern in KanbanCreateBoardTool, all remaining tools can be done in parallel:
T007: KanbanCreateTaskTool         ─┐
T008: KanbanAssignTaskTool         ─┤
T009: KanbanMoveTaskTool           ─┤
T010: KanbanAddCommentTool         ─┤ All modify different classes
T011: KanbanEditCommentTool        ─┤ within KanbanTools.cs — can
T012: KanbanDeleteCommentTool      ─┤ be done in parallel
T013: KanbanRemediateTaskTool      ─┤
T014: KanbanCollectEvidenceTool    ─┤
T015: KanbanBulkUpdateTool         ─┤
T016: KanbanExportTool             ─┤
T017: KanbanArchiveBoardTool       ─┘
```

---

## Implementation Strategy

### MVP First (US2 + US1)

1. Complete Phase 1: Setup (T001–T003) — IUserContext interface + implementation + DI registration
2. Complete Phase 2: Foundational (T004–T005) — tool resolution pattern + HttpUserContext tests
3. Complete Phase 3: US2 (T006–T019) — all 13 tools propagate real user identity
4. Complete Phase 4: US1 (T020–T024) — isAssignedToCurrentUser flag in 3 read tools
5. **STOP and VALIDATE**: Run `dotnet test` — all existing + new tests pass

### Incremental Delivery

1. Setup + Foundational → IUserContext available in DI
2. US2 → Real user identity flows through all tools → audit trails accurate
3. US1 → Board highlights current user's tasks → primary UX gap closed
4. US3 → SecurityLead can moderate comments → permission gap closed
5. Polish → zero warnings, full test pass, documentation complete

---

## Notes

- All 13 tool modifications in Phase 3 are in the same file (`KanbanTools.cs`) but different classes — parallelizable by class
- The `"Compliance.Officer"` hardcoded role is a pre-existing bug (role doesn't exist in the permission matrix) — fixing it is inherent to the identity propagation work
- `KanbanTaskHistoryTool` and `KanbanTaskCommentsTool` require no changes — they are read-only tools that don't pass user identity to service methods and don't need the `isAssignedToCurrentUser` flag
- `McpServer.cs` has 2 hardcoded `"mcp-user"` strings to replace (T030) — separate from KanbanTools changes
