# ISSO Guide — Information System Security Officer

> Day-to-day security operations — implements and monitors controls.

---

## Role Overview

- **Full Title**: Information System Security Officer
- **Abbreviation**: ISSO
- **RBAC Role**: `Compliance.Analyst`
- **Primary RMF Phases**: Implement (Lead), Assess (Support), Monitor (Day-to-day)
- **Key Responsibility**: Author SSP narratives, collect evidence, manage Watch monitoring, triage alerts, and coordinate remediation with engineers.
- **Reports to**: ISSM
- **Primary Interface**: VS Code (`@ato`), Microsoft Teams

---

## Permissions

| Capability | Allowed | Tool |
|-----------|---------|------|
| View system details | ✅ | `compliance_get_system`, `compliance_list_systems` |
| View categorization | ✅ | `compliance_get_categorization` |
| View baseline | ✅ | `compliance_get_baseline` |
| Write narratives | ✅ | `compliance_write_narrative` |
| Batch populate narratives | ✅ | `compliance_batch_populate_narratives` |
| Suggest narratives (AI) | ✅ | `compliance_suggest_narrative` |
| Track narrative progress | ✅ | `compliance_narrative_progress` |
| Collect evidence | ✅ | `compliance_collect_evidence` |
| Run compliance assessment | ✅ | `compliance_assess` |
| Import CKL/XCCDF scan results | ✅ | `compliance_import_ckl`, `compliance_import_xccdf` |
| Export CKL checklists | ✅ | `compliance_export_ckl` |
| Import Prisma Cloud scans | ✅ | `compliance_import_prisma_csv`, `compliance_import_prisma_api` |
| Create Privacy Threshold Analysis | ✅ | `compliance_create_pta` |
| Generate Privacy Impact Assessment | ✅ | `compliance_generate_pia` |
| Add/update interconnections | ✅ | `compliance_add_interconnection`, `compliance_update_interconnection` |
| Write SSP sections | ✅ | `compliance_write_ssp_section` |
| Check SSP completeness | ✅ | `compliance_ssp_completeness` |
| Enable/manage Watch monitoring | ✅ | `watch_enable_monitoring`, `watch_configure_monitoring` |
| View/acknowledge/fix alerts | ✅ | `watch_show_alerts`, `watch_acknowledge_alert`, `watch_fix_alert` |
| Create remediation boards | ✅ | `kanban_create_board` |
| Assign Kanban tasks | ✅ | `kanban_assign_task` |
| Review PIA (ISSM only) | ❌ | `compliance_review_pia` — officer (ISSM) only |
| Register/certify interconnections | ❌ | `compliance_register_agreement`, `compliance_certify_no_interconnections` — officer (ISSM) only |
| Review SSP sections | ❌ | `compliance_review_ssp_section` — officer (ISSM) only |
| Export OSCAL SSP | ❌ | `compliance_export_oscal_ssp` — officer (ISSM/SCA/AO) only |
| Dismiss alerts | ❌ | `watch_dismiss_alert` — officer (ISSM) only |
| Assess controls (SCA) | ❌ | `compliance_assess_control` — SCA only |
| Issue authorization | ❌ | `compliance_issue_authorization` — AO only |
| Accept risk | ❌ | `compliance_accept_risk` — AO only |

---

## Daily Activities

- Write and review control implementation narratives
- Collect evidence from Azure infrastructure
- Monitor compliance Watch alerts and triage new findings
- Coordinate remediation tasks with engineers on Kanban boards
- Respond to drift alerts and configuration changes
- Run periodic compliance assessments
- Ensure monitoring is enabled and properly configured

---

## RMF Phase Workflows

### Phase 3: Implement (ISSO Lead)

**Objective**: Author SSP control narratives, batch-populate inherited controls, and get AI-assisted suggestions for customer controls.

**Step-by-Step**:

1. Auto-populate inherited narratives → Tool: `compliance_batch_populate_narratives`
2. Get AI suggestions for customer controls → Tool: `compliance_suggest_narrative`
3. Write/update narrative text → Tool: `compliance_write_narrative`
4. Track completion by family → Tool: `compliance_narrative_progress`

