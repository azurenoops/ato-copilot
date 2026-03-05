# Tasks: Persona End-to-End Test Cases

**Input**: Design documents from `/specs/020-persona-test-cases/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: Not applicable — Feature 020 is a manual test suite (no automated tests to write).

**Organization**: Tasks are grouped by persona (= user story). Each persona produces an independently executable test script section that can be validated on its own once the cumulative preconditions are met.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which persona/user story this task belongs to (US1=ISSM, US2=ISSO, US3=SCA, US4=AO, US5=Engineer)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Test Infrastructure)

**Purpose**: Create test execution framework, result tracking template, and environment validation

- [X] T001 Create test results tracking template in specs/020-persona-test-cases/results-template.md with columns: TC-ID, Status (Pass/Fail/Blocked/Skip), Duration, Actual Output, Notes, Tester, Date
- [X] T002 [P] Create environment checklist in specs/020-persona-test-cases/environment-checklist.md — verify MCP server running, all 118 tools registered, Azure Gov subscription accessible, PIM eligibility for all 5 roles, VS Code @ato extension installed, Teams bot configured
- [X] T003 [P] Create test data setup script in specs/020-persona-test-cases/test-data-setup.md — document how to prepare a clean environment (or reset Eagle Eye), list all test data constants, and provide sample Prisma CSV/CKL/XCCDF files needed for import test cases

---

## Phase 2: Foundational (Tool & Environment Validation)

**Purpose**: Verify all 88 referenced tools are reachable and RBAC roles can be activated. MUST complete before persona testing.

**⚠️ CRITICAL**: No persona tests can begin until this phase confirms tool availability and role activation.

- [X] T004 Validate all 88 spec-referenced MCP tools are registered by querying the MCP server /tools/list endpoint and cross-referencing against the tool list in specs/020-persona-test-cases/research.md
- [X] T005 [P] Validate PIM role activation for all 5 personas by activating and deactivating each role (Compliance.SecurityLead, Compliance.Analyst, Compliance.Auditor, Compliance.AuthorizingOfficial, Compliance.PlatformEngineer)
- [X] T006 [P] Validate test data files are available — confirm sample Prisma Cloud CSV, Prisma API JSON, CKL checklist, and XCCDF results files exist for import test cases (ISSM-19, ISSM-20, ISSO-09, ISSO-10)
- [X] T007 Document any blocked tools or missing prerequisites in specs/020-persona-test-cases/results-template.md and update spec.md preconditions if needed

**Checkpoint**: Environment validated — persona test execution can begin

---

## Phase 3: User Story 1 — ISSM Persona (Priority: P1) 🎯 MVP

**Goal**: Execute all 43 ISSM test cases (ISSM-01 through ISSM-43) covering the full RMF lifecycle from Prepare through Monitor, plus SAP generation

**Independent Test**: ISSM section creates the Eagle Eye system and advances it through all RMF phases. After this phase, the system should be registered, categorized, baselined, have SSP/SAP artifacts, and have ConMon configured. Verify by running `compliance_get_system` for Eagle Eye and confirming RMF step and artifacts.

### Implementation for User Story 1

- [X] T008 [US1] Execute ISSM Phase 0 — Prepare test cases (ISSM-01 through ISSM-06) in Teams: register Eagle Eye, define boundary, assign roles, advance to Categorize. Record results in specs/020-persona-test-cases/results-template.md
- [X] T009 [US1] Execute ISSM Phase 1 — Categorize test cases (ISSM-07 through ISSM-10) in Teams: suggest info types, categorize system, verify categorization, advance to Select. Record results in specs/020-persona-test-cases/results-template.md
- [X] T010 [US1] Execute ISSM Phase 2 — Select test cases (ISSM-11 through ISSM-16) in Teams: select Moderate baseline, tailor, set inheritance, generate CRM, advance to Implement. Record results in specs/020-persona-test-cases/results-template.md
- [X] T011 [US1] Execute ISSM Phase 3 — Implement oversight test cases (ISSM-17 through ISSM-22) in Teams: check SSP progress, generate SSP, import Prisma CSV + API, list policies, view trends. Record results in specs/020-persona-test-cases/results-template.md
- [X] T012 [US1] Execute ISSM SAP Generation test cases (ISSM-41 through ISSM-43) in Teams: generate SAP, update SAP, finalize SAP. Record results in specs/020-persona-test-cases/results-template.md
- [X] T013 [US1] Execute ISSM Phase 4 — Assess prep test cases (ISSM-23 through ISSM-28) in Teams: create POA&M, list POA&M, generate RAR, create Kanban board, bulk assign tasks, export Kanban. Record results in specs/020-persona-test-cases/results-template.md
- [X] T014 [US1] Execute ISSM Phase 5 — Authorize test cases (ISSM-29 through ISSM-31) in Teams: bundle authorization package, advance to Authorize, view risk register. Record results in specs/020-persona-test-cases/results-template.md
- [X] T015 [US1] Execute ISSM Phase 6 — Monitor test cases (ISSM-32 through ISSM-40) in Teams: create ConMon plan, generate report, track ATO expiration, report significant change, check reauth triggers, multi-system dashboard, export eMASS, view audit log, re-import Prisma. Record results in specs/020-persona-test-cases/results-template.md
- [X] T016 [US1] Compile ISSM results summary — pass/fail count, average response time, blocked tests, issues found. Update specs/020-persona-test-cases/results-template.md with ISSM section totals

**Checkpoint**: ISSM (43 tests) complete. Eagle Eye system is fully provisioned through Monitor phase. ISSO persona testing can begin.

---

## Phase 4: User Story 2 — ISSO Persona (Priority: P2)

**Goal**: Execute all 24 ISSO test cases (ISSO-01 through ISSO-24) covering SSP authoring and day-to-day monitoring operations

**Independent Test**: After this phase, Eagle Eye should have complete SSP narratives (100% progress), imported CKL/XCCDF scan results, active monitoring with alerts, and evidence collected. Verify by running `compliance_narrative_progress` and `watch_monitoring_status`.

### Implementation for User Story 2

- [X] T017 [US2] Activate Compliance.Analyst PIM role and switch to VS Code @ato interface
- [X] T018 [US2] Execute ISSO Implement test cases (ISSO-01 through ISSO-12) in VS Code @ato: auto-populate inherited narratives, check progress, suggest/write/update narratives, generate SSP, import CKL + XCCDF, view import history/details. Record results in specs/020-persona-test-cases/results-template.md
- [X] T019 [US2] Execute ISSO Monitor test cases (ISSO-13 through ISSO-24) in VS Code @ato: enable monitoring, view status, show/acknowledge/fix alerts, collect evidence, generate ConMon report, report significant change, assign remediation task, view alert history + compliance trend. Record results in specs/020-persona-test-cases/results-template.md
- [X] T020 [US2] Compile ISSO results summary — pass/fail count, average response time, blocked tests, issues found. Update specs/020-persona-test-cases/results-template.md with ISSO section totals

**Checkpoint**: ISSO (24 tests) complete. SSP authored, scans imported, monitoring active. SCA persona testing can begin.

---

## Phase 5: User Story 3 — SCA Persona (Priority: P3)

**Goal**: Execute all 24 SCA test cases (SCA-01 through SCA-24) covering independent assessment and separation-of-duties verification

**Independent Test**: After this phase, Eagle Eye should have assessment snapshots, control effectiveness records, SAR and RAR documents generated. Verify by checking that `compliance_generate_sar` returns a complete SAR. All 4 RBAC-denied tests must return 403.

### Implementation for User Story 3

- [X] T021 [US3] Activate Compliance.Auditor PIM role and switch to Teams interface
- [X] T022 [US3] Execute SCA Assess test cases (SCA-01 through SCA-20) in Teams: take snapshot, view baseline/categorization, check evidence, verify integrity, assess controls (Satisfied/OtherThanSatisfied with Examine/Interview/Test methods), review Prisma data, compare snapshots, get/list SAPs, check SAP-SAR alignment, generate SAR + RAR, view import summary, run assessment. Record results in specs/020-persona-test-cases/results-template.md
- [X] T023 [US3] Execute SCA Separation-of-Duties test cases (SCA-21 through SCA-24) in Teams: verify 403 Forbidden for write narrative, remediate finding, issue authorization, dismiss alert. Record results in specs/020-persona-test-cases/results-template.md
- [X] T024 [US3] Compile SCA results summary — pass/fail count, RBAC denial verification (4/4 must be 403), average response time, issues found. Update specs/020-persona-test-cases/results-template.md with SCA section totals

**Checkpoint**: SCA (24 tests) complete. Assessment artifacts generated, RBAC enforced. AO persona testing can begin.

---

## Phase 6: User Story 4 — AO Persona (Priority: P4)

**Goal**: Execute all 14 AO test cases (AO-01 through AO-14) covering authorization decision-making and separation-of-duties verification

**Independent Test**: After this phase, Eagle Eye should have an ATO decision (or ATOwC/IATT/DATO depending on test path), risk acceptances recorded, and expiration tracking active. Verify by running `compliance_track_ato_expiration`. All 3 RBAC-denied tests must return 403.

### Implementation for User Story 4

- [X] T025 [US4] Activate Compliance.AuthorizingOfficial PIM role and remain on Teams (Adaptive Cards)
- [X] T026 [US4] Execute AO Authorize test cases (AO-01 through AO-11) in Teams: view dashboard, review package, view risk register, issue ATO/ATOwC/IATT/DATO, accept risk, check expirations, view compliance trend + alerts. Record results in specs/020-persona-test-cases/results-template.md
- [X] T027 [US4] Execute AO Separation-of-Duties test cases (AO-12 through AO-14) in Teams: verify 403 Forbidden for modify SSP, fix findings, assess controls. Record results in specs/020-persona-test-cases/results-template.md
- [X] T028 [US4] Compile AO results summary — pass/fail count, RBAC denial verification (3/3 must be 403), average response time, issues found. Update specs/020-persona-test-cases/results-template.md with AO section totals

**Checkpoint**: AO (14 tests) complete. Authorization decision issued, RBAC enforced. Engineer persona testing can begin.

---

## Phase 7: User Story 5 — Engineer Persona (Priority: P5)

**Goal**: Execute all 26 Engineer test cases (ENG-01 through ENG-26) covering control implementation, Kanban task workflow, Prisma remediation, and separation-of-duties verification

**Independent Test**: After this phase, Eagle Eye should have remediation applied and validated, Kanban tasks moved through workflow, evidence collected, and Prisma findings addressed. Verify by checking `kanban_task_list` shows tasks in InReview/Done and `compliance_validate_remediation` returns Pass. All 4 RBAC-denied tests must return 403.

### Implementation for User Story 5

- [X] T029 [US5] Activate Compliance.PlatformEngineer PIM role (or use default CAC mapping) and switch to VS Code @ato interface
- [X] T030 [US5] Execute Engineer Implement test cases (ENG-01 through ENG-10) in VS Code @ato: learn about controls, view STIG mappings, scan IaC, suggest/write narrative, generate remediation plan, dry run + apply + validate remediation, check narrative progress. Record results in specs/020-persona-test-cases/results-template.md
- [X] T031 [US5] Execute Engineer Kanban test cases (ENG-11 through ENG-19) in VS Code @ato: view assigned tasks, get task details, move to InProgress, dry run + apply Kanban remediation, validate task, collect evidence, add comment, move to InReview. Record results in specs/020-persona-test-cases/results-template.md
- [X] T032 [US5] Execute Engineer Prisma Remediation test cases (ENG-20 through ENG-22) in VS Code @ato: view Prisma findings with remediation steps, view CLI scripts, view Prisma trend by resource type. Record results in specs/020-persona-test-cases/results-template.md
- [X] T033 [US5] Execute Engineer Separation-of-Duties test cases (ENG-23 through ENG-26) in VS Code @ato: verify 403 Forbidden for assess control, issue authorization, dismiss alert, register system. Record results in specs/020-persona-test-cases/results-template.md
- [X] T034 [US5] Compile Engineer results summary — pass/fail count, RBAC denial verification (4/4 must be 403), average response time, issues found. Update specs/020-persona-test-cases/results-template.md with Engineer section totals

**Checkpoint**: All 5 persona sections complete (131 individual test cases). Cross-persona and edge case testing can begin.

---

## Phase 8: Cross-Persona Scenarios & Edge Cases

**Purpose**: Execute the 3 cross-persona handoff scenarios, 8 error handling tests, and 8 Auth/PIM tests

- [X] T035 Execute Error Handling test cases (ERR-01 through ERR-08): advance RMF out of order, import malformed Prisma CSV, re-categorize, generate SAR with zero assessments, bundle incomplete package, finalize already-finalized SAP, update finalized SAP, remediate non-existent finding. Record results in specs/020-persona-test-cases/results-template.md
- [X] T036 Execute Auth/PIM test cases (AUTH-01 through AUTH-08): check CAC session, list eligible PIM roles, activate/list active/deactivate roles, request JIT access, approve/deny PIM request. Record results in specs/020-persona-test-cases/results-template.md
- [X] T037 Execute Cross-Persona Scenario 1 — Full RMF Lifecycle (17 steps): Walk through Prepare → ATO with persona switching at each handoff. Record persona transitions, data flow verification, and per-step results in specs/020-persona-test-cases/results-template.md
- [X] T038 Execute Cross-Persona Scenario 2 — Prisma Cloud Import → Assessment → Remediation (13 steps): Walk through Prisma import → SCA assessment → Engineer remediation → trend verification. Record results in specs/020-persona-test-cases/results-template.md
- [X] T039 Execute Cross-Persona Scenario 3 — Continuous Monitoring Drift → Reauthorization (10 steps): Walk through alert detection → ISSO escalation → ISSM significant change → SCA re-assessment → AO re-authorization. Record results in specs/020-persona-test-cases/results-template.md

**Checkpoint**: All test cases and scenarios executed. Final reporting can begin.

---

## Phase 9: Polish & Final Reporting

**Purpose**: Compile results, identify issues, and produce the final test execution report

- [X] T040 [P] Compile overall test execution report in specs/020-persona-test-cases/test-report.md — aggregate pass/fail/blocked counts per persona, overall pass rate, response time statistics, RBAC enforcement verification (19/19 denials = 403), list all failures with root cause
- [X] T041 [P] Update spec.md with any corrections discovered during execution — fix NL inputs that didn't resolve to correct tools, update expected outputs that differed from actual, note any test case ordering dependencies not originally documented
- [X] T042 Verify all 7 acceptance criteria from spec.md are met: (1) 10s response time, (2) 403 for RBAC denials, (3) cross-persona data flow, (4) PIM gating, (5) NL→tool resolution, (6) Prisma-specific fields, (7) idempotent operations
- [X] T043 Run quickstart.md acceptance criteria checklist in specs/020-persona-test-cases/quickstart.md — confirm all 7 items checked off
- [X] T044 Final commit of all test execution artifacts to branch 020-persona-test-cases

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup; validates environment before testing
- **ISSM (Phase 3)**: Depends on Foundational — creates Eagle Eye system; **BLOCKS** all other personas
- **ISSO (Phase 4)**: Depends on ISSM — needs system in Implement phase with baseline + inheritance
- **SCA (Phase 5)**: Depends on ISSO — needs SSP complete, scans imported, evidence collected
- **AO (Phase 6)**: Depends on SCA — needs SAR generated, assessment artifacts ready
- **Engineer (Phase 7)**: Depends on ISSO/ISSM — needs Kanban board with assigned tasks, findings to remediate
- **Cross-Persona (Phase 8)**: Depends on all personas — exercises handoffs between already-tested roles
- **Polish (Phase 9)**: Depends on all test execution phases

### User Story Dependencies

- **US1 (ISSM)**: Must complete first — creates the system, baseline, and all RMF phase artifacts
- **US2 (ISSO)**: Depends on US1 — requires system in Implement phase
- **US3 (SCA)**: Depends on US2 — requires SSP narratives and imported scans
- **US4 (AO)**: Depends on US3 — requires SAR and authorization package
- **US5 (Engineer)**: Depends on US1+US2 — requires Kanban board and findings (can potentially run in parallel with US3/US4 for non-dependent test cases)

### Cumulative Execution Constraint

⚠️ **MANDATORY**: Because test data uses a single cumulative "Eagle Eye" system, all persona phases MUST execute in strict sequential order: ISSM → ISSO → SCA → AO → Engineer. Parallelism is limited to:
- Phase 1 setup tasks (T001, T002, T003)
- Phase 2 foundational tasks (T005, T006)
- Phase 9 polish tasks (T040, T041)

### Parallel Opportunities

Within each persona phase, all test cases are sequential (each depends on prior artifacts). Cross-phase parallelism is limited by the cumulative system constraint. The only parallelizable work is:

```text
# Phase 1 — all setup tasks in parallel:
T001 (results template) | T002 (env checklist) | T003 (test data setup)

