# Tool Contracts: CAC Authentication & PIM

**Feature**: 003-cac-auth-pim | **Date**: 2026-02-22

All tools follow the existing `BaseTool` pattern: each is a class extending `BaseTool` with `Name`, `Description`, `Parameters`, `RequiredPimTier`, and `ExecuteAsync()`. All responses use the uniform envelope schema per Constitution VII: `{ "status": "success|error", "data": {...}, "metadata": {...} }`.

**PIM Tier Classification** (FR-001, FR-013):
- `PimTier.None` (Tier 1): No authentication required — `cac_status`
- `PimTier.Read` (Tier 2a): CAC + read-eligible PIM role (Reader) — assessment tools, evidence collection, `pim_list_eligible`, `pim_list_active`, `pim_history`, `jit_list_sessions`
- `PimTier.Write` (Tier 2b): CAC + write-eligible PIM role (Contributor+) — remediation tools, `pim_activate_role`, `pim_deactivate_role`, `pim_extend_role`, `pim_approve_request`, `pim_deny_request`, `jit_request_access`, `jit_revoke_access`, `cac_sign_out`, `cac_set_timeout`, `cac_map_certificate`

---

## CAC Authentication Tools

### `cac_status`

**Tier**: 1 (PimTier.None)
**Description**: Check current CAC authentication status, session information, and active PIM roles.

**Parameters**: None

**Response (authenticated)**:
```json
{
  "status": "success",
  "data": {
    "authenticated": true,
    "identity": {
      "displayName": "Jane Smith",
      "email": "jane.smith@agency.mil",
      "userId": "abc-123-def"
    },
    "session": {
      "sessionId": "guid-here",
      "sessionStart": "2026-02-22T08:00:00Z",
      "expiresAt": "2026-02-22T16:00:00Z",
      "remainingMinutes": 420,
      "clientType": "VSCode"
    },
    "activePimRoles": [
      {
        "roleName": "Contributor",
        "scope": "Production Subscription",
        "expiresAt": "2026-02-22T12:00:00Z",
        "remainingMinutes": 180
      }
    ]
  },
  "metadata": {
    "toolName": "cac_status",
    "executionTimeMs": 45
  }
}
```

**Response (unauthenticated)**:
```json
{
  "status": "success",
  "data": {
    "authenticated": false,
    "message": "No active CAC session. Authenticate with your CAC/PIV card to access Azure operations."
  },
  "metadata": {
    "toolName": "cac_status",
    "executionTimeMs": 12
  }
}
```

---

### `cac_sign_out`

**Tier**: 2b (PimTier.Write)
**Description**: End the current CAC session, clear cached tokens, and revert to unauthenticated state. Tier 1 operations remain available.

**Parameters**: None

**Response**:
```json
{
  "status": "success",
  "data": {
    "message": "CAC session terminated. You can still use local features (NIST control lookup, cached assessments, Kanban board). Azure operations will require re-authentication.",
    "sessionTerminated": true,
    "activePimRolesDeactivated": 1
  },
  "metadata": {
    "toolName": "cac_sign_out",
    "executionTimeMs": 230
  }
}
```

---

### `cac_set_timeout`

**Tier**: 2b (PimTier.Write)
**Description**: Set the CAC session timeout duration within policy limits.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `timeoutHours` | `int` | Yes | Desired session timeout in hours (1-24) |

**Response**:
```json
{
  "status": "success",
  "data": {
    "previousTimeout": "8 hours",
    "newTimeout": "4 hours",
    "newExpiresAt": "2026-02-22T12:00:00Z",
    "message": "Session timeout updated to 4 hours. Your session now expires at 12:00 PM UTC."
  },
  "metadata": {
    "toolName": "cac_set_timeout",
    "executionTimeMs": 55
  }
}
```

**Error (out of policy)**:
```json
{
  "status": "error",
  "data": {
    "errorCode": "INVALID_TIMEOUT_DURATION",
    "message": "Timeout must be between 1 and 24 hours.",
    "suggestion": "Try a value between 1 and 24 hours. Current session timeout: 8 hours."
  },
  "metadata": {
    "toolName": "cac_set_timeout",
    "executionTimeMs": 10
  }
}
```

