# Tasks: Core Compliance Capabilities

**Input**: Design documents from `/specs/001-core-compliance/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included — SC-009 requires 80%+ unit test coverage for all 9 service implementations.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add dependencies, create new projects, embed required resources

- [X] T000Add NuGet packages (Azure.ResourceManager.ResourceGraph 1.1.0, Azure.ResourceManager.PolicyInsights 1.2.0, Azure.ResourceManager.Resources 1.9.0, Azure.ResourceManager.SecurityCenter 1.2.0, Microsoft.EntityFrameworkCore.Design 9.0.0) to src/Ato.Copilot.Core/Ato.Copilot.Core.csproj
- [X] T000[P] Create integration test project with WebApplicationFactory and references to Ato.Copilot.Mcp in tests/Ato.Copilot.Tests.Integration/Ato.Copilot.Tests.Integration.csproj
- [X] T000[P] Download NIST 800-53 Rev 5 OSCAL catalog JSON from usnistgov/oscal-content and add as embedded resource (offline fallback) in src/Ato.Copilot.Agents/Compliance/Resources/NIST_SP-800-53_rev5_catalog.json

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core models, database, DI wiring, and middleware that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T000[P] Add new enums (AssessmentStatus, ScanSourceType, RemediationType, RiskLevel, EvidenceCategory, RemediationStatus, StepStatus, AuditOutcome) and value types (ScanSummary, DocumentMetadata) per data-model.md in src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T000[P] Create ControlFamilies constants class with 20 NIST 800-53 Rev 5 families and HighRiskFamilies set (AC, IA, SC) in src/Ato.Copilot.Core/Constants/ControlFamilies.cs
- [X] T000[P] Create ComplianceFrameworks constants class (Nist80053, FedRampHigh, FedRampModerate, DoDIL5) with policy initiative GUIDs in src/Ato.Copilot.Core/Constants/ComplianceFrameworks.cs
- [X] T000[P] Create response envelope types (ToolResponse&lt;T&gt;, ToolErrorResponse, ErrorDetail with errorCode + suggestion) per contracts in src/Ato.Copilot.Core/Models/ToolResponse.cs
- [X] T000Extend existing entity models (ComplianceAssessment, ComplianceFinding, NistControl, ComplianceEvidence, ComplianceDocument, RemediationPlan, RemediationStep) with new fields and add new entities (ConfigurationSettings, AuditLogEntry) per data-model.md in src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs
- [X] T000Update AtoCopilotContext: add DbSets for Documents, NistControls, AuditLogs, RemediationPlans; configure entity relationships, indexes, and value conversions for list/enum properties in src/Ato.Copilot.Core/Data/Context/AtoCopilotContext.cs
- [X] T010 Create initial EF Core migration for all entity changes using dotnet ef migrations add InitialCompliance
- [X] T011 Register DbContext in DI with SQLite/SQL Server provider selection based on Database:Provider configuration in src/Ato.Copilot.Core/Extensions/CoreServiceExtensions.cs
- [X] T012 Register ArmClient as singleton with dual-cloud support (AzureGovernment/AzureCloud) using GatewayOptions.CloudEnvironment and DefaultAzureCredential with retry policy in src/Ato.Copilot.Core/Extensions/CoreServiceExtensions.cs
- [X] T013 [P] Wire ComplianceAuthorizationMiddleware and AuditLoggingMiddleware into HTTP pipeline with correct ordering in src/Ato.Copilot.Mcp/Program.cs
- [X] T014 [P] Add Database:Provider, Database:ConnectionString, and NistCatalog settings (PreferOnline, CachePath, CacheMaxAgeDays, FetchTimeoutSeconds) to src/Ato.Copilot.Mcp/appsettings.json

**Checkpoint**: Foundation ready — models extended, database configured, Azure client registered, middleware wired. User story implementation can now begin.

---

## Phase 3: User Story 1 — Configuration & Subscription Setup (Priority: P1) 🎯 MVP

**Goal**: Users can configure subscription, framework, baseline, and environment via natural language through the ConfigurationAgent

**Independent Test**: Send "set subscription abc-123", "set framework FedRAMPHigh", "get configuration" commands and verify shared state updates

### Implementation for User Story 1

- [X] T015 [P] [US1] Create ConfigurationAgent system prompt defining persona, capabilities, and sub-action routing instructions in src/Ato.Copilot.Agents/Configuration/Prompts/ConfigurationAgent.prompt.txt
- [X] T016 [P] [US1] Create ConfigurationTool extending BaseTool with sub-action routing for get_configuration, set_subscription, set_framework, set_baseline, set_preference using IAgentStateManager shared state with config: key prefix in src/Ato.Copilot.Agents/Configuration/Tools/ConfigurationTool.cs
- [X] T017 [US1] Create ConfigurationAgent extending BaseAgent, loading embedded prompt and registering ConfigurationTool in src/Ato.Copilot.Agents/Configuration/Agents/ConfigurationAgent.cs
- [X] T018 [US1] Register ConfigurationAgent and ConfigurationTool in DI container in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs
- [X] T019 [P] [US1] Add agent routing for configuration intents (set subscription, set framework, show settings) in src/Ato.Copilot.Mcp/Server/McpServer.cs
- [X] T020 [P] [US1] Register configuration_manage tool in MCP tool registry in src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs
- [X] T021 [P] [US1] Unit tests for ConfigurationAgent (agent creation, prompt loading, tool registration) in tests/Ato.Copilot.Tests.Unit/Agents/ConfigurationAgentTests.cs
- [X] T022 [P] [US1] Unit tests for ConfigurationTool (all 5 sub-actions, validation, state persistence, error cases) in tests/Ato.Copilot.Tests.Unit/Tools/ConfigurationToolTests.cs

**Checkpoint**: Users can configure subscription/framework/baseline via natural language. ConfigurationAgent routes intents and persists settings in shared state. All US1 acceptance scenarios pass.

---

## Phase 4: User Story 2 — Run Compliance Assessment (Priority: P1)

**Goal**: Users can run resource, policy, or combined compliance assessments against Azure subscriptions and view findings grouped by control family

**Independent Test**: Configure subscription, run "run compliance assessment" with each scan type, verify findings grouped by control family with pass/fail per control

### Implementation for User Story 2

- [X] T023 [P] [US2] Implement INistControlsService: dual-source catalog loading (fetch from usnistgov/oscal-content GitHub repo when NistCatalog:PreferOnline is true with 15s timeout, cache locally, fall back to embedded resource when offline/air-gapped), track LastSyncedAt and CatalogSource, query controls by family/baseline/ID, map to Azure Policy definitions in src/Ato.Copilot.Agents/Compliance/Services/NistControlsService.cs
- [X] T024 [P] [US2] Implement IAzurePolicyComplianceService: query policy compliance states via PolicyInsights SDK, map PolicyDefinitionGroups to NIST control IDs, paginate results in src/Ato.Copilot.Agents/Compliance/Services/AzurePolicyComplianceService.cs
- [X] T025 [P] [US2] Implement IDefenderForCloudService: query secure score, regulatory compliance standards/controls/assessments, map to NIST controls in src/Ato.Copilot.Agents/Compliance/Services/DefenderForCloudService.cs
- [X] T026 [US2] Implement IAtoComplianceEngine: orchestrate resource (Resource Graph), policy, and Defender scans; merge and correlate findings; compute compliance score; persist assessment via EF Core in src/Ato.Copilot.Agents/Compliance/Services/AtoComplianceEngine.cs
- [X] T027 [US2] Implement compliance_assess tool: accept scan type/framework/control families/resource types params, invoke AtoComplianceEngine, return response envelope with summary and findingsByFamily in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceAssessTool.cs
- [X] T028 [P] [US2] Implement compliance_get_control_family tool: query NistControlsService + latest assessment for family details and per-control status in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceGetControlFamilyTool.cs
- [X] T029 [P] [US2] Implement compliance_status tool: return current compliance posture from latest assessment in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceStatusTool.cs
- [X] T030 [US2] Update ComplianceAgent to register assessment tools and route assessment, control family, and status intents in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs
- [X] T031 [US2] Update ComplianceAgent system prompt with comprehensive instructions for assessment workflow, scan types, and progress reporting in src/Ato.Copilot.Agents/Compliance/Prompts/ComplianceAgent.prompt.txt
- [X] T032 [P] [US2] Unit tests for NistControlsService (online fetch success, offline fallback, cache hit/expiry, catalog loading, family queries, baseline filtering, control lookup, GetCatalogStatus) in tests/Ato.Copilot.Tests.Unit/Services/NistControlsServiceTests.cs
- [X] T033 [P] [US2] Unit tests for AzurePolicyComplianceService (policy state queries, NIST mapping, pagination, error handling) in tests/Ato.Copilot.Tests.Unit/Services/AzurePolicyComplianceServiceTests.cs
- [X] T034 [P] [US2] Unit tests for DefenderForCloudService (secure score, regulatory compliance queries, NIST mapping) in tests/Ato.Copilot.Tests.Unit/Services/DefenderForCloudServiceTests.cs
- [X] T035 [P] [US2] Unit tests for AtoComplianceEngine (scan orchestration, finding correlation, score computation, persistence, CancellationToken timeout) in tests/Ato.Copilot.Tests.Unit/Services/AtoComplianceEngineTests.cs

**Checkpoint**: Users can run resource, policy, or combined assessments. Findings grouped by control family with compliance score. All 3 scan types return correlated results. Assessment persisted to database. US2 acceptance scenarios pass.

---

## Phase 5: User Story 3 — Single & Batch Remediation (Priority: P2)

**Goal**: Users can remediate failing controls with dry-run by default, explicit confirmation, high-risk warnings for AC/IA/SC families, and batch operations

**Independent Test**: Run assessment, select failing control, step through dry-run → confirm → apply → verify cycle

### Implementation for User Story 3

- [X] T036 [US3] Implement IRemediationEngine: create remediation plans from findings, execute resource configuration and policy remediation changes, capture before/after state, enforce dry-run default, batch with stop-on-failure in src/Ato.Copilot.Agents/Compliance/Services/RemediationEngine.cs
- [X] T037 [US3] Implement compliance_remediate tool: accept findingId/controlFamily/severity params, invoke RemediationEngine, add high-risk warning for AC/IA/SC, return dry-run plan or execution results in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceRemediateTool.cs
- [X] T038 [P] [US3] Implement compliance_validate_remediation tool: re-scan specific finding to confirm remediation succeeded in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceValidateRemediationTool.cs
- [X] T039 [P] [US3] Implement compliance_generate_plan tool: generate prioritized remediation plan for all open findings in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceGeneratePlanTool.cs
- [X] T040 [US3] Register remediation tools in ComplianceAgent and add remediation intent routing in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs
- [X] T041 [P] [US3] Unit tests for RemediationEngine (dry-run, apply, batch, stop-on-failure, high-risk warning, before/after state, rollback guidance) in tests/Ato.Copilot.Tests.Unit/Services/RemediationEngineTests.cs
- [X] T042 [P] [US3] Unit tests for remediation tools (compliance_remediate, compliance_validate_remediation, compliance_generate_plan) in tests/Ato.Copilot.Tests.Unit/Tools/ComplianceRemediationToolTests.cs

**Checkpoint**: Users can remediate single findings or batch by severity/family. Dry-run default with confirmation. High-risk warnings for AC/IA/SC. Before/after state logged. US3 acceptance scenarios pass.

---

## Phase 6: User Story 4 — Evidence Collection (Priority: P2)

**Goal**: Users can collect timestamped, control-referenced evidence artifacts from Azure for audit preparation

**Independent Test**: Request evidence for AC-2, verify Azure resources queried, timestamps applied, and evidence stored with control reference

### Implementation for User Story 4

- [X] T043 [US4] Implement IEvidenceStorageService: collect evidence from Azure (config exports, policy snapshots, resource inventories, activity logs, Defender recommendations), compute SHA-256 content hash, persist via EF Core in src/Ato.Copilot.Agents/Compliance/Services/EvidenceStorageService.cs
- [X] T044 [US4] Implement compliance_collect_evidence tool: accept controlId/subscriptionId, invoke EvidenceStorageService, return evidence artifacts with categories and counts in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceCollectEvidenceTool.cs
- [X] T045 [US4] Register evidence tool in ComplianceAgent and add evidence collection intent routing in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs
- [X] T046 [P] [US4] Unit tests for EvidenceStorageService (evidence collection per type, SHA-256 hashing, persistence, error handling) in tests/Ato.Copilot.Tests.Unit/Services/EvidenceStorageServiceTests.cs
- [X] T047 [P] [US4] Unit tests for compliance_collect_evidence tool (control-level, family-level, missing control, response envelope) in tests/Ato.Copilot.Tests.Unit/Tools/ComplianceEvidenceToolTests.cs

**Checkpoint**: Evidence collected from Azure with timestamps, control references, and SHA-256 hashes. Grouped by control family. US4 acceptance scenarios pass.

---

## Phase 7: User Story 5 — Document Generation (Priority: P3)

**Goal**: Users can generate SSP, SAR, and POA&M documents in Markdown following FedRAMP template structure

**Independent Test**: Run assessment, request "generate SSP", verify output contains findings, evidence references, and FedRAMP structure

### Implementation for User Story 5

- [X] T048 [US5] Implement IDocumentGenerationService: generate SSP, SAR, and POA&M Markdown documents from assessment data and evidence, include FedRAMP template sections, control implementations, and remediation status in src/Ato.Copilot.Agents/Compliance/Services/DocumentGenerationService.cs
- [X] T049 [US5] Implement compliance_generate_document tool: accept documentType/systemName/owner/assessmentId, invoke DocumentGenerationService, return generated document content in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceGenerateDocumentTool.cs
- [X] T050 [US5] Register document generation tool in ComplianceAgent and add document intent routing in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs
- [X] T051 [P] [US5] Unit tests for DocumentGenerationService (SSP, SAR, POA&M generation, FedRAMP structure, metadata, empty assessment edge case) in tests/Ato.Copilot.Tests.Unit/Services/DocumentGenerationServiceTests.cs
- [X] T052 [P] [US5] Unit tests for compliance_generate_document tool (all document types, missing assessment data, response envelope) in tests/Ato.Copilot.Tests.Unit/Tools/ComplianceDocumentToolTests.cs

**Checkpoint**: SSP, SAR, and POA&M documents generate from assessment data in FedRAMP Markdown format. US5 acceptance scenarios pass.

---

## Phase 8: User Story 6 — Compliance Monitoring & History (Priority: P3)

**Goal**: Users can query compliance history, trends, and drift detection across assessments

**Independent Test**: Run two assessments, query history/trends/drift, verify trend data and change detection

### Implementation for User Story 6

- [X] T053 [US6] Implement IComplianceMonitoringService: query assessment history, compute compliance trends over time, detect drift (new/resolved/changed findings between assessments), support time-window and scan-type filters in src/Ato.Copilot.Agents/Compliance/Services/ComplianceMonitoringService.cs
- [X] T054 [P] [US6] Implement compliance_history tool: accept days/scanType filters, return trend data with compliance percentages over time in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceHistoryTool.cs
- [X] T055 [P] [US6] Implement compliance_monitoring tool: accept action (status/scan/alerts/trend), return drift detection, compliance alerts, and monitoring status in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceMonitoringTool.cs
- [X] T056 [US6] Register monitoring and history tools in ComplianceAgent and add monitoring intent routing in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs
- [X] T057 [P] [US6] Unit tests for ComplianceMonitoringService (trend computation, drift detection, time-window filtering, no-assessments edge case) in tests/Ato.Copilot.Tests.Unit/Services/ComplianceMonitoringServiceTests.cs
- [X] T058 [P] [US6] Unit tests for monitoring and history tools in tests/Ato.Copilot.Tests.Unit/Tools/ComplianceMonitoringToolTests.cs

**Checkpoint**: Compliance history trends and drift detection work across multiple assessments. US6 acceptance scenarios pass.

---

## Phase 9: User Story 7 — Audit Logging & Role-Based Access (Priority: P3)

**Goal**: All actions are logged with complete metadata; role-based access prevents unauthorized operations (Auditor read-only, PlatformEngineer no approval)

**Independent Test**: Perform actions as different roles, verify audit log captures who/what/when/outcome and RBAC blocks unauthorized actions

### Implementation for User Story 7

- [X] T059 [US7] Implement IAssessmentAuditService: persist audit log entries with user, role, action, scan type, subscription, affected resources/controls, outcome, and duration; query audit history with filters in src/Ato.Copilot.Agents/Compliance/Services/AssessmentAuditService.cs
- [X] T060 [US7] Implement compliance_audit_log tool: accept days/actionType/subscriptionId filters, return paginated audit entries in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceAuditLogTool.cs
- [X] T061 [US7] Implement role-based access checks in ComplianceAuthorizationMiddleware: Auditor read-only (deny remediation/approval), PlatformEngineer deny approval, ComplianceOfficer full access per ComplianceRoles mapping in src/Ato.Copilot.Mcp/Middleware/ComplianceAuthorizationMiddleware.cs
- [X] T062 [US7] Integrate IAssessmentAuditService calls into all tool execution paths (assessment, remediation, evidence, document, config changes) for 100% action logging in src/Ato.Copilot.Agents/Compliance/Agents/ComplianceAgent.cs
- [X] T063 [P] [US7] Unit tests for AssessmentAuditService (log creation, query filters, outcome types, duration tracking) in tests/Ato.Copilot.Tests.Unit/Services/AssessmentAuditServiceTests.cs
- [X] T064 [P] [US7] Unit tests for RBAC enforcement (Auditor denied remediation, PlatformEngineer denied approval, ComplianceOfficer full access) and audit log tool in tests/Ato.Copilot.Tests.Unit/Tools/ComplianceAuditToolTests.cs

**Checkpoint**: All actions logged with complete metadata (SC-008: 100% coverage). RBAC enforced per role matrix. US7 acceptance scenarios pass.

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Cross-cutting features, integration tests, documentation, and final validation

- [X] T065 Implement compliance_chat tool with conversation memory via IConversationStateManager for natural language interaction in src/Ato.Copilot.Agents/Compliance/Tools/ComplianceChatTool.cs
- [X] T066 Add agent handoff logic between ConfigurationAgent and ComplianceAgent based on intent classification in src/Ato.Copilot.Mcp/Server/McpServer.cs
- [X] T067 [P] Integration tests for MCP tool endpoints (configuration, assessment, remediation flows) using WebApplicationFactory in tests/Ato.Copilot.Tests.Integration/McpToolEndpointTests.cs
- [X] T068 [P] Update appsettings.json with all configurable options (Azure cloud, database, feature flags, rate limits) in src/Ato.Copilot.Mcp/appsettings.json
- [X] T069 [P] Update README.md with setup instructions, prerequisites, quickstart reference, and architecture overview
- [X] T070 Run quickstart.md end-to-end validation checklist (all 16 items)
- [X] T071 Code cleanup: ensure XML docs on all methods and variables (public, internal, protected, private) per FR-018, warnings-as-errors clean build, no magic strings

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — **BLOCKS all user stories**
- **User Stories (Phase 3–9)**: All depend on Foundational phase completion
  - US1 (Config) and US2 (Assessment) are both P1 and can start in parallel
  - US3 (Remediation) depends on US2 (needs assessment findings)
  - US4 (Evidence) depends on US2 (needs assessment data + Azure access patterns)
  - US5 (Documents) depends on US2 + US4 (needs assessment data + evidence references)
  - US6 (Monitoring) depends on US2 (needs multiple assessments for trend/drift)
  - US7 (Audit) can start after Foundational but integrates with all other stories
- **Polish (Phase 10)**: Depends on all desired user stories being complete

### User Story Dependencies

```text
Phase 2 (Foundational)
    ├── US1 (Config, P1) ─────────────────────────────────────────┐
    │                                                              │
    ├── US2 (Assessment, P1) ──┬── US3 (Remediation, P2)          │
    │                          ├── US4 (Evidence, P2)              ├── Phase 10
    │                          ├── US5 (Documents, P3) ← US4      │   (Polish)
    │                          └── US6 (Monitoring, P3)            │
    │                                                              │
    └── US7 (Audit, P3) ──────────────────────────────────────────┘
