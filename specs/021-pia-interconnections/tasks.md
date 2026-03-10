# Tasks: 021 — PIA Service + System Interconnections

**Input**: Design documents from `/specs/021-pia-interconnections/`
**Prerequisites**: plan.md, spec.md, data-model.md, contracts/mcp-tools.md, research.md

**Organization**: Tasks grouped by user story for independent implementation and testing. User stories map to spec capability phases:

| Story | Spec Phase | Capabilities | Priority |
|-------|-----------|--------------|----------|
| US1 | Phase 1: Privacy Threshold Analysis & PIA | Caps 1.1–1.5 | P1 (MVP) |
| US2 | Phase 2: System Interconnection Registry | Caps 2.1–2.4 | P2 |
| US3 | Phase 3: ISA/MOU Agreement Tracking | Caps 3.1–3.5 | P3 |
| US4 | Phase 4: Gate Enforcement & Integration | Caps 4.1–4.5 | P4 |

> **Phase mapping note**: Tasks use a story-oriented phase structure (7 phases: Setup, Foundational, US1–US4, Polish) rather than the architecture-oriented phases in plan.md (8 phases). This is intentional — story grouping enables parallel execution and independent validation. Cross-reference plan.md phases via the capability IDs above.

> **Terminology note**: Spec uses "Register Interconnection" (Cap 2.1); implementation uses `compliance_add_interconnection` / `AddInterconnectionAsync`. "Add" is idiomatic for the tool/service layer. Both refer to the same operation.

## Format: `[ID] [P?] [Story?] Description with file path`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[US#]**: Maps task to its user story (US1–US4)
- Setup & Foundational phases: no story label
- User Story phases: story label required

---

## Phase 1: Setup

**Purpose**: Feature branch and project structure verification

- [X] T001 Create feature branch `021-pia-interconnections` from `main`
- [X] T002 Verify existing project structure matches plan.md layout (Compliance folders exist under Core/Models, Core/Interfaces, Agents/Services, Agents/Tools)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Entity models, enums, DTOs, DbContext configuration, and service interfaces that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T003 Create PrivacyModels.cs with `PrivacyThresholdAnalysis`, `PrivacyImpactAssessment`, `PiaSection` entities, `PtaDetermination`/`PiaStatus`/`PiaReviewDecision` enums, and `PtaResult`/`PiaResult`/`PiaReviewResult`/`PrivacyComplianceResult` DTOs in src/Ato.Copilot.Core/Models/Compliance/PrivacyModels.cs
- [X] T004 [P] Create InterconnectionModels.cs with `SystemInterconnection`, `InterconnectionAgreement` entities, `InterconnectionType`/`DataFlowDirection`/`InterconnectionStatus`/`AgreementType`/`AgreementStatus` enums, and `InterconnectionResult`/`IsaGenerationResult`/`AgreementValidationResult`/`AgreementValidationItem` DTOs in src/Ato.Copilot.Core/Models/Compliance/InterconnectionModels.cs
- [X] T005 Add `PrivacyThresholdAnalysis`, `PrivacyImpactAssessment`, `SystemInterconnections` navigation properties and `HasNoExternalInterconnections` bool to `RegisteredSystem` in src/Ato.Copilot.Core/Models/Compliance/RmfModels.cs
- [X] T006 Add 4 DbSets (`PrivacyThresholdAnalyses`, `PrivacyImpactAssessments`, `SystemInterconnections`, `InterconnectionAgreements`) and configure EF Core relationships, unique indexes, and JSON column conversions per data-model.md in src/Ato.Copilot.Core/Data/Context/AtoCopilotContext.cs
- [X] T007 [P] Create `IPrivacyService` interface with `CreatePtaAsync`, `GeneratePiaAsync`, `ReviewPiaAsync`, `InvalidatePtaAsync`, `GetPrivacyComplianceAsync` methods in src/Ato.Copilot.Core/Interfaces/Compliance/IPrivacyService.cs
- [X] T008 [P] Create `IInterconnectionService` interface with `AddInterconnectionAsync`, `ListInterconnectionsAsync`, `UpdateInterconnectionAsync`, `GenerateIsaAsync`, `RegisterAgreementAsync`, `UpdateAgreementAsync`, `CertifyNoInterconnectionsAsync`, `ValidateAgreementsAsync` methods in src/Ato.Copilot.Core/Interfaces/Compliance/IInterconnectionService.cs
- [X] T009 Register `IPrivacyService` → `PrivacyService` and `IInterconnectionService` → `InterconnectionService` as scoped services in DI container in ServiceCollectionExtensions.cs
- [X] T010 Generate EF Core migration for new entities and verify `dotnet build Ato.Copilot.sln` compiles with zero warnings