---

### `cac_map_certificate`

**Tier**: 2b (PimTier.Write)
**Description**: Map the current CAC certificate identity to a platform role. Future authentications with this certificate will automatically resolve to the mapped role.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `role` | `string` | Yes | Platform role to map (Administrator, Auditor, Analyst, Viewer, SecurityLead, PlatformEngineer) |

**Response**:
```json
{
  "status": "success",
  "data": {
    "certificateThumbprint": "A1B2C3...",
    "certificateSubject": "CN=SMITH.JANE.M.1234567890",
    "mappedRole": "Auditor",
    "message": "Certificate mapped to Auditor role. Future CAC authentications will automatically assign this role."
  },
  "metadata": {
    "toolName": "cac_map_certificate",
    "executionTimeMs": 120
  }
}
```

---

## PIM Role Management Tools

### `pim_list_eligible`

**Tier**: 2a (PimTier.Read)
**Description**: List all PIM-eligible role assignments for the authenticated user, optionally filtered by scope.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `scope` | `string` | No | Filter by subscription name or resource group path |

**Response**:
```json
{
  "status": "success",
  "data": {
    "eligibleRoles": [
      {
        "roleName": "Contributor",
        "roleDefinitionId": "b24988ac-...",
        "scope": "/subscriptions/sub-id-1",
        "scopeDisplayName": "Production Subscription",
        "isActive": false,
        "maxDuration": "PT8H",
        "requiresApproval": false
      },
      {
        "roleName": "Owner",
        "roleDefinitionId": "8e3af657-...",
        "scope": "/subscriptions/sub-id-1",
        "scopeDisplayName": "Production Subscription",
        "isActive": false,
        "maxDuration": "PT4H",
        "requiresApproval": true
      }
    ],
    "totalCount": 2
  },
  "metadata": {
    "toolName": "pim_list_eligible",
    "executionTimeMs": 1200
  }
}
```

---

### `pim_activate_role`

**Tier**: 2b (PimTier.Write)
**Description**: Activate an eligible PIM role with justification. Ticket number required only when `RequireTicketNumber` is enabled in server configuration. High-privilege roles will be routed through the approval workflow.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `roleName` | `string` | Yes | Role to activate (e.g., "Contributor", "Owner") |
| `scope` | `string` | Yes | Target scope (subscription name, resource group path, or resource ID) |
| `justification` | `string` | Yes | Justification for activation (min 20 characters) |
| `ticketNumber` | `string` | Conditional | Ticket reference from an approved ticketing system. Required when server has `RequireTicketNumber=true`; optional otherwise. If provided, validated against `ApprovedTicketSystems` patterns. |
| `durationHours` | `int` | No | Activation duration in hours (default: 4, max: 8) |

**Response (standard role — immediate activation)**:
```json
{
  "status": "success",
  "data": {
    "activated": true,
    "pimRequestId": "pim-req-guid",
    "roleName": "Contributor",
    "scope": "Production Subscription",
    "activatedAt": "2026-02-22T09:00:00Z",
    "expiresAt": "2026-02-22T13:00:00Z",
    "durationHours": 4,
    "message": "Contributor role activated on Production Subscription. Expires at 1:00 PM UTC."
  },
  "metadata": {
    "toolName": "pim_activate_role",
    "executionTimeMs": 3200
  }
}
```

**Response (high-privilege role — pending approval)**:
```json
{
  "status": "success",
  "data": {
    "activated": false,
    "pendingApproval": true,
    "pimRequestId": "pim-req-guid",
    "roleName": "Owner",
    "scope": "Production Subscription",
    "requestedAt": "2026-02-22T09:00:00Z",
    "approversNotified": ["Security Lead", "Compliance Officer"],
    "message": "Owner is a high-privilege role requiring approval. Request submitted. Security Lead and Compliance Officer have been notified. You will be notified when a decision is made."
  },
  "metadata": {
    "toolName": "pim_activate_role",
    "executionTimeMs": 2800
  }
}
```

