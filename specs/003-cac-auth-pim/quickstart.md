# Quickstart: CAC Authentication & Privileged Identity Management

**Feature**: 003-cac-auth-pim | **Date**: 2026-02-22

## Prerequisites

- [x] .NET 9.0 SDK installed
- [x] Feature 001 (Core Compliance) implemented and passing
- [x] Feature 002 (Remediation Kanban) implemented and passing
- [ ] Azure AD app registration configured with:
  - API permissions: `Microsoft Graph > RoleManagement.ReadWrite.Directory`, `Microsoft Graph > User.Read`
  - Authentication: Certificate-based authentication enabled
  - Expose an API: Application ID URI set, scopes defined for OBO
- [ ] Azure AD PIM enabled (P2 license) with eligible role assignments configured (read-eligible and write-eligible tiers defined)
- [ ] Azure Defender for Cloud Standard tier enabled (for JIT VM access)
- [ ] Azure Key Vault provisioned with managed identity access (production only; dev uses appsettings)
- [ ] CAC/PIV smart card reader and middleware installed on dev workstation (skip for unit tests)

## Setup

### 1. Add NuGet Packages

```bash
cd src/Ato.Copilot.Core
dotnet add package Microsoft.Identity.Client --version 4.68.0
dotnet add package Microsoft.Identity.Web --version 3.5.0

cd ../Ato.Copilot.Agents
dotnet add package Microsoft.Graph --version 5.70.0

cd ../Ato.Copilot.Mcp
dotnet add package Microsoft.Identity.Web --version 3.5.0
```

### 2. Configure appsettings.json

