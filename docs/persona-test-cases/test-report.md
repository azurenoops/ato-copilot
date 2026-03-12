# Persona Test Cases — Test Execution Report

**Feature**: 020 — Persona End-to-End Test Cases
**Date**: __________
**Tester**: __________
**Environment**: Azure Government / VS Code @ato + Microsoft Teams
**Branch**: `020-persona-test-cases`

---

## Executive Summary

| Metric | Value |
|--------|-------|
| **Total Test Cases** | 147 + 3 scenarios (40 steps) |
| **Overall Pass Rate** | ___/147 (___%) |
| **RBAC Denial Enforcement** | ___/19 returned 403 |
| **Cross-Persona Scenarios** | ___/3 passed |
| **Error Handling Tests** | ___/8 passed |
| **Auth/PIM Tests** | ___/8 passed |
| **Avg Response Time** | ____s |
| **Execution Duration** | ____ hours |

### Overall Verdict: ☐ PASS / ☐ FAIL

---

## Per-Persona Results

| Persona | Role | Interface | Total | Passed | Failed | Blocked | Skipped | Pass Rate | Avg Time |
|---------|------|-----------|-------|--------|--------|---------|---------|-----------|----------|
| ISSM | SecurityLead | Teams | 43 | ___/43 | ___/43 | ___/43 | ___/43 | ___% | ____s |
| ISSO | Analyst | VS Code | 24 | ___/24 | ___/24 | ___/24 | ___/24 | ___% | ____s |
| SCA | Auditor | Teams | 24 | ___/24 | ___/24 | ___/24 | ___/24 | ___% | ____s |
| AO | AuthorizingOfficial | Teams | 14 | ___/14 | ___/14 | ___/14 | ___/14 | ___% | ____s |
| Engineer | PlatformEngineer | VS Code | 26 | ___/26 | ___/26 | ___/26 | ___/26 | ___% | ____s |
| **Subtotal** | | | **131** | **___/131** | | | | **___% ** | |

### Edge Cases & Scenarios

| Section | Total | Passed | Failed | Blocked |
|---------|-------|--------|--------|---------|
| Error Handling (ERR-01–08) | 8 | ___/8 | ___/8 | ___/8 |
| Auth/PIM (AUTH-01–08) | 8 | ___/8 | ___/8 | ___/8 |
| Scenario 1: Full RMF Lifecycle | 17 steps | ___/17 | ___/17 | ___/17 |
| Scenario 2: Prisma Flow | 13 steps | ___/13 | ___/13 | ___/13 |
| Scenario 3: ConMon Drift | 10 steps | ___/10 | ___/10 | ___/10 |

---

## RBAC Enforcement Verification

> **Requirement**: All 19 RBAC denial tests must return **403 Forbidden** (not 404 or 500).

| TC-ID | Persona | Action Attempted | Required Role | Expected | Actual | Status |
|-------|---------|-----------------|---------------|----------|--------|--------|
| SCA-21 | SCA | Write narrative | Analyst | 403 | ___ | ☐ |
| SCA-22 | SCA | Remediate finding | PlatformEngineer | 403 | ___ | ☐ |
| SCA-23 | SCA | Issue authorization | AuthorizingOfficial | 403 | ___ | ☐ |
| SCA-24 | SCA | Dismiss alert | SecurityLead | 403 | ___ | ☐ |
| AO-12 | AO | Modify SSP | Analyst | 403 | ___ | ☐ |
| AO-13 | AO | Fix findings | PlatformEngineer | 403 | ___ | ☐ |
| AO-14 | AO | Assess controls | Auditor | 403 | ___ | ☐ |
| ENG-23 | Engineer | Assess control | Auditor | 403 | ___ | ☐ |
| ENG-24 | Engineer | Issue authorization | AuthorizingOfficial | 403 | ___ | ☐ |
| ENG-25 | Engineer | Dismiss alert | SecurityLead | 403 | ___ | ☐ |
| ENG-26 | Engineer | Register system | SecurityLead | 403 | ___ | ☐ |
| ISSO-* | ISSO | (if any denied) | — | 403 | ___ | ☐ |

**RBAC Verdict**: ___/19 returned 403 → ☐ PASS (19/19) / ☐ FAIL

> **Note**: ISSM and ISSO personas do not have RBAC denial tests in spec — they have the broadest permissions. The 11 tests above (4 SCA + 3 AO + 4 Engineer) cover the core RBAC enforcement. The additional 8 are from ISSO separation-of-duties if applicable.

---

## Acceptance Criteria Verification

> From spec.md — all 7 criteria must be met.

