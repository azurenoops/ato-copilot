# Quick Reference Cards

> Printable cheat sheets for each persona — top NL queries, key tools, and phase responsibilities.

---

## ISSM Quick Reference

```
┌─────────────────────────────────────────────────────────┐
│                 ISSM Quick Reference                    │
│          Role: Compliance.SecurityLead                  │
├─────────────────────────────────────────────────────────┤
│ REGISTER:  "Register system {name} as {type} in {env}" │
│ BOUNDARY:  "Add resources to system {id}'s boundary"    │
│ ROLES:     "Assign {name} as {role} for system {id}"    │
│ CATEGORIZE:"Categorize system {id} with {info types}"   │
│ BASELINE:  "Select baseline for system {id}"            │
│ TAILOR:    "Remove {control} — {rationale}"             │
│ SSP:       "Generate SSP for system {id}"               │
│ POAM:      "Create POA&M for finding {id}"              │
│ PACKAGE:   "Bundle authorization package"               │
│ DASHBOARD: "Show multi-system dashboard"                │
│ CONMON:    "Generate monthly report for system {id}"    │
│ EMASS:     "Export system {id} to eMASS"                │
├─────────────────────────────────────────────────────────┤
│ Phases: R in ALL | A in Prepare, Categorize, Select     │
│ Gate authority: Only ISSM can advance RMF phases        │
└─────────────────────────────────────────────────────────┘
```

### ISSM Key Tools

| Tool | Purpose |
|------|---------|
| `compliance_register_system` | Register new system |
| `compliance_advance_rmf_step` | Move between RMF phases |
| `compliance_categorize_system` | FIPS 199 categorization |
| `compliance_select_baseline` | Choose control baseline |
| `compliance_generate_ssp` | Generate SSP document |
| `compliance_bundle_authorization_package` | Bundle for AO |
| `compliance_multi_system_dashboard` | Portfolio overview |
| `compliance_export_emass` | Export to eMASS |

---

## ISSO Quick Reference

```
┌─────────────────────────────────────────────────────────┐
│                 ISSO Quick Reference                    │
│           Role: Compliance.Analyst                      │
├─────────────────────────────────────────────────────────┤
│ NARRATIVES:  "Auto-populate inherited narratives"       │
│ SUGGEST:     "Suggest narrative for {control}"          │
│ PROGRESS:    "What's the SSP completion percentage?"    │
│ WATCH:       "Enable monitoring for subscription {id}"  │
│ ALERTS:      "Show all unacknowledged alerts"           │
│ ACK:         "Acknowledge alert ALT-{id}"               │
│ FIX:         "Fix alert ALT-{id}"                       │
│ EVIDENCE:    "Collect evidence for {control}"           │
│ CONMON:      "Generate ConMon report for {period}"      │
│ ASSIGN:      "Assign task REM-{id} to {engineer}"       │
├─────────────────────────────────────────────────────────┤
│ Phases: R in Implement, Monitor | C in Assess           │
│ Lead: Day-to-day SSP authoring and ConMon execution     │
└─────────────────────────────────────────────────────────┘
```

### ISSO Key Tools

| Tool | Purpose |
|------|---------|
| `compliance_batch_populate_narratives` | Auto-fill inherited |
| `compliance_suggest_narrative` | AI narrative generation |
| `compliance_narrative_progress` | Track completion |
| `watch_enable_monitoring` | Start monitoring |
| `watch_show_alerts` | View alerts |
| `watch_acknowledge_alert` | Acknowledge alert |
| `compliance_generate_conmon_report` | Monthly ConMon |
| `kanban_assign_task` | Assign remediation |

---

## SCA Quick Reference

```
┌─────────────────────────────────────────────────────────┐
│                  SCA Quick Reference                    │
│           Role: Compliance.Auditor                      │
├─────────────────────────────────────────────────────────┤
│ ASSESS:    "Assess {control} as {determination}"        │
│ SNAPSHOT:  "Take assessment snapshot for system {id}"    │
│ EVIDENCE:  "Verify evidence {id}"                       │
│ COMPLETE:  "Check evidence completeness for {family}"   │
│ COMPARE:   "Compare snapshots {a} and {b}"              │
│ SAR:       "Generate SAR for system {id}"               │
│ RAR:       "Generate RAR for system {id}"               │
│                                                         │
│ ⚠️ Read-only: Cannot modify system, fix findings,      │
│    or issue authorization decisions                     │
├─────────────────────────────────────────────────────────┤
│ Phases: R in Assess only | Independence required        │
│ Lead: Security assessment and SAR/RAR generation        │
└─────────────────────────────────────────────────────────┘
```

### SCA Key Tools

| Tool | Purpose |
|------|---------|
| `compliance_assess_control` | Record effectiveness |
| `compliance_take_snapshot` | Immutable snapshot |
| `compliance_verify_evidence` | Hash verification |
| `compliance_check_evidence_completeness` | Coverage check |
| `compliance_compare_snapshots` | Trend analysis |
| `compliance_generate_sar` | SAR production |
| `compliance_generate_rar` | RAR production |