**Natural Language Queries**:

> **"Auto-populate the inherited control narratives for system {id}"** → `compliance_batch_populate_narratives` — fills ~40–60% of narratives from the embedded control catalog

> **"Suggest a narrative for AC-2 on system {id}"** → `compliance_suggest_narrative` — AI draft with confidence score

> **"Write the narrative for AC-2: 'Account management is implemented using Azure AD...'"** → `compliance_write_narrative` — stores the narrative with Implemented status

> **"What's the narrative completion for the SC family?"** → `compliance_narrative_progress` — per-family completion percentages

> **"Show all controls missing narratives for system {id}"** → `compliance_narrative_progress` — lists controls without narratives

**AI Suggestion Confidence Levels**:

| Score | Meaning | Action |
|-------|---------|--------|
| ≥ 0.85 | High confidence (inherited controls) | Review and accept |
| 0.70–0.84 | Good confidence (shared controls) | Review and customize |
| 0.50–0.69 | Moderate confidence (customer controls) | Significant review needed |
| < 0.50 | Low confidence — flagged | Write manually |

**Documents Produced**:

| Document | Format | Purpose |
|----------|--------|---------|
| SSP Narratives | Per-control records | Individual control implementation statements |

!!! warning "Air-Gapped Note"
    In disconnected environments, `compliance_suggest_narrative` and `compliance_batch_populate_narratives` (AI-generated suggestions) are **unavailable**. Write all narratives manually using `compliance_write_narrative`. Inherited control narratives can still be auto-populated from the embedded control catalog (no network required).

---

### Phase 4: Assess (ISSO Support)

**Objective**: Collect evidence, support the SCA during assessment, and coordinate finding remediation.

**Step-by-Step**:

1. Collect evidence from Azure → Tool: `compliance_collect_evidence`
2. Run compliance assessment → Tool: `compliance_assess`
3. Create remediation Kanban board → Tool: `kanban_create_board`
4. Assign remediation tasks to engineers → Tool: `kanban_assign_task`
5. Fix alerts with optional dry-run → Tool: `watch_fix_alert`

**Natural Language Queries**:

> **"Collect evidence for the AC family on subscription {sub-id}"** → `compliance_collect_evidence` — collects Azure resource evidence with SHA-256 hashing

> **"Run a compliance assessment on subscription {sub-id}"** → `compliance_assess` — runs NIST 800-53 compliance assessment

> **"Create a remediation board from the latest assessment"** → `kanban_create_board` — creates Kanban board from findings

> **"Assign task REM-003 to engineer Bob Jones"** → `kanban_assign_task` — assigns remediation task

> **"Fix alert ALT-12345 with dry run first"** → `watch_fix_alert` — previews remediation before executing

---

### Phase 6: Monitor (ISSO Day-to-Day)

**Objective**: Manage continuous monitoring, triage alerts, maintain baselines, and handle auto-remediation.

**Step-by-Step**:

1. Enable monitoring → Tool: `watch_enable_monitoring`
2. View alerts → Tool: `watch_show_alerts`
3. Acknowledge alerts → Tool: `watch_acknowledge_alert`
4. Fix or escalate → Tool: `watch_fix_alert` / escalate to ISSM
5. Track trends → Tool: `watch_show_alerts` with time filters

**Natural Language Queries**:

> **"Enable daily monitoring for subscription {sub-id}"** → `watch_enable_monitoring` — enables scheduled daily scans

> **"Show all critical alerts from the last 7 days"** → `watch_show_alerts` — filtered alert list

> **"Acknowledge alert ALT-12345"** → `watch_acknowledge_alert` — pauses SLA escalation timer

> **"What drifted this week?"** → `watch_show_alerts` — shows drift alerts for recent period

> **"Configure auto-remediation for Low severity drift alerts"** → auto-remediation rule creation

> **"Set quiet hours from 22:00 to 06:00 on weekdays"** → `watch_configure_quiet_hours` — suppresses notifications during off-hours

> **"Configure escalation: if Critical alert is not acknowledged in 30 minutes, notify the ISSM"** → `watch_configure_escalation` — defines escalation path

**Watch Monitoring Tools**:

