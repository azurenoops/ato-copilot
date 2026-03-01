# Tool Inventory

> Complete reference of all 114 MCP tools available in ATO Copilot, grouped by category.

---

## How to Use This Reference

Each tool listing includes the tool name, a short description, the RMF phase(s) where it applies, and which RBAC roles can invoke it.

**RBAC Role Abbreviations**:

| Abbreviation | RBAC Role |
|--------------|-----------|
| **ISSM** | `Compliance.SecurityLead` |
| **ISSO** | `Compliance.Analyst` |
| **SCA** | `Compliance.Auditor` |
| **AO** | `Compliance.AuthorizingOfficial` |
| **Eng** | `Compliance.PlatformEngineer` (default) |
| **Admin** | `Compliance.Administrator` |

---

## Category 1: RMF Lifecycle Tools (34 tools)

Tools that drive the seven-phase RMF lifecycle from Prepare through Authorize.

| # | Tool | Description | Phase(s) | Roles |
|---|------|-------------|----------|-------|
| 1 | `compliance_register_system` | Register a new information system | Prepare | ISSM |
| 2 | `compliance_list_systems` | List registered systems | All | All |
| 3 | `compliance_get_system` | Get system details | All | All |
| 4 | `compliance_advance_rmf_step` | Advance or regress RMF phase | All | ISSM |
| 5 | `compliance_define_boundary` | Define authorization boundary | Prepare | ISSM |
| 6 | `compliance_exclude_from_boundary` | Exclude resource from boundary | Prepare | ISSM |
| 7 | `compliance_assign_rmf_role` | Assign RMF role to user | Prepare | ISSM |
| 8 | `compliance_list_rmf_roles` | List role assignments | Prepare | ISSM, ISSO |
| 9 | `compliance_categorize_system` | FIPS 199 categorization | Categorize | ISSM |
| 10 | `compliance_get_categorization` | View categorization | Categorize | All |
| 11 | `compliance_suggest_info_types` | AI-suggest SP 800-60 info types | Categorize | ISSM |
| 12 | `compliance_select_baseline` | Select NIST 800-53 baseline | Select | ISSM |
| 13 | `compliance_tailor_baseline` | Add/remove controls | Select | ISSM |
| 14 | `compliance_set_inheritance` | Set control inheritance | Select | ISSM |
| 15 | `compliance_get_baseline` | View baseline details | Select | All |
| 16 | `compliance_generate_crm` | Generate CRM | Select | ISSM |
| 17 | `compliance_write_narrative` | Write control narrative | Implement | ISSO, Eng |
| 18 | `compliance_suggest_narrative` | AI-suggest narrative | Implement | ISSO, Eng |
| 19 | `compliance_batch_populate_narratives` | Auto-fill inherited narratives | Implement | ISSO |
| 20 | `compliance_narrative_progress` | Track SSP completion | Implement | ISSM, ISSO |
| 21 | `compliance_generate_ssp` | Generate SSP document | Implement | ISSM, ISSO |
| 22 | `compliance_assess_control` | Record control effectiveness | Assess | SCA |
| 23 | `compliance_take_snapshot` | Immutable assessment snapshot | Assess | SCA |
| 24 | `compliance_compare_snapshots` | Compare snapshots | Assess | SCA, ISSM |
| 25 | `compliance_verify_evidence` | Evidence integrity check | Assess | SCA |
| 26 | `compliance_check_evidence_completeness` | Evidence coverage report | Assess | SCA |
| 27 | `compliance_generate_sar` | Generate SAR | Assess | SCA |
| 28 | `compliance_issue_authorization` | Issue ATO/ATOwC/IATT/DATO | Authorize | AO |
| 29 | `compliance_accept_risk` | Accept risk on finding | Authorize | AO |
| 30 | `compliance_show_risk_register` | View risk register | Authorize | AO, ISSM |
| 31 | `compliance_create_poam` | Create POA&M item | Assess | ISSM |
| 32 | `compliance_list_poam` | List POA&M items | Assess | ISSM, ISSO |
| 33 | `compliance_generate_rar` | Generate RAR | Assess | ISSM |
| 34 | `compliance_bundle_authorization_package` | Bundle SSP+SAR+RAR+POA&M+CRM | Authorize | ISSM |

---

## Category 2: Continuous Monitoring Tools (7 tools)

Tools for ConMon plans, reports, expiration tracking, and portfolio dashboards.

