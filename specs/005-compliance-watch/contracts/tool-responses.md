# MCP Tool Contracts: Compliance Watch (Feature 005)

**Date**: 2026-02-22  
**Spec**: [spec.md](spec.md)  
**Data Model**: [data-model.md](../data-model.md)

## Overview

Compliance Watch exposes ~20 new MCP tools through the existing `ComplianceMcpTools` class. All tools follow the established `BaseTool` → `ComplianceMcpTools` wrapper pattern. Responses use the JSON envelope format established by Kanban tools.

## Response Envelope

All Compliance Watch tools return the standard JSON envelope:

```json
{
  "status": "success" | "error",
  "data": { ... },
  "metadata": {
    "tool": "watch_<tool_name>",
    "timestamp": "2026-02-22T14:30:00Z",
    "executionTimeMs": 142
  }
}
```

Error responses include:

```json
{
  "status": "error",
  "error": {
    "message": "Human-readable error description",
    "errorCode": "MACHINE_READABLE_CODE",
    "suggestion": "Corrective guidance for the user"
  },
  "metadata": { ... }
}
```

## Webhook Signing Specification

All webhook notifications (`NotificationChannel.Webhook`) MUST include a cryptographic signature for payload integrity verification.

| Attribute | Value |
|-----------|-------|
| Algorithm | HMAC-SHA256 |
| Secret | Per-webhook shared secret (generated on webhook creation, stored encrypted) |
| Header | `X-Webhook-Signature: sha256=<hex-digest>` |
| Payload | Raw JSON body (UTF-8 encoded) |

**Verification pseudocode** (consumer side):
```
expected = HMAC_SHA256(shared_secret, raw_request_body)
actual = request.headers["X-Webhook-Signature"].removePrefix("sha256=")
valid = constant_time_compare(expected, actual)
```

**Key management**:
- Shared secret generated via `RandomNumberGenerator.GetBytes(32)` on webhook creation
- Secret returned once to the user on creation; not retrievable afterward
- Secret stored encrypted at rest in the `EscalationPath.WebhookUrl` column (or dedicated `WebhookSecret` column)
- Secret rotation: user can regenerate via `watch_configure_notifications` with `regenerateSecret: true`

## Tool Contracts

### Monitoring Configuration Tools

#### 1. `watch_enable_monitoring`

Enable continuous compliance monitoring for a scope.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| subscriptionId | string | yes | Azure subscription ID |
| resourceGroup | string | no | Resource group name (null = entire subscription) |
| frequency | string | no | "15min", "hourly", "daily", "weekly" (default: "hourly") |
| mode | string | no | "scheduled", "event-driven", "both" (default: "scheduled") |

**Success Response** (`data`):
```json
{
  "configurationId": "guid",
  "subscriptionId": "sub-123",
  "resourceGroup": null,
  "mode": "scheduled",
  "frequency": "hourly",
  "isEnabled": true,
  "nextRunAt": "2026-02-22T15:00:00Z",
  "message": "Monitoring enabled for subscription sub-123. First check scheduled at 3:00 PM UTC."
}
```

**Error Codes**: `MONITORING_ALREADY_ENABLED`, `INVALID_SUBSCRIPTION`, `INVALID_FREQUENCY`, `INSUFFICIENT_PERMISSIONS`

---

#### 2. `watch_disable_monitoring`

Disable monitoring for a scope.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| subscriptionId | string | yes | Azure subscription ID |
| resourceGroup | string | no | Resource group name |

**Success Response** (`data`):
```json
{
  "subscriptionId": "sub-123",
  "isEnabled": false,
  "message": "Monitoring disabled for subscription sub-123."
}
```

**Error Codes**: `MONITORING_NOT_CONFIGURED`, `INSUFFICIENT_PERMISSIONS`

---

#### 3. `watch_configure_monitoring`

Update monitoring settings for an existing configuration.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| subscriptionId | string | yes | Azure subscription ID |
| resourceGroup | string | no | Resource group name |
| frequency | string | no | New frequency |
| mode | string | no | New mode |

**Success Response** (`data`):
```json
{
  "configurationId": "guid",
  "subscriptionId": "sub-123",
  "frequency": "weekly",
  "mode": "both",
  "nextRunAt": "2026-03-01T00:00:00Z",
  "message": "Monitoring updated for subscription sub-123. Next check: March 1 at midnight UTC."
}
```