**Checkpoint**: Solution builds. New DbSets registered. Interfaces defined. Migration generated.

---

## Phase 3: User Story 1 — Privacy Threshold Analysis & PIA (Priority: P1) 🎯 MVP

**Goal**: ISSO conducts PTA to determine PIA requirement; generates PIA with AI-drafted narratives; ISSM reviews/approves PIA. Privacy compliance dashboard shows status.

**Independent Test**: Run `compliance_create_pta` for a system with PII info types → returns `PiaRequired`. Run `compliance_generate_pia` → returns Draft PIA with 8 sections. Run `compliance_review_pia` with `approve` → PIA status = Approved, expiration set +1 year. Run `compliance_check_privacy_compliance` → `privacyGateSatisfied = true`.

**Capabilities**: [Cap 1.1] PTA Questionnaire, [Cap 1.2] PIA Questionnaire Generation, [Cap 1.3] PIA Document Generation, [Cap 1.4] PIA Lifecycle Management, [Cap 1.5] PIA Review & Approval

### Tests for User Story 1

- [X] T011 [P] [US1] Create PTA auto-detection unit tests (known-PII prefixes D.8.x/D.17.x/D.28.x, no-PII result, PendingConfirmation for ambiguous types, Exempt path) in tests/Ato.Copilot.Tests.Unit/Services/PrivacyServiceTests.cs
- [X] T012 [P] [US1] Create PTA manual mode unit tests (explicit PII flags, exemption rationale, record count threshold) in tests/Ato.Copilot.Tests.Unit/Services/PrivacyServiceTests.cs
- [X] T013 [P] [US1] Create PIA lifecycle unit tests (generate with pre-populated sections, review → approve with expiration, review → request revision with deficiencies, invalidate PTA resets PIA to UnderReview) in tests/Ato.Copilot.Tests.Unit/Services/PrivacyServiceTests.cs
- [X] T014 [P] [US1] Create privacy MCP tool invocation tests (compliance_create_pta, compliance_generate_pia, compliance_review_pia, compliance_check_privacy_compliance) with RBAC validation in tests/Ato.Copilot.Tests.Unit/Tools/PrivacyToolTests.cs

### Implementation for User Story 1

