# Tasks: Comprehensive User & Persona Documentation

**Input**: Design documents from `/specs/016-user-documentation/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Not requested — this is a documentation-only feature. Validation is performed via `mkdocs build --strict`.

**Organization**: Tasks are grouped by user story to enable independent authoring and validation of each content area.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Documentation pages**: `docs/` at repository root
- **Site configuration**: `mkdocs.yml` at repository root
- **Spec reference**: `specs/016-user-documentation/spec.md` sections noted as `§N`

---

## Phase 1: Setup (Site Infrastructure)

**Purpose**: Initialize MkDocs configuration and establish the site skeleton so `mkdocs build` passes.

- [X] T001 Create MkDocs configuration with Material theme, navigation structure, and all extensions in mkdocs.yml
- [X] T002 [P] Add site/ to .gitignore to exclude generated static site output

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Create the landing page and onboarding hub that ALL other pages link to. These MUST exist before persona/phase pages can cross-link correctly.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T003 Create documentation site landing page with persona quick links, overview, and supported interfaces in docs/index.md
- [X] T004 [P] Create getting-started hub page with persona selector and prerequisites overview in docs/getting-started/index.md
- [X] T005 [P] Update existing getting-started.md to redirect to the new getting-started/index.md section in docs/getting-started.md

**Checkpoint**: `mkdocs build --strict` passes with landing page and getting-started hub navigable.

---

## Phase 3: User Story 1 — Per-Persona Onboarding (Priority: P1) 🎯 MVP

**Goal**: Each persona can find a dedicated getting-started page with prerequisites, first-time setup, and first 3 commands.

**Independent Test**: Navigate to Getting Started > {Persona} and verify prerequisites, setup steps, and 3 NL query examples render correctly. Each page conforms to `contracts/getting-started.md` template.

### Implementation for User Story 1

- [X] T006 [P] [US1] Create ISSM getting-started page with prerequisites, first-time setup, and first 3 commands in docs/getting-started/issm.md
- [X] T007 [P] [US1] Create ISSO getting-started page with prerequisites, first-time setup, and first 3 commands in docs/getting-started/isso.md
- [X] T008 [P] [US1] Create SCA getting-started page with prerequisites, first-time setup, and first 3 commands in docs/getting-started/sca.md
- [X] T009 [P] [US1] Create AO getting-started page with prerequisites, first-time setup, and first 3 commands in docs/getting-started/ao.md
- [X] T010 [P] [US1] Create Engineer getting-started page with prerequisites, first-time setup, and first 3 commands in docs/getting-started/engineer.md

**Checkpoint**: All 5 getting-started pages render and link back to the hub. `mkdocs build --strict` passes.

---

## Phase 4: User Story 2 — Comprehensive Persona Guides (Priority: P1)

**Goal**: Each persona has a full-length guide covering role overview, permissions, RMF phase workflows, NL queries, air-gapped notes, and cross-persona handoffs.

**Independent Test**: Navigate to Personas > {Persona Guide} and verify all sections from `contracts/persona-guide.md` are present. Tool names match codebase identifiers. Air-gapped callouts present where required.

### Implementation for User Story 2

- [X] T011 [P] [US2] Create persona overview page with RACI matrix and role definitions in docs/personas/index.md
- [X] T012 [P] [US2] Create ISSO comprehensive guide with Implement/Assess/Monitor workflows, Watch tools, and air-gapped notes in docs/personas/isso.md
- [X] T013 [P] [US2] Create Administrator guide with template management, configuration, and separation of duties in docs/personas/administrator.md
- [X] T014 [P] [US2] Enhance ISSM guide with getting-started cross-link, air-gapped Monitor notes, and cross-references to rmf-phases/ in docs/guides/issm-guide.md
- [X] T015 [P] [US2] Enhance SCA guide with RBAC constraints table, evidence integrity notes, and air-gapped Assess callout in docs/guides/sca-guide.md
- [X] T016 [P] [US2] Enhance AO guide with portfolio view section, risk expiration queries, and getting-started cross-link in docs/guides/ao-quick-reference.md
- [X] T017 [P] [US2] Enhance Engineer guide with Kanban remediation workflow, VS Code IaC diagnostics, and getting-started cross-link in docs/guides/engineer-guide.md
- [X] T018 [P] [US2] Enhance Compliance Watch guide with air-gapped monitoring notes and scheduled-only mode callout in docs/guides/compliance-watch.md

**Checkpoint**: All persona guides render. Cross-links to getting-started/ resolve. Air-gapped callouts present for ISSO (Implement), ISSM (Monitor), SCA (Assess).

---

## Phase 5: User Story 3 — RMF Phase Reference Pages (Priority: P1)

**Goal**: Each RMF phase has a dedicated page showing all persona responsibilities, gate conditions, documents produced, and transition triggers.

**Independent Test**: Navigate to RMF Phases > {Phase} and verify all personas with responsibilities in that phase have a subsection. Gate conditions reference real tools. Previous/Next phase links resolve. Each page conforms to `contracts/rmf-phase.md` template.

### Implementation for User Story 3

- [X] T019 [US3] Create RMF lifecycle overview page with phase diagram, gate summary table, and tool mapping in docs/rmf-phases/index.md
- [X] T020 [P] [US3] Create Prepare phase page with ISSM lead tasks, gate conditions, and boundary/role workflows in docs/rmf-phases/prepare.md
- [X] T021 [P] [US3] Create Categorize phase page with FIPS 199 workflow, info type suggestions, and IL derivation in docs/rmf-phases/categorize.md
- [X] T022 [P] [US3] Create Select phase page with baseline selection, tailoring, inheritance, CRM generation in docs/rmf-phases/select.md
- [X] T023 [P] [US3] Create Implement phase page with ISSO/Engineer narrative authoring, AI suggestions, and SSP generation in docs/rmf-phases/implement.md
- [X] T024 [P] [US3] Create Assess phase page with SCA assessment workflow, evidence verification, snapshot comparison, SAR/RAR generation in docs/rmf-phases/assess.md
- [X] T025 [P] [US3] Create Authorize phase page with AO decision types, risk acceptance, package bundling in docs/rmf-phases/authorize.md
- [X] T026 [P] [US3] Create Monitor phase page with ConMon plans, Watch integration, expiration alerts, significant changes, eMASS/OSCAL export in docs/rmf-phases/monitor.md

**Checkpoint**: All 7 phase pages + overview render. Previous/Next links form a connected chain. Gate conditions match spec §10.1.

---

## Phase 6: User Story 4 — Cross-Cutting Guides (Priority: P1)

**Goal**: Users can look up NL query examples by category, find the complete document production catalog, and manage multi-system portfolios.

**Independent Test**: Navigate to Guides > {Guide} and verify content is comprehensive. NL query reference covers all 11 categories from spec §12. Document catalog covers all 10 document types from spec §11. Portfolio management covers dashboard, bulk ops, and delegation from spec §9.4.

### Implementation for User Story 4

- [X] T027 [P] [US4] Create NL query reference with all 11 categories (registration, categorization, baseline, SSP, assessment, authorization, monitoring, Watch, Kanban, knowledge, PIM) in docs/guides/nl-query-reference.md
- [X] T028 [P] [US4] Create document production catalog with all 10 document types, SSP/SAR/RAR/POA&M section breakdowns, and template system in docs/guides/document-catalog.md
- [X] T029 [P] [US4] Create portfolio management guide with dashboard, bulk operations, delegation patterns, and AO portfolio view in docs/guides/portfolio-management.md

**Checkpoint**: All 3 guide pages render. NL queries use realistic examples. Document catalog covers every artifact from spec §11.

---

## Phase 7: User Story 5 — Reference Material (Priority: P2)

**Goal**: Users can look up any of the 114 MCP tools, troubleshoot 25+ common errors, use quick reference cards, and find glossary definitions.

**Independent Test**: Navigate to Reference > {Page} and verify completeness. Tool inventory has all 114 tools grouped by 8 categories. Troubleshooting has 7 error categories with 25+ scenarios. Quick reference cards cover all 5 personas + admin. Glossary includes all Appendix C terms.

### Implementation for User Story 5

- [X] T030 [P] [US5] Create complete tool inventory with all 114 MCP tools grouped by 8 categories (RMF Lifecycle, ConMon, Interop, Templates, Core, Watch, Kanban, PIM) with RBAC roles and phase applicability in docs/reference/tool-inventory.md
- [X] T031 [P] [US5] Create troubleshooting guide with 7 error categories (RBAC, gates, evidence, authorization, connectivity, monitoring, Kanban) covering 25+ error scenarios in docs/reference/troubleshooting.md
- [X] T032 [P] [US5] Create quick reference cards for ISSM, SCA, AO, Engineer, and ISSO with top NL queries, key tools, and phase responsibilities in docs/reference/quick-reference-cards.md
- [X] T033 [P] [US5] Expand glossary with ~10 new terms from spec Appendix C (ATOwC, CNSSI 1253, ConMon, CRM, DATO, IATT, IL, MCP, OSCAL, PIM) in docs/reference/glossary.md

**Checkpoint**: All 4 reference pages render. Tool names match codebase identifiers exactly. Troubleshooting cross-links to persona guides.

---

## Phase 8: User Story 6 — End-to-End Scenario (Priority: P2)

**Goal**: Users can follow a complete RMF lifecycle scenario from system registration through continuous monitoring, seeing all personas interact.

**Independent Test**: Navigate to Scenarios > Full RMF Lifecycle and verify the scenario covers all 7 phases, involves all 5 operational personas, shows realistic NL queries and tool responses, and matches the ACME Portal scenario from spec Appendix B.

### Implementation for User Story 6

- [X] T034 [US6] Create full RMF lifecycle scenario (ACME Portal) covering Day 1 registration through ongoing ConMon with all persona interactions in docs/scenarios/full-lifecycle.md

**Checkpoint**: Scenario page renders with all phases and persona handoffs. Cross-links to getting-started and persona guides resolve.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Validate all cross-links, build integrity, air-gapped consistency, and tool name accuracy.

- [X] T035 Validate all internal cross-links against data-model.md Cross-Reference Map across all documentation pages
- [X] T036 Run mkdocs build --strict and fix all warnings and errors across all documentation files
- [X] T037 Review all air-gapped callouts (🔒) for consistency across persona guides and RMF phase pages
- [X] T038 Verify all MCP tool name references match codebase identifiers in ComplianceMcpTools.cs across tool-inventory.md and persona guides

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup (mkdocs.yml must exist) — BLOCKS all user stories
- **US1 Getting Started (Phase 3)**: Depends on Foundational (getting-started hub must exist)
- **US2 Persona Guides (Phase 4)**: Depends on Foundational (index.md must exist for cross-linking)
- **US3 RMF Phase Pages (Phase 5)**: Depends on US2 (persona guides must exist for cross-linking)
- **US4 Cross-Cutting Guides (Phase 6)**: Depends on US2 (persona guides referenced by NL queries and document catalog)
- **US5 Reference Material (Phase 7)**: Depends on Foundational only (can start after Phase 2)
- **US6 Scenario (Phase 8)**: Depends on US1 + US2 (references getting-started and persona guides)
- **Polish (Phase 9)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (Getting Started)**: After Foundational → Independent of other stories
- **US2 (Persona Guides)**: After Foundational → Independent of other stories
- **US3 (RMF Phases)**: After US2 → Cross-links to persona guides
- **US4 (Cross-Cutting Guides)**: After US2 → References persona-specific tool usage
- **US5 (Reference Material)**: After Foundational → Independent of other stories
- **US6 (Scenario)**: After US1 + US2 → References both getting-started and persona guides

### Within Each User Story

- All pages marked [P] within a story can be authored in parallel (different files)
- Overview/index pages should be created before detail pages (for cross-linking)
- Enhancement tasks (existing files) are independent of new page creation

### Parallel Opportunities

- T001 and T002 (Setup) can run in parallel
- T003, T004, T005 (Foundational) — T004 and T005 are parallel; T003 is independent
- All US1 tasks (T006–T010) can run in parallel — different files, same template
- All US2 tasks (T011–T018) can run in parallel — different files, no dependencies
- All US3 tasks (T020–T026) can run in parallel after T019 (overview is the hub)
- All US4 tasks (T027–T029) can run in parallel — different files
- All US5 tasks (T030–T033) can run in parallel — different files
- US1, US2, and US5 can all run in parallel after Foundational completes
- US3 and US4 can run in parallel once US2 is complete

---

## Parallel Example: User Story 1 (Getting Started)

```bash
# All 5 getting-started pages can be authored simultaneously:
Task T006: "Create ISSM getting-started in docs/getting-started/issm.md"
Task T007: "Create ISSO getting-started in docs/getting-started/isso.md"
Task T008: "Create SCA getting-started in docs/getting-started/sca.md"
Task T009: "Create AO getting-started in docs/getting-started/ao.md"
Task T010: "Create Engineer getting-started in docs/getting-started/engineer.md"
```

## Parallel Example: User Story 2 (Persona Guides)

```bash
# All 8 persona guide tasks can run simultaneously:
Task T011: "Create persona overview in docs/personas/index.md"
Task T012: "Create ISSO guide in docs/personas/isso.md"
Task T013: "Create Administrator guide in docs/personas/administrator.md"
Task T014: "Enhance ISSM guide in docs/guides/issm-guide.md"
Task T015: "Enhance SCA guide in docs/guides/sca-guide.md"
Task T016: "Enhance AO guide in docs/guides/ao-quick-reference.md"
Task T017: "Enhance Engineer guide in docs/guides/engineer-guide.md"
Task T018: "Enhance Compliance Watch in docs/guides/compliance-watch.md"
```

## Parallel Example: After Foundational Completes

```bash
# Three user stories can start simultaneously after Phase 2:
Stream A: US1 (T006–T010) — Getting Started pages
Stream B: US2 (T011–T018) — Persona Guides
Stream C: US5 (T030–T033) — Reference Material
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (mkdocs.yml)
2. Complete Phase 2: Foundational (index.md, getting-started hub, redirect)
3. Complete Phase 3: US1 — Getting Started pages
4. **STOP and VALIDATE**: `mkdocs build --strict` passes, all 5 persona getting-started pages navigable
5. Deploy/demo if ready — users can onboard immediately

