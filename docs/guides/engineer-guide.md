# SSP Authoring Workflow — Engineer Guide

> Feature 015: Persona-Driven RMF Workflows — US5: SSP Authoring & Narrative Management

This guide walks through the complete SSP authoring workflow using the MCP compliance tools, from initial system registration through SSP document generation.

!!! tip "New to ATO Copilot?"
    If this is your first time using ATO Copilot as an Engineer, start with the [Engineer Getting Started](../getting-started/engineer.md) page for prerequisites, first-time setup, and your first 3 commands.

---

## Prerequisites

Before starting SSP authoring, the following must be completed:

1. **System registered** — `compliance_register_system`
2. **Boundary defined** — `compliance_define_boundary`
3. **RMF roles assigned** — `compliance_assign_rmf_role`
4. **System categorized** — `compliance_categorize_system` (FIPS 199)
5. **Baseline selected** — `compliance_select_baseline` (NIST 800-53)
6. **Inheritance set** — `compliance_set_inheritance` (inherited/shared/customer designations)

The system must be advanced to the **Implement** RMF phase before SSP authoring begins.

---

## Workflow Overview

```
┌─────────────────────────────────────────────────────┐
│                  SSP Authoring Flow                  │
│                                                     │
│  1. Batch populate inherited narratives             │
│     └─ compliance_batch_populate_narratives          │
│                                                     │
│  2. Review suggestions for remaining controls       │
│     └─ compliance_suggest_narrative (per control)    │
│                                                     │
│  3. Write/update narratives                         │
│     └─ compliance_write_narrative (per control)      │
│                                                     │
│  4. Track progress                                  │
│     └─ compliance_narrative_progress                 │
│                                                     │
│  5. Generate SSP document                           │
│     └─ compliance_generate_ssp                       │
└─────────────────────────────────────────────────────┘
```

---

## Step 1: Batch Populate Inherited Narratives

Start by auto-populating narratives for all inherited and shared controls. This is the fastest way to build initial SSP coverage.

```
Tool: compliance_batch_populate_narratives
Parameters:
  system_id: "<your-system-guid>"
```

This populates narratives using provider templates (e.g., "This control is fully inherited from Azure Government (FedRAMP High)"). It is **idempotent** — running it again will skip controls that already have narratives.

To populate only inherited controls first:

```
Tool: compliance_batch_populate_narratives
Parameters:
  system_id: "<your-system-guid>"
  inheritance_type: "Inherited"
```

Then populate shared controls separately:

```
Tool: compliance_batch_populate_narratives
Parameters:
  system_id: "<your-system-guid>"
  inheritance_type: "Shared"
```

**Expected result:** 40-60% of controls auto-populated depending on inheritance coverage.

---

## Step 2: Suggest Narratives for Remaining Controls

For customer-responsible controls that require manual authoring, use the suggestion tool to get AI-generated drafts:

```
Tool: compliance_suggest_narrative
Parameters:
  system_id: "<your-system-guid>"
  control_id: "AC-2"
```

The tool returns:
- **Suggested narrative text** — a draft based on system context and control requirements
- **Confidence score** — 0.85 for inherited, 0.75 for shared, 0.55 for customer controls
- **Reference sources** — NIST SP 800-53, FedRAMP, DoD SRGs

> **Important:** AI suggestions are drafts. Always review and customize before accepting.

---

## Step 3: Write Control Narratives

Write or update implementation narratives for individual controls:

```
Tool: compliance_write_narrative
Parameters:
  system_id: "<your-system-guid>"
  control_id: "AC-2"
  narrative: "Account management is implemented using Azure Active Directory..."
  status: "Implemented"
```

**Status options:**
| Status | Meaning |
|--------|---------|
| `Implemented` | Control is fully implemented (default) |
| `PartiallyImplemented` | Control is partially implemented |
| `Planned` | Control implementation is planned |
| `NotApplicable` | Control does not apply to this system |

The tool uses **upsert behavior** — calling it again for the same (system_id, control_id) pair updates the existing narrative.

---

## Step 4: Track Progress

Monitor SSP completion across all control families:

```
Tool: compliance_narrative_progress
Parameters:
  system_id: "<your-system-guid>"
```

This returns:
- **Overall percentage** — total completion across all controls
- **Per-family breakdown** — total, completed, draft, and missing counts per NIST family

To focus on a specific family:

```
Tool: compliance_narrative_progress
Parameters:
  system_id: "<your-system-guid>"
  family_filter: "AC"
```

**Progress classification:**
- **Completed** = `Implemented` or `NotApplicable`
- **Draft** = `PartiallyImplemented` or `Planned`
- **Missing** = No narrative record exists

**Target:** 100% completion before generating the final SSP.

---

## Step 5: Generate the SSP Document

Generate the complete System Security Plan:

```
Tool: compliance_generate_ssp
Parameters:
  system_id: "<your-system-guid>"
```

The generated Markdown document includes four sections:

| Section | Content |
|---------|---------|
| System Information | Name, type, mission criticality, hosting environment, RMF phase |
| Security Categorization | FIPS 199 notation, C/I/A impacts, DoD IL, information types |
| Control Baseline | Baseline level, overlay, total controls, tailoring/inheritance summary |
| Control Implementations | Per-family grouped controls with narratives and status |

To generate only specific sections:

```
Tool: compliance_generate_ssp
Parameters:
  system_id: "<your-system-guid>"
  sections: "system_information,categorization"
```