Add the following sections to `src/Ato.Copilot.Mcp/appsettings.json`:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.us/",
    "TenantId": "<your-tenant-id>",
    "ClientId": "<your-client-id>",
    "ClientSecret": "<your-client-secret-or-use-cert>",
    "RequireMfa": true,
    "RequireCac": true,
    "EnableUserTokenPassthrough": true,
    "Scopes": ["https://graph.microsoft.us/.default"]
  },
  "Pim": {
    "DefaultActivationDurationHours": 4,
    "MaxActivationDurationHours": 8,
    "DefaultJitDurationHours": 3,
    "MaxJitDurationHours": 24,
    "RequireTicketNumber": false,
    "MinJustificationLength": 20,
    "HighPrivilegeRoles": [
      "Owner",
      "User Access Administrator",
      "Security Administrator",
      "Global Administrator",
      "Privileged Role Administrator"
    ],
    "ApprovedTicketSystems": {
      "ServiceNow": "^SNOW-[A-Z]+-\\d+$",
      "Jira": "^[A-Z]{2,10}-\\d+$",
      "Remedy": "^HD-\\d+$",
      "AzureDevOps": "^AB#\\d+$"
    },
    "ApprovalTimeoutMinutes": 60,
    "SessionExpirationWarningMinutes": 15,
    "AutoDeactivateAfterRemediation": false
  },
  "CacAuth": {
    "DefaultSessionTimeoutHours": 8,
    "MaxSessionTimeoutHours": 24
  },
  "Retention": {
    "AssessmentRetentionDays": 1095,
    "AuditLogRetentionDays": 2555,
    "CleanupIntervalHours": 24,
    "EnableAutomaticCleanup": true
  },
  "KeyVault": {
    "VaultUri": "https://your-keyvault.vault.usgovcloudapi.net/"
  }
}
```

### 3. Run EF Core Migration

```bash
cd src/Ato.Copilot.Mcp
dotnet ef migrations add AddCacAuthPimEntities --project ../Ato.Copilot.Core
dotnet ef database update
```

### 4. Build and Test

```bash
# From repository root
dotnet build
dotnet test
```

## Verification Checklist

### Tier 1 Operations (No Authentication)

1. Start the MCP server in Development mode
2. Without authenticating, run:
   - `cac_status` → Should return `authenticated: false`
   - NIST control query → Should return control description
   - View cached assessment → Should return cached data
   - View Kanban board → Should return board data
3. Verify: No authentication prompts for any Tier 1 operation

### Tier 2 Operations (CAC Required)

4. Without authenticating, attempt a live assessment:
   - Should receive `AUTH_REQUIRED` error with message "This operation requires CAC authentication"
5. Authenticate via CAC:
   - MSAL interactive flow triggers
   - Session established with default 8-hour timeout
   - `cac_status` shows authenticated identity, session info
6. Run a live assessment → Should execute under user's identity
7. Sign out → `cac_sign_out` → Session terminated, Tier 1 still works

### PIM Activation

8. `pim_list_eligible` → Lists eligible roles with scope and approval requirements
9. `pim_activate_role` with:
   - `roleName`: "Contributor"
   - `scope`: "Production Subscription"
   - `justification`: "Remediating AC-2.1 finding per assessment RUN-2026-0221"
   - `ticketNumber`: "SNOW-INC-4521"
   - Should activate immediately (standard role)
10. `pim_list_active` → Shows Contributor with remaining duration
11. `pim_deactivate_role` → Deactivates and confirms least-privilege restored

### High-Privilege Approval

12. `pim_activate_role` with `roleName`: "Owner" → Should submit approval request
13. As Security Lead: `pim_approve_request` → Should approve and activate
14. Verify requester is notified of approval

### JIT VM Access

15. `jit_request_access` with vmName, justification, ticket → NSG rule created
16. `jit_list_sessions` → Shows active session
17. `jit_revoke_access` → NSG rule removed

### Audit Trail

18. `pim_history` with `days: 1` → Shows all PIM actions from verification steps
19. Verify each entry has: userId, role, scope, justification, ticket, timestamps

### Error Handling

20. Activate role with justification < 20 chars → `JUSTIFICATION_TOO_SHORT`
21. Set `RequireTicketNumber: true`, then activate role with invalid ticket format → `INVALID_TICKET`; activate without ticket → `TICKET_REQUIRED`; set back to false and activate without ticket → succeeds
22. Request duration > 24 hours → `DURATION_EXCEEDS_POLICY`
23. Activate a role the user isn't eligible for → `NOT_ELIGIBLE` with alternatives

### Tier 2a/2b PIM Sub-Tier Verification

24. With Reader PIM active but no Contributor:
    - Run a compliance assessment → Should succeed (Tier 2a operation)
    - Attempt a remediation → Should return `INSUFFICIENT_PIM_TIER` with message "Requires write-eligible PIM role (Contributor or higher)"
25. Activate Contributor PIM → Retry remediation → Should succeed (Tier 2b operation)
26. Deactivate Contributor, keep Reader → Assessment still works, remediation blocked

### Observability Verification

27. `GET /health` → Returns JSON with overall status + per-agent availability (healthy/degraded/unavailable) within 2 seconds
28. Invoke any tool → Check structured logs include: `CorrelationId`, `AgentName`, `ToolName`, `UserId` (redacted)
29. Send request with `X-Correlation-ID: test-123` header → Verify response includes same header, logs contain `test-123`
30. Send request without correlation ID header → Verify system generates one (GUID format) and includes in response + logs

### Data Retention Verification

31. Verify `RetentionPolicyOptions` loads from config: `AssessmentRetentionDays=1095`, `AuditLogRetentionDays=2555`
32. Verify audit log entries cannot be updated or deleted via the service layer

## Development Mode Notes

- When `RequireCac: false` in config, the `amr` claim check is skipped (password + MFA accepted) — development bypass mode per FR-036
- When `RequirePim: false` in config, PIM tier enforcement is skipped — tools execute without PIM elevation check
- When running in Development environment, `ComplianceAuthorizationMiddleware` skips role-based authorization
- The two-tier gate (`Tier2Tools` set) still applies in Development — you must authenticate for Azure operations
- Use `EnableUserTokenPassthrough: false` for local development without a CAC reader, combined with a service-level test token
