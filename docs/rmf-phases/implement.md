# RMF Phase 3: Implement

> Implement the controls in the security and privacy plans and document the implementation details.

---

## Phase Overview

| Attribute | Value |
|-----------|-------|
| **Phase Number** | 3 |
| **NIST Reference** | SP 800-37 Rev. 2, §3.4 |
| **Lead Persona** | ISSO and Engineer (shared) |
| **Supporting Personas** | ISSM (oversight) |
| **Key Outcome** | SSP narratives authored, SSP document generated |

---

## Persona Responsibilities

### ISSO (Lead — Narrative Authoring)

**Tasks in this phase**:

1. Auto-populate inherited narratives → Tool: `compliance_batch_populate_narratives`
2. Get AI suggestions → Tool: `compliance_suggest_narrative`
3. Write/update narratives → Tool: `compliance_write_narrative`
4. Track completion → Tool: `compliance_narrative_progress`

**Natural Language Queries**:

> **"Auto-populate the inherited control narratives for system {id}"** → `compliance_batch_populate_narratives` — fills ~40–60% of narratives from the embedded control catalog

> **"Suggest a narrative for AC-2 on system {id}"** → `compliance_suggest_narrative` — AI draft with confidence score

> **"Write the narrative for AC-2: 'Account management is implemented using Azure AD...'"** → `compliance_write_narrative` — stores narrative with status

> **"What's the narrative completion for the SC family?"** → `compliance_narrative_progress` — per-family completion percentages

> **"Show all controls missing narratives for system {id}"** → `compliance_narrative_progress` — lists controls without narratives

!!! warning "Air-Gapped Note"
    In disconnected environments, `compliance_suggest_narrative` and AI-generated narratives in `compliance_batch_populate_narratives` are **unavailable**. Write all narratives manually using `compliance_write_narrative`. Inherited control narratives can still be auto-populated from the embedded control catalog (no network required).

### Engineer (Lead — Technical Implementation)

**Tasks in this phase**:

1. Learn about controls → Tool: `compliance_get_control` / Knowledge Base
2. Scan IaC for compliance → Tool: IaC diagnostics
3. View STIG findings → Tool: `compliance_show_stig_mapping`
4. Write implementation narratives → Tool: `compliance_write_narrative`
5. Auto-populate inherited narratives → Tool: `compliance_batch_populate_narratives`
6. Generate remediation plan → Tool: `compliance_generate_plan`
7. Remediate findings → Tool: `compliance_remediate`
8. Validate remediation → Tool: `compliance_validate_remediation`

**Natural Language Queries**:

> **"What does NIST control AC-2 mean for Azure?"** → Knowledge Base — plain-language explanation tailored to Azure

> **"Show STIG findings for Azure SQL Database"** → `compliance_show_stig_mapping` — DISA STIG requirements mapped to NIST controls

> **"Scan my Bicep file for compliance issues"** → IaC diagnostics — findings with CAT severity

> **"Suggest a narrative for control SC-7 on system {id}"** → `compliance_suggest_narrative` — AI draft based on system context

> **"Auto-populate inherited control narratives for system {id}"** → `compliance_batch_populate_narratives` — batch fill

> **"Generate a remediation plan for subscription {sub-id}"** → `compliance_generate_plan` — prioritized plan across all findings

> **"Remediate finding {finding-id} with dry run"** → `compliance_remediate` — preview fix before applying (`dry_run: true` by default)

> **"Validate remediation for finding {finding-id}"** → `compliance_validate_remediation` — re-scan to confirm fix was applied

### ISSM (Oversight)

**Tasks in this phase**:

1. Track SSP completion → Tool: `compliance_narrative_progress`
2. Generate SSP document → Tool: `compliance_generate_ssp`

**Natural Language Queries**:

> **"What's the SSP completion status for system {id}?"** → `compliance_narrative_progress` — overall completion

> **"Generate the System Security Plan for system {id}"** → `compliance_generate_ssp` — formal SSP document

> **"Generate the SSP as PDF"** → `compliance_generate_ssp` with format parameter

---

## AI Suggestion Confidence Levels

| Score | Meaning | Action |
|-------|---------|--------|
| ≥ 0.85 | High confidence (inherited controls) | Review and accept |
| 0.70–0.84 | Good confidence (shared controls) | Review and customize |
| 0.50–0.69 | Moderate confidence (customer controls) | Significant review needed |
| < 0.50 | Low confidence — flagged | Write manually |

---

## Documents Produced

| Document | Owner | Format | Gate Dependency |
|----------|-------|--------|----------------|
| System Security Plan (SSP) | ISSO / ISSM | Markdown, PDF, DOCX | Advisory (Implement → Assess) |

---

## Phase Gates

| Gate | Condition | Checked By |
|------|-----------|-----------|
| Advisory | No hard block — advancement allowed regardless of SSP completion | `compliance_advance_rmf_step` |

---

## Transition to Next Phase

| Trigger | From Phase | To Phase | Handoff |
|---------|-----------|----------|---------|
| `compliance_advance_rmf_step` (advisory gate) | Implement | Assess | SSP and evidence package ready for SCA assessment |

---

## See Also

- [Previous Phase: Select](select.md)
- [Next Phase: Assess](assess.md)
- [ISSO Guide](../personas/isso.md) — Full ISSO workflow documentation
- [Engineer Guide](../guides/engineer-guide.md) — SSP authoring and IaC diagnostics
