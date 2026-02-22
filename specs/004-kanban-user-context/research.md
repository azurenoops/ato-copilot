# Research: Kanban User Context & Comment Permission Enhancement

**Feature**: `004-kanban-user-context` | **Date**: 2026-02-22

## Research Task 1: IUserContext Abstraction Pattern

### Decision: Scoped interface in Core, IHttpContextAccessor-backed implementation in Mcp

### Rationale
- `IUserContext` interface lives in `Ato.Copilot.Core/Interfaces/Auth/` (alongside existing `ICacSessionService`, `IPimService`, `IJitVmAccessService`, `ICertificateRoleResolver`).
- The interface has no ASP.NET Core dependencies — just `string UserId`, `string DisplayName`, `string Role` properties — so `Ato.Copilot.Agents` can reference it without adding an ASP.NET Core framework reference.
- The concrete implementation (`HttpUserContext`) lives in `Ato.Copilot.Mcp` and uses `IHttpContextAccessor` to lazily read claims from `HttpContext.User`.
- **Critical**: Tools are registered as **Singletons** (in `ServiceCollectionExtensions.cs` L86) but create per-invocation DI scopes via `ScopeFactory.CreateScope()`. A naively scoped `IUserContext` registered in the request scope would NOT be available in the tool's child scope. The `IHttpContextAccessor`-based implementation solves this because `HttpContext` is ambient (accessible across scopes within the same HTTP request via `AsyncLocal<T>` backing).

### Alternatives Considered
1. **HttpContext.Items dictionary** — Rejected: requires string-keyed lookups, no compile-time safety, couples tools to HttpContext awareness.
2. **AsyncLocal\<UserContext\>** — Rejected: redundant — `IHttpContextAccessor` already uses `AsyncLocal` internally. Adding a second `AsyncLocal` would be unnecessary complexity.
3. **Add IUserContext as constructor parameter to BaseTool** — Rejected: tools are Singletons; scoped services cannot be injected into Singleton constructors.
4. **Add `IHttpContextAccessor` to Agents project** — Rejected: would add ASP.NET Core dependency to the Agents layer, violating the current separation of concerns.

## Research Task 2: Claim Extraction from ComplianceAuthorizationMiddleware

### Decision: Reuse existing claim extraction patterns; populate IUserContext via middleware

### Rationale
The `ComplianceAuthorizationMiddleware` already extracts all needed identity claims:

| Data | Claim Source | Existing Code |
|------|-------------|---------------|
| User ID | `"oid"` (primary), `"sub"` (fallback) | `context.User?.FindFirst("oid")?.Value ?? context.User?.FindFirst("sub")?.Value` |
| Display Name | `Identity.Name` | `context.User.Identity?.Name` |
| Roles | ASP.NET Core role claims | `context.User.IsInRole(ComplianceRoles.Administrator)` etc. |

The `HttpUserContext` implementation reads these same claims lazily when properties are accessed, rather than requiring the middleware to explicitly populate it. This means:
- No changes to `ComplianceAuthorizationMiddleware` are needed for data population.
- The `HttpUserContext` class reads from `IHttpContextAccessor.HttpContext.User` directly.
- Fallback defaults (`"anonymous"` for userId, userId for displayName, `ComplianceRoles.Viewer` for role) are built into the implementation.

### Role Resolution Logic
The `HttpUserContext` resolves the highest-privilege applicable role by checking roles in priority order:
1. `ComplianceRoles.Administrator` → `"Compliance.Administrator"`
2. `ComplianceRoles.SecurityLead` → `"Compliance.SecurityLead"`
3. `ComplianceRoles.Analyst` → `"Compliance.Analyst"`
4. `ComplianceRoles.Auditor` → `"Compliance.Auditor"`
5. `ComplianceRoles.PlatformEngineer` → `"Compliance.PlatformEngineer"`
6. Default: `ComplianceRoles.Viewer` → `"Compliance.Viewer"`

## Research Task 3: Hardcoded Identity Bug — "Compliance.Officer" Does Not Exist

### Decision: Replace all hardcoded identity strings with IUserContext properties

### Rationale
**Critical finding**: The hardcoded role string `"Compliance.Officer"` used in 8 places across KanbanTools.cs **does not exist** in the `ComplianceRoles` constants or the `KanbanPermissionsHelper` role-permission matrix. The actual roles are:
- `Compliance.Administrator`
- `Compliance.SecurityLead`
- `Compliance.Analyst`
- `Compliance.Auditor`
- `Compliance.Viewer`
- `Compliance.PlatformEngineer`

