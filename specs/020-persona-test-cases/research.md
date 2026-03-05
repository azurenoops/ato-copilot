# Research: Persona End-to-End Test Cases

**Feature**: 020 | **Date**: 2026-03-05 | **Status**: Complete

---

## Research Question 1: Do all spec tool names map to real MCP methods?

**Decision**: Yes — all 88 unique tool names in the spec map 1:1 to registered MCP methods in `ComplianceMcpTools.cs`.

**Rationale**: Cross-referenced every tool name in the spec's "Tool(s) Invoked" columns against the 130 `public async Task` methods in `ComplianceMcpTools.cs`. The spec uses `snake_case` convention (e.g., `compliance_register_system`) which maps to `PascalCaseAsync` method names (e.g., `RegisterSystemAsync`). Every spec reference resolves correctly.

**Alternatives considered**: None — this was a verification task, not a design choice.

---

## Research Question 2: What MCP tools are NOT covered by any test case?

**Decision**: 42 of 130 MCP methods (32%) have no dedicated test case. This is acceptable for the initial test suite because:
- 12 are admin/configuration tools (templates, quiet hours, escalation, notifications) that are infrastructure-level, not persona-workflow-level
- 8 are low-priority CRUD completeness gaps (edit comment, delete comment, list comments, task history, suppress alerts, list suppressions, etc.)
- 22 have medium-to-high significance and should be tracked for a future test suite expansion

**Rationale**: The spec intentionally focuses on persona workflows — the critical path through each role's daily/weekly tasks. Admin configuration tools are not persona-specific and would belong in a separate "Admin/Configuration" test suite.

**High-significance gaps to address in future iteration**:

| # | MCP Method | Why It Matters |
|---|-----------|---------------|
| 1 | `GetSystemAsync` | Basic "show system details" — fundamental query |
| 2 | `ListSystemsAsync` | "List all systems" — portfolio management |
| 3 | `GetComplianceStatusAsync` | Overall compliance status summary |
| 4 | `ExplainNistControlAsync` | Individual control explanations (vs. family-level) |
| 5 | `SearchNistControlsAsync` | Search/filter across all 800-53 controls |
| 6 | `ExportCklAsync` | CKL import tested but export is not |
| 7 | `ExportOscalAsync` | OSCAL export — key interoperability feature |
| 8 | `ScanIacComplianceAsync` | ENG-03 references in-editor diagnostics, not the MCP tool |
| 9 | `KanbanBoardShowAsync` | Viewing a board overview has no test |
| 10 | `WatchDisableMonitoringAsync` | Enable tested but disable is not |
| 11 | `WatchCreateTaskFromAlertAsync` | Common workflow: alert → Kanban task |
| 12 | `JitRevokeAccessAsync` | Security-critical JIT revocation |

**Alternatives considered**: Adding all 42 uncovered tools to the spec now. Rejected because it would inflate the test suite to 180+ cases, making the mandatory cumulative execution impractical for a single test session. Better to address in a follow-up "Admin & Configuration Test Cases" spec.

---

## Research Question 3: Are there non-tool references in the spec that need resolution?

**Decision**: Two test cases reference non-tool constructs:

| TC-ID | Current Reference | Resolution |
|-------|------------------|------------|
| SCA-16 | `SAP-SAR alignment query` | No dedicated MCP method exists. This is a composite query that uses `GetSapAsync` + `GenerateSarAsync` data to produce an alignment report. The test case should clarify this is a natural language query that the AI resolves by combining multiple tool outputs. **No code change needed** — this tests the AI's ability to synthesize cross-tool data. |
| ENG-03 | `IaC diagnostics (in-editor)` | The MCP method `ScanIacComplianceAsync` exists for explicit scans. ENG-03 describes the passive in-editor squiggly-underline experience, which is a VS Code extension feature, not a tool invocation. **No change needed** — this is a valid VS Code UX test case that complements the tool-level test. |

**Rationale**: Both references are intentional — they test AI synthesis and VS Code UX respectively, which are valid manual test scenarios beyond simple tool invocation.

