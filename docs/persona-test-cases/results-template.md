# Test Execution Results: Persona End-to-End Test Cases

**Feature**: 020 | **Spec Version**: 1.0.0 | **Branch**: `020-persona-test-cases`
**Tester**: _______________ | **Date Started**: _______________ | **Date Completed**: _______________
**Environment**: Azure Government | **MCP Server Version**: _______________

---

## Execution Summary

| Metric | Value |
|--------|-------|
| Total Test Cases | 147 |
| Cross-Persona Scenarios | 3 (40 steps) |
| Passed | ___ |
| Failed | ___ |
| Blocked | ___ |
| Skipped | ___ |
| Overall Pass Rate | ___% |
| RBAC Denial Verification | ___/19 returned 403 |
| Avg Response Time | ___s |

---

## Phase 2: Environment Validation

### T004 — Tool Registration Verification

| # | Check | Status | Notes |
|---|-------|--------|-------|
| 1 | MCP server responds to `/health` | ⬜ | |
| 2 | `/tools/list` returns ≥118 tools | ⬜ | Actual count: ___ |
| 3 | All 88 spec-referenced tools present | ⬜ | Missing: ___ |

### T005 — PIM Role Activation

| Role | Activate | Verify | Deactivate | Status |
|------|----------|--------|------------|--------|
| `Compliance.SecurityLead` | ⬜ | ⬜ | ⬜ | |
| `Compliance.Analyst` | ⬜ | ⬜ | ⬜ | |
| `Compliance.Auditor` | ⬜ | ⬜ | ⬜ | |
| `Compliance.AuthorizingOfficial` | ⬜ | ⬜ | ⬜ | |
| `Compliance.PlatformEngineer` | ⬜ | ⬜ | ⬜ | |

### T006 — Test Data Files

| File | Format | Purpose | Available | Notes |
|------|--------|---------|-----------|-------|
| Sample Prisma CSV | `.csv` | ISSM-19, ISSO import | ⬜ | |
| Prisma API JSON | `.json` | ISSM-20 | ⬜ | |
| CKL checklist | `.ckl` | ISSO-09 | ⬜ | |
| XCCDF results | `.xml` | ISSO-10 | ⬜ | |

### T007 — Blocked Items

| Item | Reason | Impact | Workaround |
|------|--------|--------|------------|
| _none documented_ | | | |

**Checkpoint**: ⬜ Environment validated — persona test execution can begin

---

## Phase 3: ISSM Persona (US1) — 43 Test Cases

**Role**: `Compliance.SecurityLead` | **Interface**: Microsoft Teams
**Activated At**: _______________ | **Completed At**: _______________

### Phase 0 — Prepare (ISSM-01 to ISSM-06)

| TC-ID | Task | Status | Duration | Actual Output | Notes |
|-------|------|--------|----------|---------------|-------|
| ISSM-01 | Register Eagle Eye system | ⬜ | | | system_id: |
| ISSM-02 | Define authorization boundary | ⬜ | | | resource count: |
| ISSM-03 | Exclude dev Key Vault from boundary | ⬜ | | | |
| ISSM-04 | Assign ISSO (Jane Smith) + SCA (Bob Jones) | ⬜ | | | |
| ISSM-05 | List RMF role assignments | ⬜ | | | |
| ISSM-06 | Advance to Categorize | ⬜ | | | RMF step: |

**Subtotal**: ___/6 passed | Avg duration: ___s

### Phase 1 — Categorize (ISSM-07 to ISSM-10)

| TC-ID | Task | Status | Duration | Actual Output | Notes |
|-------|------|--------|----------|---------------|-------|
| ISSM-07 | Suggest info types (mission planning) | ⬜ | | | |
| ISSM-08 | Categorize as Mod/Mod/Low | ⬜ | | | overall impact: |
| ISSM-09 | View categorization | ⬜ | | | |
| ISSM-10 | Advance to Select | ⬜ | | | RMF step: |

**Subtotal**: ___/4 passed | Avg duration: ___s

### Phase 2 — Select (ISSM-11 to ISSM-16)