- [X] T015 [US1] Implement `CreatePtaAsync` auto-detection mode — retrieve SecurityCategorization + InformationTypes, cross-reference SP 800-60 IDs against known-PII prefixes (D.8.x, D.17.x, D.28.x), flag ambiguous types as PendingConfirmation, generate rationale in src/Ato.Copilot.Agents/Compliance/Services/PrivacyService.cs
- [X] T016 [US1] Implement `CreatePtaAsync` manual mode — accept explicit `collects_pii`, `maintains_pii`, `disseminates_pii`, `pii_categories`, `estimated_record_count`, `exemption_rationale` parameters; apply E-Gov Act ≥10 record threshold in src/Ato.Copilot.Agents/Compliance/Services/PrivacyService.cs
- [X] T017 [US1] Implement `GeneratePiaAsync` — validate PTA exists with PiaRequired determination, create 8 OMB M-03-22 sections (per research.md R2), pre-populate from system data (description, info types, safeguards), AI-draft narrative sections via IChatCompletionService with section-by-section prompts and template-only fallback in src/Ato.Copilot.Agents/Compliance/Services/PrivacyService.cs
- [X] T018 [US1] Implement `ReviewPiaAsync` — validate PIA in Draft/UnderReview status, handle approve (set Approved + ExpirationDate = now + 1 year) and request_revision (set Draft + record deficiencies), increment version on resubmission in src/Ato.Copilot.Agents/Compliance/Services/PrivacyService.cs
- [X] T019 [US1] Implement `InvalidatePtaAsync` — delete existing PTA, set any Approved PIA to UnderReview status (preserve document content), log state transition in src/Ato.Copilot.Agents/Compliance/Services/PrivacyService.cs
- [X] T020 [US1] Implement `GetPrivacyComplianceAsync` — aggregate PTA determination, PIA status, privacy gate satisfaction, interconnection counts, agreement coverage, upcoming expirations, overall status (Compliant/ActionRequired/NotStarted) in src/Ato.Copilot.Agents/Compliance/Services/PrivacyService.cs
- [X] T021 [US1] Create `compliance_create_pta` tool extending BaseTool with system_id, manual_mode, PII parameter mapping, RBAC (Analyst, SecurityLead), and validation per contracts/mcp-tools.md in src/Ato.Copilot.Agents/Compliance/Tools/PrivacyTools.cs
- [X] T022 [P] [US1] Create `compliance_generate_pia` tool extending BaseTool with system_id parameter, RBAC (Analyst, SecurityLead), PTA existence validation in src/Ato.Copilot.Agents/Compliance/Tools/PrivacyTools.cs
- [X] T023 [P] [US1] Create `compliance_review_pia` tool extending BaseTool with system_id, decision, comments, deficiencies parameters, RBAC (SecurityLead only) in src/Ato.Copilot.Agents/Compliance/Tools/PrivacyTools.cs
- [X] T024 [P] [US1] Create `compliance_check_privacy_compliance` tool extending BaseTool with system_id parameter, RBAC (Auditor, SecurityLead, AuthorizingOfficial) in src/Ato.Copilot.Agents/Compliance/Tools/PrivacyTools.cs
- [X] T025 [US1] Register 4 privacy tools via RegisterTool() in ComplianceAgent constructor and add IPrivacyService DI parameter in src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs

**Checkpoint**: PTA analysis works for all 4 determination paths. PIA generates with 8 pre-populated/AI-drafted sections. Review lifecycle complete (approve + revise). Privacy dashboard returns accurate compliance status. AT-01 through AT-05 passing.

---

## Phase 4: User Story 2 — System Interconnection Registry (Priority: P2)

**Goal**: Engineer/ISSO registers external system interconnections with full technical details. ISSM/Analyst queries and updates interconnection records. Suspend/terminate lifecycle with audit trail.

**Independent Test**: Run `compliance_add_interconnection` with target system, VPN type, bidirectional flow → returns Proposed interconnection. Run `compliance_list_interconnections` → shows 1 interconnection. Run `compliance_update_interconnection` with status=active → status updated. Run with status=terminated + reason → terminated, retained for audit.

**Capabilities**: [Cap 2.1] Register Interconnection, [Cap 2.2] List Interconnections, [Cap 2.3] Update Interconnection, [Cap 2.4] Suspend/Terminate Interconnection

### Tests for User Story 2

- [X] T026 [P] [US2] Create interconnection CRUD unit tests (add with all fields, add clears HasNoExternalInterconnections, list with status filter, list empty, update details, suspend with reason, terminate with reason, reject terminate without reason) in tests/Ato.Copilot.Tests.Unit/Services/InterconnectionServiceTests.cs
- [X] T027 [P] [US2] Create interconnection MCP tool invocation tests (compliance_add_interconnection, compliance_list_interconnections, compliance_update_interconnection) with RBAC validation and enum parsing in tests/Ato.Copilot.Tests.Unit/Tools/InterconnectionToolTests.cs

### Implementation for User Story 2