**Error (not eligible)**:
```json
{
  "status": "error",
  "data": {
    "errorCode": "NOT_ELIGIBLE",
    "message": "You are not eligible for Contributor on Production Subscription.",
    "suggestion": "You are eligible for: Reader on Production Subscription, Contributor on Staging Subscription.",
    "eligibleRoles": [
      { "roleName": "Reader", "scope": "Production Subscription" },
      { "roleName": "Contributor", "scope": "Staging Subscription" }
    ]
  },
  "metadata": {
    "toolName": "pim_activate_role",
    "executionTimeMs": 1500
  }
}
```

**Error (justification too short)**:
```json
{
  "status": "error",
  "data": {
    "errorCode": "JUSTIFICATION_TOO_SHORT",
    "message": "Justification must be at least 20 characters. You provided 12 characters.",
    "suggestion": "Provide a descriptive justification, e.g., 'Remediating AC-2.1 finding per assessment RUN-2026-0221'"
  },
  "metadata": {
    "toolName": "pim_activate_role",
    "executionTimeMs": 15
  }
}
```

---

### `pim_deactivate_role`

**Tier**: 2b (PimTier.Write)
**Description**: Deactivate an active PIM role to restore least-privilege posture.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `roleName` | `string` | Yes | Role to deactivate |
| `scope` | `string` | Yes | Scope of the role to deactivate |

**Response**:
```json
{
  "status": "success",
  "data": {
    "deactivated": true,
    "roleName": "Contributor",
    "scope": "Production Subscription",
    "deactivatedAt": "2026-02-22T11:00:00Z",
    "actualDuration": "2 hours",
    "message": "Contributor role deactivated on Production Subscription. Least-privilege posture restored."
  },
  "metadata": {
    "toolName": "pim_deactivate_role",
    "executionTimeMs": 2100
  }
}
```

---

### `pim_extend_role`

**Tier**: 2b (PimTier.Write)
**Description**: Extend an active PIM role's duration within policy limits.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `roleName` | `string` | Yes | Role to extend |
| `scope` | `string` | Yes | Scope of the role |
| `additionalHours` | `int` | Yes | Hours to add to the current expiration |

**Response**:
```json
{
  "status": "success",
  "data": {
    "extended": true,
    "roleName": "Contributor",
    "scope": "Production Subscription",
    "previousExpiresAt": "2026-02-22T17:00:00Z",
    "newExpiresAt": "2026-02-22T19:00:00Z",
    "message": "Contributor role extended by 2 hours. New expiration: 7:00 PM UTC."
  },
  "metadata": {
    "toolName": "pim_extend_role",
    "executionTimeMs": 2500
  }
}
```

---

### `pim_list_active`

**Tier**: 2a (PimTier.Read)
**Description**: List all currently active PIM role assignments for the authenticated user.

**Parameters**: None

**Response**:
```json
{
  "status": "success",
  "data": {
    "activeRoles": [
      {
        "roleName": "Contributor",
        "scope": "Production Subscription",
        "activatedAt": "2026-02-22T09:00:00Z",
        "expiresAt": "2026-02-22T17:00:00Z",
        "remainingMinutes": 360,
        "pimRequestId": "pim-req-guid"
      }
    ],
    "totalCount": 1
  },
  "metadata": {
    "toolName": "pim_list_active",
    "executionTimeMs": 900
  }
}
```

---

### `pim_history`

**Tier**: 2a (PimTier.Read)
**Description**: Query PIM action history for compliance evidence and audit trail.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `days` | `int` | No | Number of days to look back (default: 7, max: 365) |
| `roleName` | `string` | No | Filter by role name |
| `userId` | `string` | No | Filter by user ID (admin/auditor only) |
| `scope` | `string` | No | Filter by scope |