| TC-ID | Task | Status | Duration | Actual Output | Notes |
|-------|------|--------|----------|---------------|-------|
| ISSM-11 | Select Moderate baseline | ⬜ | | | control count: |
| ISSM-12 | Tailor — remove PE-1 | ⬜ | | | new count: |
| ISSM-13 | Set AC-1 through AC-4 inherited | ⬜ | | | |
| ISSM-14 | Generate CRM | ⬜ | | | |
| ISSM-15 | View baseline details | ⬜ | | | |
| ISSM-16 | Advance to Implement | ⬜ | | | RMF step: |

**Subtotal**: ___/6 passed | Avg duration: ___s

### Phase 3 — Implement Oversight (ISSM-17 to ISSM-22)

| TC-ID | Task | Status | Duration | Actual Output | Notes |
|-------|------|--------|----------|---------------|-------|
| ISSM-17 | Check SSP progress | ⬜ | | | overall %: |
| ISSM-18 | Generate SSP | ⬜ | | | warnings: |
| ISSM-19 | Import Prisma CSV scan | ⬜ | | | findings created: |
| ISSM-20 | Import Prisma API scan | ⬜ | | | CLI scripts extracted: |
| ISSM-21 | List Prisma policies | ⬜ | | | policy count: |
| ISSM-22 | View Prisma trends (90d by severity) | ⬜ | | | |

**Subtotal**: ___/6 passed | Avg duration: ___s

### SAP Generation (ISSM-41 to ISSM-43)

| TC-ID | Task | Status | Duration | Actual Output | Notes |
|-------|------|--------|----------|---------------|-------|
| ISSM-41 | Generate SAP (Draft) | ⬜ | | | control entries: |
| ISSM-42 | Update SAP (team, schedule, override) | ⬜ | | | |
| ISSM-43 | Finalize SAP (immutable) | ⬜ | | | SHA-256 hash: |

**Subtotal**: ___/3 passed | Avg duration: ___s

### Phase 4 — Assess Prep (ISSM-23 to ISSM-28)

| TC-ID | Task | Status | Duration | Actual Output | Notes |
|-------|------|--------|----------|---------------|-------|
| ISSM-23a | Create POA&M (from assessment) | ⬜ | | | |
| ISSM-23b | Create POA&M (from STIG/scan import) | ⬜ | | | |
| ISSM-23c | Create POA&M (from Prisma Cloud) | ⬜ | | | |
| ISSM-24 | List POA&M items | ⬜ | | | item count: |
| ISSM-25 | Generate RAR | ⬜ | | | |
| ISSM-26 | Create Kanban remediation board | ⬜ | | | task count: |
| ISSM-27 | Bulk assign CAT I to SSgt Rodriguez | ⬜ | | | updated count: |
| ISSM-28 | Export Kanban as POA&M | ⬜ | | | |

**Subtotal**: ___/8 passed | Avg duration: ___s

### Phase 5 — Authorize Submission (ISSM-29 to ISSM-31)

| TC-ID | Task | Status | Duration | Actual Output | Notes |
|-------|------|--------|----------|---------------|-------|
| ISSM-29 | Bundle authorization package | ⬜ | | | completeness warnings: |
| ISSM-30 | Advance to Authorize | ⬜ | | | RMF step: |
| ISSM-31 | View risk register | ⬜ | | | entry count: |

**Subtotal**: ___/3 passed | Avg duration: ___s

### Phase 6 — Monitor (ISSM-32 to ISSM-40)

| TC-ID | Task | Status | Duration | Actual Output | Notes |
|-------|------|--------|----------|---------------|-------|
| ISSM-32 | Create ConMon plan (monthly/quarterly) | ⬜ | | | |
| ISSM-33 | Generate ConMon report | ⬜ | | | compliance score: |
| ISSM-34 | Track ATO expiration | ⬜ | | | alert level: |
| ISSM-35 | Report significant change (SIPR interconnection) | ⬜ | | | requires_reauth: |
| ISSM-36 | Check reauthorization triggers | ⬜ | | | trigger count: |
| ISSM-37 | Multi-system dashboard | ⬜ | | | system count: |
| ISSM-38 | Export to eMASS | ⬜ | | | |
| ISSM-39 | View audit log | ⬜ | | | entry count: |
| ISSM-40 | Re-import Prisma (verify remediation) | ⬜ | | | resolved findings: |