**Error Codes**: `MONITORING_NOT_CONFIGURED`, `INVALID_FREQUENCY`, `INVALID_MODE`

---

#### 4. `watch_monitoring_status`

Show current monitoring configuration and status for a scope.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| subscriptionId | string | no | Filter by subscription (null = all) |

**Success Response** (`data`):
```json
{
  "configurations": [
    {
      "configurationId": "guid",
      "subscriptionId": "sub-123",
      "resourceGroup": null,
      "mode": "scheduled",
      "frequency": "hourly",
      "isEnabled": true,
      "lastRunAt": "2026-02-22T14:00:00Z",
      "nextRunAt": "2026-02-22T15:00:00Z"
    }
  ],
  "totalConfigurations": 1,
  "activeConfigurations": 1
}
```

---

### Alert Management Tools

#### 5. `watch_show_alerts`

List active alerts with filtering.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| subscriptionId | string | no | Filter by subscription |
| severity | string | no | "Critical", "High", "Medium", "Low" |
| status | string | no | "New", "Acknowledged", "InProgress", "Escalated" |
| controlFamily | string | no | Filter by NIST control family |
| days | int | no | Lookback period in days (default: 7) |
| page | int | no | Page number (default: 1) |
| pageSize | int | no | Results per page (default: 50) |

**Success Response** (`data`):
```json
{
  "alerts": [
    {
      "alertId": "ALT-2026022200001",
      "type": "Drift",
      "severity": "High",
      "status": "New",
      "title": "Encryption disabled on storage account stgprod001",
      "subscriptionId": "sub-123",
      "affectedResources": ["/subscriptions/sub-123/resourceGroups/rg-prod/providers/Microsoft.Storage/storageAccounts/stgprod001"],
      "controlId": "SC-8",
      "controlFamily": "SC",
      "actorId": "user@example.com",
      "createdAt": "2026-02-22T14:15:00Z",
      "slaDeadline": "2026-02-22T18:15:00Z",
      "isGrouped": false,
      "childAlertCount": 0
    }
  ],
  "totalCount": 12,
  "page": 1,
  "pageSize": 50
}
```

---

#### 6. `watch_get_alert`

Get full details of a specific alert.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| alertId | string | yes | Alert ID (e.g., "ALT-2026022200001") |

**Success Response** (`data`):
```json
{
  "alertId": "ALT-2026022200001",
  "type": "Drift",
  "severity": "High",
  "status": "New",
  "title": "Encryption disabled on storage account stgprod001",
  "description": "HTTPS-only transport was disabled on stgprod001, violating control SC-8 (Transmission Confidentiality and Integrity).",
  "subscriptionId": "sub-123",
  "affectedResources": ["stgprod001"],
  "controlId": "SC-8",
  "controlFamily": "SC",
  "changeDetails": {
    "property": "supportsHttpsTrafficOnly",
    "oldValue": "true",
    "newValue": "false"
  },
  "actorId": "user@example.com",
  "recommendedAction": "Re-enable HTTPS-only on storage account stgprod001 or use 'Fix alert ALT-2026022200001' for automated remediation.",
  "assignedTo": null,
  "createdAt": "2026-02-22T14:15:00Z",
  "slaDeadline": "2026-02-22T18:15:00Z",
  "notifications": [
    { "channel": "Chat", "sentAt": "2026-02-22T14:15:01Z", "isDelivered": true }
  ],
  "childAlerts": []
}
```

**Error Codes**: `ALERT_NOT_FOUND`

---

#### 7. `watch_acknowledge_alert`

Acknowledge an alert (pauses escalation timer).

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| alertId | string | yes | Alert ID |

**Success Response** (`data`):
```json
{
  "alertId": "ALT-2026022200001",
  "previousStatus": "New",
  "newStatus": "Acknowledged",
  "acknowledgedBy": "user@example.com",
  "acknowledgedAt": "2026-02-22T14:20:00Z",
  "message": "Alert ALT-2026022200001 acknowledged. Escalation timer paused."
}
```

**Error Codes**: `ALERT_NOT_FOUND`, `INVALID_TRANSITION` (e.g., already resolved)

---

#### 8. `watch_fix_alert`

Execute remediation for an alert.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| alertId | string | yes | Alert ID |
| dryRun | bool | no | Preview fix without applying (default: false) |

