# RMF Phase Reference

> The Risk Management Framework (RMF) lifecycle as implemented by ATO Copilot.

---

## Lifecycle Overview

The RMF defines seven phases that take an information system from initial registration through continuous monitoring. ATO Copilot supports all seven phases with MCP tools, natural language queries, and automated workflows.

```
┌──────────┐   ┌────────────┐   ┌──────────┐   ┌───────────┐
│  Prepare │──▶│ Categorize │──▶│  Select  │──▶│ Implement │
│ (Phase 0)│   │ (Phase 1)  │   │ (Phase 2)│   │ (Phase 3) │
└──────────┘   └────────────┘   └──────────┘   └───────────┘
                                                      │
                                                      ▼
┌──────────┐   ┌───────────┐   ┌──────────┐   ┌───────────┐
│ Monitor  │◀──│ Authorize │◀──│  Assess  │◀──│     │     │
│ (Phase 6)│   │ (Phase 5) │   │ (Phase 4)│   └───────────┘
└──────────┘   └───────────┘   └──────────┘
      │
      └──▶ (Reauthorization triggers loop back to Prepare or Assess)
```

---

## Phase Summary

| Phase | Name | Lead Persona | Key Outcome | Gate Tool |
|-------|------|-------------|-------------|-----------|
| 0 | [Prepare](prepare.md) | ISSM | System registered, boundary defined, roles assigned | `compliance_advance_rmf_step` |
| 1 | [Categorize](categorize.md) | ISSM | FIPS 199 categorization, DoD IL derived | `compliance_advance_rmf_step` |
| 2 | [Select](select.md) | ISSM | NIST 800-53 baseline selected, tailored, inheritance set | `compliance_advance_rmf_step` |
| 3 | [Implement](implement.md) | ISSO / Engineer | SSP narratives authored, document generated | `compliance_advance_rmf_step` |
| 4 | [Assess](assess.md) | SCA | Controls assessed, SAR/RAR generated | `compliance_advance_rmf_step` |
| 5 | [Authorize](authorize.md) | AO | Authorization decision issued | `compliance_advance_rmf_step` |
| 6 | [Monitor](monitor.md) | ISSM / ISSO | Continuous monitoring, ConMon reports, reauthorization | — |

---

## Gate Summary

Each phase transition is guarded by gate conditions enforced by `compliance_advance_rmf_step`:

| Transition | Gate Conditions |
|-----------|----------------|
| Prepare → Categorize | ≥ 1 RMF role assigned + ≥ 1 boundary resource |
| Categorize → Select | Categorization exists + ≥ 1 information type |
| Select → Implement | Baseline exists |
| Implement → Assess | Advisory only (no hard block) |
| Assess → Authorize | Advisory only (no hard block) |
| Authorize → Monitor | Authorization decision issued |

---

## Tool Mapping by Phase

| Phase | Primary Tools |
|-------|--------------|
| Prepare | `compliance_register_system`, `compliance_define_boundary`, `compliance_assign_rmf_role`, `compliance_list_rmf_roles` |
| Categorize | `compliance_suggest_info_types`, `compliance_categorize_system`, `compliance_get_categorization` |
| Select | `compliance_select_baseline`, `compliance_tailor_baseline`, `compliance_set_inheritance`, `compliance_generate_crm`, `compliance_show_stig_mapping` |
| Implement | `compliance_batch_populate_narratives`, `compliance_suggest_narrative`, `compliance_write_narrative`, `compliance_narrative_progress`, `compliance_generate_ssp` |
| Assess | `compliance_assess_control`, `compliance_take_snapshot`, `compliance_verify_evidence`, `compliance_check_evidence_completeness`, `compliance_compare_snapshots`, `compliance_generate_sar`, `compliance_generate_rar`, `compliance_create_poam` |
| Authorize | `compliance_bundle_authorization_package`, `compliance_issue_authorization`, `compliance_accept_risk`, `compliance_show_risk_register` |
| Monitor | `compliance_create_conmon_plan`, `compliance_generate_conmon_report`, `compliance_track_ato_expiration`, `compliance_report_significant_change`, `compliance_reauthorization_workflow`, `compliance_multi_system_dashboard`, `compliance_export_emass`, `compliance_export_oscal`, Watch tools |

---

## Persona Responsibilities by Phase

| Phase | ISSM | ISSO | SCA | AO | Engineer |
|-------|------|------|-----|-----|----------|
| Prepare | **Lead** | Support | — | Informed | Support |
| Categorize | **Lead** | Support | — | Informed | Consulted |
| Select | **Lead** | Support | Review | — | Consulted |
| Implement | Oversight | **Lead** | — | — | **Lead** |
| Assess | Support | Support | **Lead** | Informed | Support |
| Authorize | Package prep | Support | SAR delivery | **Decide** | — |
| Monitor | **Lead** | **Day-to-day** | Periodic | Escalation | Remediation |

---

## Documents Produced by Phase

| Phase | Documents | Owner |
|-------|-----------|-------|
| Prepare | — (informational only) | — |
| Categorize | FIPS 199 Categorization Report | ISSM |
| Select | Customer Responsibility Matrix (CRM) | ISSM |
| Implement | System Security Plan (SSP) | ISSO / ISSM |
| Assess | SAR, RAR, POA&M, Assessment Snapshots | SCA / ISSM |
| Authorize | Authorization Decision Letter, Risk Acceptance Memo, Terms & Conditions | AO |
| Monitor | ConMon Reports, Reauthorization Package | ISSM / ISSO |

---

## See Also

- [Persona Overview](../personas/index.md) — Role definitions and RACI matrix
- [Tool Inventory](../reference/tool-inventory.md) — Complete list of 114 MCP tools
- [NIST Controls Reference](../reference/nist-controls.md) — Control baseline details