```

### Within Each User Story

1. Services before tools (services implement business logic, tools invoke services)
2. Tools before agent registration (agent needs tool types)
3. Agent updates before MCP wiring (MCP routes to agents)
4. Tests can run in parallel with MCP wiring tasks (different files)
5. Commit after each task or logical group

### Parallel Opportunities

- **Phase 1**: T002 and T003 can run in parallel (different new files)
- **Phase 2**: T004–T007 can run in parallel (different files); T013–T014 can run in parallel (different files)
- **Phase 3 (US1)**: T015 + T016 in parallel (different new files); T019–T022 in parallel after T018
- **Phase 4 (US2)**: T023–T025 in parallel (three independent services in different files); T028–T029 in parallel; T032–T035 in parallel
- **Phase 5 (US3)**: T038 + T039 in parallel; T041 + T042 in parallel
- **Phase 6 (US4)**: T046 + T047 in parallel
- **Phase 7 (US5)**: T051 + T052 in parallel
- **Phase 8 (US6)**: T054 + T055 in parallel; T057 + T058 in parallel
- **Phase 9 (US7)**: T063 + T064 in parallel
- **Phase 10**: T067–T069 in parallel

---

## Parallel Example: User Story 2

```bash
# Launch all 3 independent services in parallel:
Task: "Implement INistControlsService in .../Services/NistControlsService.cs"
Task: "Implement IAzurePolicyComplianceService in .../Services/AzurePolicyComplianceService.cs"
Task: "Implement IDefenderForCloudService in .../Services/DefenderForCloudService.cs"