**Success Response** (`data`):
```json
{
  "alertId": "ALT-2026022200001",
  "previousStatus": "Acknowledged",
  "newStatus": "Resolved",
  "remediationAction": "Re-enabled HTTPS-only on stgprod001",
  "resolvedAt": "2026-02-22T14:25:00Z",
  "message": "Alert ALT-2026022200001 resolved. HTTPS-only re-enabled on stgprod001."
}
```

**Error Codes**: `ALERT_NOT_FOUND`, `REMEDIATION_FAILED`, `INSUFFICIENT_PERMISSIONS`

---

#### 9. `watch_dismiss_alert`

Dismiss an alert (Compliance Officer only, requires justification).

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| alertId | string | yes | Alert ID |
| justification | string | yes | Reason for dismissal |

**Success Response** (`data`):
```json
{
  "alertId": "ALT-2026022200001",
  "previousStatus": "Acknowledged",
  "newStatus": "Dismissed",
  "dismissedBy": "complianceofficer@example.com",
  "justification": "False positive — resource is in decommissions scope",
  "message": "Alert ALT-2026022200001 dismissed."
}
```

**Error Codes**: `ALERT_NOT_FOUND`, `INSUFFICIENT_PERMISSIONS` ("Only Compliance Officers can dismiss alerts"), `JUSTIFICATION_REQUIRED`

---

### Alert Rules & Suppression Tools

#### 10. `watch_create_rule`

Create a custom alert rule.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| name | string | yes | Rule name |
| subscriptionId | string | no | Scope to subscription |
| resourceGroup | string | no | Scope to resource group |
| controlFamily | string | no | Trigger on control family |
| controlId | string | no | Trigger on specific control |
| severity | string | no | Severity override |
| recipients | string | no | Comma-separated recipient overrides |
| description | string | no | Rule description |

**Success Response** (`data`):
```json
{
  "ruleId": "guid",
  "name": "Critical encryption changes",
  "scope": "subscription sub-123",
  "triggerCondition": "controlFamily = SC",
  "severityOverride": "Critical",
  "isEnabled": true,
  "message": "Alert rule 'Critical encryption changes' created."
}
```

**Error Codes**: `DUPLICATE_RULE_NAME`, `INSUFFICIENT_PERMISSIONS`

---

#### 11. `watch_list_rules`

List alert rules.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| subscriptionId | string | no | Filter by subscription |
| includeDefaults | bool | no | Include default rules (default: true) |

---

#### 12. `watch_suppress_alerts`

Create a suppression rule (temporary or permanent).

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| subscriptionId | string | no | Scope |
| resourceGroup | string | no | Scope |
| resourceId | string | no | Specific resource |
| controlFamily | string | no | Specific control family |
| type | string | yes | "temporary" or "permanent" |
| durationHours | int | no | Required for temporary (hours until expiry) |
| justification | string | no | Required for permanent |

**Success Response** (`data`):
```json
{
  "suppressionId": "guid",
  "type": "Temporary",
  "scope": "resource stgdev001",
  "expiresAt": "2026-02-22T18:30:00Z",
  "message": "Alerts suppressed for stgdev001 for 4 hours."
}
```

**Error Codes**: `INSUFFICIENT_PERMISSIONS`, `JUSTIFICATION_REQUIRED`, `DURATION_REQUIRED`

---

#### 13. `watch_list_suppressions`

List active suppression rules.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| subscriptionId | string | no | Filter by subscription |
| includeExpired | bool | no | Include expired temporary rules (default: false) |

---

#### 14. `watch_configure_quiet_hours`

Configure quiet hours for non-Critical notifications.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| startTime | string | yes | Start time in HH:mm format (e.g., "22:00") |
| endTime | string | yes | End time in HH:mm format (e.g., "06:00") |
| subscriptionId | string | no | Scope (null = global) |

---

### Notification & Escalation Tools

#### 15. `watch_configure_notifications`

Configure notification channels (email, webhook).

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| channel | string | yes | "email" or "webhook" |
| target | string | yes | Email address or webhook URL |
| severity | string | no | Minimum severity to trigger (default: all) |
| mode | string | no | "immediate" or "digest" (email only, default: "immediate") |
| subscriptionId | string | no | Scope |

---

#### 16. `watch_configure_escalation`

Configure an escalation path.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| name | string | yes | Escalation path name |
| severity | string | yes | Trigger severity |
| delayMinutes | int | yes | Minutes after SLA to escalate |
| recipients | string | yes | Comma-separated recipients |
| channel | string | no | Notification channel (default: "chat") |
| repeatMinutes | int | no | Re-notify interval (default: 30) |