**Warnings:** The tool reports controls with missing narratives in the `warnings` array. Resolve these before final submission.

---

## Recommended Workflow Order

| Step | Tool | Persona | Purpose |
|------|------|---------|---------|
| 1 | `compliance_batch_populate_narratives` | Platform Engineer | Auto-fill inherited controls |
| 2 | `compliance_narrative_progress` | Security Lead | Review initial coverage |
| 3 | `compliance_suggest_narrative` | Platform Engineer | Get AI drafts for remaining controls |
| 4 | `compliance_write_narrative` | Platform Engineer | Write/edit customer narratives |
| 5 | `compliance_narrative_progress` | Security Lead | Verify completion |
| 6 | `compliance_generate_ssp` | Security Lead | Produce final SSP document |

---

## Tips

- **Start with batch populate** — it handles inherited controls automatically and is idempotent
- **Use family_filter** in progress checks to focus on one family at a time
- **Write narratives iteratively** — use `PartiallyImplemented` status for in-progress work
- **Review AI suggestions** — confidence scores indicate reliability; lower scores need more review
- **Generate SSP incrementally** — use the `sections` parameter to generate and review one section at a time
- **Check warnings** — the SSP generator flags controls missing narratives; address these before assessment

---

## Architecture Notes

- **Entity:** `ControlImplementation` — stores per-control narratives with unique constraint on `(RegisteredSystemId, ControlId)`
- **Service:** `ISspService` / `SspService` — business logic with `IProgress<string>` support for long-running operations
- **Tools:** 5 MCP tools registered via DI in `ServiceCollectionExtensions.cs` and wired in `ComplianceMcpTools.cs`
- **Tests:** 35 unit tests (`SspAuthoringToolTests.cs`) + 5 integration tests (`SspAuthoringIntegrationTests.cs`)

---

## Remediation Workflows

ATO Copilot provides two remediation paths:

| Path | Tools | When to Use |
|------|-------|-------------|
| **Standalone** | `compliance_generate_plan` → `compliance_remediate` → `compliance_validate_remediation` | Quick fixes by finding ID — no task tracking needed |
| **Kanban** | `kanban_task_list` → `kanban_remediate_task` → `kanban_task_validate` | Task-managed remediation with assignment, audit trails, and POA&M export |

### Standalone Remediation

Use the standalone tools when you want to fix a finding directly without task tracking:

| Step | Command | Tool | Purpose |
|------|---------|------|---------|
| 1 | Generate remediation plan | `compliance_generate_plan` | Prioritized plan for all findings on a subscription |
| 2 | Remediate with dry run | `compliance_remediate` | Preview fix — `dry_run: true` by default |
| 3 | Apply the fix | `compliance_remediate` | Set `dry_run: false` to apply |
| 4 | Validate the fix | `compliance_validate_remediation` | Re-scan to confirm finding is resolved |

!!! tip "Remediation workflow chaining"
    After an assessment reveals findings, generate a remediation plan first (`compliance_generate_plan`), then fix individual findings (`compliance_remediate`). Always validate after applying (`compliance_validate_remediation`).

### Kanban Remediation Workflow

When the ISSO or ISSM creates a remediation board from assessment findings, engineers receive Kanban tasks to fix compliance issues.

### Task Lifecycle

```
Backlog → ToDo → InProgress → InReview → Done
                     ↕
                  Blocked
```

### Common Commands

| Command | Tool | Purpose |
|---------|------|---------|
| Show my assigned tasks | `kanban_task_list` | View assigned remediation tasks |
| Show task details | `kanban_get_task` | Full details with control ID, resources, script |
| Move to In Progress | `kanban_move_task` | Start working on a task |
| Fix with dry run | `kanban_remediate_task` | Preview remediation before applying |
| Validate the fix | `kanban_task_validate` | Re-scan resources to verify remediation |
| Collect evidence | `kanban_collect_evidence` | Collect compliance evidence for the task |
| Move to In Review | `kanban_move_task` | Submit for ISSO review |

### Status Transition Rules

| Transition | Rule |
|-----------|------|
| → Blocked | Requires blocker comment |
| Blocked → | Requires resolution comment |
| → Done | Requires validation pass (or officer override) |
| → InProgress | Auto-assigns if unassigned |
| → InReview | Triggers automatic validation scan |
| Done → anything | Terminal — cannot reopen |

---

## VS Code IaC Diagnostics

ATO Copilot integrates compliance checking directly into your VS Code editing experience:

- **IaC Diagnostics** — Compliance findings appear as squiggly underlines in Bicep, Terraform, and ARM template files
  - CAT I / CAT II findings → Error severity (red underline)
  - CAT III findings → Warning severity (yellow underline)
- **Quick Fix** — Lightbulb Code Actions suggest fixes based on STIG findings
- **Hover Info** — Hovering over a flagged resource shows the NIST control, STIG rule, and CAT severity
- **`@ato` Chat Participant** — Ask compliance questions in the Copilot Chat panel

---

## See Also

- [Engineer Getting Started](../getting-started/engineer.md) — First-time setup and first 3 commands
- [Persona Overview](../personas/index.md) — All personas, RACI matrix, and role definitions
- [RMF Phase Reference](../rmf-phases/index.md) — Phase-by-phase workflow details
- [Remediation Kanban Guide](remediation-kanban.md) — Full Kanban board documentation
- [Compliance Watch Guide](compliance-watch.md) — Alert handling for drift findings
- [Quick Reference Card](../reference/quick-reference-cards.md) — Printable Engineer cheat sheet
