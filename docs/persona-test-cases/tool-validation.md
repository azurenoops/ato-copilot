# Tool Validation Report: 88 Spec-Referenced MCP Tools

**Feature**: 020 | **Date**: _______________ | **Validated By**: _______________

---

## Purpose

This document provides the cross-reference validation of all 88 MCP tools referenced in `spec.md` against the actual MCP server `/tools/list` endpoint. Complete this validation before beginning any persona test case execution.

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
| 34 | `compliance_create_poam` | ISSM-23 | ⬜ | |
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

### Import (6 tools)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 62 | `compliance_import_ckl` | ISSO-09 | ⬜ | |
| 63 | `compliance_import_xccdf` | ISSO-10 | ⬜ | |
| 64 | `compliance_list_imports` | ISSO-11 | ⬜ | |
| 65 | `compliance_get_import_summary` | ISSO-12, SCA-19 | ⬜ | |
| 66 | `compliance_import_prisma_csv` | ISSM-19,40, ERR-02 | ⬜ | |
| 67 | `compliance_import_prisma_api` | ISSM-20 | ⬜ | |

### Prisma (2 tools)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 68 | `compliance_list_prisma_policies` | ISSM-21, SCA-10 | ⬜ | |
| 69 | `compliance_prisma_trend` | ISSM-22, SCA-11, ENG-22 | ⬜ | |

### Kanban (9 tools)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 70 | `kanban_create_board` | ISSM-26 | ⬜ | |
| 71 | `kanban_bulk_update` | ISSM-27 | ⬜ | |
| 72 | `kanban_export` | ISSM-28 | ⬜ | |
| 73 | `kanban_task_list` | ENG-11 | ⬜ | |
| 74 | `kanban_get_task` | ENG-12 | ⬜ | |
| 75 | `kanban_move_task` | ENG-13, ENG-19 | ⬜ | |
| 76 | `kanban_assign_task` | ISSO-22 | ⬜ | |
| 77 | `kanban_remediate_task` | ENG-14, ENG-15 | ⬜ | |
| 78 | `kanban_task_validate` | ENG-16 | ⬜ | |

### Kanban Evidence & Comments (2 tools)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 79 | `kanban_collect_evidence` | ENG-17 | ⬜ | |
| 80 | `kanban_add_comment` | ENG-18 | ⬜ | |

### PIM / Auth (8 tools)

| # | Tool Name | Spec TC-IDs | Present | Notes |
|---|-----------|-------------|---------|-------|
| 81 | `cac_status` | AUTH-01 | ⬜ | |
| 82 | `pim_list_eligible` | AUTH-02 | ⬜ | |
| 83 | `pim_activate_role` | AUTH-03 | ⬜ | |
| 84 | `pim_list_active` | AUTH-04 | ⬜ | |
| 85 | `pim_deactivate_role` | AUTH-08 | ⬜ | |
| 86 | `pim_approve_request` | AUTH-06 | ⬜ | |
| 87 | `pim_deny_request` | AUTH-07 | ⬜ | |
| 88 | `jit_request_access` | AUTH-05 | ⬜ | |

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
| Import | 6 | ___ | ___ |
| Prisma | 2 | ___ | ___ |
| Kanban | 11 | ___ | ___ |
| PIM / Auth | 8 | ___ | ___ |
| **Total** | **88** | **___** | **___** |

---

## Missing Tools

If any tools are missing, document them here for T007 blocked items:

| # | Tool Name | Impact | Blocked Test Cases | Resolution |
|---|-----------|--------|-------------------|------------|
| | | | | |

---

## Validation Result

- ⬜ **PASS** — All 88 tools present. Proceed to persona testing.
- ⬜ **FAIL** — Missing tools detected. Resolve before proceeding.

**Validated By**: _______________ | **Date**: _______________
