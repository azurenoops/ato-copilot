# Tasks: Remediation Kanban

**Input**: Design documents from `/specs/002-remediation-kanban/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/kanban-tools.md, quickstart.md

**Tests**: Included — spec requires 80%+ coverage; status transitions, RBAC, concurrency, and comment edit windows all require positive + negative test cases.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add new NuGet packages, create file scaffolding, extend configuration

- [X] T001 Add NuGet packages (MailKit >= 4.10.0, Microsoft.Extensions.Http.Resilience >= 9.0.0) to src/Ato.Copilot.Core/Ato.Copilot.Core.csproj
- [X] T002 [P] Add Kanban configuration section (Kanban:Sla:CriticalHours=24, Kanban:Sla:HighDays=7, Kanban:Sla:MediumDays=30, Kanban:Sla:LowDays=90, Kanban:OverdueScan:IntervalMinutes=5, Kanban:Notifications:Email:*, Kanban:Notifications:Teams:WebhookUrl, Kanban:Notifications:Slack:WebhookUrl) to src/Ato.Copilot.Mcp/appsettings.json
- [X] T003 [P] Add SecurityLead role constant and Kanban-specific permission constants (CanCreateBoard, CanCreateTask, CanAssignAny, CanSelfAssign, CanMoveOwn, CanMoveAny, CanCloseWithoutValidation, CanComment, CanDeleteAnyComment, CanExport, CanArchive) to src/Ato.Copilot.Core/Constants/ComplianceRoles.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Enums, entities, DbContext extension, concurrency infrastructure, interfaces, and DI wiring that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 [P] Create KanbanEnums.cs with TaskStatus (Backlog, ToDo, InProgress, InReview, Blocked, Done), HistoryEventType (Created, StatusChanged, Assigned, CommentAdded, CommentEdited, CommentDeleted, RemediationAttempt, ValidationRun, DueDateChanged, SeverityChanged), NotificationEventType (TaskAssigned, StatusChanged, CommentAdded, TaskOverdue, TaskClosed, Mentioned), and NotificationChannelType (Email, Teams, Slack) enums in src/Ato.Copilot.Core/Models/Kanban/KanbanEnums.cs
- [X] T005 [P] Create KanbanConstants.cs with default SLA values, task ID format (REM-{n:D3}), max tasks per board (500), max comment length (4000), comment edit window (24h), comment delete window (1h), default page size (25), max page size (100), and allowed status transition dictionary skeleton in src/Ato.Copilot.Core/Constants/KanbanConstants.cs
- [X] T006 [P] Create ConcurrentEntity abstract base class with Guid RowVersion property in src/Ato.Copilot.Core/Models/Kanban/KanbanModels.cs
- [X] T007 Create KanbanModels.cs with RemediationBoard (extends ConcurrentEntity: Id, Name, SubscriptionId, AssessmentId?, Owner, CreatedAt, UpdatedAt, IsArchived, NextTaskNumber, Tasks nav), RemediationTask (extends ConcurrentEntity: Id, TaskNumber, BoardId, Title, Description, ControlId, ControlFamily, Severity, Status, AssigneeId?, AssigneeName?, DueDate, CreatedAt, UpdatedAt, AffectedResources, RemediationScript?, ValidationCriteria?, FindingId?, CreatedBy, LastOverdueNotifiedAt?, Comments nav, History nav), TaskComment (Id, TaskId, AuthorId, AuthorName, Content, CreatedAt, EditedAt?, IsEdited, IsDeleted, IsSystemComment, ParentCommentId?, Mentions), TaskHistoryEntry (Id, TaskId, EventType, OldValue?, NewValue?, ActingUserId, ActingUserName, Timestamp, Details?) per data-model.md in src/Ato.Copilot.Core/Models/Kanban/KanbanModels.cs
- [X] T008 Create ephemeral models SavedView (Name, OwnerId, BoardId, Filters), ViewFilters (AssigneeId?, Severities?, ControlFamilies?, Statuses?, DueDateFrom/To?, CreatedFrom/To?, IsOverdue?), and NotificationConfig (UserId, ChannelType, ChannelAddress, EnabledEvents, IsEnabled) in src/Ato.Copilot.Core/Models/Kanban/KanbanModels.cs
- [X] T009 Extend AtoCopilotContext: add DbSet<RemediationBoard>, DbSet<RemediationTask>, DbSet<TaskComment>, DbSet<TaskHistoryEntry>; configure entity relationships (Board 1:N Task cascade, Task 1:N Comment cascade, Task 1:N History cascade, Board→Assessment optional restrict); configure indexes (BoardId+Status, BoardId+ControlFamily, TaskId+CreatedAt, TaskId+Timestamp, SubscriptionId+IsArchived); configure ValueConverter<List<string>, string> for AffectedResources and Mentions; configure .IsConcurrencyToken() on RowVersion for ConcurrentEntity types in src/Ato.Copilot.Core/Data/Context/AtoCopilotContext.cs
- [X] T010 Override SaveChangesAsync in AtoCopilotContext to auto-regenerate RowVersion = Guid.NewGuid() for all modified ConcurrentEntity entries per research R-001 in src/Ato.Copilot.Core/Data/Context/AtoCopilotContext.cs
- [ ] T011 Create EF Core migration AddKanbanEntities for all four new entity tables with indexes and relationships using dotnet ef migrations add AddKanbanEntities
- [X] T012 [P] Create StatusTransitionEngine static helper class with static readonly Dictionary<(TaskStatus, TaskStatus), TransitionRule> encoding all 16 allowed transitions and their rules (RequiresComment, RequiresResolutionComment, RequiresValidation, AllowSkipValidation, TriggersValidation) per research R-007 and data-model transition table, plus IsTransitionAllowed() and GetTransitionRule() methods in src/Ato.Copilot.Agents/Compliance/Services/StatusTransitionEngine.cs
- [X] T013 [P] Create KanbanPermissions static helper class with CanPerformAction(string role, string action) checking the role permissions matrix from data-model.md (create board, create task, assign any, self-assign, move own, move any, close without validation, comment, delete any comment, export, archive) in src/Ato.Copilot.Agents/Compliance/Services/KanbanPermissions.cs
- [X] T014 [P] Create IKanbanService interface with board operations (CreateBoardAsync, CreateBoardFromAssessmentAsync, UpdateBoardFromAssessmentAsync, GetBoardAsync, ListBoardsAsync, ArchiveBoardAsync, ExportBoardCsvAsync, ExportBoardHistoryAsync), task operations (CreateTaskAsync, GetTaskAsync, ListTasksAsync, MoveTaskAsync, AssignTaskAsync, ValidateTaskAsync, GetOpenTasksForPoamAsync), remediation operations (ExecuteTaskRemediationAsync, CollectTaskEvidenceAsync), comment operations (AddCommentAsync, EditCommentAsync, DeleteCommentAsync, ListCommentsAsync), history operations (GetTaskHistoryAsync), view operations (SaveViewAsync, GetViewAsync, ListViewsAsync, DeleteViewAsync), and bulk operations (BulkAssignAsync, BulkMoveAsync, BulkSetDueDateAsync) — all with CancellationToken — in src/Ato.Copilot.Core/Interfaces/Kanban/IKanbanService.cs
- [X] T015 [P] Create INotificationService interface with EnqueueAsync(NotificationMessage), and NotificationMessage record (EventType, TaskId, TaskNumber, BoardId, TargetUserId, Title, Details) in src/Ato.Copilot.Core/Interfaces/Kanban/INotificationService.cs
- [X] T016 Register IKanbanService (Scoped), INotificationService (Singleton), and OverdueScanHostedService (Singleton via AddHostedService) in DI in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs

**Checkpoint**: Foundation ready — models, database, interfaces, transition engine, permissions helper, and DI wiring complete. User story implementation can now begin.

---

## Phase 3: User Story 1 — Board Creation from Assessment (Priority: P1) 🎯 MVP

**Goal**: As a Compliance Officer, I want to create a remediation board from a completed compliance assessment so that every non-compliant finding becomes a trackable Kanban task in Backlog with correct severity, control ID, affected resources, and SLA-derived due dates.

**Independent Test**: Run an assessment that returns findings, confirm board creation, verify each finding appears as a task in Backlog with correct metadata.

### Tests for User Story 1

- [X] T017 [P] [US1] Unit tests for CreateBoardFromAssessmentAsync: board created with correct name/subscription/assessmentId, one task per finding, tasks in Backlog, severity-based SLA due dates (Critical=24h, High=7d, Medium=30d, Low=90d), sequential REM-NNN IDs, zero findings returns empty board, concurrency conflict on NextTaskNumber triggers retry — in tests/Ato.Copilot.Tests.Unit/Services/KanbanServiceBoardCreationTests.cs
- [X] T018 [P] [US1] Unit tests for kanban_create_board MCP tool: valid input returns envelope with boardId/name/taskCount/tasksByStatus/tasksBySeverity, missing name returns error, RBAC blocks Auditor/PE roles, assessmentId not found returns ASSESSMENT_NOT_FOUND — in tests/Ato.Copilot.Tests.Unit/Tools/KanbanCreateBoardToolTests.cs

### Implementation for User Story 1

- [X] T019 [US1] Implement KanbanService.CreateBoardFromAssessmentAsync: query findings by assessmentId from AtoCopilotContext, create RemediationBoard with name/subscription/owner, iterate findings to create RemediationTask per finding (map controlId, severity, affectedResources, remediationScript, description), set TaskNumber via atomic NextTaskNumber increment with concurrency retry, set SLA-based DueDate from KanbanConstants, add Created history entry per task, save all in single transaction — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T020 [US1] Implement KanbanService.CreateBoardAsync: create empty board with name/subscription/owner, validate RBAC via KanbanPermissions, return board — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T021 [US1] Implement KanbanService.GetBoardAsync: load board with task counts grouped by Status and Severity, compute overdueTasks count, compute completionPercentage — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T022 [US1] Create KanbanCreateBoardTool extending BaseTool with Name="kanban_create_board", parameters (name, subscriptionId?, assessmentId?, owner?) per contracts, wrapping KanbanService.CreateBoardFromAssessmentAsync / CreateBoardAsync, returning standard envelope with board summary — in src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs
- [X] T023 [US1] Create KanbanBoardShowTool extending BaseTool with Name="kanban_board_show", parameters (boardId, includeTaskSummaries?, page?, pageSize?) per contracts, wrapping KanbanService.GetBoardAsync with pagination, returning columns with task summaries — in src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs
- [X] T100 [US1] Create KanbanGetTaskTool extending BaseTool with Name="kanban_get_task", parameters (taskId, boardId?) per contracts, wrapping KanbanService.GetTaskAsync, returning full task details (affectedResources, remediationScript, validationCriteria, description, all fields, commentCount, historyCount) — in src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs
- [X] T024 [US1] Register KanbanCreateBoardTool, KanbanBoardShowTool, and KanbanGetTaskTool in ComplianceAgent tool list and add board creation MCP method wrappers to ComplianceMcpTools — in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs and src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs
- [X] T025 [US1] Extend ComplianceAssessmentTool post-assessment flow: after assessment completes with findings, check for existing board on same subscription; if exists, offer "Update existing board or create new?" and route to UpdateBoardFromAssessmentAsync or CreateBoardFromAssessmentAsync accordingly; if no board exists, offer "Create a remediation board to track fixes?" and route to CreateBoardFromAssessmentAsync on confirmation — in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceAssessmentTool.cs (or KanbanTools.cs helper)
- [X] T094 [P] [US1] Unit tests for UpdateBoardFromAssessmentAsync: new findings create new tasks, previously-remediated findings auto-close matching tasks with system comment, unchanged findings left untouched, matching by controlId+affectedResources, concurrent board update handled — in tests/Ato.Copilot.Tests.Unit/Services/KanbanServiceBoardUpdateTests.cs
- [X] T095 [US1] Implement KanbanService.UpdateBoardFromAssessmentAsync: load existing board by subscriptionId, diff new assessment findings against existing tasks (match by controlId + affectedResources), create new RemediationTask for unmatched findings, auto-close tasks whose findings are now compliant (move to Done + system comment "Auto-closed: finding resolved in latest assessment"), leave matched-still-open tasks unchanged, return summary (added/closed/unchanged counts) — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs

**Checkpoint**: Board creation from assessment works end-to-end. A compliance officer can run an assessment and get a populated Kanban board. Existing boards can be updated with new assessment findings.

---

## Phase 4: User Story 2 — Manual Task and Board Creation (Priority: P1)

**Goal**: As a Compliance Officer or Security Lead, I want to create remediation tasks and boards manually without running an assessment so that I can track ad-hoc findings, audit preparations, or externally reported issues.

**Independent Test**: Create a standalone task via "Create remediation task for AC-2.1" and verify it appears in a board's Backlog with correct control ID and severity lookup.

### Tests for User Story 2

- [X] T026 [P] [US2] Unit tests for CreateTaskAsync: task created with correct controlId/title/severity, auto-creates default board if none exists, sequential REM-NNN ID, severity→SLA due date mapping, invalid controlId rejected, RBAC blocks PE/Auditor — in tests/Ato.Copilot.Tests.Unit/Services/KanbanServiceTaskCreationTests.cs

### Implementation for User Story 2

- [X] T027 [US2] Implement KanbanService.CreateTaskAsync: validate controlId format (^[A-Z]{2}-\\d+(\\.\\d+)?$), lookup control metadata for title/severity if not provided, create RemediationTask with TaskNumber from board.NextTaskNumber, set ControlFamily from first 2 chars, set SLA-based DueDate, add Created history entry, auto-create default board if boardId is null, RBAC check via KanbanPermissions — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T028 [US2] Implement KanbanService.ListBoardsAsync: query boards by subscriptionId, filter isArchived, paginate, return with task count summaries — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T029 [US2] Create KanbanCreateTaskTool extending BaseTool with Name="kanban_create_task", parameters (boardId, title, controlId, description?, severity?, assigneeId?, dueDate?, affectedResources?, remediationScript?, validationCriteria?) per contracts — in src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs
- [X] T030 [US2] Register KanbanCreateTaskTool in ComplianceAgent and add MCP method wrapper to ComplianceMcpTools — in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs and src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs

**Checkpoint**: Manual board and task creation works. Users can create empty boards and ad-hoc tasks independently of assessments.

---

## Phase 5: User Story 3 — Task Assignment and Self-Assignment (Priority: P1)

**Goal**: As a Compliance Officer, I want to assign remediation tasks to team members so that every task has a clear owner accountable for its completion. As a Platform Engineer, I want to self-assign unassigned tasks so that I can pick up work independently.

**Independent Test**: Assign a task via "Assign REM-001 to John Smith" and verify the assignee updates and the change is logged in task history.

### Tests for User Story 3

- [X] T031 [P] [US3] Unit tests for AssignTaskAsync: assign/unassign updates assigneeId/Name, logs Assigned history entry, CO can assign any, SL can assign any, PE can self-assign unassigned only, PE cannot reassign others, Auditor blocked, concurrency conflict returns error, assigns on Done task blocked — in tests/Ato.Copilot.Tests.Unit/Services/KanbanServiceAssignmentTests.cs

### Implementation for User Story 3

- [X] T032 [US3] Implement KanbanService.AssignTaskAsync: validate RBAC (CO/SL assign any, PE self-assign unassigned only), update task AssigneeId/AssigneeName, add Assigned history entry with old→new value, handle concurrency via RowVersion, reject assign on Done tasks, enqueue TaskAssigned notification — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T033 [US3] Create KanbanAssignTaskTool extending BaseTool with Name="kanban_assign_task", parameters (taskId, boardId?, assigneeId?, assigneeName?) per contracts — in src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs
- [X] T034 [US3] Register KanbanAssignTaskTool in ComplianceAgent and add MCP method wrapper to ComplianceMcpTools — in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs and src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs

**Checkpoint**: Task assignment works with full RBAC. CO/SL can assign anyone, PE can self-assign, Auditor is blocked.

---

## Phase 6: User Story 4 — Status Transitions (Priority: P1)

**Goal**: As a user with an assigned task, I want to move it between Kanban columns via chat commands so that the board reflects current remediation progress. As a system, I enforce transition rules — blocking requires a comment, unblocking requires a resolution comment, closing requires validation.

**Independent Test**: Move a task Backlog → ToDo → InProgress → InReview → Done via sequential commands and verify each transition is recorded in history.

### Tests for User Story 4

- [X] T035 [P] [US4] Unit tests for MoveTaskAsync: all 16 valid transitions succeed, invalid transitions rejected (Done→anything returns TERMINAL_STATE, Backlog→Done returns INVALID_TRANSITION), →Blocked requires comment (BLOCKER_COMMENT_REQUIRED), Blocked→ requires resolution comment (RESOLUTION_COMMENT_REQUIRED), →Done requires validation or CO skipValidation, →InProgress auto-assigns if unassigned, →InReview triggers validation, history entry logged for every transition, concurrency conflict handled — in tests/Ato.Copilot.Tests.Unit/Services/KanbanServiceTransitionTests.cs
- [X] T036 [P] [US4] Unit tests for StatusTransitionEngine: IsTransitionAllowed returns true for all 16 valid pairs, false for all invalid pairs, GetTransitionRule returns correct flags for each transition — in tests/Ato.Copilot.Tests.Unit/Services/StatusTransitionEngineTests.cs

### Implementation for User Story 4

- [X] T037 [US4] Implement KanbanService.MoveTaskAsync: load task with concurrency token, call StatusTransitionEngine.IsTransitionAllowed, enforce TransitionRule conditions (comment for Blocked, resolution comment for leaving Blocked, validation for Done), validate RBAC (CO/SL move any, PE move own only), auto-assign on →InProgress if unassigned, add StatusChanged history entry, update task.Status and UpdatedAt, save with concurrency handling, enqueue StatusChanged notification — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T038 [US4] Implement KanbanService.GetTaskHistoryAsync: load history entries for taskId with optional eventType filter, paginate, return chronologically — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T039 [US4] Create KanbanMoveTaskTool extending BaseTool with Name="kanban_move_task", parameters (taskId, boardId?, targetStatus, comment?, skipValidation?) per contracts — in src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs
- [X] T040 [US4] Create KanbanTaskHistoryTool extending BaseTool with Name="kanban_task_history", parameters (taskId, boardId?, eventType?, page?, pageSize?) per contracts — in src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs
- [X] T041 [US4] Register KanbanMoveTaskTool and KanbanTaskHistoryTool in ComplianceAgent and add MCP method wrappers to ComplianceMcpTools — in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs and src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs

**Checkpoint**: Full Kanban workflow complete — tasks can be created, assigned, and moved through all 6 columns with proper rule enforcement. This is the MVP milestone.

---

## Phase 7: User Story 5 — Validation Workflow (Priority: P2)

**Goal**: As a Compliance Officer, I want the system to automatically re-scan affected resources when a task moves to In Review so that I can confirm remediation was applied correctly before closing the task.

**Independent Test**: Move a task to InReview, trigger validation, verify the agent re-scans affected resources and reports pass/fail.

### Tests for User Story 5

- [X] T042 [P] [US5] Unit tests for ValidateTaskAsync: re-scans affected resources for task's controlId via IAtoComplianceEngine, returns pass/fail per resource, adds ValidationRun history entry, adds system comment with results, full pass sets canClose=true, partial fail reports remaining non-compliant resources — in tests/Ato.Copilot.Tests.Unit/Services/KanbanServiceValidationTests.cs

### Implementation for User Story 5

- [X] T043 [US5] Implement KanbanService.ValidateTaskAsync: load task and affected resources, call IAtoComplianceEngine to re-scan each resource for the task's controlId, aggregate pass/fail results, add ValidationRun history entry with details, add system comment with per-resource results, return validation summary with canClose flag — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T044 [US5] Wire validation trigger into MoveTaskAsync: when targetStatus is InReview, call ValidateTaskAsync after status change; when targetStatus is Done and skipValidation is false, check last validation result or trigger new one — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T045 [US5] Create KanbanValidateTaskTool extending BaseTool with Name="kanban_task_validate", parameters (taskId, boardId?, subscriptionId?) per contracts — in src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs
- [X] T046 [US5] Register KanbanValidateTaskTool in ComplianceAgent and add MCP method wrapper to ComplianceMcpTools — in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs and src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs

**Checkpoint**: Validation workflow complete. Tasks moving to InReview trigger resource re-scans. Closing requires validation pass or CO override.

---

## Phase 8: User Story 6 — Comments and Discussion (Priority: P2)

**Goal**: As a team member, I want to add threaded comments on remediation tasks so that I can document progress, share blockers, tag colleagues with @mentions, and build an audit-ready discussion trail. As a system, I enforce 24-hour edit windows and 1-hour delete windows to protect the audit trail.

**Independent Test**: Add a comment to a task, edit within 24h, verify "edited" badge appears. Attempt edit after 24h and verify rejection.

### Tests for User Story 6

- [X] T047 [P] [US6] Unit tests for comment operations: AddCommentAsync creates with correct author/content/timestamp, supports @mentions extraction to Mentions list, EditCommentAsync within 24h succeeds and sets IsEdited/EditedAt, EditCommentAsync after 24h returns COMMENT_EDIT_WINDOW_EXPIRED, DeleteCommentAsync within 1h replaces content with "[deleted]" and sets IsDeleted, DeleteCommentAsync after 1h returns COMMENT_DELETE_WINDOW_EXPIRED, CO can delete any comment, edit/delete on Done tasks blocked, CommentAdded history entry logged, Mentioned notification enqueued for each @mention — in tests/Ato.Copilot.Tests.Unit/Services/KanbanServiceCommentTests.cs

### Implementation for User Story 6

- [X] T048 [US6] Implement KanbanService.AddCommentAsync: create TaskComment with author/content/timestamp, parse @mentions from content (regex: @[\\w.@]+), set Mentions list, add CommentAdded history entry, enqueue CommentAdded notification, enqueue Mentioned notification per mention, handle threading via ParentCommentId, RBAC check (CO/SL/PE can comment, Auditor blocked) — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T049 [US6] Implement KanbanService.EditCommentAsync: validate author owns comment, validate within 24h window (CreatedAt + 24h > UtcNow), validate task not Done, update Content/EditedAt/IsEdited, add CommentEdited history entry — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T050 [US6] Implement KanbanService.DeleteCommentAsync: validate author owns comment OR user is CO, validate within 1h window for non-CO (CreatedAt + 1h > UtcNow), validate task not Done, replace Content with "[deleted]", set IsDeleted=true, add CommentDeleted history entry — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T051 [US6] Implement KanbanService.ListCommentsAsync: load comments by taskId, optionally include/exclude deleted, paginate by CreatedAt, return with author/threading info — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T052 [US6] Create KanbanAddCommentTool extending BaseTool with Name="kanban_add_comment", parameters (taskId, boardId?, content, parentCommentId?) per contracts — in src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs
- [X] T053 [US6] Create KanbanTaskCommentsTool extending BaseTool with Name="kanban_task_comments", parameters (taskId, boardId?, includeDeleted?, page?, pageSize?) per contracts — in src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs
- [X] T054 [US6] Register KanbanAddCommentTool and KanbanTaskCommentsTool in ComplianceAgent and add MCP method wrappers to ComplianceMcpTools — in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs and src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs
- [X] T101 [US6] Create KanbanEditCommentTool extending BaseTool with Name="kanban_edit_comment", parameters (commentId, taskId, boardId?, content) per contracts, wrapping KanbanService.EditCommentAsync, enforcing 24h edit window — in src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs
- [X] T102 [US6] Create KanbanDeleteCommentTool extending BaseTool with Name="kanban_delete_comment", parameters (commentId, taskId, boardId?) per contracts, wrapping KanbanService.DeleteCommentAsync, enforcing 1h delete window for non-CO — in src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs
- [X] T103 [US6] Register KanbanEditCommentTool and KanbanDeleteCommentTool in ComplianceAgent and add MCP method wrappers to ComplianceMcpTools — in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs and src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs

**Checkpoint**: Comments with threading, @mentions, edit/delete windows, and audit protection work. Edit and delete operations are exposed via MCP tools.

---

## Phase 9: User Story 7 — Remediation Execution from Task (Priority: P2)

**Goal**: As a Platform Engineer, I want to say "Fix REM-005" to execute the remediation script for that task's affected resources so that remediation and task tracking are integrated in a single workflow. On success, the task moves to In Review; on failure, an error comment is added.

**Independent Test**: Say "Fix REM-005" for a task with a remediation script, verify the script runs and the task moves to InReview on success.

### Tests for User Story 7

- [X] T055 [P] [US7] Unit tests for ExecuteTaskRemediationAsync: delegates to IRemediationEngine with task's remediationScript and affectedResources, success moves task to InReview and adds RemediationAttempt history entry, failure adds error comment and keeps status, task with no script returns error with suggestion — in tests/Ato.Copilot.Tests.Unit/Services/KanbanServiceRemediationTests.cs

### Implementation for User Story 7

- [X] T056 [US7] Implement KanbanService.ExecuteTaskRemediationAsync: validate task has RemediationScript (error if not), call IRemediationEngine.ExecuteAsync with script and affected resources, on success: move task to InReview via MoveTaskAsync, add RemediationAttempt history entry; on failure: add error comment with exception details, keep current status, add RemediationAttempt history entry with failure details — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T057 [US7] Extend existing RemediationExecuteTool or create KanbanRemediateTaskTool: detect "Fix REM-NNN" intent, resolve task by number, delegate to KanbanService.ExecuteTaskRemediationAsync — in src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs
- [X] T096 [P] [US7] Unit tests for CollectTaskEvidenceAsync: delegates to IEvidenceStorageService with task's controlId and affectedResources, adds system comment with evidence summary, task with no affectedResources returns error, RBAC allows CO/SL/PE — in tests/Ato.Copilot.Tests.Unit/Services/KanbanServiceEvidenceTests.cs
- [X] T097 [US7] Implement KanbanService.CollectTaskEvidenceAsync: load task, validate affectedResources non-empty, call IEvidenceStorageService to gather evidence for task's controlId scoped to affectedResources, add system comment with evidence collection summary, add history entry (EventType: ValidationRun) — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T098 [US7] Create KanbanCollectEvidenceTool extending BaseTool with Name="kanban_collect_evidence", parameters (taskId, boardId?, subscriptionId?) per contracts, detect "Collect evidence for REM-NNN" intent, delegate to KanbanService.CollectTaskEvidenceAsync — in src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs
- [X] T099 [US7] Register KanbanCollectEvidenceTool in ComplianceAgent and add MCP method wrapper to ComplianceMcpTools — in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs and src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs

**Checkpoint**: "Fix REM-NNN" runs remediation and "Collect evidence for REM-NNN" gathers evidence — both integrate with task lifecycle.

---

## Phase 10: User Story 8 — Filtering, Views, and Board Display (Priority: P2)

**Goal**: As a user, I want to filter my board by assignee, severity, control family, and status so that I can focus on relevant tasks. I want to save filter combinations as named views. When I say "Show my remediation board," the board renders visually with columns, severity badges, and overdue highlights.

**Independent Test**: Create a board with 10 tasks of varying severity, say "Show Critical tasks," verify only Critical tasks display.

### Tests for User Story 8

- [X] T058 [P] [US8] Unit tests for task filtering and views: ListTasksAsync filters by status/severity/assigneeId/controlFamily/isOverdue/sortBy/sortOrder, pagination works, SaveViewAsync persists via IAgentStateManager, GetViewAsync retrieves, ListViewsAsync returns all for user, DeleteViewAsync removes — in tests/Ato.Copilot.Tests.Unit/Services/KanbanServiceFilterTests.cs

### Implementation for User Story 8

- [X] T059 [US8] Implement KanbanService.ListTasksAsync: query tasks by boardId with optional filters (status, severity, assigneeId, controlFamily, isOverdue, dueDateFrom/To), apply sorting (severity desc default, dueDate, createdAt, status, controlId), paginate, compute isOverdue per task, compute commentCount — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T060 [US8] Implement KanbanService saved view operations: SaveViewAsync (serialize ViewFilters to JSON, store via IAgentStateManager with key kanban:view:{userId}:{viewName}), GetViewAsync (retrieve and deserialize), ListViewsAsync (scan keys), DeleteViewAsync (remove key) — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T061 [US8] Create KanbanTaskListTool extending BaseTool with Name="kanban_task_list", parameters (boardId, status?, severity?, assigneeId?, controlFamily?, isOverdue?, sortBy?, sortOrder?, page?, pageSize?) per contracts — in src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs
- [X] T062 [US8] Register KanbanTaskListTool in ComplianceAgent and add MCP method wrapper to ComplianceMcpTools — in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs and src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs

**Checkpoint**: Board display with filtering, sorting, pagination, and saved views works.

---

## Phase 11: User Story 9 — Bulk Operations (Priority: P3)

**Goal**: As a Compliance Officer, I want to bulk-assign, bulk-move, or bulk-set-due-date on multiple tasks in one command so that I can efficiently triage a large board without repetitive individual commands. All bulk operations require confirmation before execution.

**Independent Test**: Create 5 tasks, say "Move all my In Progress tasks to In Review," confirm, verify all 5 move.

### Tests for User Story 9

- [X] T063 [P] [US9] Unit tests for bulk operations: BulkAssignAsync assigns multiple tasks with individual history entries, BulkMoveAsync moves multiple with per-task transition validation (partial success if some fail), BulkSetDueDateAsync updates multiple due dates, each operation returns aggregate result with succeeded/failed counts and per-task results, concurrency conflicts on individual tasks reported but don't block others — in tests/Ato.Copilot.Tests.Unit/Services/KanbanServiceBulkTests.cs

### Implementation for User Story 9

- [X] T064 [US9] Implement KanbanService bulk operations: BulkAssignAsync (iterate taskIds, call AssignTaskAsync per task, aggregate results with succeeded/failed), BulkMoveAsync (iterate taskIds, call MoveTaskAsync per task, aggregate results), BulkSetDueDateAsync (iterate taskIds, update DueDate per task, add DueDateChanged history entry) — all with per-task error isolation (one failure doesn't block others) — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T065 [US9] Create KanbanBulkUpdateTool extending BaseTool with Name="kanban_bulk_update", parameters (boardId, taskIds, operation, assigneeId?, targetStatus?, dueDate?, comment?) per contracts — in src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs
- [X] T066 [US9] Register KanbanBulkUpdateTool in ComplianceAgent and add MCP method wrapper to ComplianceMcpTools — in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs and src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs

**Checkpoint**: Bulk assign/move/date operations work with partial-success reporting.

---

## Phase 12: User Story 10 — Document Generation Integration (Priority: P3)

**Goal**: As a Compliance Officer, I want POA&M documents to pull open tasks from my remediation board so that the generated document reflects the current state of remediation work with correct statuses, assignees, and due dates.

**Independent Test**: Create a board with 5 open tasks, say "Generate POA&M from current board," verify document has 5 line items.

### Tests for User Story 10

- [X] T067 [P] [US10] Unit tests for GetOpenTasksForPoamAsync: returns all non-Done tasks with controlId, status, assignee, dueDate, severity; excludes Done tasks; maps task fields to POA&M columns — in tests/Ato.Copilot.Tests.Unit/Services/KanbanServicePoamTests.cs

### Implementation for User Story 10

- [X] T068 [US10] Implement KanbanService.GetOpenTasksForPoamAsync: query tasks by boardId where Status != Done, project to POA&M line item format (ControlId, Title, Status, Assignee, DueDate, Severity, IsOverdue) — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T069 [US10] Extend DocumentGenerationTool to accept optional boardId parameter: when provided, call KanbanService.GetOpenTasksForPoamAsync and merge board tasks into POA&M output alongside existing assessment data — in src/Ato.Copilot.Agents/Compliance/Tools/DocumentGenerationTool.cs (extend, do not replace)

**Checkpoint**: POA&M generation integrates with Kanban board data.

---

## Phase 13: User Story 11 — History, Audit Trail, and Export (Priority: P3)

**Goal**: As an Auditor, I want to view and export the complete immutable audit trail for any task or board so that I can provide compliance evidence during ATO reviews. History entries cannot be modified or deleted.

**Independent Test**: Perform create/assign/move/comment/validate on a task, then "Show history for REM-005" and verify complete log.

### Tests for User Story 11

- [X] T070 [P] [US11] Unit tests for audit trail export: ExportBoardHistoryAsync returns all history across all board tasks, sorted chronologically, formatted as CSV with EventType/TaskNumber/OldValue/NewValue/ActingUser/Timestamp/Details columns, RBAC allows CO/SL/Auditor and blocks PE — in tests/Ato.Copilot.Tests.Unit/Services/KanbanServiceAuditTests.cs

### Implementation for User Story 11

- [X] T071 [US11] Implement KanbanService.ExportBoardHistoryAsync: query all TaskHistoryEntry rows for all tasks on boardId, sort by Timestamp ascending, format as CSV using StringBuilder per research R-003 (headers: EventType, TaskNumber, OldValue, NewValue, ActingUserId, ActingUserName, Timestamp, Details), RBAC check — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T072 [US11] Implement KanbanService.ExportBoardCsvAsync: query all tasks on boardId, format as CSV (TaskNumber, Title, ControlId, Severity, Status, AssigneeName, DueDate, IsOverdue, CreatedAt, UpdatedAt, Description), RBAC check — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T073 [US11] Create KanbanExportTool extending BaseTool with Name="kanban_export", parameters (boardId, format=csv|poam, statuses?, includeHistory?) per contracts — in src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs
- [X] T074 [US11] Register KanbanExportTool in ComplianceAgent and add MCP method wrapper to ComplianceMcpTools — in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs and src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs

**Checkpoint**: Full audit trail viewable and exportable. CSV and POA&M exports work.

---

## Phase 14: User Story 12 — Notifications (Priority: P3)

**Goal**: As a task assignee, I want to receive notifications via email, Teams, or Slack when a task is assigned to me, its status changes, someone comments on my task, or it becomes overdue so that I can respond promptly without polling the board.

**Independent Test**: Configure a notification channel, assign a task, verify notification is delivered.

### Tests for User Story 12

- [X] T075 [P] [US12] Unit tests for NotificationService: EnqueueAsync writes to Channel<NotificationMessage>, BackgroundService dispatcher reads and routes to correct INotificationChannel implementation, email uses MailKit SmtpClient, Teams sends Adaptive Card JSON to webhook, Slack sends Block Kit JSON to webhook, 429 responses trigger retry, permanent failures (400/401) log and skip, notification for overdue tasks enqueued by OverdueScanHostedService — in tests/Ato.Copilot.Tests.Unit/Services/NotificationServiceTests.cs
- [X] T076 [P] [US12] Unit tests for OverdueScanHostedService: queries tasks where DueDate < UtcNow and Status not Done/Blocked and LastOverdueNotifiedAt is null or > 24h ago, enqueues TaskOverdue notification per task, updates LastOverdueNotifiedAt, creates scoped DI per tick — in tests/Ato.Copilot.Tests.Unit/Services/OverdueScanHostedServiceTests.cs

### Implementation for User Story 12

- [X] T077 [US12] Implement NotificationService: Singleton with Channel<NotificationMessage> (BoundedChannel, 500 capacity, DropOldest on full), BackgroundService loop reading from channel, dispatch to per-channel implementations (EmailNotificationChannel using MailKit SmtpClient, TeamsNotificationChannel posting Adaptive Card v1.4 JSON, SlackNotificationChannel posting Block Kit JSON), IHttpClientFactory for Teams/Slack with Microsoft.Extensions.Http.Resilience retry (3x exponential backoff + jitter for 429/5xx, no retry for 400/401) — in src/Ato.Copilot.Agents/Compliance/Services/NotificationService.cs
- [X] T078 [US12] Implement OverdueScanHostedService: BackgroundService with PeriodicTimer (configurable interval from Kanban:OverdueScan:IntervalMinutes), per tick create IServiceScope, resolve IKanbanService (Scoped), query overdue tasks, enqueue TaskOverdue notification per task, update task.LastOverdueNotifiedAt, add system comment "Task is overdue" — in src/Ato.Copilot.Agents/Compliance/Services/OverdueScanHostedService.cs
- [X] T079 [US12] Implement notification config operations: save/load NotificationConfig per user via IAgentStateManager with key kanban:notifications:{userId} — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs

**Checkpoint**: Multi-channel notifications (email, Teams, Slack) with overdue detection background service work.

---

## Phase 15: User Story 13 — Board Management (Priority: P3)

**Goal**: As a Compliance Officer, I want to archive completed boards so that old boards are excluded from active views but remain retrievable for audit purposes. I want to export board data to CSV for offline reporting.

**Independent Test**: Archive a board with all tasks Done, verify it disappears from active list but is retrievable with "Show archived boards."

### Tests for User Story 13

- [X] T080 [P] [US13] Unit tests for ArchiveBoardAsync: archives board with all Done tasks, rejects archive with open tasks (TASKS_REMAINING), sets IsArchived=true, RBAC allows CO only — in tests/Ato.Copilot.Tests.Unit/Services/KanbanServiceBoardMgmtTests.cs

### Implementation for User Story 13

- [X] T081 [US13] Implement KanbanService.ArchiveBoardAsync: validate all tasks are Done (return TASKS_REMAINING if not), validate RBAC (CO only), set IsArchived=true and UpdatedAt, save — in src/Ato.Copilot.Agents/Compliance/Services/KanbanService.cs
- [X] T082 [US13] Create KanbanArchiveBoardTool extending BaseTool with Name="kanban_archive_board", parameters (boardId, confirm) per contracts — in src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs
- [X] T083 [US13] Register KanbanArchiveBoardTool in ComplianceAgent and add MCP method wrapper to ComplianceMcpTools — in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs and src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs

**Checkpoint**: Board archival and lifecycle management works.

---

## Phase 16: User Story 14 — Context-Aware Chat (Priority: P3)

**Goal**: As a user, I want the Compliance Agent to remember my last displayed tasks so that I can say "Move the first one to In Progress" without repeating the task ID — making the chat interface feel natural and efficient.

**Independent Test**: Say "Show my tasks," receive a list, then "Move the first one to In Progress," verify correct task moves.

### Tests for User Story 14

- [X] T084 [P] [US14] Unit tests for context resolution: after listing tasks, resolve "the first one" / "the second one" to correct task ID from last result stored in IConversationStateManager, no context available returns prompt asking for task ID — in tests/Ato.Copilot.Tests.Unit/Agents/KanbanContextResolutionTests.cs

### Implementation for User Story 14

- [X] T085 [US14] Implement context-aware task resolution in ComplianceAgent: after any tool that returns a task list, store task IDs in IConversationStateManager with key kanban:lastResults; on subsequent commands with ordinal references ("first," "second," "last"), resolve to stored task ID; if no context, prompt user for explicit task ID — in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs

**Checkpoint**: Conversational context resolution makes the chat feel natural.

---

## Phase 17: Polish & Cross-Cutting Concerns

**Purpose**: Integration tests, documentation, performance, and final validation

- [X] T086 [P] Integration test for board creation from assessment via HTTP: POST to MCP endpoint with kanban_create_board + assessmentId, verify board created with correct tasks in tests/Ato.Copilot.Tests.Integration/KanbanBoardCreationIntegrationTests.cs
- [X] T087 [P] Integration test for full task lifecycle via HTTP: create board → create task → assign → move through all statuses → close, verify history trail in tests/Ato.Copilot.Tests.Integration/KanbanTaskLifecycleIntegrationTests.cs
- [X] T088 [P] Integration test for RBAC enforcement via HTTP: verify Auditor blocked from create/assign/move, PE blocked from assign-others, CO can archive in tests/Ato.Copilot.Tests.Integration/KanbanRbacIntegrationTests.cs
- [X] T089 [P] Integration test for concurrency: two simultaneous MoveTaskAsync on same task, verify one succeeds and one returns CONCURRENCY_CONFLICT in tests/Ato.Copilot.Tests.Integration/KanbanConcurrencyIntegrationTests.cs
- [X] T090 Add Kanban agent prompt template defining Kanban-specific persona, capabilities, and natural language routing for board/task/comment/filter/bulk intents in src/Ato.Copilot.Agents/Compliance/Prompts/KanbanAgent.prompt.txt
- [ ] T091 Run quickstart.md verification checklist (32 items) to validate all end-to-end scenarios pass
- [X] T092 Verify build: 0 errors, 0 warnings with dotnet build Ato.Copilot.sln
- [X] T093 Verify all tests pass with dotnet test Ato.Copilot.sln

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — **BLOCKS all user stories**
- **User Stories P1 (Phases 3–6)**: All depend on Foundational phase. Execute sequentially: US1 → US2 → US3 → US4 (each builds on prior)
- **User Stories P2 (Phases 7–10)**: Depend on P1 completion. Can run in parallel with each other.
- **User Stories P3 (Phases 11–16)**: Depend on P2 completion. Can run in parallel with each other.
- **Polish (Phase 17)**: Depends on all desired user stories being complete

### User Story Dependencies

| Story | Depends On | Can Parallel With |
|-------|-----------|-------------------|
| US1 (Board from Assessment) | Foundational | — (first story) |
| US2 (Manual Creation) | US1 (shares CreateBoard/GetBoard) | — |
| US3 (Assignment) | US2 (needs tasks to exist) | — |
| US4 (Status Transitions) | US3 (needs assigned tasks) | — |
| US5 (Validation) | US4 (needs MoveTaskAsync) | US6, US7, US8 |
| US6 (Comments) | US4 (needs task lifecycle) | US5, US7, US8 |
| US7 (Remediation Exec) | US4 (needs MoveTaskAsync) | US5, US6, US8 |
| US8 (Filtering/Views) | US4 (needs tasks with statuses) | US5, US6, US7 |
| US9 (Bulk Ops) | US4 (needs move/assign) | US10, US11, US12, US13, US14 |
| US10 (POA&M Integration) | US4 (needs open tasks) | US9, US11, US12, US13, US14 |
| US11 (Audit/Export) | US4 (needs history entries) | US9, US10, US12, US13, US14 |
| US12 (Notifications) | US4 (needs lifecycle events) | US9, US10, US11, US13, US14 |
| US13 (Board Mgmt) | US4 (needs board with tasks) | US9, US10, US11, US12, US14 |
| US14 (Context Chat) | US8 (needs task lists) | US9, US10, US11, US12, US13 |

### Within Each User Story

1. Tests MUST be written and FAIL before implementation
2. Service methods before tool wrappers
3. Tool registration after tool classes exist
4. Core implementation before integration with existing tools
5. Story complete before moving to next priority

### Parallel Opportunities per Phase

**Foundational (Phase 2)**:
```
T004 (enums) ─┐
T005 (consts) ─┤── all [P], no dependencies
T006 (base)  ──┤
T012 (trans) ──┤
T013 (perms) ──┤
T014 (iface) ──┤
T015 (notify)──┘
                then → T007, T008 (models, depend on enums)
                then → T009, T010 (DbContext, depend on models)
                then → T011 (migration, depends on DbContext)
                then → T016 (DI, depends on all)
