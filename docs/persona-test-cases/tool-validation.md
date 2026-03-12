# Tool Validation Report: 106 Spec-Referenced MCP Tools

**Feature**: 020 | **Date**: _______________ | **Validated By**: _______________

---

## Purpose

This document provides the cross-reference validation of all MCP tools referenced in specs against the actual MCP server `/tools/list` endpoint. Complete this validation before beginning any persona test case execution.

---

## Validation Method

1. Start the MCP server: `dotnet run --project src/Ato.Copilot.Mcp`
2. Query the tools endpoint: `curl http://localhost:{port}/tools/list | jq '.tools | length'`
3. For each tool below, verify it appears in the response
4. Mark each tool as ✅ (present) or ❌ (missing)

---

## Tool Validation Matrix

### System & RMF Lifecycle (11 tools)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 1 | `compliance_register_system` | ISSM-01, ENG-26(denied) | ⬜ | |
| 2 | `compliance_define_boundary` | ISSM-02 | ⬜ | |
| 3 | `compliance_exclude_from_boundary` | ISSM-03 | ⬜ | |
| 4 | `compliance_assign_rmf_role` | ISSM-04 | ⬜ | |
| 5 | `compliance_list_rmf_roles` | ISSM-05 | ⬜ | |
| 6 | `compliance_advance_rmf_step` | ISSM-06,10,16,30, ERR-01 | ⬜ | |
| 7 | `compliance_suggest_info_types` | ISSM-07 | ⬜ | |
| 8 | `compliance_categorize_system` | ISSM-08, ERR-03 | ⬜ | |
| 9 | `compliance_get_categorization` | ISSM-09, SCA-03 | ⬜ | |
| 10 | `compliance_select_baseline` | ISSM-11 | ⬜ | |
| 11 | `compliance_get_baseline` | ISSM-15, SCA-02 | ⬜ | |

### Baseline & Tailoring (3 tools)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 12 | `compliance_tailor_baseline` | ISSM-12 | ⬜ | |
| 13 | `compliance_set_inheritance` | ISSM-13 | ⬜ | |
| 14 | `compliance_generate_crm` | ISSM-14 | ⬜ | |

### Narratives & SSP (6 tools)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 15 | `compliance_narrative_progress` | ISSM-17, ISSO-02,06, ENG-10 | ⬜ | |
| 16 | `compliance_suggest_narrative` | ISSO-03, ENG-04 | ⬜ | |
| 17 | `compliance_write_narrative` | ISSO-04,05, ENG-05, SCA-21(denied), AO-12(denied) | ⬜ | |
| 18 | `compliance_batch_populate_narratives` | ISSO-01 | ⬜ | |
| 19 | `compliance_generate_ssp` | ISSM-18, ISSO-07,08 | ⬜ | |
| 20 | `compliance_collect_evidence` | ISSO-19 | ⬜ | |

### Assessment (7 tools)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 21 | `compliance_take_snapshot` | SCA-01, SCA-13 | ⬜ | |
| 22 | `compliance_compare_snapshots` | SCA-12 | ⬜ | |
| 23 | `compliance_check_evidence_completeness` | SCA-04 | ⬜ | |
| 24 | `compliance_verify_evidence` | SCA-05 | ⬜ | |
| 25 | `compliance_assess_control` | SCA-06,07,08,09, ENG-23(denied), AO-14(denied) | ⬜ | |
| 26 | `compliance_assess` | SCA-20 | ⬜ | |
| 27 | `compliance_generate_sar` | SCA-17, ERR-04 | ⬜ | |

### SAP (5 tools)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 28 | `compliance_generate_sap` | ISSM-41 | ⬜ | |
| 29 | `compliance_update_sap` | ISSM-42, ERR-07 | ⬜ | |
| 30 | `compliance_finalize_sap` | ISSM-43, ERR-06 | ⬜ | |
| 31 | `compliance_get_sap` | SCA-14 | ⬜ | |
| 32 | `compliance_list_saps` | SCA-15 | ⬜ | |

### Risk & Authorization (7 tools)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 33 | `compliance_generate_rar` | ISSM-25, SCA-18 | ⬜ | |
| 34 | `compliance_create_poam` | ISSM-23a,23b,23c | ⬜ | |
| 35 | `compliance_list_poam` | ISSM-24 | ⬜ | |
| 36 | `compliance_show_risk_register` | ISSM-31, AO-03 | ⬜ | |
| 37 | `compliance_bundle_authorization_package` | ISSM-29, AO-02, ERR-05 | ⬜ | |
| 38 | `compliance_issue_authorization` | AO-04,05,06,07, SCA-23(denied), ENG-24(denied) | ⬜ | |
| 39 | `compliance_accept_risk` | AO-08 | ⬜ | |