**Alternatives considered**: Replacing with explicit tool names. Rejected because it would remove coverage of these important composite/UX scenarios.

---

## Research Question 4: Are the RBAC roles in the spec correct?

**Decision**: All 7 roles in `ComplianceRoles.cs` are correctly referenced:

| Persona | Spec Role | `ComplianceRoles` Constant | Match |
|---------|-----------|---------------------------|-------|
| ISSM | `Compliance.SecurityLead` | `SecurityLead` | ✅ |
| ISSO | `Compliance.Analyst` | `Analyst` | ✅ |
| SCA | `Compliance.Auditor` | `Auditor` | ✅ |
| AO | `Compliance.AuthorizingOfficial` | `AuthorizingOfficial` | ✅ |
| Engineer | `Compliance.PlatformEngineer` | `PlatformEngineer` | ✅ |

Two additional roles (`Administrator`, `Viewer`) exist in the codebase but are not tested because:
- `Administrator` is a super-user role not assigned to any RMF persona
- `Viewer` is a read-only role not used in any standard RMF workflow

**Rationale**: The 5 tested roles cover 100% of persona-based RMF workflows. Admin/Viewer roles would belong in the admin test suite.

---

## Research Question 5: Is the cumulative execution order valid?

**Decision**: Yes — the execution order (ISSM → ISSO → SCA → AO → Engineer) aligns with the RMF lifecycle (Prepare → Categorize → Select → Implement → Assess → Authorize → Monitor):

1. **ISSM** (43 tests): Prepare through Monitor — creates system, categorizes, selects baseline, manages package
2. **ISSO** (24 tests): Implement + Monitor — authors SSP, imports scans, monitors operations
3. **SCA** (24 tests): Assess — independent assessment, SAR generation
4. **AO** (14 tests): Authorize — reviews package, issues decision
5. **Engineer** (26 tests): Implement + Monitor — fixes findings, runs Kanban tasks

**Rationale**: Each persona depends on artifacts created by earlier personas. ISSO needs the system registered & baselined (ISSM); SCA needs SSP complete (ISSO); AO needs SAR (SCA); Engineer needs tasks assigned (via ISSO/ISSM Kanban setup).

**Risk**: If any test case in an earlier persona fails, all downstream persona tests may be blocked. Mitigation: the tester should note the failure and proceed as far as possible, documenting which downstream tests were skipped due to blocked preconditions.

---

## Research Question 6: SAP tool availability

**Decision**: All 5 SAP tools (Feature 018) are confirmed present in `ComplianceMcpTools.cs`:

| Tool | MCP Method | Spec Coverage |
|------|-----------|---------------|
| `compliance_generate_sap` | `GenerateSapAsync` | ISSM-41 |
| `compliance_update_sap` | `UpdateSapAsync` | ISSM-42, ERR-07 |
| `compliance_finalize_sap` | `FinalizeSapAsync` | ISSM-43, ERR-06 |
| `compliance_get_sap` | `GetSapAsync` | SCA-14 |
| `compliance_list_saps` | `ListSapsTool` | SCA-15 |

**Rationale**: Feature 018 was fully implemented. All 5 tools are registered and have dedicated test cases in the spec.

---

## Research Question 7: Prisma Cloud tool availability

**Decision**: All 4 Prisma Cloud tools (Feature 019) are confirmed present:

| Tool | MCP Method | Spec Coverage |
|------|-----------|---------------|
| `compliance_import_prisma_csv` | `ImportPrismaCsvAsync` | ISSM-19, ISSM-40, ERR-02 |
| `compliance_import_prisma_api` | `ImportPrismaApiAsync` | ISSM-20 |
| `compliance_list_prisma_policies` | `ListPrismaPoliciesAsync` | ISSM-21, SCA-10 |
| `compliance_prisma_trend` | `PrismaTrendAsync` | ISSM-22, SCA-11, ENG-22 |

**Rationale**: Feature 019 was committed as `9bb64c5`. All 4 tools are registered and have multiple test cases covering ISSM, SCA, and Engineer personas.