- [X] T028 [US2] Implement `AddInterconnectionAsync` — validate system exists, create SystemInterconnection entity with Proposed status, clear HasNoExternalInterconnections flag if set, log creation in src/Ato.Copilot.Agents/Compliance/Services/InterconnectionService.cs
- [X] T029 [US2] Implement `ListInterconnectionsAsync` — query SystemInterconnections with optional status filter, include Agreements navigation, paginate results, return with agreement summary per interconnection in src/Ato.Copilot.Agents/Compliance/Services/InterconnectionService.cs
- [X] T030 [US2] Implement `UpdateInterconnectionAsync` — validate interconnection exists, update fields, require StatusReason for Suspended/Terminated status changes, warn on DataClassification change ("ISA review recommended"), exclude Terminated from active counts in src/Ato.Copilot.Agents/Compliance/Services/InterconnectionService.cs
- [X] T031 [US2] Create `compliance_add_interconnection` tool extending BaseTool with system_id + target details + connection_type/data_flow_direction/data_classification enum parsing, RBAC (PlatformEngineer, Analyst) in src/Ato.Copilot.Agents/Compliance/Tools/InterconnectionTools.cs
- [X] T032 [P] [US2] Create `compliance_list_interconnections` tool extending BaseTool with system_id + optional status_filter, RBAC (all roles) in src/Ato.Copilot.Agents/Compliance/Tools/InterconnectionTools.cs
- [X] T033 [P] [US2] Create `compliance_update_interconnection` tool extending BaseTool with interconnection_id + optional update fields, RBAC (Analyst, SecurityLead) in src/Ato.Copilot.Agents/Compliance/Tools/InterconnectionTools.cs
- [X] T034 [US2] Register 3 interconnection tools via RegisterTool() in ComplianceAgent constructor and add IInterconnectionService DI parameter in src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs

**Checkpoint**: Full interconnection CRUD operational. Suspend/terminate preserves records for audit. Status filtering works. AT-06 passing.

---

## Phase 5: User Story 3 — ISA/MOU Agreement Tracking (Priority: P3)

**Goal**: ISSM generates AI-drafted ISA from interconnection data. Agreements are registered with signatory info and expiration tracking. Validation checks all active interconnections for signed, current agreements. ConMon detects approaching/expired ISAs.

**Independent Test**: Add an interconnection (US2), then run `compliance_generate_isa` → returns ISA document with NIST 800-47 structure. Run `compliance_register_agreement` with Signed status + expiration. Run `compliance_validate_agreements` → returns Compliant. Run `compliance_update_agreement` to change expiration to past date → returns Expired. Run `compliance_certify_no_interconnections` for a system with no interconnections → Gate 4 satisfied.

**Capabilities**: [Cap 3.1] Generate ISA Template, [Cap 3.2] Register Agreement, [Cap 3.3] Agreement Validation, [Cap 3.4] Agreement Expiration Monitoring, [Cap 3.5] Agreement Registration & Update

**Dependencies**: Requires US2 (interconnections must exist to create agreements)

### Tests for User Story 3

- [X] T035 [P] [US3] Create ISA generation unit tests (AI-drafted 7-section template, pre-population from interconnection + RmfRoleAssignments, template-only fallback when AI unavailable, reject terminated interconnection) in tests/Ato.Copilot.Tests.Unit/Services/InterconnectionServiceTests.cs
- [X] T036 [P] [US3] Create agreement validation unit tests (all signed → compliant, one expired → fail, missing agreement → fail, expiring within 90 days → warning, multiple agreements per interconnection with at least one valid → pass, HasNoExternalInterconnections → pass) in tests/Ato.Copilot.Tests.Unit/Services/InterconnectionServiceTests.cs
- [X] T037 [P] [US3] Create ISA/agreement MCP tool invocation tests (compliance_generate_isa, compliance_register_agreement, compliance_update_agreement, compliance_validate_agreements) with RBAC validation in tests/Ato.Copilot.Tests.Unit/Tools/InterconnectionToolTests.cs

### Implementation for User Story 3

