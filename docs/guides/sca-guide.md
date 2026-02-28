# Security Control Assessor (SCA) Guide

> Feature 015 — Phase 9: Assessment Artifacts & CAT Severity

This guide walks Security Control Assessors through the assessment workflow using ATO Copilot's MCP tools.

---

## Overview

As an SCA, you record per-control effectiveness determinations, map findings to DoD CAT severity levels, take immutable assessment snapshots, verify evidence chain of custody, and generate Security Assessment Reports (SARs).

### Prerequisite Workflow

Before assessment, the system should have completed:

1. **Registration** — `compliance_register_system`
2. **Categorization** — `compliance_categorize_system` (FIPS 199)
3. **Baseline Selection** — `compliance_select_baseline` (NIST 800-53)
4. **SSP Authoring** — `compliance_write_narrative` / `compliance_generate_ssp`

---

## 1. Assess Controls

Use `compliance_assess_control` to record your determination for each control.

### Satisfied Determination

```
assessment_id: "assess-123"
control_id: "AC-2"
determination: "Satisfied"
method: "Test"
notes: "Account management procedures verified against STIG checklist."
```

### OtherThanSatisfied with CAT Severity

When a control is not satisfied, you **must** specify a CAT severity level:

| CAT Level | Impact | Examples |
|-----------|--------|----------|
| **CAT I** | Critical — direct risk to availability, integrity, or confidentiality | Missing encryption, no access control enforcement |
| **CAT II** | Significant — potential for system compromise | Weak password policies, incomplete logging |
| **CAT III** | Low — administrative or documentation gaps | Missing audit review procedures, incomplete labels |

```
assessment_id: "assess-123"
control_id: "AC-3"
determination: "OtherThanSatisfied"
method: "Test"
cat_severity: "CatII"
notes: "Mandatory access control checks missing for privileged operations."
```

### Assessment Methods

| Method | When to Use |
|--------|-------------|
| `Test` | Execute procedures, observe behavior, validate configurations |
| `Interview` | Question personnel about policies, procedures, practices |
| `Examine` | Review documentation, logs, configuration artifacts |

### Linking Evidence

Attach evidence IDs to your assessment:

```
evidence_ids: "ev-001,ev-002,ev-003"
```

---

## 2. Take Assessment Snapshots

After completing a batch of assessments, create an immutable snapshot:

```
system_id: "sys-456"
assessment_id: "assess-123"
```

This creates a **tamper-proof record** with:
- SHA-256 integrity hash over all determinations, findings, and evidence
- Timestamp of capture
- Compliance score at that point in time
- Immutable flag (cannot be updated or deleted)

**Best Practice:** Take snapshots at key milestones — after each assessment cycle, before/after remediation, and before authorization decisions.

---

## 3. Verify Evidence Integrity

Use `compliance_verify_evidence` to verify that evidence content hasn't been tampered with:

```
evidence_id: "ev-001"
```

The tool recomputes the SHA-256 hash and compares it with the stored hash:
- **verified** — Hash matches, evidence is authentic
- **tampered** — Hash differs, evidence content was modified after collection

---

## 4. Check Evidence Completeness

Before generating a SAR, verify all controls have supporting evidence:

```
system_id: "sys-456"
assessment_id: "assess-123"
family_filter: "AC"  (optional — filter to one family)
```

The report shows:
- Overall completeness percentage
- Per-control status: `verified`, `unverified`, or `missing`
- Controls lacking any evidence

---

## 5. Compare Assessment Cycles

After remediation and reassessment, compare snapshots to track progress:

```
snapshot_id_a: "snap-001"  (before remediation)
snapshot_id_b: "snap-002"  (after remediation)
```

The comparison shows:
- Score delta (improvement or regression)
- Controls newly satisfied
- Controls newly other-than-satisfied
- New and resolved findings
- Evidence changes

---

## 6. Generate Security Assessment Report (SAR)

Generate the SAR for authorization decision-makers:

```
system_id: "sys-456"
assessment_id: "assess-123"
format: "markdown"
```

The SAR includes:
1. **Executive Summary** — System, assessor, overall score, finding counts
2. **CAT Severity Breakdown** — CAT I/II/III counts with risk categorization
3. **Control Family Results** — Per-family pass/fail with CAT mapping
4. **Risk Summary** — Assessment-to-authorization risk posture
5. **Detailed Findings** — Per-control determination details

---

## Typical SCA Workflow

```
1. compliance_assess_control  ← Assess each control (batch)
2. compliance_take_snapshot   ← Snapshot current state
3. compliance_verify_evidence ← Spot-check evidence integrity
4. compliance_check_evidence_completeness ← Verify coverage
5. compliance_generate_sar    ← Generate SAR
6. (Remediation occurs)
7. compliance_assess_control  ← Re-assess remediated controls
8. compliance_take_snapshot   ← Snapshot after remediation
9. compliance_compare_snapshots ← Show improvement
10. compliance_generate_sar   ← Updated SAR for AO decision
```

---

## RBAC Requirements

| Tool | Required Role |
|------|--------------|
| `compliance_assess_control` | `Compliance.Auditor` (SCA) |
| `compliance_take_snapshot` | `Compliance.Auditor` |
| `compliance_verify_evidence` | `Compliance.Auditor` |
| `compliance_check_evidence_completeness` | Any compliance role |
| `compliance_compare_snapshots` | Any compliance role |
| `compliance_generate_sar` | `Compliance.Auditor` |
