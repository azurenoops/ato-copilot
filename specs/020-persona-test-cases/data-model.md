# Data Model: Persona End-to-End Test Cases

**Feature**: 020 | **Date**: 2026-03-05

---

## Overview

Feature 020 is a **documentation-only** feature. It introduces no new database entities, EF Core migrations, or domain models. However, the test scripts operate on a well-defined data model that flows across personas. This document captures the entity relationships and state transitions exercised by the 147 test cases.

---

## Test Execution Data Model

These entities describe how the manual test suite itself is structured.

### TestSuite

| Field | Type | Description |
|-------|------|-------------|
| Name | string | "Persona End-to-End Test Cases" |
| Version | string | Spec version (e.g., "1.0.0") |
| ExecutionOrder | ordered list | ISSM → ISSO → SCA → AO → Engineer |
| SystemConstants | TestDataConstants | Shared constants for all test cases |
| TotalTestCases | int | 147 |
| CrossPersonaScenarios | int | 3 |

### TestCase

| Field | Type | Description |
|-------|------|-------------|
| TcId | string | Unique ID: `{Persona}-{##}` or `ERR-{##}` or `AUTH-{##}` |
| Persona | enum | ISSM, ISSO, SCA, AO, Engineer, Any |
| RbacRole | string | `ComplianceRoles.*` constant required |
| RmfPhase | enum | Prepare, Categorize, Select, Implement, Assess, Authorize, Monitor |
| Task | string | What the persona is trying to accomplish |
| NaturalLanguageInput | string | Exact query the tester types |
| ToolsInvoked | string[] | MCP tool name(s) the AI should resolve to |
| ExpectedOutput | string | Key fields / behavior to verify |
| Preconditions | string | What must exist before this test runs |
| Category | enum | Positive, RbacDenied, ErrorHandling, Auth |

### TestDataConstants

| Constant | Value | Used By |
|----------|-------|---------|
| SYSTEM_NAME | "Eagle Eye" | All personas |
| SYSTEM_TYPE | "Major Application" | ISSM-01 |
| ENVIRONMENT | "Azure Government" | ISSM-01 |
| BASELINE | "Moderate" (325 controls) | ISSM-11+ |
| SUBSCRIPTION_ID | "sub-12345-abcde" | ISSO-13, ENG-06 |
| ENGINEER_NAME | "SSgt Rodriguez" | ISSM-27, ISSO-22 |
| ISSO_NAME | "Jane Smith" | ISSM-04 |
| SCA_NAME | "Bob Jones" | ISSM-04 |
| AO_NAME | "Col. Thompson" | AO-04+ |

---

## Domain Entities Exercised by Tests

These are pre-existing entities from Features 001–019 that the test cases exercise. No new entities are introduced.

### System (RMF Registration)

| Field | Type | Created By | Used By |
|-------|------|-----------|---------|
| SystemId | GUID | ISSM-01 | All subsequent tests |
| Name | string | ISSM-01 | All |
| Type | enum | ISSM-01 | — |
| Environment | enum | ISSM-01 | — |
| RmfStep | enum | ISSM-01, ISSM-06, ISSM-10, ISSM-16 | All |
| Boundary | Boundary | ISSM-02, ISSM-03 | SCA, Engineer |
| RoleAssignments | RmfRole[] | ISSM-04 | ISSO, SCA |

**State Transitions**:
```
Prepare → Categorize → Select → Implement → Assess → Authorize → Monitor
   (ISSM-06)  (ISSM-10)  (ISSM-16)   (SCA-phase)  (ISSM-30)  (post-ATO)
```

### Categorization (FIPS 199)

| Field | Type | Created By | Used By |
|-------|------|-----------|---------|
| Confidentiality | enum (Low/Mod/High) | ISSM-08 | SCA-03 |
| Integrity | enum | ISSM-08 | SCA-03 |
| Availability | enum | ISSM-08 | SCA-03 |
| OverallImpact | enum | computed | Baseline selection |
| InfoTypes | InfoType[] | ISSM-07, ISSM-08 | — |

### Baseline

| Field | Type | Created By | Used By |
|-------|------|-----------|---------|
| Level | enum (Low/Mod/High) | ISSM-11 | ISSO, SCA, Engineer |
| TotalControls | int | ISSM-11 (325) | All |
| Tailorings | Tailoring[] | ISSM-12 | SCA |
| Inheritances | Inheritance[] | ISSM-13 | ISSO-01, SCA |
| Crm | CRM | ISSM-14 | ISSM-29 |

### Narrative (SSP Control Implementation)

| Field | Type | Created By | Used By |
|-------|------|-----------|---------|
| ControlId | string | ISSO-04, ISSO-05, ENG-05 | SCA, SSP gen |
| Text | string | ISSO-04, ISSO-05 | SSP |
| Status | enum | ISSO-04, ISSO-05 | Progress tracking |
| Method | enum | ISSO-04 | — |