- [X] T038 [US3] Implement `GenerateIsaAsync` — validate interconnection exists and not Terminated, retrieve system + interconnection + RmfRoleAssignment data, AI-draft ISA via IChatCompletionService using NIST 800-47 7-section structure (per research.md R3), create InterconnectionAgreement with Draft status in src/Ato.Copilot.Agents/Compliance/Services/InterconnectionService.cs
- [X] T039 [US3] Implement `RegisterAgreementAsync` — create InterconnectionAgreement with type, title, document reference, effective/expiration dates, signatory info, link to interconnection in src/Ato.Copilot.Agents/Compliance/Services/InterconnectionService.cs
- [X] T039a [US3] Implement `UpdateAgreementAsync` — update agreement status, metadata, signatories; validate status transitions; prevent updates to Terminated agreements (except review_notes) in src/Ato.Copilot.Agents/Compliance/Services/InterconnectionService.cs
- [X] T039b [US3] Implement `CertifyNoInterconnectionsAsync` — set/clear `HasNoExternalInterconnections` on RegisteredSystem; reject if Active interconnections exist when certifying in src/Ato.Copilot.Agents/Compliance/Services/InterconnectionService.cs
- [X] T040 [US3] Implement `ValidateAgreementsAsync` — query all Active interconnections, check each has ≥1 Signed + non-expired agreement, flag Missing/Expired/ExpiringSoon (≤90 days), support HasNoExternalInterconnections bypass, return AgreementValidationResult in src/Ato.Copilot.Agents/Compliance/Services/InterconnectionService.cs
- [X] T041 [US3] Create `compliance_generate_isa` tool extending BaseTool with interconnection_id parameter, RBAC (SecurityLead only) in src/Ato.Copilot.Agents/Compliance/Tools/InterconnectionTools.cs
- [X] T041a [P] [US3] Create `compliance_register_agreement` tool extending BaseTool with interconnection_id + agreement details parameters, RBAC (SecurityLead only) in src/Ato.Copilot.Agents/Compliance/Tools/InterconnectionTools.cs
- [X] T041b [P] [US3] Create `compliance_update_agreement` tool extending BaseTool with agreement_id + update fields parameters, RBAC (SecurityLead only) in src/Ato.Copilot.Agents/Compliance/Tools/InterconnectionTools.cs
- [X] T041c [P] [US3] Create `compliance_certify_no_interconnections` tool extending BaseTool with system_id + certify parameters, RBAC (Analyst, SecurityLead) in src/Ato.Copilot.Agents/Compliance/Tools/InterconnectionTools.cs
- [X] T042 [P] [US3] Create `compliance_validate_agreements` tool extending BaseTool with system_id parameter, RBAC (Auditor, SecurityLead) in src/Ato.Copilot.Agents/Compliance/Tools/InterconnectionTools.cs
- [X] T043 [US3] Register 5 agreement/certification tools via RegisterTool() in ComplianceAgent constructor in src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs

**Checkpoint**: ISA generation produces NIST 800-47 template with correct system details. Agreement registration and status updates work end-to-end. Agreement validation correctly identifies Compliant/Expired/Missing/ExpiringSoon. `HasNoExternalInterconnections` certification satisfies Gate 4. AT-07 through AT-09 passing.

---

## Phase 6: User Story 4 — Gate Enforcement & Integration (Priority: P4)

**Goal**: Enforce privacy readiness (Gate 3) and interconnection documentation (Gate 4) at Prepare→Categorize boundary. Generate SSP §10 from interconnection data. ConMon monitors ISA expirations. Authorization pre-check validates PIA + ISA. Categorization changes invalidate PTA.

**Independent Test**: Create system with PTA + approved PIA + signed ISA → advance Prepare→Categorize succeeds (all 4 gates pass). Remove PIA → Gate 3 blocks. Remove ISA → Gate 4 blocks. System already past Prepare → advisory warning only, no block.

**Capabilities**: [Cap 4.1] Privacy Readiness Gate, [Cap 4.2] Interconnection Documentation Gate, [Cap 4.3] SSP §10 Integration, [Cap 4.4] Authorization Pre-check, [Cap 4.5] Privacy Compliance Dashboard (implemented in US1/T020)

**Dependencies**: Requires US1 (privacy data for Gate 3), US2 + US3 (interconnection + agreement data for Gate 4)

### Tests for User Story 4

- [X] T044 [P] [US4] Create Gate 3 tests (PTA PiaNotRequired → pass, PTA PiaRequired + PIA Approved → pass, PTA PiaRequired + no PIA → fail, PTA PendingConfirmation → fail, no PTA → fail, system past Prepare → advisory only) in tests/Ato.Copilot.Tests.Unit/Gates/PrivacyGateTests.cs
- [X] T045 [P] [US4] Create Gate 4 tests (all interconnections with signed ISA → pass, missing agreement → fail, expired agreement → fail, HasNoExternalInterconnections → pass, no interconnections and not certified → fail, system past Prepare → advisory only) in tests/Ato.Copilot.Tests.Unit/Gates/PrivacyGateTests.cs
- [X] T046 [P] [US4] Create SSP §10 generation tests (active interconnections produce table with target system, connection type, data flow, classification, agreement status, security measures; terminated excluded; empty produces "no interconnections" note) in tests/Ato.Copilot.Tests.Unit/Integration/SspInterconnectionTests.cs
- [X] T047 [P] [US4] Create ConMon ISA and PIA expiration tests (≤90 days → advisory alert, expired ISA → SignificantChange record with ChangeType "New Interconnection", expired PIA → status set to Expired + SignificantChange, multiple agreements with only some expired → correct flagging) in tests/Ato.Copilot.Tests.Unit/Integration/ConMonIsaTests.cs

