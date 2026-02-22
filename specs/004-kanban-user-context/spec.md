# Feature Specification: Kanban User Context & Comment Permission Enhancement

**Feature Branch**: `004-kanban-user-context`  
**Created**: 2026-02-22  
**Status**: Draft  
**Input**: User description: "Kanban board user context propagation and comment permission enhancement — address gaps in FR-055 (current user task distinction) and FR-054 (SecurityLead comment deletion permission)"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Current User Task Highlighting (Priority: P1)

As a compliance team member viewing the Kanban board, I want tasks assigned to me to be visually distinguished from other tasks so I can immediately identify my workload without scanning assignee fields on every card.

**Why this priority**: This is the primary gap identified. Without user identity flowing through the system, the board cannot differentiate between the current user's tasks and others' — reducing efficiency for every team member on every board interaction.

**Independent Test**: Can be fully tested by authenticating as a specific user, assigning tasks to that user and to others, viewing the board, and verifying that the response includes a flag distinguishing the current user's tasks from other tasks.

**Acceptance Scenarios**:

1. **Given** a board with 5 tasks (3 assigned to the current user, 2 to others), **When** the current user views the board, **Then** each task in the response includes a flag indicating whether it is assigned to the current user.
2. **Given** a board with tasks, **When** a user views the board who has no assigned tasks, **Then** no tasks are flagged as assigned to the current user.
3. **Given** a task is reassigned from the current user to another user, **When** the current user refreshes the board, **Then** that task is no longer flagged as assigned to the current user.
4. **Given** the system cannot determine the current user identity, **When** the board is viewed, **Then** the current-user flag defaults to false for all tasks and the system does not error.

---

### User Story 2 - User Identity Propagation Through Tools (Priority: P1)

As a system operator, I need the real user identity (user ID and display name) to flow from the request context into every Kanban tool invocation so that actions are attributed to the actual user rather than a hardcoded "system" placeholder.

**Why this priority**: This is the foundational enabler for User Story 1 and is also critical for accurate audit trails. Currently, all Kanban tool invocations use hardcoded values ("system", "System", "Compliance.Officer") rather than the authenticated user's identity.

**Independent Test**: Can be fully tested by invoking any Kanban tool with a known user identity in the request context and verifying that the tool passes that identity (not "system") to the underlying service layer.

**Acceptance Scenarios**:

1. **Given** a user authenticated as "jane.doe" with display name "Jane Doe", **When** they invoke any Kanban tool (e.g., move task, add comment), **Then** the service receives "jane.doe" as the acting user ID and "Jane Doe" as the display name.
2. **Given** a request with no authenticated user context, **When** a Kanban tool is invoked, **Then** the system falls back to a reasonable default identity (e.g., "anonymous") rather than erroring.
3. **Given** a user performs multiple Kanban operations in one session, **When** history and audit logs are reviewed, **Then** all actions are attributed to the actual user identity rather than "system".

---

### User Story 3 - SecurityLead Comment Deletion (Priority: P2)

As a Security Lead (Compliance Officer), I need the ability to delete any comment on any task so I can moderate discussions and remove inappropriate or incorrect content, just as an Administrator can.

**Why this priority**: This is a minor permission gap. The spec defines "Compliance Officers" as having the ability to delete any comment, but the current implementation only grants this to the Administrator role. Security Leads are the primary day-to-day compliance officers and should have this capability.

**Independent Test**: Can be fully tested by authenticating as a SecurityLead role, attempting to delete another user's comment on a task, and verifying success.

**Acceptance Scenarios**:

1. **Given** a comment authored by an Analyst on a task, **When** a user with SecurityLead role attempts to delete it, **Then** the comment is successfully soft-deleted.
2. **Given** a comment older than the 1-hour delete window, **When** a SecurityLead attempts to delete it, **Then** the deletion succeeds (SecurityLeads bypass the time window restriction).
3. **Given** a user with Analyst role, **When** they attempt to delete another user's comment, **Then** the operation is rejected with an authorization error.

---

### Edge Cases

- What happens when a user's identity is present but their user ID does not match any assignee on the board? The current-user flag is false for all tasks; no error occurs.
- What happens when the user identity claim format changes (e.g., email vs. UPN vs. GUID)? The system performs exact string matching on the user ID; identity format consistency is the responsibility of the authentication layer.
- What happens when a task has no assignee? The current-user flag is false; the task renders normally.
- What happens when the authenticated user context is missing the display name claim? The system falls back to the user ID as the display name.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST extract the authenticated user's identity (user ID, display name, and role) from the request context and make it available to all Kanban tool invocations.
- **FR-002**: System MUST pass the real user identity to the Kanban service layer and MCP server session initialization for all operations that currently use hardcoded identity values ("system", "mcp-user") — including task creation, task moves, assignments, comments, bulk updates, exports, and conversation state initialization.
- **FR-003**: System MUST include an `isAssignedToCurrentUser` flag in every task representation returned by the board show tool, task list tool, and get-task tool.
- **FR-004**: The `isAssignedToCurrentUser` flag MUST be computed by comparing the authenticated user's ID against the task's assignee ID.
- **FR-005**: When no authenticated user context is available, the `isAssignedToCurrentUser` flag MUST default to `false` for all tasks.
- **FR-006**: When no authenticated user context is available, the system MUST fall back to the default identity `"anonymous"` rather than failing.
- **FR-007**: The SecurityLead role MUST be granted the "delete any comment" permission, matching the Administrator role's existing capability.
- **FR-008**: All new audit log entries and task history entries MUST reflect the real user identity when user context is available, replacing the current hardcoded "system" attribution.

### Key Entities

- **User Context**: The authenticated user's identity extracted from the request context. Key attributes: user ID, display name, compliance role. Relationship: consumed by all Kanban tools to attribute actions and compute the current-user flag.
- **Task Response**: The data representation of a Kanban task returned by tools. Extended with `isAssignedToCurrentUser` boolean flag. Relationship: derived from the RemediationTask entity and the current User Context.

### Assumptions

- The authentication middleware already populates user identity claims in the request context (established in Feature 003 CAC/PIM work). Tools need to extract these claims rather than creating a new authentication mechanism.
- User ID format is consistent within the system (the same identifier format used in `AssigneeId` is used in the authentication claims).
- "Compliance Officer" in the original FR-054 spec refers to both Administrator and SecurityLead roles, consistent with their day-to-day operational responsibilities.
- The Analyst and Auditor roles should NOT receive the "delete any comment" permission — only Administrator and SecurityLead.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of Kanban tool invocations pass the real user identity to the service layer when user context is available, with zero instances of hardcoded "system" user attribution in authenticated requests.
- **SC-002**: Every task in board show, task list, and get-task responses includes the `isAssignedToCurrentUser` flag, correctly computed for the requesting user.
- **SC-003**: Users with the SecurityLead role can successfully delete any comment on any task, matching the Administrator role's existing capability.
- **SC-004**: When user context is unavailable, all Kanban tools continue to function without errors, using a default identity and setting `isAssignedToCurrentUser` to false.
- **SC-005**: All existing unit and integration tests continue to pass after changes, with new tests covering the user context propagation, current-user flag computation, and SecurityLead comment deletion permission.