**Response**:
```json
{
  "status": "success",
  "data": {
    "history": [
      {
        "requestType": "PimRoleActivation",
        "roleName": "Contributor",
        "scope": "Production Subscription",
        "userId": "abc-123",
        "userDisplayName": "Jane Smith",
        "justification": "Remediating AC-2.1 finding per assessment RUN-2026-0221",
        "ticketNumber": "SNOW-INC-4521",
        "status": "Deactivated",
        "requestedAt": "2026-02-22T09:00:00Z",
        "activatedAt": "2026-02-22T09:00:15Z",
        "deactivatedAt": "2026-02-22T11:00:00Z",
        "actualDuration": "2 hours"
      }
    ],
    "totalCount": 1,
    "nistControlMapping": ["AC-2", "AC-6", "AU-2", "AU-3"]
  },
  "metadata": {
    "toolName": "pim_history",
    "executionTimeMs": 450
  }
}
```

---

## PIM Approval Tools

### `pim_approve_request`

**Tier**: 2b (PimTier.Write)
**Roles**: SecurityLead, Administrator
**Description**: Approve a pending PIM activation request for a high-privilege role.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `requestId` | `string` | Yes | PIM request ID to approve |
| `comments` | `string` | No | Approval comments |

**Response**:
```json
{
  "status": "success",
  "data": {
    "approved": true,
    "requestId": "pim-req-guid",
    "requester": "Jane Smith",
    "roleName": "Owner",
    "scope": "Production Subscription",
    "approvedAt": "2026-02-22T09:15:00Z",
    "message": "Approved Owner activation for Jane Smith on Production Subscription. Requester has been notified."
  },
  "metadata": {
    "toolName": "pim_approve_request",
    "executionTimeMs": 2300
  }
}
```

---

### `pim_deny_request`

**Tier**: 2b (PimTier.Write)
**Roles**: SecurityLead, Administrator
**Description**: Deny a pending PIM activation request for a high-privilege role.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `requestId` | `string` | Yes | PIM request ID to deny |
| `reason` | `string` | Yes | Reason for denial |

**Response**:
```json
{
  "status": "success",
  "data": {
    "denied": true,
    "requestId": "pim-req-guid",
    "requester": "Jane Smith",
    "roleName": "Owner",
    "reason": "Insufficient justification — please provide specific control ID and finding reference.",
    "deniedAt": "2026-02-22T09:15:00Z",
    "message": "Denied Owner activation for Jane Smith. Requester has been notified with denial reason."
  },
  "metadata": {
    "toolName": "pim_deny_request",
    "executionTimeMs": 1800
  }
}
```

---

## JIT VM Access Tools

### `jit_request_access`

**Tier**: 2b (PimTier.Write)
**Description**: Request Just-in-Time VM access through Azure Defender for Cloud. Creates a temporary NSG rule for the specified port and source IP.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `vmName` | `string` | Yes | Target VM name |
| `resourceGroup` | `string` | Yes | Resource group containing the VM |
| `subscriptionId` | `string` | No | Subscription ID (uses default if not specified) |
| `port` | `int` | No | Port number (default: 22 for SSH) |
| `protocol` | `string` | No | ssh or rdp (default: ssh) |
| `sourceIp` | `string` | No | Source IP address (auto-detected if not provided) |
| `durationHours` | `int` | No | Access duration in hours (default: 3, max: 24) |
| `justification` | `string` | Yes | Justification for access |
| `ticketNumber` | `string` | Conditional | Ticket reference. Required when `RequireTicketNumber=true`; optional otherwise. |

**Response**:
```json
{
  "status": "success",
  "data": {
    "jitRequestId": "jit-req-guid",
    "vmName": "vm-web01",
    "resourceGroup": "rg-prod",
    "port": 22,
    "protocol": "SSH",
    "sourceIp": "10.0.1.50",
    "activatedAt": "2026-02-22T09:00:00Z",
    "expiresAt": "2026-02-22T12:00:00Z",
    "durationHours": 3,
    "connectionCommand": "ssh user@vm-web01.agency.mil",
    "message": "JIT SSH access granted to vm-web01 on port 22 from 10.0.1.50. Expires at 12:00 PM UTC."
  },
  "metadata": {
    "toolName": "jit_request_access",
    "executionTimeMs": 4500
  }
}
```