### Implementation for User Story 4

- [X] T048 [US4] Add Gate 3 (privacy readiness) — yield return GateCheckResult checking PTA exists + determination is not PendingConfirmation + PIA Approved if PiaRequired, in `CheckPrepareToCategorize` method in src/Ato.Copilot.Agents/Compliance/Services/RmfLifecycleService.cs
- [X] T049 [US4] Add Gate 4 (interconnection documentation) — yield return GateCheckResult checking all Active interconnections have ≥1 Signed+current agreement OR HasNoExternalInterconnections is true, in `CheckPrepareToCategorize` method in src/Ato.Copilot.Agents/Compliance/Services/RmfLifecycleService.cs
- [X] T050 [US4] Update `Include()` chain in RmfLifecycleService to eager-load `PrivacyThresholdAnalysis`, `PrivacyImpactAssessment`, `SystemInterconnections` with `ThenInclude(Agreements)` for gate evaluation in src/Ato.Copilot.Agents/Compliance/Services/RmfLifecycleService.cs
- [X] T051 [US4] Add `GenerateInterconnectionSection` method for SSP §10 — produce markdown table of all active interconnections with target system, connection type, data flow direction, data classification, agreement status, and security measures; add "interconnections" to section list in src/Ato.Copilot.Agents/Compliance/Services/SspService.cs
- [X] T052 [US4] Add ISA and PIA expiration check to ConMon monitoring cycle — query InterconnectionAgreements approaching expiration (≤90 days → Info/Warning/Urgent alerts, expired → create SignificantChange with ChangeType "New Interconnection" via ReportChangeAsync); query PIAs with ExpirationDate in the past → set status to Expired + create SignificantChange, re-opening privacy gate in src/Ato.Copilot.Agents/Compliance/Services/ConMonService.cs
- [X] T053 [US4] Add PIA + ISA pre-checks to authorization readiness — validate PIA Approved (if PTA = PiaRequired) and all active interconnections have signed agreements in src/Ato.Copilot.Agents/Compliance/Services/AuthorizationService.cs
- [X] T054 [US4] Add PTA invalidation trigger — call `IPrivacyService.InvalidatePtaAsync` when information types are updated in `CategorizeSystemAsync` method in src/Ato.Copilot.Agents/Compliance/Services/CategorizationService.cs

**Checkpoint**: Systems cannot advance Prepare→Categorize without satisfying Gates 3 + 4. SSP §10 generated from interconnection data. ConMon detects ISA expirations. Authorization pre-check validates privacy + interconnection compliance. AT-10 through AT-20 passing.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: End-to-end integration tests, documentation, and final validation

- [X] T055 [P] Create end-to-end integration tests covering full PTA → PIA → gate → SSP flow and interconnection → ISA → agreement → gate flow in tests/Ato.Copilot.Tests.Integration/Tools/PrivacyIntegrationTests.cs
- [X] T056 [P] Update API documentation for 12 new MCP tools (parameters, returns, RBAC) in docs/api/mcp-server.md
- [X] T057 [P] Update architecture documentation with privacy + interconnection data model and service layer in docs/architecture/data-model.md and docs/architecture/overview.md
- [X] T058 Run quickstart.md validation scenarios (6 scenarios: PTA → PIA → ISA → gates → SSP → ConMon)
- [X] T059 Verify all 20 acceptance tests from spec.md Part 10 are passing
- [X] T060 Run full test suite (`dotnet test`) and confirm zero regressions
- [X] T061 [P] Verify structured logging on all new service methods and tool invocations per Constitution Principle V — confirm input parameters, execution duration, and success/failure are logged with ILogger<T> on PrivacyService, InterconnectionService, and all 12 MCP tools
- [X] T062 [P] Add response-time assertions for PTA (< 5s), PIA generation (< 30s), gate evaluation (< 2s), and agreement validation (< 5s) per plan.md performance goals and Constitution Principle VIII in tests/Ato.Copilot.Tests.Integration/Tools/PrivacyIntegrationTests.cs

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup)
  └──▶ Phase 2 (Foundational) — BLOCKS all user stories
         ├──▶ Phase 3 (US1: Privacy)         — independent
         ├──▶ Phase 4 (US2: Interconnections) — independent
         │       └──▶ Phase 5 (US3: Agreements) — depends on US2
         │               └──▶ Phase 6 (US4: Gates & Integration) — depends on US1 + US2 + US3
         └──────────────────▶ Phase 7 (Polish) — after all desired stories complete