| # | Tool | Description | Phase(s) | Roles |
|---|------|-------------|----------|-------|
| 35 | `compliance_create_conmon_plan` | Create/update ConMon plan | Monitor | ISSM |
| 36 | `compliance_generate_conmon_report` | Generate periodic report | Monitor | ISSM, ISSO |
| 37 | `compliance_track_ato_expiration` | Check expiration status | Monitor | ISSM, AO |
| 38 | `compliance_report_significant_change` | Report significant change | Monitor | ISSM, ISSO |
| 39 | `compliance_reauthorization_workflow` | Check/initiate reauthorization | Monitor | ISSM |
| 40 | `compliance_multi_system_dashboard` | Portfolio dashboard | Monitor | ISSM, AO |
| 41 | `compliance_send_notification` | Send notification via channels | Monitor | ISSM |

---

## Category 3: Interoperability Tools (4 tools)

Tools for eMASS, OSCAL, and STIG cross-reference.

| # | Tool | Description | Phase(s) | Roles |
|---|------|-------------|----------|-------|
| 42 | `compliance_export_emass` | Export to eMASS Excel format | All | ISSM |
| 43 | `compliance_import_emass` | Import eMASS Excel with conflict resolution | All | ISSM |
| 44 | `compliance_export_oscal` | Export OSCAL v1.0.6 JSON | All | ISSM |
| 45 | `compliance_show_stig_mapping` | NIST-to-STIG cross-reference | All | All |

---

## Category 4: Template Management Tools (4 tools)

Tools for custom document templates. **Administrator only**.

| # | Tool | Description | Phase(s) | Roles |
|---|------|-------------|----------|-------|
| 46 | `compliance_upload_template` | Upload custom DOCX template | All | Admin |
| 47 | `compliance_list_templates` | List templates by document type | All | Admin, ISSM |
| 48 | `compliance_update_template` | Update template content | All | Admin |
| 49 | `compliance_delete_template` | Delete template | All | Admin |

---

## Category 5: Core Compliance Tools (11 tools)

General-purpose compliance tools from Features 001–014.

| # | Tool | Description | Phase(s) | Roles |
|---|------|-------------|----------|-------|
| 50 | `compliance_assess` | Run NIST 800-53 assessment | Assess | SCA, ISSM |
| 51 | `compliance_get_control_family` | Get control family info | All | All |
| 52 | `compliance_generate_document` | Generate compliance documents | All | ISSM, ISSO |
| 53 | `compliance_collect_evidence` | Collect Azure evidence | Assess | ISSO, Eng |
| 54 | `compliance_remediate` | Remediate findings | Implement | Eng |
| 55 | `compliance_validate_remediation` | Validate remediation | Implement | Eng, ISSO |
| 56 | `compliance_generate_plan` | Generate remediation plan | Implement | ISSM, Eng |
| 57 | `compliance_audit_log` | View audit trail | All | ISSM |
| 58 | `compliance_history` | View compliance history | All | ISSM, ISSO |
| 59 | `compliance_status` | Current compliance posture | All | All |
| 60 | `compliance_monitoring` | Monitoring setup | Monitor | ISSM |

---

## Category 6: Compliance Watch Tools (23 tools)

Real-time monitoring, alerting, auto-remediation, and trend analysis.

| # | Tool | Description | Phase(s) | Roles |
|---|------|-------------|----------|-------|
| 61 | `watch_enable_monitoring` | Enable scheduled monitoring | Monitor | ISSM, ISSO |
| 62 | `watch_disable_monitoring` | Disable monitoring | Monitor | ISSM |
| 63 | `watch_configure_monitoring` | Update frequency/mode | Monitor | ISSM |
| 64 | `watch_monitoring_status` | View monitoring status | Monitor | ISSM, ISSO |
| 65 | `watch_show_alerts` | List alerts with filters | Monitor | All |
| 66 | `watch_get_alert` | Get alert details | Monitor | All |
| 67 | `watch_acknowledge_alert` | Acknowledge alert | Monitor | ISSO, ISSM |
| 68 | `watch_fix_alert` | Remediate alert finding | Monitor | Eng, ISSO |
| 69 | `watch_dismiss_alert` | Dismiss alert (officer only) | Monitor | ISSM |
| 70 | `watch_create_rule` | Create custom alert rule | Monitor | ISSM |
| 71 | `watch_list_rules` | List alert rules | Monitor | ISSM, ISSO |
| 72 | `watch_suppress_alerts` | Suppress alert pattern | Monitor | ISSM |
| 73 | `watch_list_suppressions` | List suppressions | Monitor | ISSM |
| 74 | `watch_configure_quiet_hours` | Set notification quiet hours | Monitor | ISSM |
| 75 | `watch_configure_notifications` | Configure channels | Monitor | ISSM |
| 76 | `watch_configure_escalation` | Define escalation paths | Monitor | ISSM |
| 77 | `watch_alert_history` | Natural language alert queries | Monitor | ISSM, ISSO |
| 78 | `watch_compliance_trend` | Compliance score over time | Monitor | All |
| 79 | `watch_alert_statistics` | Alert counts and metrics | Monitor | ISSM, ISSO |
| 80 | `watch_auto_remediation_create` | Create auto-remediation rule | Monitor | ISSM |
| 81 | `watch_auto_remediation_list` | List auto-remediation rules | Monitor | ISSM |
| 82 | `watch_auto_remediation_status` | View execution status | Monitor | ISSM, ISSO |
| 83 | `watch_capture_baseline` | Capture compliance baseline | Monitor | ISSM |