### Remediation (4 tools)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 40 | `compliance_generate_plan` | ENG-06 | ⬜ | |
| 41 | `compliance_remediate` | ENG-07,08, SCA-22(denied), AO-13(denied), ERR-08 | ⬜ | |
| 42 | `compliance_validate_remediation` | ENG-09 | ⬜ | |
| 43 | `compliance_get_control_family` | ENG-01 | ⬜ | |

### STIG (1 tool)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 44 | `compliance_show_stig_mapping` | ENG-02 | ⬜ | |

### Monitoring & ConMon (11 tools)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 45 | `compliance_create_conmon_plan` | ISSM-32 | ⬜ | |
| 46 | `compliance_generate_conmon_report` | ISSM-33, ISSO-20 | ⬜ | |
| 47 | `compliance_track_ato_expiration` | ISSM-34, AO-09 | ⬜ | |
| 48 | `compliance_report_significant_change` | ISSM-35, ISSO-21 | ⬜ | |
| 49 | `compliance_reauthorization_workflow` | ISSM-36 | ⬜ | |
| 50 | `compliance_multi_system_dashboard` | ISSM-37, AO-01 | ⬜ | |
| 51 | `compliance_export_emass` | ISSM-38 | ⬜ | |
| 52 | `compliance_audit_log` | ISSM-39 | ⬜ | |
| 53 | `watch_enable_monitoring` | ISSO-13 | ⬜ | |
| 54 | `watch_monitoring_status` | ISSO-14 | ⬜ | |
| 55 | `watch_show_alerts` | ISSO-15, AO-11, ENG-20 | ⬜ | |

### Watch Alerts (6 tools)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 56 | `watch_get_alert` | ISSO-16 | ⬜ | |
| 57 | `watch_acknowledge_alert` | ISSO-17 | ⬜ | |
| 58 | `watch_fix_alert` | ISSO-18 | ⬜ | |
| 59 | `watch_dismiss_alert` | SCA-24(denied), ENG-25(denied) | ⬜ | |
| 60 | `watch_alert_history` | ISSO-23 | ⬜ | |
| 61 | `watch_compliance_trend` | ISSO-24, AO-10 | ⬜ | |

### Import (8 tools)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 62 | `compliance_import_ckl` | ISSO-09 | ⬜ | |
| 63 | `compliance_import_xccdf` | ISSO-10 | ⬜ | |
| 64 | `compliance_list_imports` | ISSO-11 | ⬜ | |
| 65 | `compliance_get_import_summary` | ISSO-12, SCA-19 | ⬜ | |
| 66 | `compliance_import_prisma_csv` | ISSM-19,40, ERR-02 | ⬜ | |
| 67 | `compliance_import_prisma_api` | ISSM-20 | ⬜ | |
| 116 | `compliance_import_nessus` | F026 | ⬜ | ACAS/Nessus scan import |
| 117 | `compliance_list_nessus_imports` | F026 | ⬜ | Nessus import history |

### Prisma (2 tools)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 68 | `compliance_list_prisma_policies` | ISSM-21, SCA-10 | ⬜ | |
| 69 | `compliance_prisma_trend` | ISSM-22, SCA-11, ENG-22 | ⬜ | |

### Privacy Analysis (4 tools) — F021

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 70 | `compliance_create_pta` | ISSM-44, ISSO-26 | ⬜ | |
| 71 | `compliance_generate_pia` | ISSM-46 | ⬜ | |
| 72 | `compliance_review_pia` | ISSM-47 | ⬜ | |
| 73 | `compliance_check_privacy_compliance` | ISSM-61, ISSO-28, SCA-29 | ⬜ | |

### Interconnection Management (8 tools) — F021

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 74 | `compliance_add_interconnection` | ISSM-48, ISSO-29, ENG-28 | ⬜ | |
| 75 | `compliance_list_interconnections` | ISSM-49, ISSO-30 | ⬜ | |
| 76 | `compliance_update_interconnection` | ISSM-50 | ⬜ | |
| 77 | `compliance_generate_isa` | ISSM-52 | ⬜ | |
| 78 | `compliance_register_agreement` | ISSM-53 | ⬜ | |
| 79 | `compliance_update_agreement` | ISSM-54 | ⬜ | |
| 80 | `compliance_validate_agreements` | ISSM-55, SCA-26 | ⬜ | |
| 81 | `compliance_certify_no_interconnections` | ISSM-56(neg) | ⬜ | |

### SSP Authoring & OSCAL (5 tools) — F022

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 82 | `compliance_write_ssp_section` | ISSO-31,32,33,35, ENG-29 | ⬜ | |
| 83 | `compliance_review_ssp_section` | ISSM-56,60 | ⬜ | |
| 84 | `compliance_ssp_completeness` | ISSM-57, ISSO-34, SCA-25, AO-15, ENG-30 | ⬜ | |
| 85 | `compliance_export_oscal_ssp` | ISSM-58, SCA-27, AO-16 | ⬜ | |
| 86 | `compliance_validate_oscal_ssp` | ISSM-59, SCA-28 | ⬜ | |