```

**P2 Stories (Phases 7–10)** — all can start simultaneously after US4:
```
US5 (Validation)     ──┐
US6 (Comments)       ──┤── different service methods, different tool classes
US7 (Remediation)    ──┤
US8 (Filtering)      ──┘
```

---

## Implementation Strategy

### MVP First (User Stories 1–4 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (**CRITICAL — blocks everything**)
3. Complete Phase 3: US1 — Board Creation from Assessment
4. Complete Phase 4: US2 — Manual Task/Board Creation
5. Complete Phase 5: US3 — Task Assignment
6. Complete Phase 6: US4 — Status Transitions
7. **STOP and VALIDATE**: Test full Kanban workflow independently
8. Deploy/demo if ready — this is a fully functional Kanban board

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. US1–US4 → Core Kanban workflow (**MVP!**)
3. US5 → + Validation → Deploy/Demo
4. US6 → + Comments → Deploy/Demo
5. US7 → + Remediation integration → Deploy/Demo
6. US8 → + Filtering/Views → Deploy/Demo
7. US9–US14 → + Polish features → Deploy/Demo
8. Each story adds value without breaking previous stories

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks in the same phase
- [Story] label maps task to its user story for traceability
- Each user story goal is written as a user story ("As a..., I want... so that...")
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- KanbanService.cs is the single service file — tasks within that file are NOT parallelizable
- KanbanTools.cs contains all tool classes — tool registration tasks depend on tool creation tasks