**Subtotal**: ___/9 passed | Avg duration: ___s

### ISSM Totals

| Metric | Value |
|--------|-------|
| Total | 43 |
| Passed | ___ |
| Failed | ___ |
| Blocked | ___ |
| Avg Response Time | ___s |
| Issues Found | |

**Checkpoint**: ⬜ ISSM complete — Eagle Eye fully provisioned through Monitor phase

---

## Phase 4: ISSO Persona (US2) — 24 Test Cases

**Role**: `Compliance.Analyst` | **Interface**: VS Code `@ato`
**Activated At**: _______________ | **Completed At**: _______________

### Phase 3 — Implement / SSP Authoring (ISSO-01 to ISSO-12)

| TC-ID | Task | Status | Duration | Actual Output | Notes |
|-------|------|--------|----------|---------------|-------|
| ISSO-01 | Auto-populate inherited narratives | ⬜ | | | populated: ___ skipped: ___ |
| ISSO-02 | Check narrative progress | ⬜ | | | overall %: |
| ISSO-03 | Get AI narrative suggestion for AC-2 | ⬜ | | | confidence: |
| ISSO-04 | Write narrative for AC-2 (Implemented) | ⬜ | | | |
| ISSO-05 | Update AC-3 to PartiallyImplemented | ⬜ | | | |
| ISSO-06 | Filter progress by AC family | ⬜ | | | |
| ISSO-07 | Generate full SSP | ⬜ | | | warnings: |
| ISSO-08 | Generate SSP system info section only | ⬜ | | | |
| ISSO-09 | Import CKL checklist | ⬜ | | | created/updated/skipped: |
| ISSO-10 | Import XCCDF results | ⬜ | | | benchmark: |
| ISSO-11 | View import history | ⬜ | | | import count: |
| ISSO-12 | View import details | ⬜ | | | finding count: |

**Subtotal**: ___/12 passed | Avg duration: ___s

### Phase 6 — Monitor / Day-to-Day (ISSO-13 to ISSO-24)

| TC-ID | Task | Status | Duration | Actual Output | Notes |
|-------|------|--------|----------|---------------|-------|
| ISSO-13 | Enable daily monitoring | ⬜ | | | next scan: |
| ISSO-14 | View monitoring status | ⬜ | | | status: |
| ISSO-15 | Show unacknowledged alerts | ⬜ | | | alert count: |
| ISSO-16 | Get alert details (ALT-{id}) | ⬜ | | | severity: |
| ISSO-17 | Acknowledge alert | ⬜ | | | |
| ISSO-18 | Fix alert | ⬜ | | | validation result: |
| ISSO-19 | Collect evidence for AC-2 | ⬜ | | | SHA-256: |
| ISSO-20 | Generate Feb 2026 ConMon report | ⬜ | | | compliance score: |
| ISSO-21 | Report significant change (API gateway) | ⬜ | | | requires_reauth: |
| ISSO-22 | Assign remediation task to SSgt Rodriguez | ⬜ | | | |
| ISSO-23 | View alert history (30 days) | ⬜ | | | |
| ISSO-24 | View compliance trend | ⬜ | | | |

**Subtotal**: ___/12 passed | Avg duration: ___s

### ISSO Totals

| Metric | Value |
|--------|-------|
| Total | 24 |
| Passed | ___ |
| Failed | ___ |
| Blocked | ___ |
| Avg Response Time | ___s |
| Issues Found | |

**Checkpoint**: ⬜ ISSO complete — SSP authored, scans imported, monitoring active

---

## Phase 5: SCA Persona (US3) — 24 Test Cases

**Role**: `Compliance.Auditor` | **Interface**: Microsoft Teams
**Activated At**: _______________ | **Completed At**: _______________