### Narrative Governance (8 tools) — F024

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 107 | `compliance_narrative_history` | ISSO-36, ENG-31 | ⬜ | |
| 108 | `compliance_narrative_diff` | ISSO-37, ENG-32 | ⬜ | |
| 109 | `compliance_rollback_narrative` | ISSO-38, ENG-33 | ⬜ | |
| 110 | `compliance_submit_narrative` | ISSO-39, ENG-34 | ⬜ | |
| 111 | `compliance_review_narrative` | ISSM-62 | ⬜ | |
| 112 | `compliance_batch_review_narratives` | ISSM-63 | ⬜ | |
| 113 | `compliance_narrative_approval_progress` | ISSM-64, ISSO-40, SCA-29, AO-17 | ⬜ | |
| 114 | `compliance_batch_submit_narratives` | ISSO-41, ENG-35 | ⬜ | |

### CKL Export (1 tool) — F017

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 87 | `compliance_export_ckl` | ISSM-61, ISSO-25, ENG-27 | ⬜ | |

### Kanban (9 tools)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 88 | `kanban_create_board` | ISSM-26 | ⬜ | |
| 89 | `kanban_bulk_update` | ISSM-27 | ⬜ | |
| 90 | `kanban_export` | ISSM-28 | ⬜ | |
| 91 | `kanban_task_list` | ENG-11 | ⬜ | |
| 92 | `kanban_get_task` | ENG-12 | ⬜ | |
| 93 | `kanban_move_task` | ENG-13, ENG-19 | ⬜ | |
| 94 | `kanban_assign_task` | ISSO-22 | ⬜ | |
| 95 | `kanban_remediate_task` | ENG-14, ENG-15 | ⬜ | |
| 96 | `kanban_task_validate` | ENG-16 | ⬜ | |

### Kanban Evidence & Comments (2 tools)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 97 | `kanban_collect_evidence` | ENG-17 | ⬜ | |
| 98 | `kanban_add_comment` | ENG-18 | ⬜ | |

### PIM / Auth (8 tools)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 99 | `cac_status` | AUTH-01 | ⬜ | |
| 100 | `pim_list_eligible` | AUTH-02 | ⬜ | |
| 101 | `pim_activate_role` | AUTH-03 | ⬜ | |
| 102 | `pim_list_active` | AUTH-04 | ⬜ | |
| 103 | `pim_deactivate_role` | AUTH-08 | ⬜ | |
| 104 | `pim_approve_request` | AUTH-06 | ⬜ | |
| 105 | `pim_deny_request` | AUTH-07 | ⬜ | |
| 106 | `jit_request_access` | AUTH-05 | ⬜ | |

### HW/SW Inventory (9 tools) — F025

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 107 | `inventory_add_item` | INV-01 | ⬜ | |
| 108 | `inventory_update_item` | INV-02 | ⬜ | |
| 109 | `inventory_decommission_item` | INV-03 | ⬜ | |
| 110 | `inventory_list` | INV-04 | ⬜ | |
| 111 | `inventory_get` | INV-05 | ⬜ | |
| 112 | `inventory_export` | INV-06 | ⬜ | |
| 113 | `inventory_import` | INV-07 | ⬜ | |
| 114 | `inventory_completeness` | INV-08 | ⬜ | |
| 115 | `inventory_auto_seed` | INV-09 | ⬜ | |

---

## Validation Summary

| Category | Count | Present | Missing |
|----------|-------|---------|---------|
| System & RMF Lifecycle | 11 | ___ | ___ |
| Baseline & Tailoring | 3 | ___ | ___ |
| Narratives & SSP | 6 | ___ | ___ |
| Assessment | 7 | ___ | ___ |
| SAP | 5 | ___ | ___ |
| Risk & Authorization | 7 | ___ | ___ |
| Remediation | 4 | ___ | ___ |
| STIG | 1 | ___ | ___ |
| Monitoring & ConMon | 11 | ___ | ___ |
| Watch Alerts | 6 | ___ | ___ |
| Import | 8 | ___ | ___ |
| Prisma | 2 | ___ | ___ |
| Privacy Analysis | 4 | ___ | ___ |
| Interconnection Management | 8 | ___ | ___ |
| SSP Authoring & OSCAL | 5 | ___ | ___ |
| Narrative Governance | 8 | ___ | ___ |
| CKL Export | 1 | ___ | ___ |
| Kanban | 11 | ___ | ___ |
| PIM / Auth | 8 | ___ | ___ |
| HW/SW Inventory | 9 | ___ | ___ |
| **Total** | **117** | **___** | **___** |

---

## Missing Tools

If any tools are missing, document them here for T007 blocked items:

| # | Tool Name | Impact | Blocked Test Cases | Resolution |
|---|-----------|--------|-------------------|------------|
| | | | | |

---

## Validation Result

- ⬜ **PASS** — All 117 tools present. Proceed to persona testing.
- ⬜ **FAIL** — Missing tools detected. Resolve before proceeding.

**Validated By**: _______________ | **Date**: _______________