| # | Criterion | Evidence | Status |
|---|-----------|----------|--------|
| 1 | Every positive test case produces expected output within **10 seconds** | Avg response time: ____s; Max: ____s; Violations: ___ | ☐ PASS / ☐ FAIL |
| 2 | Every RBAC-denied test case returns **403 Forbidden** (not 404 or 500) | ___/19 returned 403 | ☐ PASS / ☐ FAIL |
| 3 | Cross-persona handoff scenarios complete **end-to-end** with correct data flow | Scenarios: ___/3 passed; Data flow breaks: ___ | ☐ PASS / ☐ FAIL |
| 4 | **PIM role activation** correctly gates tool access — denied before, allowed after | AUTH-03/AUTH-08 verified; Pre-activation denial confirmed: ☐ | ☐ PASS / ☐ FAIL |
| 5 | All NL inputs resolved by AI to **correct tool** with correct parameters | Tool resolution accuracy: ___/131 positive tests | ☐ PASS / ☐ FAIL |
| 6 | Prisma import tests verify findings include **Prisma-specific fields** (PrismaAlertId, CloudResourceType, RemediationCli) | Fields verified in: ISSM-19, ISSM-20, ENG-20, ENG-21 | ☐ PASS / ☐ FAIL |
| 7 | Nessus import tests verify **plugin-family → NIST control mapping** and POA&M auto-generation for Cat I/II/III findings | Mapping verified: ☐; POA&M entries created: ☐ | ☐ PASS / ☐ FAIL |
| 8 | Idempotent operations produce **consistent results** on re-run | batch_populate_narratives: ☐; ConMon plan: ☐ | ☐ PASS / ☐ FAIL |

**Acceptance Criteria Verdict**: ___/8 met → ☐ PASS / ☐ FAIL

---

## Response Time Analysis

| Percentile | Time |
|-----------|------|
| P50 (median) | ____s |
| P90 | ____s |
| P95 | ____s |
| P99 | ____s |
| Max | ____s |

### Slowest Test Cases

| TC-ID | Operation | Response Time | Notes |
|-------|-----------|--------------|-------|
| | | | |
| | | | |
| | | | |

---

## Failures & Root Cause Analysis

| # | TC-ID | Persona | Expected | Actual | Root Cause | Severity | Fix Required |
|---|-------|---------|----------|--------|------------|----------|-------------|
| 1 | | | | | | | |
| 2 | | | | | | | |
| 3 | | | | | | | |
| 4 | | | | | | | |
| 5 | | | | | | | |

---

## Blocked Tests

| # | TC-ID | Persona | Blocking Reason | Resolution |
|---|-------|---------|----------------|------------|
| 1 | | | | |
| 2 | | | | |

---

## Spec Corrections Discovered During Execution

> Document any corrections to spec.md discovered during testing (T041).

| # | TC-ID | Correction Type | Original | Corrected | Notes |
|---|-------|----------------|----------|-----------|-------|
| 1 | | NL Input / Tool / Output | | | |
| 2 | | NL Input / Tool / Output | | | |
| 3 | | NL Input / Tool / Output | | | |

---

## Test Artifacts

| Artifact | Location | Created |
|----------|----------|---------|
| Results Template | `specs/020-persona-test-cases/results-template.md` | ☐ |
| Environment Checklist | `specs/020-persona-test-cases/environment-checklist.md` | ☐ |
| Test Data Setup | `specs/020-persona-test-cases/test-data-setup.md` | ☐ |
| Tool Validation | `specs/020-persona-test-cases/tool-validation.md` | ☐ |
| ISSM Test Script | `specs/020-persona-test-cases/scripts/issm-test-script.md` | ☐ |
| ISSO Test Script | `specs/020-persona-test-cases/scripts/isso-test-script.md` | ☐ |
| SCA Test Script | `specs/020-persona-test-cases/scripts/sca-test-script.md` | ☐ |
| AO Test Script | `specs/020-persona-test-cases/scripts/ao-test-script.md` | ☐ |
| Engineer Test Script | `specs/020-persona-test-cases/scripts/engineer-test-script.md` | ☐ |
| Cross-Persona Script | `specs/020-persona-test-cases/scripts/cross-persona-test-script.md` | ☐ |
| This Report | `specs/020-persona-test-cases/test-report.md` | ☐ |

---

## Quickstart Checklist Verification (T043)

> Cross-reference with `specs/020-persona-test-cases/quickstart.md`

- [ ] Environment setup instructions followed and working
- [ ] Persona execution order correct (ISSM → ISSO → SCA → AO → Engineer)
- [ ] Results recording process documented
- [ ] All 7 acceptance criteria checkable
- [ ] Error handling scenarios executable
- [ ] Cross-persona scenarios executable
- [ ] Auth/PIM scenarios executable

---

## Sign-Off

| Role | Name | Signature | Date |
|------|------|-----------|------|
| Test Lead | | | |
| ISSM SME | | | |
| Dev Lead | | | |
| Product Owner | | | |

**Test Execution Report Status**: ☐ DRAFT / ☐ FINAL

---

*Generated from Feature 020 — Persona End-to-End Test Cases*
*Spec: specs/020-persona-test-cases/spec.md*
*Tasks: specs/020-persona-test-cases/tasks.md*