| Tool | Purpose |
|------|---------|
| `watch_enable_monitoring` | Enable scheduled/event-driven/combined monitoring |
| `watch_disable_monitoring` | Disable monitoring for a subscription |
| `watch_configure_monitoring` | Update frequency or mode |
| `watch_monitoring_status` | View all monitoring configurations |
| `watch_show_alerts` | List alerts with severity/status/family/time filters |
| `watch_get_alert` | Full alert details with history and correlations |
| `watch_acknowledge_alert` | Acknowledge (pauses SLA escalation) |
| `watch_fix_alert` | Remediate with optional dry-run |
| `watch_create_rule` | Create custom alert rules |
| `watch_list_rules` | List active alert rules |
| `watch_suppress_alerts` | Suppress alert patterns with expiration |
| `watch_configure_quiet_hours` | Set notification quiet hours |
| `watch_configure_notifications` | Configure channels (Chat, Email, Webhook) |
| `watch_configure_escalation` | Define escalation paths for SLA violations |

**Alert Lifecycle**:

```
New → Acknowledged → InProgress → Resolved
  ↓        ↓
Dismissed  Escalated (SLA violation)
```

**SLA Due Dates by Severity**:

| Severity | Due Date | Escalation |
|----------|----------|------------|
| Critical | 24 hours | After 30 min unacknowledged |
| High | 7 days | After 4 hours unacknowledged |
| Medium | 30 days | After 24 hours unacknowledged |
| Low | 90 days | After 7 days unacknowledged |

!!! info "Air-Gapped Note"
    In disconnected environments, Watch **event-driven** monitoring is unavailable (requires Azure Event Grid). Use **scheduled-only mode** with local policy cache. Alert notifications are limited to local channels (VS Code, audit log) — external email/webhook channels are unavailable.

---

### STIG & SCAP Scan Import

**Objective**: Upload CKL and XCCDF scan results to update compliance findings and link them to NIST 800-53 controls.

**Step-by-Step**:

1. Import CKL file from DISA STIG Viewer → Tool: `compliance_import_ckl`
2. Import XCCDF results from SCAP scanner → Tool: `compliance_import_xccdf`
3. Review import summary → Tool: `compliance_get_import_summary`
4. Export updated CKL for external review → Tool: `compliance_export_ckl`

**Natural Language Queries**:

> **"Upload the latest CKL for system {id}"** → `compliance_import_ckl` — parses DISA STIG Viewer checklist

> **"Import the SCAP scan results from last week"** → `compliance_import_xccdf` — parses XCCDF automated scan output

> **"Show the import summary for system {id}"** → `compliance_get_import_summary` — finding counts by status

> **"Export a CKL checklist for the Windows Server 2022 STIG"** → `compliance_export_ckl` — generates CKL file for external reviewers

---

### Prisma Cloud Scan Import

**Objective**: Import Prisma Cloud security posture data via CSV or API to enrich compliance findings.

**Step-by-Step**:

1. Import CSV export → Tool: `compliance_import_prisma_csv`
2. Or connect via API → Tool: `compliance_import_prisma_api`
3. Review mapped policies → Tool: `compliance_list_prisma_policies`
4. Track posture trends → Tool: `compliance_prisma_trend`

**Natural Language Queries**:

> **"Import the Prisma Cloud CSV export for system {id}"** → `compliance_import_prisma_csv` — parses CSV and maps policies to controls

> **"Connect Prisma Cloud API and pull the latest scan data"** → `compliance_import_prisma_api` — real-time API import

> **"List all Prisma policies mapped to our controls"** → `compliance_list_prisma_policies` — shows policy-to-control mapping

> **"Show the Prisma compliance trend for the last 30 days"** → `compliance_prisma_trend` — pass/fail trend data

---

### Privacy Analysis (PTA & PIA)

**Objective**: Conduct Privacy Threshold Analysis to determine if a PIA is required, then generate and submit the PIA for ISSM review.

**Step-by-Step**:

1. Create a Privacy Threshold Analysis → Tool: `compliance_create_pta`
2. If PTA indicates PIA required, generate PIA → Tool: `compliance_generate_pia`
3. Submit PIA for ISSM review (ISSM uses `compliance_review_pia`)

