# Portfolio Management Guide

> Multi-system workflows for ISSMs and AOs managing portfolios of information systems.

---

## Overview

ISSMs and AOs often manage portfolios of dozens of systems simultaneously. This guide covers workflows that span multiple systems — portfolio dashboards, bulk operations, delegation patterns, and AO portfolio views.

---

## Portfolio Dashboard

The multi-system dashboard provides a single view of all registered systems with key metrics.

### Natural Language Queries

> **"Show the multi-system compliance dashboard"** — all systems with scores, phases, and alerts

> **"Show all systems at the Assess phase"** — filtered by RMF phase

> **"Which systems have compliance scores below 80%?"** — risk-based filtering

> **"Show systems with expired or expiring ATOs"** — expiration urgency view

> **"Compare compliance trends across all IL5 systems"** — trend analysis

> **"Show portfolio risk summary"** — aggregate risk view

### Dashboard Data Points Per System

| Field | Description |
|-------|-------------|
| System Name | Registered name |
| Impact Level | IL2–IL6 |
| Current RMF Phase | Prepare through Monitor |
| Authorization Status | ATO / ATOwC / IATT / DATO / None |
| Expiration Date | ATO expiration with color-coded urgency |
| Compliance Score | Latest assessment percentage |
| Open Findings | CAT I / CAT II / CAT III counts |
| POA&M Status | Overdue / On Track / Completed counts |
| ConMon Status | Last report date, next due date |

---

## Bulk Operations

### ISSM Bulk Workflows

| Query | Tool | Purpose |
|-------|------|---------|
| Export all my systems to eMASS format | `compliance_export_emass` | Portfolio-wide eMASS export |
| Generate ConMon reports for all Monitor phase systems, period 2026-02 | `compliance_generate_conmon_report` | Batch ConMon |
| Show overdue POA&M items across all systems | `compliance_list_poam` | Cross-system POA&M tracking |
| Bulk assign all High severity Kanban tasks in system {id} to the security team | `kanban_bulk_update` | Mass task assignment |
| Check ATO expiration for all systems | `compliance_track_ato_expiration` | Portfolio expiration check |

### Available Bulk Tools

| Tool | Bulk Capability |
|------|----------------|
| `compliance_multi_system_dashboard` | All systems in one view |
| `compliance_list_systems` | Filter/search across all systems |
| `compliance_list_poam` | POA&M items across systems (when `systemId` omitted) |
| `compliance_track_ato_expiration` | All systems' expiration status |
| `kanban_bulk_update` | Bulk assign, move, or set due dates on tasks |
| `kanban_export` | Export board as CSV or POA&M for portfolio reporting |
| `compliance_export_emass` | Per-system export (loop for portfolio) |

---

## Delegation Patterns

When an ISSM manages many systems, delegation to ISSOs is critical.

### Delegation Model

| Pattern | How It Works |
|---------|-------------|
| **System-level ISSO assignment** | Each system has a named ISSO via `compliance_assign_rmf_role` — that ISSO owns day-to-day operations |
| **Alert routing** | Watch alerts route to the system's assigned ISSO first; escalation path goes to ISSM |
| **Kanban task assignment** | ISSM creates boards; ISSOs assign tasks to engineers per system |
| **ConMon responsibility** | ISSM sets the ConMon plan; ISSO executes monitoring and generates reports |
| **Reporting rollup** | ISSM uses dashboard for portfolio view; drills into individual systems as needed |

### Delegation Queries

| Query | Purpose |
|-------|---------|
| Who is the ISSO for system {id}? | Check system assignment |
| Show all systems where Bob Jones is the assigned ISSO | ISSO workload view |
| Reassign ISSO role for system {id} from Bob Jones to Sarah Lee | Transfer responsibility |
| Show alert summary grouped by ISSO | Delegation monitoring |
| Which ISSOs have overdue tasks? | Accountability check |

---

## AO Portfolio View

The AO typically authorizes many systems and needs portfolio-level risk visibility.

### AO Portfolio Queries

| Query | Purpose |
|-------|---------|
| Show all systems I have authorized | Authorization portfolio |
| What is my total portfolio risk exposure? | Aggregate risk |
| Show risk acceptances expiring in the next 90 days across all systems | Upcoming expirations |
| Which of my authorized systems have CAT I findings? | Critical risk filter |
| Show authorization decisions I have issued this year | Decision history |

### AO Expiration Alerts

| Days Remaining | Alert Level | Action |
|----------------|-------------|--------|
| ≤ 90 days | Info | Begin reauthorization planning |
| ≤ 60 days | Warning | Submit reauthorization package |
| ≤ 30 days | Urgent | Escalate immediately |
| Expired | Critical | System operating without authorization |

---

## See Also

- [ISSM Guide](issm-guide.md) — Full ISSM workflow documentation
- [AO Guide](ao-quick-reference.md) — Authorization decisions and risk acceptance
- [Compliance Watch Guide](compliance-watch.md) — Alert management and escalation
- [Persona Overview](../personas/index.md) — Role definitions and RACI matrix
