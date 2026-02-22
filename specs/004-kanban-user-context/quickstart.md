# Quickstart: Kanban User Context & Comment Permission Enhancement

**Feature**: `004-kanban-user-context` | **Date**: 2026-02-22

## What This Feature Does

This feature makes the Kanban board aware of who is using it. Currently, all actions are attributed to a generic "system" user. After this change:

1. **Your tasks are highlighted** — When you view the board, tasks assigned to you are flagged with `isAssignedToCurrentUser: true` so clients can visually distinguish them.
2. **Actions are attributed to you** — When you move a task, add a comment, or perform any operation, your real identity is recorded in history and audit logs.
3. **Security Leads can moderate comments** — Security Leads (Compliance Officers) can delete any comment, not just Administrators.

## Key Concepts

### User Context Flow

```
HTTP Request → Auth Middleware → HttpContext.User (claims)
                                       │
                                       ▼
                               IUserContext (interface)
                                       │
                                       ▼
                            KanbanTools → KanbanService
                              (uses real user identity)
```

- **IUserContext** is the interface that all tools use to get user identity.
- **HttpUserContext** reads claims from the HTTP request automatically.
- When no auth is present (e.g., development, stdio mode), safe defaults are used (`"anonymous"`, `Compliance.Viewer`).

### What Changed for Developers

| Before | After |
|--------|-------|
| `svc.MoveTaskAsync(taskId, status, "system", "System", "Compliance.Officer", ...)` | `svc.MoveTaskAsync(taskId, status, userContext.UserId, userContext.DisplayName, userContext.Role, ...)` |
| No `isAssignedToCurrentUser` in responses | Every task response includes `isAssignedToCurrentUser` boolean |
| Only Administrator can delete any comment | Administrator **and** SecurityLead can delete any comment |

## Build & Test

```bash
# Build
dotnet build Ato.Copilot.sln

# Run all tests
dotnet test Ato.Copilot.sln

# Run only unit tests
dotnet test tests/Ato.Copilot.Tests.Unit/Ato.Copilot.Tests.Unit.csproj

# Run only integration tests
dotnet test tests/Ato.Copilot.Tests.Integration/Ato.Copilot.Tests.Integration.csproj
```

## Files to Know

| File | Purpose |
|------|---------|
| `src/Ato.Copilot.Core/Interfaces/Auth/IUserContext.cs` | The user context interface (UserId, DisplayName, Role, IsAuthenticated) |
| `src/Ato.Copilot.Mcp/Models/HttpUserContext.cs` | Implementation using IHttpContextAccessor |
| `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs` | All 18 Kanban tools — resolves IUserContext from DI scope |
| `src/Ato.Copilot.Agents/Compliance/Services/KanbanPermissionsHelper.cs` | Role-permission matrix (SecurityLead now has CanDeleteAnyComment) |
