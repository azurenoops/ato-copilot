# Quickstart: Remediation Kanban

**Branch**: `002-remediation-kanban` | **Date**: 2026-02-21

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Azure CLI (`az`) authenticated to an Azure subscription
- A prior compliance assessment (from Feature 001) — or create boards manually
- Required Azure RBAC roles on target subscription:
  - **Reader** (resource discovery for validation scans)
  - **Security Reader** (Defender for Cloud) [optional: validation enrichment]

## Build & Run

```bash
# Build
dotnet build Ato.Copilot.sln

# Run tests
dotnet test

# Run in HTTP mode
cd src/Ato.Copilot.Mcp
dotnet run -- --http
# Server starts at http://localhost:3001

# Run in stdio mode (for GitHub Copilot / Claude Desktop)
cd src/Ato.Copilot.Mcp
dotnet run -- --stdio
```

## End-to-End Walkthrough

### Step 1: Create a Board from an Assessment

```
User: Create a remediation board from my latest assessment
Agent: Creating board from assessment a1b2c3d4...

  ━━━ Board Created ━━━
  Name: Assessment 2026-02-21 Remediation
  Board ID: b1-...
  Subscription: abc-123-def-456

  48 tasks created from findings:
  ┌───────────┬──────┐
  │ Critical  │    3 │
  │ High      │   12 │
  │ Medium    │   20 │
  │ Low       │   13 │
  └───────────┴──────┘

  All tasks placed in Backlog column.
  SLA deadlines applied: Critical=24h, High=7d, Medium=30d, Low=90d.
```

### Step 2: View the Board

```
User: Show the board
Agent: ━━━ Q1 2026 FedRAMP Audit ━━━
  Owner: admin@contoso.com | 48 tasks | 3 overdue

  Backlog (20)         ToDo (10)        InProgress (8)
  ──────────────       ──────────       ──────────────
  REM-001 AC-2.1 ⚠️C   REM-005 AU-3 H   REM-015 SC-7 H
  REM-002 AC-6   H     REM-006 CM-2 M   REM-016 SC-8 M
  REM-003 AC-17  M     REM-007 CM-6 M   ...
  ...                  ...

  InReview (5)         Blocked (2)      Done (3)
  ──────────────       ──────────       ──────────
  REM-030 IA-2   M     REM-040 PE-3 H   REM-045 MP-2 L
  ...                  REM-041 PS-6 M   ...

  Completion: 6.25% | Overdue: REM-001, REM-002, REM-003
```

### Step 3: Triage — Move Tasks to ToDo

```
User: Move all critical tasks to ToDo
Agent: Moving 3 critical tasks to ToDo...

  ✅ REM-001 (AC-2.1: Enable MFA) → ToDo
  ✅ REM-012 (SC-7: Boundary Protection) → ToDo
  ✅ REM-033 (IA-5: Authenticator Mgmt) → ToDo

  3/3 tasks moved successfully.
```

### Step 4: Assign Tasks

```
User: Assign REM-001 to Jane Smith
Agent: ✅ REM-001 (AC-2.1: Enable MFA) assigned to Jane Smith (jane@contoso.com)
  Due: Feb 22, 2026 (OVERDUE — 1 day past)
```

### Step 5: Start Work — Move to InProgress

```
User: Move REM-001 to InProgress
Agent: ✅ REM-001 moved: ToDo → InProgress
  Assigned to: Jane Smith
  Due: Feb 22, 2026 (OVERDUE)
```

### Step 6: Add a Comment

```
User: Add comment to REM-001: Applied MFA enforcement policy via Azure AD Conditional Access. @lead@contoso.com please review.
Agent: ✅ Comment added to REM-001
  Author: jane@contoso.com
  Mentions: lead@contoso.com (notified)
  Timestamp: 2026-02-21T14:30:00Z
```

### Step 7: Submit for Review

```
User: Move REM-001 to InReview
Agent: ✅ REM-001 moved: InProgress → InReview
  Triggering validation scan for AC-2.1...

  Validation Results:
  ✅ MFA enforced for 3/3 admin accounts
  ✅ Conditional Access policy is active
  ✅ No exemptions detected

  Validation: PASS — task can be closed.
  Reply 'close REM-001' to mark as Done.
```

### Step 8: Close the Task

```
User: Close REM-001
Agent: ✅ REM-001 moved: InReview → Done
  AC-2.1: Enable MFA — RESOLVED
  Duration: 4 hours (from creation to close)
  History: 6 events logged
```

### Step 9: Handle a Blocked Task