---

## Category 7: Kanban Remediation Tools (18 tools)

Task management for remediation lifecycle tracking.

| # | Tool | Description | Phase(s) | Roles |
|---|------|-------------|----------|-------|
| 84 | `kanban_create_board` | Create board from assessment | Assess | ISSM |
| 85 | `kanban_board_show` | Display board overview | Assess | All |
| 86 | `kanban_get_task` | Get task details | Assess | All |
| 87 | `kanban_create_task` | Create remediation task | Assess | ISSM, ISSO |
| 88 | `kanban_assign_task` | Assign/reassign task | Assess | ISSM, ISSO |
| 89 | `kanban_move_task` | Move task between columns | Assess | All |
| 90 | `kanban_task_list` | List/filter tasks | Assess | All |
| 91 | `kanban_task_history` | View task audit trail | Assess | All |
| 92 | `kanban_task_validate` | Validate remediation | Assess | Eng, ISSO |
| 93 | `kanban_add_comment` | Add comment/@mention | Assess | All |
| 94 | `kanban_task_comments` | List comments | Assess | All |
| 95 | `kanban_edit_comment` | Edit comment (24hr window) | Assess | All |
| 96 | `kanban_delete_comment` | Delete comment (1hr window) | Assess | All |
| 97 | `kanban_remediate_task` | Execute remediation script | Assess | Eng |
| 98 | `kanban_collect_evidence` | Collect evidence for task | Assess | Eng, ISSO |
| 99 | `kanban_bulk_update` | Bulk assign/move/set dates | Assess | ISSM |
| 100 | `kanban_export` | Export as CSV or POA&M | Assess | ISSM |
| 101 | `kanban_archive_board` | Archive completed board | Assess | ISSM |

---

## Category 8: PIM & Authentication Tools (13 tools)

Privileged Identity Management, just-in-time access, and CAC session management.

| # | Tool | Description | Phase(s) | Roles |
|---|------|-------------|----------|-------|
| 102 | `pim_list_eligible` | List PIM-eligible roles | All | All |
| 103 | `pim_list_active` | List active PIM roles | All | All |
| 104 | `pim_activate_role` | Activate PIM role | All | All |
| 105 | `pim_deactivate_role` | Deactivate PIM role | All | All |
| 106 | `pim_extend_role` | Extend role activation | All | All |
| 107 | `pim_approve_request` | Approve activation request | All | ISSM |
| 108 | `pim_deny_request` | Deny activation request | All | ISSM |
| 109 | `pim_history` | View activation history | All | ISSM |
| 110 | `jit_request_access` | Request just-in-time access | All | All |
| 111 | `jit_list_sessions` | List active JIT sessions | All | All |
| 112 | `jit_revoke_access` | Revoke JIT session | All | ISSM |
| 113 | `cac_status` | Check CAC session status | All | All |
| 114 | `cac_sign_out` | Sign out CAC session | All | All |

---

## Tool Count Summary

| Category | Tools | Description |
|----------|-------|-------------|
| RMF Lifecycle | 34 | Seven-phase lifecycle management |
| Continuous Monitoring | 7 | ConMon, expiration, dashboards |
| Interoperability | 4 | eMASS, OSCAL, STIG |
| Template Management | 4 | Custom document templates |
| Core Compliance | 11 | Assessment, evidence, remediation |
| Compliance Watch | 23 | Real-time alerts and monitoring |
| Kanban Remediation | 18 | Task lifecycle management |
| PIM & Authentication | 13 | JIT access and CAC sessions |
| **Total** | **114** | |

---

## See Also

- [NL Query Reference](../guides/nl-query-reference.md) — Natural language queries mapped to tools
- [RBAC Roles](../personas/index.md#rbac-role-resolution) — Role resolution and inheritance
- [Troubleshooting](troubleshooting.md) — Common error scenarios
