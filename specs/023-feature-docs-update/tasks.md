# Tasks: Feature 023 — Documentation Update (Features 017–022)

**Input**: Design documents from `/specs/023-feature-docs-update/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, quickstart.md

**Tests**: Not requested — documentation-only feature with manual verification.

**Organization**: Tasks are grouped by user story to enable independent implementation and verification of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Documentation**: `docs/` at repository root
- **Source reference**: `src/Ato.Copilot.Agents/Tools/` for parameter/response verification
- **Format patterns**: See `specs/023-feature-docs-update/quickstart.md` and `research.md` RT3

---

## Phase 1: Setup

**Purpose**: Verify branch and establish working context

- [X] T001 Verify branch `023-feature-docs-update` is checked out, confirm source tool files are accessible in `src/Ato.Copilot.Agents/Compliance/Tools/`, and review existing documentation format patterns in `docs/architecture/agent-tool-catalog.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: N/A for documentation-only feature

**Note**: User Stories 1 and 2 (Phase 3, Phase 4) serve as the foundational layer. The agent tool catalog and tool inventory are the reference documents that persona guides, RMF phases, and other documentation reference. Complete US1 and US2 before starting US3–US5.

**Checkpoint**: No foundational infrastructure needed — proceed directly to User Story 1

---

## Phase 3: User Story 1 — Agent Tool Catalog Update (Priority: P1) 🎯 MVP

**Goal**: Add 22 new tool entries, update 2 enhanced tool entries, and verify 9 existing entries in the agent tool catalog so all 33 tools from Features 017–022 are fully documented with parameters, response JSON, RBAC roles, and use cases.

**Independent Test**: Open `docs/architecture/agent-tool-catalog.md`. Verify all 22 new tools from Features 018, 021, 022 have complete entries. Verify 2 enhanced tools are updated. Verify 9 existing entries from Features 017, 019 are accurate.

**FRs**: FR-001, FR-002, FR-003, FR-004, FR-005, FR-006

### Implementation for User Story 1

- [X] T002 [US1] Add Feature 018 SAP Generation section with 5 tool entries (`compliance_generate_sap`, `compliance_update_sap`, `compliance_finalize_sap`, `compliance_get_sap`, `compliance_list_saps`) — each with parameters table, response JSON, RBAC, and use cases — verified against `src/Ato.Copilot.Agents/Tools/SapTools.cs` in docs/architecture/agent-tool-catalog.md
- [X] T003 [US1] Add Feature 021 Privacy section with 4 tool entries (`compliance_create_pta`, `compliance_generate_pia`, `compliance_review_pia`, `compliance_check_privacy_compliance`) verified against `src/Ato.Copilot.Agents/Tools/PrivacyTools.cs` in docs/architecture/agent-tool-catalog.md
- [X] T004 [US1] Add Feature 021 Interconnection section with 8 tool entries (`compliance_add_interconnection`, `compliance_list_interconnections`, `compliance_update_interconnection`, `compliance_generate_isa`, `compliance_register_agreement`, `compliance_update_agreement`, `compliance_certify_no_interconnections`, `compliance_validate_agreements`) verified against `src/Ato.Copilot.Agents/Tools/InterconnectionTools.cs` in docs/architecture/agent-tool-catalog.md
- [X] T005 [US1] Add Feature 022 SSP Authoring & OSCAL section with 5 tool entries (`compliance_write_ssp_section`, `compliance_review_ssp_section`, `compliance_ssp_completeness`, `compliance_export_oscal_ssp`, `compliance_validate_oscal_ssp`) verified against `src/Ato.Copilot.Agents/Tools/SspAuthoringTools.cs` in docs/architecture/agent-tool-catalog.md
- [X] T006 [US1] Update `compliance_generate_ssp` entry to reflect 13-section output, new section keys (`system_identification`, `minimum_controls`, `control_implementations`, etc.), backward-compatible old keys mapping table, and YAML front-matter metadata in docs/architecture/agent-tool-catalog.md
- [X] T007 [US1] Update `compliance_export_oscal` entry to note SSP export delegation to `OscalSspExportService`, verified against `src/Ato.Copilot.Agents/Tools/EmassExportTools.cs` in docs/architecture/agent-tool-catalog.md
- [X] T008 [US1] Verify and update Feature 017 existing 5 CKL/XCCDF tool entries (`compliance_import_ckl`, `compliance_import_xccdf`, `compliance_export_ckl`, `compliance_list_imports`, `compliance_get_import_summary`) against `src/Ato.Copilot.Agents/Tools/ScanImportTools.cs` — fix any stale parameters, response fields, or RBAC mismatches in docs/architecture/agent-tool-catalog.md
- [X] T009 [US1] Verify and update Feature 019 existing 4 Prisma tool entries (`compliance_import_prisma_csv`, `compliance_import_prisma_api`, `compliance_list_prisma_policies`, `compliance_prisma_trend`) against `src/Ato.Copilot.Agents/Tools/PrismaImportTools.cs` — fix any stale parameters, response fields, or RBAC mismatches in docs/architecture/agent-tool-catalog.md

