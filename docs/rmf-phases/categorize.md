# RMF Phase 1: Categorize

> Categorize the system and the information processed, stored, and transmitted by the system using FIPS 199 and SP 800-60 information types.

---

## Phase Overview

| Attribute | Value |
|-----------|-------|
| **Phase Number** | 1 |
| **NIST Reference** | SP 800-37 Rev. 2, §3.2 |
| **Lead Persona** | ISSM |
| **Supporting Personas** | ISSO, Engineer (consulted) |
| **Key Outcome** | FIPS 199 categorization with C/I/A high-water mark and DoD Impact Level |

---

## Persona Responsibilities

### ISSM (Lead)

**Tasks in this phase**:

1. Get AI-suggested information types → Tool: `compliance_suggest_info_types`
2. Apply categorization → Tool: `compliance_categorize_system`
3. Review categorization → Tool: `compliance_get_categorization`
4. Advance to Select → Tool: `compliance_advance_rmf_step`

**Natural Language Queries**:

> **"Suggest information types for system {id} — it's a financial management and audit logging system"** → `compliance_suggest_info_types` — AI suggests SP 800-60 information types with confidence scores

> **"Categorize system {id} with these information types: Financial Management (C: Moderate, I: Moderate, A: Low), Information Security (C: Moderate, I: High, A: Moderate)"** → `compliance_categorize_system` — applies info types and computes high-water mark

> **"What is the current categorization for system {id}?"** → `compliance_get_categorization` — shows FIPS 199 notation and DoD IL

> **"What DoD Impact Level was derived for system {id}?"** → `compliance_get_categorization` — IL2–IL6 derived from categorization + NSS flag

> **"Advance to the Select phase"** → `compliance_advance_rmf_step` — transitions to Select (gate-checked)

### ISSO (Support)

- Assist with identifying information types processed by the system
- Review categorization for accuracy

### Engineer (Consulted)

- Provide domain knowledge about data types and processing activities
- Confirm hosting environment details affecting IL derivation

---

## Key Outputs

| Output | Description |
|--------|-------------|
| FIPS 199 Notation | `SC System = {(confidentiality, MODERATE), (integrity, HIGH), (availability, MODERATE)}` |
| Overall Categorization | High (the maximum across C/I/A) |
| DoD Impact Level | IL5 (derived from categorization + NSS flag) |
| NIST Baseline | High (determined by overall categorization) |

---

## Documents Produced

| Document | Owner | Format | Gate Dependency |
|----------|-------|--------|----------------|
| FIPS 199 Categorization Report | ISSM | Embedded in SSP | Required before Select |

---

## Phase Gates

| Gate | Condition | Checked By |
|------|-----------|-----------|
| Categorization exists | System has been categorized with FIPS 199 | `compliance_advance_rmf_step` |
| Information types defined | At least one information type assigned | `compliance_advance_rmf_step` |

---

## Transition to Next Phase

| Trigger | From Phase | To Phase | Handoff |
|---------|-----------|----------|---------|
| `compliance_advance_rmf_step` with gate pass | Categorize | Select | Categorization determines baseline level |

---

## See Also

- [Previous Phase: Prepare](prepare.md)
- [Next Phase: Select](select.md)
- [ISSM Guide](../guides/issm-guide.md) — Full ISSM workflow documentation
- [Impact Levels Reference](../reference/impact-levels.md) — DoD IL details