---

### `jit_list_sessions`

**Tier**: 2a (PimTier.Read)
**Description**: List all active JIT VM access sessions for the authenticated user.

**Parameters**: None

**Response**:
```json
{
  "status": "success",
  "data": {
    "activeSessions": [
      {
        "jitRequestId": "jit-req-guid",
        "vmName": "vm-web01",
        "resourceGroup": "rg-prod",
        "port": 22,
        "sourceIp": "10.0.1.50",
        "activatedAt": "2026-02-22T09:00:00Z",
        "expiresAt": "2026-02-22T12:00:00Z",
        "remainingMinutes": 150
      }
    ],
    "totalCount": 1
  },
  "metadata": {
    "toolName": "jit_list_sessions",
    "executionTimeMs": 800
  }
}
```

---

### `jit_revoke_access`

**Tier**: 2b (PimTier.Write)
**Description**: Immediately revoke JIT VM access, removing the NSG rule.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `vmName` | `string` | Yes | VM name to revoke access for |
| `resourceGroup` | `string` | Yes | Resource group containing the VM |

**Response**:
```json
{
  "status": "success",
  "data": {
    "revoked": true,
    "vmName": "vm-web01",
    "resourceGroup": "rg-prod",
    "revokedAt": "2026-02-22T10:30:00Z",
    "message": "JIT access to vm-web01 revoked. NSG rule removed."
  },
  "metadata": {
    "toolName": "jit_revoke_access",
    "executionTimeMs": 3200
  }
}
```

---

## Error Code Reference

| Error Code | HTTP-equiv | Description |
|------------|-----------|-------------|
| `AUTH_REQUIRED` | 401 | Operation requires CAC authentication |
| `SESSION_EXPIRED` | 401 | CAC session has expired |
| `CAC_NOT_DETECTED` | 401 | No CAC/PIV certificate detected in token |
| `MFA_CLAIM_MISSING` | 401 | JWT missing required `amr` claims (mfa, rsa) |
| `TOKEN_EXPIRED` | 401 | JWT token has expired |
| `NOT_ELIGIBLE` | 403 | User not eligible for the requested PIM role |
| `APPROVAL_REQUIRED` | 202 | High-privilege role requires approval before activation |
| `INSUFFICIENT_ROLE` | 403 | User's role lacks permission for this tool |
| `INSUFFICIENT_PIM_TIER` | 403 | User's PIM elevation is insufficient — read-eligible PIM active but write-eligible required (FR-001, FR-013) |
| `PIM_ELEVATION_REQUIRED` | 403 | Operation requires PIM elevation but user has no active PIM role |
| `JUSTIFICATION_TOO_SHORT` | 400 | Justification below minimum length |
| `INVALID_TICKET` | 400 | Ticket number doesn't match approved format (only when ticket is provided or RequireTicketNumber=true) |
| `TICKET_REQUIRED` | 400 | Ticket number is required by server policy (RequireTicketNumber=true) but was not provided |
| `DURATION_EXCEEDS_POLICY` | 400 | Requested duration exceeds maximum allowed |
| `INVALID_TIMEOUT_DURATION` | 400 | Session timeout out of allowed range |
| `ROLE_ALREADY_ACTIVE` | 409 | Role is already active on the specified scope |
| `MAX_SESSIONS_REACHED` | 429 | Maximum concurrent PIM sessions reached |
| `VM_NOT_FOUND` | 404 | Target VM not found in the specified resource group |
| `JIT_NOT_ENABLED` | 400 | JIT access not enabled (Defender for Cloud Standard required) |
| `PIM_SERVICE_UNAVAILABLE` | 503 | Azure AD PIM API unreachable |
| `OPERATION_PAUSED` | 202 | Operation paused due to session expiration, re-auth required |