**Checkpoint**: Agent tool catalog has complete documentation for all 33 tools. This is the foundation for all downstream documentation.

---

## Phase 4: User Story 2 — Tool Inventory Reference Update (Priority: P1)

**Goal**: Add 22 new tool rows across 3 new categories and update the summary table to show 140 total tools in the tool inventory reference.

**Independent Test**: Open `docs/reference/tool-inventory.md`. Count tools — should be 140 total. Verify Categories 10, 11, 12 exist with correct tool counts, RMF phases, and RBAC roles.

**FRs**: FR-007, FR-008, FR-009, FR-010

### Implementation for User Story 2

- [X] T010 [US2] Add Category 10 (SAP Generation) with 5 Feature 018 tool rows (#119–#123) with RMF phase Assess and correct RBAC roles in docs/reference/tool-inventory.md
- [X] T011 [US2] Add Category 11 (Privacy & Interconnections) with 12 Feature 021 tool rows (#124–#135) with RMF phase Prepare and correct RBAC roles per research.md RT2 in docs/reference/tool-inventory.md
- [X] T012 [US2] Add Category 12 (SSP Authoring & OSCAL) with 5 Feature 022 tool rows (#136–#140) with RMF phases Implement/Assess and correct RBAC roles in docs/reference/tool-inventory.md
- [X] T013 [US2] Update tool count summary table from 118 to 140 total tools, add 3 new category names to the category listing in docs/reference/tool-inventory.md

**Checkpoint**: Tool inventory shows 140 tools across 12 categories with accurate phase and role mappings.

---

## Phase 5: User Story 3 — ISSM Guide Update (Priority: P2)

**Goal**: Add 7 new workflow sections to the ISSM guide covering STIG import oversight, SAP review, Prisma monitoring, privacy oversight, interconnection management, SSP review, and OSCAL export.

**Independent Test**: Open `docs/guides/issm-guide.md`. Verify 7 new sections exist with step-by-step tool invocations, RBAC notes, and expected results.

**FRs**: FR-011

### Implementation for User Story 3

- [X] T014 [US3] Add "STIG/SCAP Import Management" workflow section with steps for importing CKL/XCCDF files, reviewing import summaries (`compliance_list_imports`, `compliance_get_import_summary`), and tracking scan history in docs/guides/issm-guide.md
- [X] T015 [US3] Add/verify "Prisma Cloud Monitoring" section with CSV/API import oversight and trend analysis workflow, and add "Security Assessment Plan Review" section with SAP status review, scope verification, and finalization confirmation in docs/guides/issm-guide.md
- [X] T016 [US3] Add "Privacy Oversight" section with PTA determination review and PIA approval/rejection workflow using `compliance_review_pia` in docs/guides/issm-guide.md
- [X] T017 [US3] Add "Interconnection Agreement Management" section with `compliance_validate_agreements`, ISA/MOU expiration monitoring, and agreement lifecycle tracking in docs/guides/issm-guide.md
- [X] T018 [US3] Add "SSP Section Review" section with `compliance_review_ssp_section` and `compliance_ssp_completeness` workflows, and "OSCAL Export for Authorization Package" section with `compliance_export_oscal_ssp` and `compliance_validate_oscal_ssp` steps in docs/guides/issm-guide.md

**Checkpoint**: ISSM guide has complete workflow documentation for all Features 017–022 capabilities accessible to the ISSM role.

---

## Phase 6: User Story 4 — ISSO/SCA/Engineer/AO Guide Updates (Priority: P2)

**Goal**: Update 5 persona guide files with new workflow sections and capability summaries for Features 017–022 tools accessible to each role.

**Independent Test**: Open each persona guide file. Verify new sections exist with workflows and tool invocation examples appropriate for that persona's RBAC level.

**FRs**: FR-012, FR-013, FR-014, FR-015

### Implementation for User Story 4

- [X] T019 [P] [US4] Update ISSO getting-started "what you can do" summary to include CKL/XCCDF upload, Prisma scan import, PTA/PIA analysis, SSP section authoring, interconnection registration, and completeness tracking in docs/getting-started/isso.md
- [X] T020 [P] [US4] Add ISSO persona guide workflow sections for STIG import, Prisma import, PTA analysis, PIA authoring, interconnection registration, and SSP section authoring lifecycle in docs/personas/isso.md (FR-012)
- [X] T021 [P] [US4] Add SCA guide assessment workflow sections: SAP generation (`compliance_generate_sap`), method customization (`compliance_update_sap`), SAP finalization (`compliance_finalize_sap`), CKL export (`compliance_export_ckl`), privacy compliance check (`compliance_check_privacy_compliance`), OSCAL validation (`compliance_validate_oscal_ssp`), SSP completeness verification in docs/guides/sca-guide.md
- [X] T022 [P] [US4] Add Engineer guide sections: STIG remediation context from imported findings, Prisma remediation CLI scripts, interconnection registration (`compliance_add_interconnection`), and SSP §5/§6 system environment contribution (`compliance_write_ssp_section`) in docs/guides/engineer-guide.md
- [X] T023 [P] [US4] Update AO quick reference pre-authorization checklist with SAP finalization status, Privacy Readiness Gate, SSP completeness percentage, and OSCAL export status items in docs/reference/quick-reference-cards.md

**Checkpoint**: All 5 persona guides have role-appropriate workflow documentation for Features 017–022.

---

## Phase 7: User Story 5 — RMF Phase Guide Updates (Priority: P2)

**Goal**: Update 5 RMF phase guides with new capabilities, persona responsibilities, NL query examples, and gate requirements from Features 017–022.

**Independent Test**: Open each RMF phase guide. Verify new content covers Features 017–022 capabilities applicable to that phase with persona-specific tool references.

**FRs**: FR-016, FR-017, FR-018, FR-019, FR-020

### Implementation for User Story 5

- [X] T024 [P] [US5] Add Prepare phase content: PTA analysis workflow, PIA generation steps, interconnection registration, ISA/MOU creation, Privacy Readiness Gate requirements, and Interconnection Documentation Gate requirements in docs/rmf-phases/prepare.md
- [X] T025 [P] [US5] Add Assess phase content: STIG CKL/XCCDF import as assessment evidence, SAP generation and finalization steps, Prisma Cloud scan import for automated assessment, CKL export for eMASS upload, and SSP section authoring/review as assessment support in docs/rmf-phases/assess.md
- [X] T026 [P] [US5] Add Authorize phase content: SAP-to-SAR alignment references, OSCAL SSP export for authorization package (`compliance_export_oscal_ssp`), and privacy compliance as authorization prerequisites in docs/rmf-phases/authorize.md
- [X] T027 [P] [US5] Add Monitor phase content: ISA/MOU expiration monitoring, PIA annual review cycle tracking, Prisma Cloud periodic re-import with cadence table, and SSP section status monitoring as ConMon activities in docs/rmf-phases/monitor.md
- [X] T028 [US5] Add Categorize phase note: PTA PII categories from Prepare phase carry forward and may affect system categorization in docs/rmf-phases/categorize.md

**Checkpoint**: All 5 RMF phase guides reflect Features 017–022 capabilities with correct persona responsibilities and gate requirements.

---

## Phase 8: User Story 6 — Architecture & Data Model Reference Updates (Priority: P3)

**Goal**: Document all 19 new entities, 16+ new enumerations, ~15 glossary terms, and NL query examples in the architecture and reference documentation.

**Independent Test**: Open `docs/architecture/data-model.md`. Verify all new entities appear with field-level detail. Open glossary and NL query reference for completeness.

**FRs**: FR-021, FR-022, FR-023, FR-024

### Implementation for User Story 6

- [X] T029 [US6] Add Feature 017 entities (ScanImportRecord, ScanImportFinding, StigControl, enhanced ComplianceFinding) and Feature 018 entities (SecurityAssessmentPlan, SapControlEntry, SapTeamMember, SapMethodOverride) with fields, types, constraints, and foreign key relationships to docs/architecture/data-model.md
- [X] T030 [US6] Add Feature 019 entities (enhanced ScanImportFinding with Prisma fields, PrismaPolicy, PrismaTrendRecord) and Feature 021 entities (PrivacyThresholdAnalysis, PrivacyImpactAssessment, PiaSection, SystemInterconnection, InterconnectionAgreement) with fields, types, constraints, and relationships to docs/architecture/data-model.md
- [X] T031 [US6] Add Feature 022 entities (SspSection, ContingencyPlanReference, enhanced RegisteredSystem) and all 16+ new enumerations from Features 017–022 with their values and descriptions to docs/architecture/data-model.md
- [X] T032 [P] [US6] Add ~15 new glossary terms (PTA, PIA, ISA, MOU, OSCAL, SAP, CKL, XCCDF, SCAP, STIG, SSP Section, FIPS 200, SspSectionStatus, OperationalStatus, ConMon) to docs/reference/glossary.md
- [X] T033 [P] [US6] Add 6 NL query reference categories (STIG Import, SAP Generation, Prisma Cloud, Privacy, Interconnections, SSP Authoring) with natural language examples mapping to the 31 new tools in docs/guides/nl-query-reference.md

**Checkpoint**: Data model reference documents all new entities and enums. Glossary covers all new terms. NL query reference maps queries to all new tools.

---

## Phase 9: User Story 7 — API/MCP Server Reference Update (Priority: P3)

**Goal**: Update the MCP server API reference with all 31 new tool registrations and 7 new service interfaces from Features 017–022.

**Independent Test**: Open `docs/api/mcp-server.md`. Verify all 31 new tools appear in the tool registration list with MCP method names. Verify all 7 new services are listed.

**FRs**: FR-025

### Implementation for User Story 7

- [X] T034 [US7] Add 31 new tool registrations to the MCP tool list (5 F017, 5 F018, 4 F019, 12 F021, 5 F022) and 7 new service interfaces (IStigImportService, ISapService, IPrismaImportService, IPrivacyService, IInterconnectionService, IOscalSspExportService, IOscalValidationService) verified against `src/Ato.Copilot.Agents/ServiceCollectionExtensions.cs` in docs/api/mcp-server.md

**Checkpoint**: MCP server reference lists all 140 tools and all registered services.

---

## Phase 10: User Story 8 — Missing Release Notes (Priority: P3)

**Goal**: Create release notes for Features 017 and 018 following the established v1.20.0 format.

**Independent Test**: Check `docs/release-notes/` for `v1.18.0.md` and `v1.19.0.md`. Verify each follows the release notes format with tool tables, key capabilities, and data model sections.

**FRs**: FR-026, FR-027

### Implementation for User Story 8

- [X] T035 [P] [US8] Create v1.18.0 release notes for Feature 017 SCAP/STIG Import: header with version/date/branch/test counts, 5 new MCP tools table, key capabilities (CKL import, XCCDF import, CKL export, import tracking), data model entities (ScanImportRecord, ScanImportFinding, StigControl) and enums, following `docs/release-notes/v1.20.0.md` format in docs/release-notes/v1.18.0.md
- [X] T036 [P] [US8] Create v1.19.0 release notes for Feature 018 SAP Generation: header with version/date/branch/test counts, 5 new MCP tools table, key capabilities (SAP generation, assessment method customization, SAP finalization with SHA-256), data model entities (SecurityAssessmentPlan, SapControlEntry, SapTeamMember, SapMethodOverride) and enums, following `docs/release-notes/v1.20.0.md` format in docs/release-notes/v1.19.0.md

**Checkpoint**: Release notes exist for all shipped features (v1.18.0 through v1.21.0).

---

## Phase 11: User Story 9 — Persona Test Case Documentation (Priority: P3)

**Goal**: Add a "Persona End-to-End Tests" section to the dev/testing guide referencing Feature 020 test scripts with execution instructions.

**Independent Test**: Open `docs/dev/testing.md`. Verify a section exists pointing to persona test case scripts organized by persona with test data setup instructions.

**FRs**: FR-028

### Implementation for User Story 9

- [X] T037 [US9] Add "Persona End-to-End Tests" section referencing Feature 020 test scripts from `docs/persona-test-cases/` organized by persona (ISSM, ISSO, SCA, AO, Engineer), with execution order, test data setup instructions ("Eagle Eye" system), and links to environment-checklist.md, test-data-setup.md, results-template.md, and tool-validation.md in docs/dev/testing.md

**Checkpoint**: QA testers can discover and execute persona test scenarios from the dev/testing guide.

---

## Phase 12: Polish & Cross-Cutting Concerns

**Purpose**: Navigation updates, cross-reference verification, and final validation

- [X] T038 Update mkdocs.yml nav to add v1.18.0 and v1.19.0 under Release Notes section, and verify persona-test-cases are linked in nav in mkdocs.yml
- [X] T039 Verify all cross-references between documentation files are accurate: Feature 022 SSP §7 → Feature 021 interconnection data, Feature 019 → Feature 017 shared entities, Feature 018 SAP → Feature 017 STIG benchmarks, enhanced tool entries reference correct services (FR-029, FR-030); also verify v1.21.0 release notes include Feature 021 content (12 privacy/interconnection tools) per SC-011
- [X] T040 Run quickstart.md verification checklist: tool names match source `Name` properties, parameter tables match source `Parameters` dictionaries, RBAC roles match `ServiceCollectionExtensions.cs`, response JSON examples are valid, no orphan links
- [X] T041 Verify each tool catalog entry (T002–T005) includes a "tool not found" error response example in its troubleshooting section, covering the edge case where a feature is not yet deployed

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: N/A — documentation-only feature
- **US1 Tool Catalog (Phase 3)**: Depends on Setup — serves as foundation for persona guides and RMF phases
- **US2 Tool Inventory (Phase 4)**: Depends on Setup — can run in parallel with US1 (different file)
- **US3 ISSM Guide (Phase 5)**: Depends on US1 completion (references catalog entries)
- **US4 Other Persona Guides (Phase 6)**: Depends on US1 completion (references catalog entries)
- **US5 RMF Phase Guides (Phase 7)**: Depends on US1 completion (references catalog entries)
- **US6 Architecture & Data Model (Phase 8)**: Independent — can start after Setup (no catalog dependency)
- **US7 MCP Server Reference (Phase 9)**: Independent — can start after Setup
- **US8 Release Notes (Phase 10)**: Independent — can start after Setup
- **US9 Persona Test Cases (Phase 11)**: Independent — can start after Setup
- **Polish (Phase 12)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: No dependencies on other stories — START HERE
- **US2 (P1)**: No dependencies on other stories — can run in **parallel** with US1
- **US3 (P2)**: Depends on US1 (catalog entries referenced in workflows)
- **US4 (P2)**: Depends on US1 — can run in **parallel** with US3
- **US5 (P2)**: Depends on US1 — can run in **parallel** with US3 and US4
- **US6 (P3)**: Independent of US1–US5 — can run in **parallel** with any other story
- **US7 (P3)**: Independent — can run in **parallel** with any other story
- **US8 (P3)**: Independent — can run in **parallel** with any other story
- **US9 (P3)**: Independent — can run in **parallel** with any other story

### Within Each User Story

- Read source code to verify tool parameters and responses
- Follow format patterns from research.md RT3 and quickstart.md
- Add content following existing file structure and heading hierarchy
- Verify RBAC roles match `ServiceCollectionExtensions.cs` DI registration
- Commit after each task or logical group

### Parallel Opportunities

**Maximum parallelism**: After Setup, up to 9 independent work streams:
- Stream A: US1 (catalog) → US3+US4+US5 (guides that reference catalog)
- Stream B: US2 (inventory) — independent
- Stream C: US6 (data model, glossary, NL queries) — independent
- Stream D: US7 (MCP server reference) — independent
- Stream E: US8 (release notes) — independent
- Stream F: US9 (persona test docs) — independent

**Practical parallelism**: With a single agent, execute in priority order:
- US1 → US2 → US3 → US4 → US5 → US6 → US7 → US8 → US9 → Polish

---

## Parallel Example: User Story 4 (Phase 6)

```bash
# All 5 tasks work on different files — launch in parallel:
Task T019: "Update ISSO getting-started in docs/getting-started/isso.md"
Task T020: "Add ISSO persona workflows in docs/personas/isso.md"
Task T021: "Add SCA guide sections in docs/guides/sca-guide.md"
Task T022: "Add Engineer guide sections in docs/guides/engineer-guide.md"
Task T023: "Update AO quick reference in docs/reference/quick-reference-cards.md"
```

## Parallel Example: User Story 5 (Phase 7)

```bash
# 4 of 5 tasks work on different files — launch in parallel:
Task T024: "Add Prepare phase content in docs/rmf-phases/prepare.md"
Task T025: "Add Assess phase content in docs/rmf-phases/assess.md"
Task T026: "Add Authorize phase content in docs/rmf-phases/authorize.md"
Task T027: "Add Monitor phase content in docs/rmf-phases/monitor.md"
# Then sequentially:
Task T028: "Add Categorize phase note in docs/rmf-phases/categorize.md"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 3: US1 — Agent Tool Catalog (all 33 tool entries)
3. **STOP and VALIDATE**: Verify all tool names, parameters, RBAC match source code
4. The tool catalog alone provides the most critical documentation value

### Incremental Delivery

1. US1 + US2 → Foundation ready (catalog + inventory = complete tool reference)
2. Add US3 → ISSM guide (primary decision-maker documentation)
3. Add US4 + US5 → All persona guides + RMF phases (user workflow documentation)
4. Add US6–US9 → Reference docs, release notes, test docs (completeness)
5. Polish → Cross-references, nav updates, final verification
6. Each story adds value without breaking previous stories

### Single-Agent Sequential Strategy

1. Complete US1 (catalog) — 8 tasks, ~800 lines
2. Complete US2 (inventory) — 4 tasks, ~80 lines
3. Complete US3 (ISSM guide) — 5 tasks, ~350 lines
4. Complete US4 (persona guides) — 5 tasks across 5 files, ~510 lines
5. Complete US5 (RMF phases) — 5 tasks across 5 files, ~580 lines
6. Complete US6 (data model, glossary, NL queries) — 5 tasks, ~440 lines
7. Complete US7 (MCP server) — 1 task, ~60 lines
8. Complete US8 (release notes) — 2 tasks, ~400 lines (2 new files)
9. Complete US9 (testing guide) — 1 task, ~60 lines
10. Polish — 3 tasks

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story should be independently verifiable after completion
- Source files for parameter verification are listed in quickstart.md "Source Files Reference"
- RBAC role mappings are documented in research.md RT2
- Format patterns are documented in research.md RT3 and quickstart.md
- Cross-feature dependencies are documented in research.md RT6 and quickstart.md
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