This means that when tools pass `"Compliance.Officer"` as the `actingUserRole` to service methods, the `KanbanPermissionsHelper.CanPerformAction("Compliance.Officer", ...)` call **always returns false** because the role is not in the permission matrix. The service methods that rely on role checks for fine-grained authorization (e.g., `CanCloseWithoutValidation`, `CanDeleteAnyComment`) have been operating with **no effective RBAC permissions** for the hardcoded role.

This is a pre-existing bug that this feature will fix: replacing `"Compliance.Officer"` with `userContext.Role` will ensure the authenticated user's actual role is checked against the permission matrix.

### Evidence
- KanbanPermissionsHelper matrix (4 roles): `Compliance.Administrator`, `Compliance.SecurityLead`, `Compliance.Analyst`, `Compliance.Auditor`
- Hardcoded role in tools: `"Compliance.Officer"` (not in matrix → always fails permission check)
- ComplianceRoles constants: 6 roles defined, none named `"Compliance.Officer"`

## Research Task 4: SecurityLead Permission Gap

### Decision: Add `KanbanPermissions.CanDeleteAnyComment` to SecurityLead role in KanbanPermissionsHelper

### Rationale
The spec (FR-054) states "Compliance Officers can delete any comment." In the organizational model:
- `Compliance.Administrator` = Chief Compliance Officer (already has `CanDeleteAnyComment`)
- `Compliance.SecurityLead` = Day-to-day Compliance Officer (currently missing `CanDeleteAnyComment`)

Adding `CanDeleteAnyComment` to the SecurityLead permission set aligns with the spec intent and operational reality.

### Alternatives Considered
1. **Create a new "ComplianceOfficer" role** — Rejected: would require schema changes, new role constant, migration. The SecurityLead role already serves this function.
2. **Grant to Analyst too** — Rejected: Analysts should only manage their own comments per the principle of least privilege. The spec explicitly limits "delete any" to Compliance Officers.

## Research Task 5: Tool Registration and Dispatch

### Decision: No changes to tool registration or dispatch pattern needed

### Rationale
All 18 Kanban tool classes follow the same pattern:
1. Registered as Singletons in `ServiceCollectionExtensions.cs`
2. Constructor takes `IServiceScopeFactory` + `ILogger<T>`
3. `ExecuteCoreAsync` creates a scope, resolves `IKanbanService`, performs the operation

The only change is to also resolve `IUserContext` from the scope:
```
var userContext = scope.ServiceProvider.GetRequiredService<IUserContext>();
```

Since the `HttpUserContext` implementation uses `IHttpContextAccessor` internally, it reads the ambient `HttpContext.User` regardless of which DI scope it's resolved from. This is the standard ASP.NET Core pattern for cross-scope access to request-scoped data.

### Tools Requiring Changes (12 of 18 have hardcoded identity strings)

| Tool Class | Lines with hardcoded identity | Changes needed |
|------------|-------------------------------|----------------|
| `KanbanCreateBoardTool` | L128 | Replace `"system"` with `userContext.UserId` |
| `KanbanCreateTaskTool` | L290 | Replace `"system"` with `userContext.UserId` |
| `KanbanAssignTaskTool` | L326, L330-331 | Replace 3 hardcoded strings |
| `KanbanMoveTaskTool` | L367, L371-372 | Replace 3 hardcoded strings |
| `KanbanAddCommentTool` | L735 | Replace 3 hardcoded strings |
| `KanbanEditCommentTool` | L801 | Replace `"system"` with `userContext.UserId` |
| `KanbanDeleteCommentTool` | L836 | Replace 2 hardcoded strings |
| `KanbanRemediateTaskTool` | L903 | Replace 2 hardcoded strings |
| `KanbanCollectEvidenceTool` | L949 | Replace 2 hardcoded strings |
| `KanbanBulkUpdateTool` | L1016, L1022, L1027 | Replace 3×3 hardcoded strings |
| `KanbanExportTool` | L1085-1086 | Replace 2×2 hardcoded strings |
| `KanbanArchiveBoardTool` | L1131, L1137 | Replace 3 hardcoded strings |

**6 tools do NOT have hardcoded identity** (read-only tools or validation tools that don't pass user identity to service):
- `KanbanBoardShowTool` — but needs `isAssignedToCurrentUser` flag added
- `KanbanGetTaskTool` — but needs `isAssignedToCurrentUser` flag added
- `KanbanTaskListTool` — but needs `isAssignedToCurrentUser` flag added
- `KanbanValidateTaskTool` — read-only validation, no changes
- `KanbanTaskHistoryTool` — read-only, no changes
- `KanbanTaskCommentsTool` — read-only, no changes

**Total**: 15 of 18 tools need IUserContext resolution (12 for identity replacement, 3 for isAssignedToCurrentUser flag). KanbanValidateTaskTool, KanbanTaskHistoryTool, and KanbanTaskCommentsTool are truly unchanged.