### Phase 4 — Assess (SCA-01 to SCA-20)

| TC-ID | Task | Status | Duration | Actual Output | Notes |
|-------|------|--------|----------|---------------|-------|
| SCA-01 | Take pre-assessment snapshot | ⬜ | | | snapshot_id: |
| SCA-02 | View system baseline | ⬜ | | | |
| SCA-03 | View system categorization | ⬜ | | | |
| SCA-04 | Check AC family evidence completeness | ⬜ | | | coverage %: |
| SCA-05 | Verify evidence integrity (SHA-256) | ⬜ | | | integrity: |
| SCA-06 | Assess AC-2 — Satisfied (Examine) | ⬜ | | | |
| SCA-07 | Assess SI-4 — OtherThanSatisfied CAT II | ⬜ | | | |
| SCA-08 | Assess CP-2 — Satisfied (Interview) | ⬜ | | | |
| SCA-09 | Assess AC-7 — Satisfied (Test) | ⬜ | | | |
| SCA-10 | View Prisma policies with NIST mappings | ⬜ | | | |
| SCA-11 | Review Prisma trend for assessment | ⬜ | | | |
| SCA-12 | Compare snapshots (pre vs current) | ⬜ | | | deltas: |
| SCA-13 | Take post-assessment snapshot | ⬜ | | | snapshot_id: |
| SCA-14 | Get SAP (finalized) | ⬜ | | | status: |
| SCA-15 | List all SAPs | ⬜ | | | SAP count: |
| SCA-16 | Check SAP-SAR alignment | ⬜ | | | coverage %: |
| SCA-17 | Generate SAR | ⬜ | | | |
| SCA-18 | Generate RAR | ⬜ | | | |
| SCA-19 | View Prisma import summary | ⬜ | | | |
| SCA-20 | Run NIST 800-53 assessment | ⬜ | | | compliance score: |

**Subtotal**: ___/20 passed | Avg duration: ___s

### SCA Separation-of-Duties — RBAC Denied (SCA-21 to SCA-24)

| TC-ID | Task | Expected | Status | Actual HTTP Code | Notes |
|-------|------|----------|--------|-----------------|-------|
| SCA-21 | Write narrative (DENIED) | 403 | ⬜ | | |
| SCA-22 | Remediate finding (DENIED) | 403 | ⬜ | | |
| SCA-23 | Issue authorization (DENIED) | 403 | ⬜ | | |
| SCA-24 | Dismiss alert (DENIED) | 403 | ⬜ | | |

**RBAC Verification**: ___/4 returned 403

### SCA Totals

| Metric | Value |
|--------|-------|
| Total | 24 |
| Passed | ___ |
| Failed | ___ |
| Blocked | ___ |
| RBAC Denied (403) | ___/4 |
| Avg Response Time | ___s |
| Issues Found | |

**Checkpoint**: ⬜ SCA complete — assessment artifacts generated, RBAC enforced

---

## Phase 6: AO Persona (US4) — 14 Test Cases

**Role**: `Compliance.AuthorizingOfficial` | **Interface**: Microsoft Teams (Adaptive Cards)
**Activated At**: _______________ | **Completed At**: _______________

### Phase 5 — Authorize (AO-01 to AO-11)

| TC-ID | Task | Status | Duration | Actual Output | Notes |
|-------|------|--------|----------|---------------|-------|
| AO-01 | View portfolio dashboard | ⬜ | | | system count: |
| AO-02 | Review authorization package | ⬜ | | | completeness: |
| AO-03 | View risk register | ⬜ | | | entry count: |
| AO-04 | Issue ATO (Low risk, Jan 2028) | ⬜ | | | auth type: |
| AO-05 | Issue ATOwC (SI-4 remediation 60d) | ⬜ | | | conditions: |
| AO-06 | Issue IATT (90d, dev only) | ⬜ | | | scope: |
| AO-07 | Deny authorization (DATO) | ⬜ | | | rationale: |
| AO-08 | Accept risk on finding (compensating control) | ⬜ | | | |
| AO-09 | Check ATOs expiring in 90 days | ⬜ | | | expiring count: |
| AO-10 | View compliance trend | ⬜ | | | |
| AO-11 | View critical alerts across portfolio | ⬜ | | | alert count: |