# Phase 2 — validation tasks in parallel:
T005 (PIM roles) | T006 (test data files)

# Phase 9 — reporting tasks in parallel:
T040 (test report) | T041 (spec corrections)
```

---

## Implementation Strategy

### MVP First (ISSM Only)

1. Complete Phase 1: Setup (T001–T003)
2. Complete Phase 2: Foundational (T004–T007)
3. Complete Phase 3: ISSM (T008–T016)
4. **STOP and VALIDATE**: Eagle Eye system should be fully provisioned through all RMF phases
5. This validates the test suite framework and 43/147 test cases

### Incremental Delivery

1. Setup + Foundational → Environment ready
2. ISSM (43 tests) → System exists, all phases traversed (MVP!)
3. ISSO (24 tests) → SSP authored, monitoring active
4. SCA (24 tests) → Assessment complete, RBAC verified
5. AO (14 tests) → Authorization issued, RBAC verified
6. Engineer (26 tests) → Remediation workflow validated, RBAC verified
7. Cross-Persona (3 scenarios + 16 edge cases) → End-to-end validation
8. Each phase adds confidence without breaking prior results

---

## Notes

- Feature 020 is **documentation-only** — no source code changes, no dotnet build/test required
- All tasks produce markdown artifacts in `docs/persona-test-cases/`
- The spec is already the primary test script — tasks are about executing and recording results
- [P] tasks = different files/concerns, no sequential dependency
- [US*] labels map to personas: US1=ISSM, US2=ISSO, US3=SCA, US4=AO, US5=Engineer
- Cumulative system constraint means strict sequential execution per persona
- Total: 44 tasks across 9 phases (3 setup, 4 foundational, 9 ISSM, 4 ISSO, 4 SCA, 4 AO, 6 Engineer, 5 cross-persona, 5 polish)