```

### User Story Dependencies

- **US1 (Privacy)**: Can start after Foundational (Phase 2). No dependencies on other stories.
- **US2 (Interconnections)**: Can start after Foundational (Phase 2). No dependencies on other stories. **Can run in parallel with US1.**
- **US3 (Agreements)**: Depends on US2 — agreements are linked to interconnections via FK.
- **US4 (Gates & Integration)**: Depends on US1 + US2 + US3 — gates check privacy and interconnection data across all stories.

### Within Each User Story

1. Test files created first (marked [P] for parallel creation)
2. Service implementation (sequential — methods build on each other)
3. Tool creation ([P] where tools are independent)
4. Tool registration in ComplianceMcpTools.cs (sequential — modifies shared file)

### Parallel Opportunities

Within Phase 2 (Foundational):
- T003 + T004 (PrivacyModels.cs + InterconnectionModels.cs) — different files
- T007 + T008 (IPrivacyService + IInterconnectionService) — different files

Within Phase 3 (US1):
- T011 + T012 + T013 + T014 — all test files can be created in parallel
- T022 + T023 + T024 — independent tool files (after T021)

Within Phase 4 (US2):
- T026 + T027 — test files in parallel
- T032 + T033 — independent tools (after T031)

Within Phase 5 (US3):
- T035 + T036 + T037 — test files in parallel
- T041a + T041b + T041c + T042 — independent tools (after T041)

Cross-story:
- **US1 (Phase 3) and US2 (Phase 4) can run in full parallel** after Phase 2 completes

---

## Parallel Examples

### US1 + US2 in Parallel (Two Workers)

```
Worker A (US1 — Privacy):          Worker B (US2 — Interconnections):
T011–T014 (tests)                  T026–T027 (tests)
T015–T020 (PrivacyService)         T028–T030 (InterconnectionService)
T021–T024 (PrivacyTools)           T031–T033 (InterconnectionTools)
T025 (register tools)              T034 (register tools)
```

### Single Worker (Sequential)

```
Phase 2 → US1 (T011–T025) → US2 (T026–T034) → US3 (T035–T043) → US4 (T044–T054) → Polish (T055–T062)
```

---

## Implementation Strategy

### MVP First (US1 Only)

1. Complete Phase 1: Setup (T001–T002)
2. Complete Phase 2: Foundational (T003–T010) — **CRITICAL, blocks all stories**
3. Complete Phase 3: US1 Privacy (T011–T025)
4. **STOP and VALIDATE**: Run PTA → PIA → review workflow end-to-end
5. Privacy compliance dashboard operational — deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. **US1** (Privacy) → Test independently → **MVP deployed** (AT-01 through AT-05)
3. **US2** (Interconnections) → Test independently → AT-06 covered
4. **US3** (Agreements) → Test independently → AT-07 through AT-09 covered
5. **US4** (Gates & Integration) → Test independently → AT-10 through AT-20 covered
6. Each story adds value without breaking previous stories

---

## Notes

- All service methods require `CancellationToken` on async paths per constitution
- All services use `IServiceScopeFactory` pattern for scoped `AtoCopilotContext` access
- All tools extend `BaseTool` and use `RegisterTool()` in agent constructors
- AI drafting (PIA narratives, ISA templates) uses existing `IChatCompletionService` with fallback to template-only output
- JSON columns use existing `JsonColumnConverter<T>()` pattern from AtoCopilotContext
- Gate pattern follows existing `yield return GateCheckResult` in `CheckPrepareToCategorize`
- Structured logging via `ILogger<T>` with operation + outcome on all service methods
- XML documentation required on all new public types per constitution Principle VI