---

### History & Query Tools

#### 17. `watch_alert_history`

Query historical alert data.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| query | string | yes | Natural language query (e.g., "What drifted this week?") |
| subscriptionId | string | no | Scope |
| days | int | no | Lookback period (default: 30, max: 730) |

**Success Response** (`data`):
```json
{
  "query": "What drifted this week?",
  "summary": "3 drift events detected across 2 subscriptions this week.",
  "results": [
    {
      "alertId": "ALT-2026022200001",
      "type": "Drift",
      "severity": "High",
      "title": "Encryption disabled on stgprod001",
      "status": "Resolved",
      "createdAt": "2026-02-22T14:15:00Z",
      "resolvedAt": "2026-02-22T14:25:00Z"
    }
  ],
  "statistics": {
    "totalDriftEvents": 3,
    "resolvedCount": 2,
    "openCount": 1
  }
}
```

---

#### 18. `watch_compliance_trend`

Get compliance trend over time.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| subscriptionId | string | no | Scope |
| days | int | no | Period (default: 30) |

**Success Response** (`data`):
```json
{
  "subscriptionId": "sub-123",
  "period": "30 days",
  "currentScore": 87.5,
  "previousScore": 92.0,
  "direction": "declining",
  "snapshots": [
    { "date": "2026-02-22", "score": 87.5, "activeAlerts": 3 },
    { "date": "2026-02-21", "score": 89.0, "activeAlerts": 2 }
  ]
}
```

---

#### 19. `watch_alert_statistics`

Get alert statistics for a period.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| subscriptionId | string | no | Scope |
| days | int | no | Period (default: 30) |

**Success Response** (`data`):
```json
{
  "period": "30 days",
  "totalAlerts": 47,
  "bySeverity": { "Critical": 3, "High": 12, "Medium": 22, "Low": 10 },
  "byType": { "Drift": 28, "Violation": 10, "Degradation": 5, "Anomaly": 2, "Resolution": 2 },
  "byStatus": { "Resolved": 35, "Dismissed": 4, "New": 5, "Acknowledged": 2, "InProgress": 1 },
  "averageResolutionTimeMinutes": 127,
  "escalationCount": 3,
  "autoResolvedCount": 22
}
```

---

### Integration Tools

#### 20. `watch_create_task_from_alert`

Create a Kanban remediation task from an alert.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| alertId | string | yes | Alert ID |
| boardId | string | no | Target board (default: first active board) |

**Success Response** (`data`):
```json
{
  "alertId": "ALT-2026022200001",
  "taskId": "guid",
  "taskNumber": "REM-001",
  "boardId": "guid",
  "title": "[ALT-2026022200001] Encryption disabled on stgprod001",
  "message": "Remediation task created from alert ALT-2026022200001."
}
```

**Error Codes**: `ALERT_NOT_FOUND`, `NO_ACTIVE_BOARD`, `TASK_ALREADY_EXISTS`

---

#### 21. `watch_collect_evidence_from_alert`

Collect compliance evidence from alert data.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| alertId | string | yes | Alert ID |

---

### Auto-Remediation Tools

#### 22. `watch_create_auto_remediation_rule`

Create an opt-in auto-remediation rule.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| name | string | yes | Rule name |
| subscriptionId | string | no | Scope |
| resourceGroup | string | no | Scope |
| controlFamily | string | no | Target control family (AC, IA, SC blocked) |
| controlId | string | no | Target control |
| action | string | yes | Remediation action description |
| approvalMode | string | no | "auto" or "require-approval" (default: "require-approval") |

**Success Response** (`data`):
```json
{
  "ruleId": "guid",
  "name": "Auto-fix missing tags",
  "scope": "subscription sub-123",
  "approvalMode": "auto",
  "isEnabled": true,
  "message": "Auto-remediation rule created. Matching violations will be automatically fixed."
}
```

**Error Codes**: `BLOCKED_CONTROL_FAMILY` ("Auto-remediation is not allowed for control families AC, IA, SC — these require human approval"), `INSUFFICIENT_PERMISSIONS`

---

#### 23. `watch_list_auto_remediation_rules`

List auto-remediation rules and their execution history.

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| subscriptionId | string | no | Filter by subscription |
| includeDisabled | bool | no | Include disabled rules (default: false) |