**Subtotal**: ___/11 passed | Avg duration: ___s

### AO Separation-of-Duties — RBAC Denied (AO-12 to AO-14)

| TC-ID | Task | Expected | Status | Actual HTTP Code | Notes |
|-------|------|----------|--------|-----------------|-------|
| AO-12 | Modify SSP (DENIED) | 403 | ⬜ | | |
| AO-13 | Fix findings (DENIED) | 403 | ⬜ | | |
| AO-14 | Assess controls (DENIED) | 403 | ⬜ | | |

**RBAC Verification**: ___/3 returned 403

### AO Totals

| Metric | Value |
|--------|-------|
| Total | 14 |
| Passed | ___ |
| Failed | ___ |
| Blocked | ___ |
| RBAC Denied (403) | ___/3 |
| Avg Response Time | ___s |
| Issues Found | |

**Checkpoint**: ⬜ AO complete — authorization decision issued, RBAC enforced

---

## Phase 7: Engineer Persona (US5) — 26 Test Cases

**Role**: `Compliance.PlatformEngineer` | **Interface**: VS Code `@ato`
**Activated At**: _______________ | **Completed At**: _______________

### Phase 3 — Implement / Build & Configure (ENG-01 to ENG-10)

| TC-ID | Task | Status | Duration | Actual Output | Notes |
|-------|------|--------|----------|---------------|-------|
| ENG-01 | Learn about AC-2 for Azure | ⬜ | | | |
| ENG-02 | View STIG mappings (Win Server 2022) | ⬜ | | | rule count: |
| ENG-03 | Scan Bicep file (in-editor diagnostics) | ⬜ | | | CAT I errors: |
| ENG-04 | Suggest narrative for SC-7 | ⬜ | | | confidence: |
| ENG-05 | Write SC-7 narrative (Implemented) | ⬜ | | | |
| ENG-06 | Generate remediation plan | ⬜ | | | finding count: |
| ENG-07 | Remediate with dry run | ⬜ | | | changes previewed: |
| ENG-08 | Apply remediation | ⬜ | | | resources changed: |
| ENG-09 | Validate remediation | ⬜ | | | result: Pass/Fail |
| ENG-10 | Check SC family narrative progress | ⬜ | | | SC completion %: |

**Subtotal**: ___/10 passed | Avg duration: ___s

### Kanban Task Workflow (ENG-11 to ENG-19)

| TC-ID | Task | Status | Duration | Actual Output | Notes |
|-------|------|--------|----------|---------------|-------|
| ENG-11 | View assigned tasks | ⬜ | | | task count: |
| ENG-12 | Get task details (REM-{id}) | ⬜ | | | |
| ENG-13 | Move task to In Progress | ⬜ | | | |
| ENG-14 | Fix task with dry run | ⬜ | | | |
| ENG-15 | Apply Kanban remediation | ⬜ | | | |
| ENG-16 | Validate task | ⬜ | | | result: |
| ENG-17 | Collect evidence for task | ⬜ | | | SHA-256: |
| ENG-18 | Add comment on task | ⬜ | | | |
| ENG-19 | Move task to In Review | ⬜ | | | |

**Subtotal**: ___/9 passed | Avg duration: ___s

### Prisma Remediation Workflow (ENG-20 to ENG-22)

| TC-ID | Task | Status | Duration | Actual Output | Notes |
|-------|------|--------|----------|---------------|-------|
| ENG-20 | View Prisma findings with remediation | ⬜ | | | auto-remediable: |
| ENG-21 | View CLI scripts for Prisma findings | ⬜ | | | scripts available: |
| ENG-22 | Prisma trend by resource type | ⬜ | | | |

**Subtotal**: ___/3 passed | Avg duration: ___s

### Engineer Separation-of-Duties — RBAC Denied (ENG-23 to ENG-26)

