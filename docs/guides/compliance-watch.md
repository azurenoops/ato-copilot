# Compliance Watch — User Guide

> Continuous compliance monitoring, alerting, and automated response for Azure Government and Azure Commercial environments.

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [Monitoring Setup](#monitoring-setup)
  - [Scheduled Monitoring](#scheduled-monitoring)
  - [Event-Driven Monitoring](#event-driven-monitoring)
  - [Combined Mode](#combined-mode)
- [Alert Lifecycle](#alert-lifecycle)
  - [Alert States](#alert-states)
  - [Viewing Alerts](#viewing-alerts)
  - [Acknowledging Alerts](#acknowledging-alerts)
  - [Fixing Alerts](#fixing-alerts)
  - [Dismissing Alerts](#dismissing-alerts)
- [Rules & Suppression](#rules--suppression)
  - [Custom Alert Rules](#custom-alert-rules)
  - [Alert Suppression](#alert-suppression)
  - [Quiet Hours](#quiet-hours)
- [Notifications & Escalation](#notifications--escalation)
  - [Notification Channels](#notification-channels)
  - [Escalation Paths](#escalation-paths)
  - [SLA Timers](#sla-timers)
- [Dashboard & Reporting](#dashboard--reporting)
  - [Alert History (Natural Language)](#alert-history-natural-language)
  - [Compliance Trends](#compliance-trends)
  - [Alert Statistics](#alert-statistics)
- [Kanban Integration](#kanban-integration)
- [Auto-Remediation](#auto-remediation)
  - [Creating Rules](#creating-rules)
  - [Safety Guardrails](#safety-guardrails)
  - [Monitoring Execution](#monitoring-execution)
- [Alert Correlation](#alert-correlation)
- [Data Retention](#data-retention)
- [Role-Based Access](#role-based-access)
- [Troubleshooting](#troubleshooting)

---

## Overview

Compliance Watch provides continuous monitoring of your Azure subscriptions against NIST 800-53, FedRAMP, and DoD IL baselines. It detects configuration drift, generates actionable alerts, and optionally auto-remediates findings — all with full audit logging.

Key capabilities:
- **Scheduled & event-driven** monitoring modes
- **Drift detection** against captured compliance baselines
- **Alert lifecycle** management (acknowledge → fix → resolve)
- **Configurable rules, suppression, and quiet hours**
- **Notification channels** (Chat, Email, Webhook) with escalation paths
- **Natural-language alert queries** ("What drifted this week?")
- **Kanban task integration** for tracking remediation work
- **Auto-remediation** with dry-run safety and rate limiting
- **Alert correlation** to reduce noise (resource/control/actor grouping)

## Quick Start

```
# 1. Enable daily monitoring on a subscription
> Enable compliance monitoring for subscription sub-12345

# 2. View monitoring status
> Show monitoring status

# 3. Check for alerts after first scan
> Show me all critical alerts

# 4. Acknowledge and fix a finding
> Acknowledge alert ALT-001
> Fix alert ALT-001 with dry run first
```

## Monitoring Setup

### Scheduled Monitoring

Enable periodic compliance scans at configurable intervals:

```
> Enable monitoring for sub-12345 with hourly frequency
> Enable monitoring for sub-12345 daily for resource group rg-prod
```

**Frequencies**: `hourly`, `daily` (default), `weekly`, `monthly`

**Modes**:
- `full` (default) — Run complete compliance assessment and compare against baseline
- `drift_only` — Only check for configuration changes since last baseline

### Event-Driven Monitoring

Event-driven mode listens to Azure Activity Log for compliance-relevant operations (policy changes, resource modifications, RBAC changes) and triggers targeted checks automatically.

Relevant operations detected automatically:
- Policy assignment changes (`Microsoft.Authorization/policyAssignments/*`)
- Role assignment changes (`Microsoft.Authorization/roleAssignments/*`)
- NSG/firewall rule modifications
- Storage account configuration changes
- Key Vault access policy updates

### Combined Mode

Use `both` mode to get scheduled scans plus real-time event-driven checks:

```
> Configure monitoring for sub-12345 with mode both and frequency daily
```

## Alert Lifecycle

### Alert States

| State | Description |
|-------|-------------|
| **New** | Just created, awaiting triage |
| **Acknowledged** | Team is aware, SLA timer paused |
| **InProgress** | Remediation underway |
| **Resolved** | Finding fixed and verified |
| **Dismissed** | False positive (requires justification) |
| **Escalated** | SLA violated, escalated per configured path |

### Viewing Alerts

```
> Show all alerts                          # All active alerts
> Show critical alerts from last 7 days    # Severity + time filter
> Show alerts for control family AC        # Control family filter
> Get details for alert ALT-12345          # Full alert detail with history
```

### Acknowledging Alerts

Acknowledging an alert pauses the SLA escalation timer:

```
> Acknowledge alert ALT-12345
```

### Fixing Alerts

Remediate findings directly from the alert:

```
> Fix alert ALT-12345 with dry run     # Preview changes first
> Fix alert ALT-12345                   # Apply remediation
```

### Dismissing Alerts

**Compliance Officer only.** Requires written justification:

```
> Dismiss alert ALT-12345 — false positive, resource is in decommission phase
```

## Rules & Suppression

### Custom Alert Rules

Create rules that match specific control families with custom severity thresholds:

```
> Create alert rule for AC family with High severity threshold
> List all alert rules
```

### Alert Suppression

Temporarily suppress alerts matching specific patterns with time-limited expiration:

```
> Suppress alerts for control AC-2 until 2026-03-15 — maintenance window
> List active suppressions
```

### Quiet Hours

During quiet hours, alerts are still generated but notifications are held:

```
> Set quiet hours from 22:00 to 06:00 UTC on weekdays
```

## Notifications & Escalation

### Notification Channels

Configure where alerts are delivered:

- **Chat** — In-app notifications (default)
- **Email** — Email delivery to specified recipients
- **Webhook** — HTTP POST to external systems (SIEM, ticketing)

```
> Configure notifications: email to security-team@agency.gov for Critical and High alerts
```

### Escalation Paths

Define automatic escalation when SLA timers expire:

```
> Configure escalation: Critical alerts escalate to isso@agency.gov after 2 hours, repeat every 30 minutes
```

### SLA Timers

| Severity | Default SLA |
|----------|------------|
| 🔴 Critical | 4 hours |
| 🟠 High | 24 hours |
| 🟡 Medium | 72 hours |
| 🔵 Low | 168 hours (7 days) |

## Dashboard & Reporting

### Alert History (Natural Language)

Query alert history using natural language or structured filters:

```
> What drifted this week?
> Show dismissed alerts from last month
> Critical alerts for control family SC
> Show escalated alerts
```

The system parses natural language queries to extract:
- **Alert type**: "drift" → Drift type alerts
- **Status**: "dismissed", "escalated", "resolved"
- **Severity**: "critical", "high"
- **Time range**: "today", "week", "month"
- **Control family**: AC, AU, CM, SC, SI, etc.

### Compliance Trends

View how your compliance score changes over time:

```
> Show compliance trend for sub-12345 over 30 days
> Show weekly compliance trend for sub-12345
```

Returns per-datapoint direction indicators:
- ↑ **Improving** — Score increased by >1 point
- ↓ **Declining** — Score decreased by >1 point
- → **Stable** — Score within ±1 point

### Alert Statistics

Get aggregate metrics for executive reporting:

```
> Show alert statistics for last 30 days
> Show alert statistics for sub-12345
```

Includes: counts by severity/type/status, average resolution time, escalation count, auto-resolved count, grouped alert count.

## Kanban Integration

Link compliance alerts to Kanban remediation tasks for structured tracking:

```
> Create a Kanban task from alert ALT-12345
```

- Automatically sets task title, description, and priority from alert data
- Prevents duplicates — returns existing linked task if one exists
- Auto-closes linked Kanban task when the alert is resolved

**Evidence collection** from alerts:

```
> Collect evidence for alert ALT-12345
```

Captures point-in-time evidence (assessment results, baseline state, configuration snapshot) as a compliance evidence package.

## Auto-Remediation

### Creating Rules

Map control families + severity levels to automated remediation actions:

```
> Create auto-remediation rule for AC family, High severity, action enforce_nsg_rules
> Create auto-remediation rule for SC family, Critical severity, action enforce_encryption, dry run first
```

### Safety Guardrails

Auto-remediation includes multiple safety layers:

1. **Dry-run first** (default: enabled) — Simulates the fix before applying
2. **Rate limiting** — `maxExecutionsPerHour` limits rapid-fire execution (default: 10)
3. **Audit trail** — All actions logged with full before/after state
4. **Failure handling** — Failed remediations leave the alert open for manual review
5. **Scope limitation** — Only applies to alerts in operational control families

### Monitoring Execution

```
> List auto-remediation rules
```

Shows each rule with execution statistics: last execution time, total run count, failure count.

## Alert Correlation

The system automatically correlates related alerts to reduce noise:

| Correlation Type | Trigger | Result |
|-----------------|---------|--------|
| **Resource grouping** | Multiple findings on same resource within 5 min | Grouped under single parent alert |
| **Control grouping** | Same control violations across resources | Correlated by control family |
| **Actor anomaly** | Single actor triggers 10+ alerts in 5 min | Anomaly flag raised |
| **Alert storm** | 50+ alerts within a window | Storm detection activates |

Grouped alerts show parent/child relationships with `ChildAlertCount` for at-a-glance understanding.

## Data Retention

Data retention is managed automatically per federal compliance requirements:

| Data Type | Default Retention | Configurable |
|-----------|------------------|--------------|
| Assessment results | 3 years | Yes (`AssessmentRetentionDays`) |
| Daily snapshots | 90 days | Yes (`DailySnapshotRetentionDays`) |
| Weekly snapshots | 2 years | Yes (`WeeklySnapshotRetentionDays`) |
| Alerts (resolved/dismissed) | 2 years | Yes (`AlertRetentionDays`, 2–7 years) |
| Audit logs | 7+ years | **Never deleted** (immutable per FR-043) |

Retention cleanup runs automatically every 24 hours (configurable via `CleanupIntervalHours`).

## Role-Based Access

| Role | Capabilities |
|------|-------------|
| **Compliance Officer** | Full access — dismiss alerts, create rules, manage suppression, configure auto-remediation |
| **Security Lead** | Configure escalation, notifications, auto-remediation rules |
| **Administrator** | View, acknowledge, fix alerts; create Kanban tasks |
| **Analyst** | View, acknowledge, fix alerts; create Kanban tasks |
| **Platform Engineer** | View, acknowledge, fix alerts; create Kanban tasks |
| **Auditor** | **Read-only** — view alerts, history, trends, statistics |

Access control is enforced via PIM (Privileged Identity Management). Use `pim_activate_role` to elevate when needed.

## Troubleshooting

### Monitoring Not Running

1. Verify monitoring is enabled: `Show monitoring status for sub-12345`
2. Check the subscription ID is correct
3. Ensure the service principal has Reader access to the subscription
4. Review logs for authentication errors

### No Alerts Generated

1. Confirm a baseline was captured (happens on first successful scan)
2. Check if drift has occurred since baseline was set
3. Verify suppression rules aren't hiding expected alerts: `List suppressions`
4. Check quiet hours configuration: alerts are generated but notifications may be held

### Alert Actions Denied

1. Check your current role: the error message will indicate the required role
2. Activate the required PIM role: `Activate Compliance Officer role`
3. Retry the action after role activation

### Auto-Remediation Not Executing

1. Verify the rule is enabled: `List auto-remediation rules`
2. Check if the rule's `maxExecutionsPerHour` limit has been reached
3. Review the rule's `failCount` — repeated failures may indicate a configuration issue
4. Ensure the service principal has Contributor access for write operations

### Event-Driven Mode Not Triggering

1. Verify monitoring mode is set to `EventDriven` or `Both`
2. Check that the service principal has Reader access to Azure Activity Log
3. Event polling interval is configurable (default: 120 seconds)
4. Only compliance-relevant operations trigger scans (policy, RBAC, NSG, storage, Key Vault changes)

---

## Air-Gapped Environment Notes

!!! warning "Disconnected / Air-Gapped Monitoring"
    In air-gapped or disconnected environments, Compliance Watch operates with the following limitations:
    
    - **Event-driven monitoring** is unavailable (requires Azure Event Grid / Activity Log access). Use **scheduled-only mode** with local policy cache.
    - **Notification channels**: External email and webhook channels are unavailable. Notifications are limited to local channels (VS Code notifications, audit log entries).
    - **Auto-remediation**: Requires network access to Azure resources for write operations. In air-gapped environments, generate remediation scripts and apply manually.
    - **Baseline capture**: Initial baseline capture requires one-time network access. Subsequent drift detection works against the cached baseline.
    - **Alert data**: All alert storage, lifecycle management, and SLA tracking work fully offline.

---

## See Also

- [ISSO Guide](../personas/isso.md) — ISSO workflows including Monitor phase
- [ISSM Guide](issm-guide.md) — ISSM oversight and portfolio monitoring
- [Remediation Kanban Guide](remediation-kanban.md) — Task management for findings
- [RMF Phase Reference](../rmf-phases/index.md) — Monitor phase details