### Incremental Delivery

1. Complete Setup + Foundational → Site skeleton live
2. Add US1 (Getting Started) → Users can onboard → **MVP!**
3. Add US2 (Persona Guides) → Full persona documentation → Deploy/Demo
4. Add US3 (RMF Phases) → Phase-by-phase reference → Deploy/Demo
5. Add US4 (Cross-Cutting Guides) → NL queries, documents, portfolio → Deploy/Demo
6. Add US5 (Reference Material) → Tool inventory, troubleshooting, quick ref → Deploy/Demo
7. Add US6 (Scenario) → End-to-end walkthrough → Deploy/Demo
8. Polish → All cross-links validated, build clean → **Final Release**

### Parallel Team Strategy

With multiple authors:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Author A: US1 (Getting Started) — 5 pages
   - Author B: US2 (Persona Guides) — 8 pages
   - Author C: US5 (Reference Material) — 4 pages
3. After US2 completes:
   - Author A: US3 (RMF Phases) — 8 pages
   - Author B: US4 (Cross-Cutting Guides) — 3 pages
   - Author C: US6 (Scenario) — 1 page
4. All complete → Polish phase together

---

## Notes

- [P] tasks = different files, no dependencies between them
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and verifiable via `mkdocs build --strict`
- No tests are generated — validation is `mkdocs build --strict` + manual review
- All page content should follow the corresponding template in `contracts/`
- Tool names MUST match the exact MCP tool identifiers from `ComplianceMcpTools.cs`
- Air-gapped callouts (🔒) are required for: ISSO Implement, ISSM Monitor, SCA Assess, Interface Guide
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