| TC-ID | Task | Expected | Status | Actual HTTP Code | Notes |
|-------|------|----------|--------|-----------------|-------|
| ENG-23 | Assess control (DENIED) | 403 | ⬜ | | |
| ENG-24 | Issue authorization (DENIED) | 403 | ⬜ | | |
| ENG-25 | Dismiss alert (DENIED) | 403 | ⬜ | | |
| ENG-26 | Register system (DENIED) | 403 | ⬜ | | |

**RBAC Verification**: ___/4 returned 403

### Engineer Totals

| Metric | Value |
|--------|-------|
| Total | 26 |
| Passed | ___ |
| Failed | ___ |
| Blocked | ___ |
| RBAC Denied (403) | ___/4 |
| Avg Response Time | ___s |
| Issues Found | |

**Checkpoint**: ⬜ Engineer complete — all 5 persona sections finished (131 test cases)

---

## Phase 8: Error Handling (ERR-01 to ERR-08)

| TC-ID | Persona | Task | Status | Duration | Actual Output | Expected Behavior Verified |
|-------|---------|------|--------|----------|---------------|---------------------------|
| ERR-01 | ISSM | Advance RMF out of order | ⬜ | | | Error: cannot skip phases ⬜ |
| ERR-02 | ISSM | Import malformed Prisma CSV | ⬜ | | | Error: CSV parsing failed ⬜ |
| ERR-03 | ISSM | Re-categorize (upsert behavior) | ⬜ | | | Upsert or phase error ⬜ |
| ERR-04 | SCA | Generate SAR with zero assessments | ⬜ | | | Warning/error: no assessments ⬜ |
| ERR-05 | ISSM | Bundle incomplete package | ⬜ | | | Warnings array with gaps ⬜ |
| ERR-06 | ISSM | Finalize already-finalized SAP | ⬜ | | | Error: already finalized ⬜ |
| ERR-07 | ISSM | Update finalized SAP | ⬜ | | | Error: immutable ⬜ |
| ERR-08 | Engineer | Remediate non-existent finding | ⬜ | | | Error: finding not found ⬜ |

**Subtotal**: ___/8 passed

---

## Phase 8: Auth/PIM (AUTH-01 to AUTH-08)

| TC-ID | Persona | Task | Status | Duration | Actual Output | Notes |
|-------|---------|------|--------|----------|---------------|-------|
| AUTH-01 | Any | Check CAC session | ⬜ | | | |
| AUTH-02 | Any | List eligible PIM roles | ⬜ | | | role count: |
| AUTH-03 | Any | Activate SecurityLead role (4h) | ⬜ | | | expiration: |
| AUTH-04 | Any | List active roles | ⬜ | | | |
| AUTH-05 | Any | Request JIT access (2h) | ⬜ | | | |
| AUTH-06 | ISSM | Approve PIM request | ⬜ | | | |
| AUTH-07 | ISSM | Deny PIM request | ⬜ | | | |
| AUTH-08 | Any | Deactivate role | ⬜ | | | |

**Subtotal**: ___/8 passed

---

## Phase 8: Cross-Persona Scenarios

### Scenario 1 — Full RMF Lifecycle: Prepare Through ATO (17 steps)

| Step | Persona | Action | Status | Duration | Notes |
|------|---------|--------|--------|----------|-------|
| 1 | ISSM | Register system | ⬜ | | |
| 2 | ISSM | Define boundary + assign roles | ⬜ | | |
| 3 | ISSM | Categorize Mod/Mod/Low | ⬜ | | |
| 4 | ISSM | Select baseline + set inheritance | ⬜ | | |
| 5 | ISSM | Advance to Implement | ⬜ | | |
| 6 | ISSO | Author SSP (auto-populate + write) | ⬜ | | |
| 7 | ISSO | Import CKL + Prisma CSV | ⬜ | | |
| 8 | Engineer | Fix findings + validate | ⬜ | | |
| 9 | ISSM | Advance to Assess | ⬜ | | |
| 10 | SCA | Assess controls (Satisfied + OTS) | ⬜ | | |
| 11 | SCA | Generate SAR | ⬜ | | |
| 12 | ISSM | Create POA&M + RAR | ⬜ | | |
| 13 | ISSM | Bundle authorization package | ⬜ | | |
| 14 | ISSM | Advance to Authorize | ⬜ | | |
| 15 | AO | Review + issue ATO | ⬜ | | |
| 16 | ISSM | Set up ConMon plan | ⬜ | | |
| 17 | ISSO | Enable monitoring | ⬜ | | |