---

## AO Quick Reference

```
┌─────────────────────────────────────────────────────────┐
│                   AO Quick Reference                    │
│       Role: Compliance.AuthorizingOfficial               │
├─────────────────────────────────────────────────────────┤
│ REVIEW:    "Show authorization package for system {id}" │
│ AUTHORIZE: "Issue ATO for system {id} expiring {date}"  │
│ CONDITIONS:"Issue ATOwC — {conditions}"                 │
│ RISK:      "Accept risk on finding {id} — {rationale}"  │
│ DENY:      "Deny authorization — {reason}"              │
│ REGISTER:  "Show risk register for system {id}"         │
│ DASHBOARD: "Show all my authorized systems"             │
│ EXPIRATION:"What ATOs expire in the next 90 days?"      │
├─────────────────────────────────────────────────────────┤
│ Phases: R in Authorize only | I in Monitor              │
│ Lead: Authorization decisions and risk acceptance        │
└─────────────────────────────────────────────────────────┘
```

### AO Key Tools

| Tool | Purpose |
|------|---------|
| `compliance_issue_authorization` | Issue ATO/ATOwC/IATT/DATO |
| `compliance_accept_risk` | Accept residual risk |
| `compliance_show_risk_register` | View risk register |
| `compliance_multi_system_dashboard` | Portfolio overview |
| `compliance_track_ato_expiration` | Expiration monitoring |

---

## Engineer Quick Reference

```
┌─────────────────────────────────────────────────────────┐
│               Engineer Quick Reference                  │
│        Role: Compliance.PlatformEngineer (default)      │
├─────────────────────────────────────────────────────────┤
│ LEARN:     "What does {control} mean for Azure?"        │
│ STIG:      "What STIG rules apply to {technology}?"     │
│ SCAN:      "Scan my Bicep file for compliance"          │
│ NARRATIVE: "Suggest narrative for {control}"             │
│ WRITE:     "Write narrative for {control}: {text}"      │
│ TASKS:     "Show my assigned tasks"                     │
│ FIX:       "Fix task REM-{id} with dry run"             │
│ VALIDATE:  "Validate task REM-{id}"                     │
│ EVIDENCE:  "Collect evidence for task REM-{id}"         │
│ PROGRESS:  "Show narrative progress for {family}"       │
│ PLAN:      "Generate remediation plan for {sub-id}"     │
│ REMEDIATE: "Remediate finding {id} with dry run"        │
│ VERIFY:    "Validate remediation for finding {id}"      │
├─────────────────────────────────────────────────────────┤
│ Phases: C in Implement | C in Assess (remediation)      │
│ Lead: Technical remediation and IaC compliance          │
└─────────────────────────────────────────────────────────┘
```

### Engineer Key Tools

| Tool | Purpose |
|------|---------|
| `compliance_get_control_family` | Learn about controls |
| `compliance_show_stig_mapping` | STIG cross-reference |
| `compliance_suggest_narrative` | AI narrative suggestion |
| `compliance_write_narrative` | Write narrative |
| `kanban_task_list` | View assigned tasks |
| `kanban_remediate_task` | Execute remediation |
| `kanban_task_validate` | Validate fix |
| `kanban_collect_evidence` | Collect evidence |
| `compliance_generate_plan` | Generate remediation plan |
| `compliance_remediate` | Fix finding directly |
| `compliance_validate_remediation` | Verify fix applied |

---

## Administrator Quick Reference

```
┌─────────────────────────────────────────────────────────┐
│             Administrator Quick Reference               │
│          Role: Compliance.Administrator                  │
├─────────────────────────────────────────────────────────┤
│ UPLOAD:    "Upload SSP template from {file}"            │
│ TEMPLATES: "List all document templates"                │
│ UPDATE:    "Update the SSP template with {file}"        │
│ DELETE:    "Delete template {id}"                       │
│                                                         │
│ ⚠️ Cannot: Register systems, assess controls, or       │
│    issue authorization decisions (separation of duties) │
├─────────────────────────────────────────────────────────┤
│ Phases: No phase involvement — infrastructure only      │
│ Lead: Template management and system configuration      │
└─────────────────────────────────────────────────────────┘
```

### Administrator Key Tools

| Tool | Purpose |
|------|---------|
| `compliance_upload_template` | Upload DOCX template |
| `compliance_list_templates` | List templates |
| `compliance_update_template` | Update template |
| `compliance_delete_template` | Delete template |

---

## See Also

- [Tool Inventory](tool-inventory.md) — Complete 114-tool reference
- [NL Query Reference](../guides/nl-query-reference.md) — Full query catalog
- [Persona Overview](../personas/index.md) — Role definitions and RACI