**Status Values**: `Implemented`, `PartiallyImplemented`, `Planned`, `NotApplicable`

### Finding (Compliance / Scan)

| Field | Type | Created By | Used By |
|-------|------|-----------|---------|
| FindingId | GUID | Import tools | SCA, Engineer, AO |
| Source | enum | CKL/XCCDF/Prisma/Assessment | All |
| Severity | enum (CAT I/II/III) | Import | Prioritization |
| ControlId | string | Import | Assessment |
| Status | enum | Import → Remediation | POA&M, SAR |
| PrismaAlertId | string? | Prisma import | ENG-20 |
| RemediationCli | string? | Prisma API import | ENG-21 |

### ControlEffectiveness (Assessment)

| Field | Type | Created By | Used By |
|-------|------|-----------|---------|
| ControlId | string | SCA-06 through SCA-09 | SAR, RAR |
| Determination | enum | SCA | SAR gen |
| Method | enum (Examine/Interview/Test) | SCA | SAR gen |
| CatSeverity | enum? | SCA-07 | POA&M |

**Determination Values**: `Satisfied`, `OtherThanSatisfied`

### Authorization Decision

| Field | Type | Created By | Used By |
|-------|------|-----------|---------|
| Type | enum | AO-04 through AO-07 | ConMon, expiration tracking |
| ExpirationDate | DateTime | AO-04 | ISSM-34 |
| ResidualRisk | enum | AO-04 | — |
| Conditions | string[]? | AO-05 | — |

**Type Values**: `ATO`, `ATOwC`, `IATT`, `DATO`

### SAP (Security Assessment Plan)

| Field | Type | Created By | Used By |
|-------|------|-----------|---------|
| SapId | GUID | ISSM-41 | SCA-14 |
| Status | enum (Draft/Finalized) | ISSM-41, ISSM-43 | SCA-15 |
| ContentHash | string? | ISSM-43 (SHA-256) | Immutability enforcement |
| ControlEntries | SapControl[] | ISSM-41 | SCA-16 |
| Schedule | Schedule? | ISSM-42 | SCA |
| Team | TeamMember[]? | ISSM-42 | SCA |

### POA&M

| Field | Type | Created By | Used By |
|-------|------|-----------|---------|
| FindingRef | GUID | ISSM-23 | Package bundle |
| ScheduledCompletion | DateTime | ISSM-23 | ConMon |
| Status | enum | ISSM-23 | Dashboard |

### ConMon Plan

| Field | Type | Created By | Used By |
|-------|------|-----------|---------|
| Frequency | enum | ISSM-32 | ISSO-20 |
| ReviewSchedule | Schedule | ISSM-32 | Monthly reports |

---

## Entity Relationship Diagram (Textual)

```
System (1) ──── (1) Categorization
   │
   ├──── (1) Baseline ──── (*) Tailoring
   │         │              (*) Inheritance
   │         └──── (1) CRM
   │
   ├──── (*) Narrative
   │
   ├──── (*) Finding ──── (*) ControlEffectiveness
   │         │
   │         ├──── (0..1) POA&M
   │         └──── (0..1) KanbanTask
   │
   ├──── (*) SAP
   │
   ├──── (*) Snapshot
   │
   ├──── (0..1) AuthorizationDecision
   │
   ├──── (0..1) ConMonPlan ──── (*) ConMonReport
   │
   ├──── (*) MonitoringConfig ──── (*) Alert
   │
   └──── (*) Import (CKL/XCCDF/Prisma)
```

---

## Validation Rules Exercised by Tests

| Rule | Enforced In | Test Cases |
|------|------------|------------|
| RMF step advancement is sequential (no skipping) | `AdvanceRmfStepAsync` | ERR-01 |
| Finalized SAP is immutable | `UpdateSapAsync`, `FinalizeSapAsync` | ERR-06, ERR-07 |
| SCA cannot write narratives | RBAC middleware | SCA-21 |
| SCA cannot remediate | RBAC middleware | SCA-22 |
| SCA cannot authorize | RBAC middleware | SCA-23 |
| AO cannot modify SSP | RBAC middleware | AO-12 |
| AO cannot remediate | RBAC middleware | AO-13 |
| AO cannot assess | RBAC middleware | AO-14 |
| Engineer cannot assess | RBAC middleware | ENG-23 |
| Engineer cannot authorize | RBAC middleware | ENG-24 |
| Engineer cannot dismiss alerts | RBAC middleware | ENG-25 |
| Engineer cannot register systems | RBAC middleware | ENG-26 |
| SAR requires assessments | `GenerateSarAsync` | ERR-04 |
| Prisma CSV requires valid columns | `ImportPrismaCsvAsync` | ERR-02 |
| Finding must exist for remediation | `RemediateAsync` | ERR-08 |
| Auth package flags missing artifacts | `BundleAuthorizationPackageAsync` | ERR-05 |