**Scenario 1 Result**: ⬜ PASS / ⬜ FAIL | Persona transitions verified: ___/5

### Scenario 2 — Prisma Cloud Import → Assessment → Remediation (13 steps)

| Step | Persona | Action | Status | Duration | Notes |
|------|---------|--------|--------|----------|-------|
| 1 | ISSM | Import Prisma CSV | ⬜ | | |
| 2 | ISSM | Import Prisma API JSON | ⬜ | | |
| 3 | ISSM | Review policies | ⬜ | | |
| 4 | SCA | Review Prisma trend | ⬜ | | |
| 5 | SCA | Assess cloud controls (OTS CAT II) | ⬜ | | |
| 6 | SCA | Generate SAR | ⬜ | | |
| 7 | ISSM | Create Kanban board | ⬜ | | |
| 8 | ISSO | Assign to engineer | ⬜ | | |
| 9 | Engineer | View CLI scripts | ⬜ | | |
| 10 | Engineer | Apply fix | ⬜ | | |
| 11 | Engineer | Validate fix | ⬜ | | |
| 12 | ISSM | Re-import Prisma CSV | ⬜ | | |
| 13 | ISSM | Review trend improvement | ⬜ | | |

**Scenario 2 Result**: ⬜ PASS / ⬜ FAIL | Prisma data flow verified: ⬜

### Scenario 3 — Continuous Monitoring Drift → Reauthorization (10 steps)

| Step | Persona | Action | Status | Duration | Notes |
|------|---------|--------|--------|----------|-------|
| 1 | ISSO | Show unacknowledged alerts | ⬜ | | |
| 2 | ISSO | Acknowledge CAT I alert | ⬜ | | |
| 3 | ISSO | Escalate to ISSM | ⬜ | | |
| 4 | ISSM | Report significant change | ⬜ | | |
| 5 | ISSM | Check reauthorization triggers | ⬜ | | |
| 6 | ISSM | Initiate reauthorization | ⬜ | | |
| 7 | SCA | Re-assess controls | ⬜ | | |
| 8 | SCA | Generate new SAR | ⬜ | | |
| 9 | ISSM | Re-bundle package | ⬜ | | |
| 10 | AO | Issue ATOwC (30-day condition) | ⬜ | | |

**Scenario 3 Result**: ⬜ PASS / ⬜ FAIL | Reauth workflow verified: ⬜

---

## Acceptance Criteria Verification

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Every positive test ≤10s response time | ⬜ | Avg: ___s, Max: ___s |
| 2 | Every RBAC denial returns 403 (not 404/500) | ⬜ | ___/19 = 403 |
| 3 | Cross-persona data flows correctly | ⬜ | ___/3 scenarios passed |
| 4 | PIM role gates tool access | ⬜ | Denied before activation: ⬜ Allowed after: ⬜ |
| 5 | NL inputs resolve to correct tools | ⬜ | ___/128 correct resolution |
| 6 | Prisma fields present (PrismaAlertId, CloudResourceType, RemediationCli) | ⬜ | Fields verified in: ___ |
| 7 | Idempotent operations consistent on re-run | ⬜ | batch_populate: ⬜ ConMon: ⬜ |

**Overall Result**: ⬜ ALL CRITERIA MET / ⬜ CRITERIA FAILED

---

## Issues Log

| # | TC-ID | Severity | Description | Root Cause | Resolution |
|---|-------|----------|-------------|------------|------------|
| | | | | | |

---

## Sign-Off

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Tester | | | |
| ISSM | | | |
| QA Lead | | | |