# Then orchestrator (depends on all 3 services):
Task: "Implement IAtoComplianceEngine in .../Services/AtoComplianceEngine.cs"

# Then tools + agent wiring (depends on engine):
Task: "Implement compliance_assess tool"
Task: "Implement compliance_get_control_family tool"  # [P] with assess
Task: "Implement compliance_status tool"               # [P] with assess

# Launch all unit tests in parallel:
Task: "Unit tests for NistControlsService"
Task: "Unit tests for AzurePolicyComplianceService"
Task: "Unit tests for DefenderForCloudService"
Task: "Unit tests for AtoComplianceEngine"
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1 (Configuration)
4. Complete Phase 4: User Story 2 (Assessment)
5. **STOP and VALIDATE**: Test US1 + US2 independently — users can configure and assess
6. Deploy/demo if ready — this is the functional MVP

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. US1 (Config) + US2 (Assessment) → MVP! Configure + assess
3. US3 (Remediation) → Assess + fix
4. US4 (Evidence) → Assess + collect evidence for audit
5. US5 (Documents) → Generate SSP/SAR/POA&M
6. US6 (Monitoring) → Track compliance over time
7. US7 (Audit) → Full audit trail + RBAC
8. Polish → Chat, handoff, integration tests, docs

Each story adds value without breaking previous stories.

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (Config) → then US3 (Remediation)
   - Developer B: User Story 2 (Assessment) → then US4 (Evidence)
   - Developer C: User Story 7 (Audit/RBAC) → then US5 (Documents) → US6 (Monitoring)
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable after Foundational
- All tool responses MUST use the standard envelope schema ({status, data, metadata})
- All Azure calls MUST use CancellationToken with 60s timeout for assessments
- All remediations MUST default to dry-run mode
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