**Natural Language Queries**:

> **"Create a privacy threshold analysis for system {id}"** → `compliance_create_pta` — determines whether PII is collected/processed

> **"Generate a privacy impact assessment based on the PTA"** → `compliance_generate_pia` — drafts the PIA document

> **"Check overall privacy compliance for system {id}"** → `compliance_check_privacy_compliance` — validates all privacy artifacts

---

### Interconnection Registration

**Objective**: Document system-to-system connections that cross the authorization boundary and generate ISA documents for ISSM review.

**Step-by-Step**:

1. Add a new interconnection → Tool: `compliance_add_interconnection`
2. List existing interconnections → Tool: `compliance_list_interconnections`
3. Update interconnection details → Tool: `compliance_update_interconnection`
4. ISSM generates ISA from interconnection record → Tool: `compliance_generate_isa`

**Natural Language Queries**:

> **"Add an interconnection to the HR payroll system for system {id}"** → `compliance_add_interconnection` — registers the connection with direction, protocol, and data types

> **"List all interconnections for system {id}"** → `compliance_list_interconnections` — shows active/pending connections

> **"Update the bandwidth for interconnection ICN-001"** → `compliance_update_interconnection` — modifies connection metadata

---

### SSP Section Authoring

**Objective**: Write and submit NIST 800-18 SSP sections, track completeness, and support the authorization package.

**Step-by-Step**:

1. Write an SSP section → Tool: `compliance_write_ssp_section`
2. Check overall SSP completeness → Tool: `compliance_ssp_completeness`
3. Submit sections for ISSM review (ISSM uses `compliance_review_ssp_section`)

**Natural Language Queries**:

> **"Write SSP section 1 (System Identification) for system {id}"** → `compliance_write_ssp_section` — creates/updates using 13-section NIST 800-18 structure

> **"What's the SSP completeness for system {id}?"** → `compliance_ssp_completeness` — percentage by section with status breakdown

> **"Write the system environment section of the SSP"** → `compliance_write_ssp_section` — section 5: System Environment

**SSP Section Reference**:

| Section | Title |
|---------|-------|
| 1 | System Identification |
| 2 | System Categorization |
| 3 | System Owner & Contacts |
| 4 | Authorization Boundary |
| 5 | System Environment |
| 6 | System Interconnections |
| 7 | Applicable Laws & Regulations |
| 8 | Minimum Security Controls |
| 9 | Control Implementation |
| 10 | Continuous Monitoring |
| 11 | System Maintenance |
| 12 | Personnel Security |
| 13 | Contingency Planning |

---

## Cross-Persona Handoffs

| From | To | Trigger | Data |
|------|----|---------|------|
| ISSM → ISSO | System registered, baseline selected | System ID, control list for narrative authoring |
| ISSO → Engineer | Findings identified | Kanban remediation tasks with severity and SLA |
| Engineer → ISSO | Remediation complete | Task moved to InReview, evidence collected |
| ISSO → SCA | SSP ready for assessment | System ID, assessment scope, evidence package |
| ISSO → ISSM | Significant change detected | Change record, reauthorization flag |
| ISSO → ISSM | SLA escalation | Unacknowledged critical/high alert details |
| ISSO → ISSM | PIA submitted for review | PIA document generated from PTA |
| ISSO → ISSM | Interconnection registered | Interconnection record for ISA generation |
| ISSO → ISSM | SSP section submitted | Section content awaiting ISSM review |
| Watch → ISSO | Drift/violation detected | Alert with severity, control ID, resource ID |

---

## See Also

- [Getting Started: ISSO](../getting-started/isso.md) — First-time setup and orientation
- [RMF Phase Reference](../rmf-phases/index.md) — Phase-by-phase workflow details
- [Quick Reference Card](../reference/quick-reference-cards.md) — Printable ISSO cheat sheet
- [Compliance Watch Guide](../guides/compliance-watch.md) — Detailed monitoring documentation
- [Remediation Kanban Guide](../guides/remediation-kanban.md) — Task management workflows
- [SSP Authoring Guide](../guides/engineer-guide.md) — Narrative writing workflows