```
User: Block REM-040 — waiting on physical access card reader installation
Agent: ✅ REM-040 moved: InProgress → Blocked
  Blocker comment recorded: "Waiting on physical access card reader installation"
  History entry created.

User: Unblock REM-040 — card readers installed on Feb 20
Agent: ✅ REM-040 moved: Blocked → InProgress
  Resolution comment: "Card readers installed on Feb 20"
```

### Step 10: Filter and Search

```
User: Show all overdue tasks assigned to Jane
Agent: 2 overdue tasks assigned to Jane Smith:

  REM-001  AC-2.1   Critical  InReview   Due: Feb 22 (1d overdue)
  REM-015  SC-7.3   High      InProgress Due: Feb 20 (3d overdue)

User: Show all blocked tasks
Agent: 2 blocked tasks:

  REM-040  PE-3    High    Blocked since Feb 19
           Blocker: "Waiting on card reader installation"
  REM-041  PS-6    Medium  Blocked since Feb 18
           Blocker: "Pending HR background check completion"
```

### Step 11: View Task History

```
User: Show history for REM-001
Agent: ━━━ REM-001: AC-2.1 Enable MFA — History ━━━

  Feb 21 10:30  Created         admin@contoso.com     From assessment a1b2c3d4
  Feb 21 10:45  Status Changed  admin@contoso.com     Backlog → ToDo
  Feb 21 10:46  Assigned        admin@contoso.com     → Jane Smith
  Feb 21 11:00  Status Changed  jane@contoso.com      ToDo → InProgress
  Feb 21 14:30  Comment Added   jane@contoso.com      "Applied MFA enforcement..."
  Feb 21 14:35  Status Changed  jane@contoso.com      InProgress → InReview
  Feb 21 14:35  Validation Run  system                PASS — 3/3 resources compliant
  Feb 21 15:00  Status Changed  lead@contoso.com      InReview → Done
```

### Step 12: Export for POA&M

```
User: Export board as POA&M
Agent: Generating POA&M export...

  ✅ POA&M export ready
  Format: CSV (POA&M-compatible)
  Tasks: 48 | Open: 45 | Closed: 3
  Size: 12.5 KB

  Columns: TaskNumber, ControlId, Title, Severity, Status,
           Assignee, DueDate, IsOverdue, RemediationPlan, ValidationStatus
```

### Step 13: Create a Manual Board

```
User: Create a new empty board called "Infrastructure Hardening Q2"
Agent: ✅ Board created
  Name: Infrastructure Hardening Q2
  Board ID: b2-...
  Tasks: 0

  Use 'add task to board b2-...' to add tasks manually.

User: Add task: SC-7.5 boundary protection for VNet peering, severity High
Agent: ✅ Task REM-001 created on "Infrastructure Hardening Q2"
  Control: SC-7.5 | Severity: High
  Due: Feb 28, 2026 (7 days)
  Status: Backlog
```

## Verification Checklist

After implementation, verify these scenarios work end-to-end:

- [ ] Create board from existing assessment with auto-populated tasks
- [ ] Create empty board manually
- [ ] Create task on a board manually with control ID and severity
- [ ] View board showing all 6 columns with task counts
- [ ] Move task forward through Backlog → ToDo → InProgress → InReview → Done
- [ ] Move task to Blocked with required blocker comment
- [ ] Move task from Blocked with required resolution comment
- [ ] Attempt invalid transition (Done → ToDo) and see error
- [ ] Assign task to a user and see notification
- [ ] Unassign a task
- [ ] Self-assign an unassigned task as Platform Engineer
- [ ] Add comment with @mention
- [ ] Edit own comment within 24h window
- [ ] Attempt to edit comment after 24h and see error
- [ ] Soft-delete a comment and see "[deleted]" placeholder
- [ ] View task history with all event types
- [ ] Filter tasks by severity, status, assignee, control family
- [ ] Filter overdue tasks
- [ ] Bulk move multiple tasks to ToDo
- [ ] Bulk assign multiple tasks to same user
- [ ] Trigger validation scan from InReview and see pass/fail
- [ ] Close task after validation pass
- [ ] Compliance Officer closes task without validation (skipValidation)
- [ ] Export board as CSV
- [ ] Export board as POA&M-compatible CSV
- [ ] Archive board with all tasks Done
- [ ] Attempt to archive board with open tasks and see error
- [ ] Attempt to modify task on archived board and see error
- [ ] Platform Engineer cannot see other team's tasks
- [ ] Auditor cannot create or modify tasks
- [ ] Overdue tasks trigger notification (background service)
- [ ] Concurrency conflict on simultaneous task move shows retry message
- [ ] Missing subscription falls back to configured default
