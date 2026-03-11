# Tasks: HW/SW Inventory Management

**Input**: Design documents from `/specs/025-hw-sw-inventory/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/inventory-service.md, quickstart.md

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the entity model, service interface, and database configuration that all user stories depend on

- [X] T001 [P] Create InventoryItem entity, 4 enums (InventoryItemType, InventoryItemStatus, HardwareFunction, SoftwareFunction), 5 computed types (InventoryCompleteness, InventoryIssue, UnmatchedBoundaryResource, InventoryImportResult, ImportRowError), and 3 input types (InventoryItemInput, InventoryListOptions, InventoryExportOptions) in src/Ato.Copilot.Core/Models/Compliance/InventoryModels.cs
- [X] T002 [P] Create IInventoryService interface with all 9 method signatures (AddItemAsync, UpdateItemAsync, DecommissionItemAsync, GetItemAsync, ListItemsAsync, ExportToExcelAsync, ImportFromExcelAsync, CheckCompletenessAsync, AutoSeedFromBoundaryAsync) in src/Ato.Copilot.Core/Interfaces/Compliance/IInventoryService.cs
- [X] T003 Add DbSet<InventoryItem> and OnModelCreating EF Core configuration (string-backed enum conversions, indexes on RegisteredSystemId+Type, unique IP index, self-referencing ParentHardwareId FK, optional BoundaryResourceId FK) in src/Ato.Copilot.Core/Data/Context/AtoCopilotContext.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Create service and tool scaffolding with DI registration so user story implementation can begin

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Create InventoryService class implementing IInventoryService with constructor injection (IServiceScopeFactory, ILogger<InventoryService>) and stub methods throwing NotImplementedException in src/Ato.Copilot.Agents/Compliance/Services/InventoryService.cs
- [X] T005 Create InventoryTools.cs file with BaseTool scaffolding for all 9 tool classes (InventoryAddItemTool, InventoryUpdateItemTool, InventoryDecommissionItemTool, InventoryListTool, InventoryGetTool, InventoryExportTool, InventoryImportTool, InventoryCompletenessTool, InventoryAutoSeedTool) with Name, Description, and Parameters definitions in src/Ato.Copilot.Agents/Compliance/Tools/InventoryTools.cs
- [X] T006 Register IInventoryService as singleton, InventoryService as singleton, and all 9 InventoryTool classes using the dual-registration pattern (AddSingleton<ToolType>() + AddSingleton<BaseTool>(sp => sp.GetRequiredService<ToolType>())) in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs

**Checkpoint**: Foundation ready — user story implementation can now begin

---

## Phase 3: User Story 1 — Register & Manage HW/SW Items (Priority: P1) 🎯 MVP

**Goal**: ISSO can add, update, decommission, and retrieve hardware and software inventory items. Auto-seed from boundary resources provides a quick-start path.

**Independent Test**: Add a hardware item and a linked software item, update fields, decommission the hardware item → verify software is also decommissioned. Run auto-seed on a system with boundary resources → verify inventory items are created and re-running does not duplicate.

### Implementation for User Story 1

- [X] T007 [US1] Implement AddItemAsync with function-based required field validation (FR-018: all HW requires name+function+manufacturer; server/network requires +IP; all SW requires name+vendor+version), unique IP constraint within system (FR-016), system existence check, and ParentHardwareId validation in src/Ato.Copilot.Agents/Compliance/Services/InventoryService.cs
- [X] T008 [US1] Implement UpdateItemAsync with partial field update (null fields unchanged), modified timestamp and modifier identity tracking (FR-003), IP uniqueness re-validation, and ITEM_NOT_FOUND error handling in src/Ato.Copilot.Agents/Compliance/Services/InventoryService.cs
- [X] T009 [US1] Implement DecommissionItemAsync with soft-deletion setting DecommissionedDate and DecommissionRationale, cascade decommission of all child software items with same rationale (FR-004, FR-005), and ALREADY_DECOMMISSIONED rejection if item is already decommissioned, in src/Ato.Copilot.Agents/Compliance/Services/InventoryService.cs
- [X] T010 [US1] Implement GetItemAsync with eager-loading of InstalledSoftware navigation property for hardware items (FR-007), returning null when item does not exist, in src/Ato.Copilot.Agents/Compliance/Services/InventoryService.cs
- [X] T011 [US1] Implement AutoSeedFromBoundaryAsync with boundary resource querying, ResourceType-to-HardwareFunction mapping (VMs→Server, Network→NetworkDevice, Storage→Storage, else→Other), BoundaryResourceId FK for idempotency (FR-013, FR-014), and NO_BOUNDARY_DATA error handling in src/Ato.Copilot.Agents/Compliance/Services/InventoryService.cs
- [X] T012 [US1] Implement inventory_add_item tool ExecuteAsync — parse system_id/type/item_name/function and optional fields, call AddItemAsync, return created item in standard envelope in src/Ato.Copilot.Agents/Compliance/Tools/InventoryTools.cs
- [X] T013 [US1] Implement inventory_update_item tool ExecuteAsync — parse item_id and optional update fields, call UpdateItemAsync, return updated item in src/Ato.Copilot.Agents/Compliance/Tools/InventoryTools.cs
- [X] T014 [US1] Implement inventory_decommission_item tool ExecuteAsync — parse item_id and rationale, call DecommissionItemAsync, return decommissioned item in standard envelope with cascaded child count in metadata in src/Ato.Copilot.Agents/Compliance/Tools/InventoryTools.cs
- [X] T015 [US1] Implement inventory_get tool ExecuteAsync — parse item_id, call GetItemAsync, return item with software children in src/Ato.Copilot.Agents/Compliance/Tools/InventoryTools.cs
- [X] T016 [US1] Implement inventory_auto_seed tool ExecuteAsync — parse system_id, call AutoSeedFromBoundaryAsync, return list of created items in src/Ato.Copilot.Agents/Compliance/Tools/InventoryTools.cs

**Checkpoint**: US1 complete — ISSO can register, manage, and auto-seed inventory items

---

## Phase 4: User Story 2 — Query & Filter Inventory (Priority: P2)

**Goal**: ISSO can list and search inventory items with filtering by type, function, vendor, status, and free-text name search with pagination.

**Independent Test**: Add several hardware and software items with different functions/vendors, then filter by type=hardware, filter by function=server, search by name substring, and paginate results.

### Implementation for User Story 2

- [X] T017 [US2] Implement ListItemsAsync with IQueryable filtering by Type, Function (string match against HardwareFunction/SoftwareFunction), Vendor/Manufacturer (contains), Status (default Active), free-text SearchText on ItemName (contains), and PageSize/PageNumber pagination (FR-006) in src/Ato.Copilot.Agents/Compliance/Services/InventoryService.cs
- [X] T018 [US2] Implement inventory_list tool ExecuteAsync — parse system_id and optional filter parameters (type, function, vendor, status, search, page_size, page), call ListItemsAsync, return paginated results in src/Ato.Copilot.Agents/Compliance/Tools/InventoryTools.cs

**Checkpoint**: US2 complete — ISSO can query and filter inventory

---

## Phase 5: User Story 3 — Export to eMASS Excel (Priority: P2)

**Goal**: ISSO can export hardware and software inventory to an eMASS-compatible Excel workbook with separate Hardware and Software worksheets.

**Independent Test**: Add hardware and software items (including one decommissioned), export with default settings → verify two worksheets with correct column headers and only active items. Export with include_decommissioned=true → verify decommissioned items appear.

### Implementation for User Story 3

- [X] T019 [US3] Implement ExportToExcelAsync with ClosedXML — create workbook with Hardware worksheet (columns: System Name, Hardware Name, Manufacturer, Model, Serial Number, Function, IP Address, MAC Address, Location, Status per FR-008) and Software worksheet (columns: System Name, Software Name, Vendor, Version, Patch Level, Function, License Type, Installed On, Status per FR-009), support ExportType filter (hardware/software/all per FR-010), exclude decommissioned by default with IncludeDecommissioned option (FR-011), and return byte array with NO_INVENTORY_DATA error for empty inventory in src/Ato.Copilot.Agents/Compliance/Services/InventoryService.cs
- [X] T020 [US3] Implement inventory_export tool ExecuteAsync — parse system_id, optional export_type and include_decommissioned, call ExportToExcelAsync, return base64-encoded workbook in src/Ato.Copilot.Agents/Compliance/Tools/InventoryTools.cs

**Checkpoint**: US3 complete — ISSO can export inventory to eMASS Excel

---

## Phase 6: User Story 6 — Import from Excel (Priority: P2)

**Goal**: ISSO can import existing HW/SW inventory from an Excel workbook using the same eMASS column format as the export, with dry-run mode and partial import with row-level error reporting.

**Independent Test**: Create an Excel file matching the export format with valid and invalid rows, import with dry_run=true → verify report without persistence. Import without dry_run → verify valid rows created, invalid rows reported with row numbers and error details. Re-import same file → verify duplicates are skipped.

### Implementation for User Story 6

- [X] T021 [US6] Implement ImportFromExcelAsync with ClosedXML parsing — read Hardware and Software worksheets using eMASS column headers (FR-019), process Hardware worksheet first so SW "Installed On" references resolve (edge case), apply FR-018 validation per row, skip and report invalid rows with row number and error detail (FR-020), skip duplicates (same name+type as existing), support dry-run mode returning InventoryImportResult without persisting (FR-021), and return created/skipped/error counts in src/Ato.Copilot.Agents/Compliance/Services/InventoryService.cs
- [X] T022 [US6] Implement inventory_import tool ExecuteAsync — parse system_id, file_base64, and optional dry_run flag, decode base64 to byte array, call ImportFromExcelAsync, return import result summary in src/Ato.Copilot.Agents/Compliance/Tools/InventoryTools.cs

**Checkpoint**: US6 complete — ISSO can import inventory from Excel with round-trip compatibility

---

## Phase 7: User Story 4 — Completeness Check (Priority: P3)

**Goal**: ISSO can run a completeness check that identifies missing required fields per item, boundary resources without inventory entries, and hardware items without any software entries.

**Independent Test**: Add items with some missing optional fields, add boundary resources without corresponding inventory, add HW without SW → run completeness check → verify all three issue categories are reported with accurate counts.

### Implementation for User Story 4

- [X] T023 [US4] Implement CheckCompletenessAsync — query all active inventory items and all boundary resources for the system, compute three issue dimensions: (1) per-item missing required fields based on FR-018 rules returning InventoryIssue list, (2) boundary resources with no matching BoundaryResourceId FK returning UnmatchedBoundaryResource list, (3) hardware items with no software children, aggregate into InventoryCompleteness result with TotalItems/IssueCount/IsComplete flag (FR-012) in src/Ato.Copilot.Agents/Compliance/Services/InventoryService.cs
- [X] T024 [US4] Implement inventory_completeness tool ExecuteAsync — parse system_id, call CheckCompletenessAsync, return completeness result with issue details in src/Ato.Copilot.Agents/Compliance/Tools/InventoryTools.cs

**Checkpoint**: US4 complete — ISSO can assess inventory completeness before export

---

## Phase 8: User Story 5 — SSP Integration (Priority: P3)

**Goal**: Generated SSP document includes HW/SW inventory tables in the §11 authorization boundary section.

**Independent Test**: Add hardware and software items to a system, generate SSP → verify §11 contains HW inventory table and SW inventory table with correct data.

### Implementation for User Story 5

- [X] T025 [US5] Integrate HW/SW inventory into SSP generation — inject IInventoryService into SspService, query active inventory items for the system, render hardware inventory table and software inventory table in the §11 boundary section of the SSP document (FR-015) in src/Ato.Copilot.Agents/Compliance/Services/SspService.cs

**Checkpoint**: US5 complete — SSP §11 includes HW/SW inventory tables

---

## Phase 9: Testing

**Purpose**: Unit and integration tests for InventoryService and InventoryTools following xUnit + FluentAssertions + Moq patterns

- [X] T026 Create InventoryServiceTests.cs with unit tests covering: AddItemAsync (valid HW, valid SW, function-based validation failures, duplicate IP rejection, missing parent HW rejection, SaaS SW without parent), UpdateItemAsync (partial update, modified timestamp, IP uniqueness), DecommissionItemAsync (soft-delete, cascade to child SW, already-decommissioned rejection), GetItemAsync (with InstalledSoftware children, not found), AutoSeedFromBoundaryAsync (create from boundary, idempotent re-run, no boundary data error), ListItemsAsync (filter by type/function/vendor/status/search, pagination), ExportToExcelAsync (HW+SW worksheets, decommissioned filter, empty inventory error), ImportFromExcelAsync (valid import, partial import with row errors, dry-run mode, duplicate skip, HW-before-SW ordering), CheckCompletenessAsync (missing fields, unmatched boundary, HW without SW, all-complete scenario) in tests/Ato.Copilot.Tests.Unit/Compliance/InventoryServiceTests.cs
- [X] T027 [P] Create InventoryToolTests.cs with unit tests for all 9 MCP tools verifying: correct parameter parsing, service method delegation, standard response envelope format, error propagation with errorCode and suggestion in tests/Ato.Copilot.Tests.Unit/Tools/InventoryToolTests.cs
- [X] T028 [P] Create InventoryIntegrationTests.cs with integration tests for all 9 MCP tools (inventory_add_item, inventory_update_item, inventory_decommission_item, inventory_list, inventory_get, inventory_export, inventory_import, inventory_completeness, inventory_auto_seed) covering happy-path and at least one error-path per tool in tests/Ato.Copilot.Tests.Integration/Compliance/InventoryIntegrationTests.cs

---

## Phase 10: Documentation

**Purpose**: Update all documentation files specified in the feature specification

- [X] T029 [P] Add all 9 inventory MCP tools (inventory_add_item, inventory_update_item, inventory_decommission_item, inventory_list, inventory_get, inventory_export, inventory_import, inventory_completeness, inventory_auto_seed) with parameter descriptions, example invocations, and return types in docs/api/mcp-server.md
- [X] T030 [P] Add InventoryItem entity with all fields, RegisteredSystem FK, optional ParentHardwareId FK, optional BoundaryResourceId FK, and InventoryCompleteness computed type in docs/architecture/data-model.md
- [X] T031 [P] Register all 9 inventory tools in the tool catalog with capability descriptions, required parameters, and persona access mappings (ISSO, Engineer, SCA) in docs/architecture/agent-tool-catalog.md
- [X] T032 [P] Update architecture overview to include InventoryService in the system component diagram in docs/architecture/overview.md
- [X] T033 [P] Add "Managing HW/SW Inventory" section covering end-to-end workflow (auto-seed → add/edit → completeness check → export → import) in docs/guides/isso-guide.md
- [X] T034 [P] Add inventory registration guidance for engineers registering components and keeping versions current in docs/guides/engineer-guide.md
- [X] T035 [P] Add inventory review guidance for SCAs reviewing completeness and verifying HW/SW coverage before assessment in docs/guides/sca-guide.md
- [X] T036 [P] Update ISSO getting-started guide to mention HW/SW inventory as a core capability with link to detailed guide in docs/getting-started/isso.md
- [X] T037 [P] Update Engineer getting-started guide to mention component registration in docs/getting-started/engineer.md
- [X] T038 [P] Add terms (Hardware Inventory, Software Inventory, Inventory Completeness Check, Auto-Seed, eMASS HW/SW Export) in docs/reference/glossary.md
- [X] T039 [P] Add validation test cases for all 9 inventory MCP tools covering expected inputs, outputs, and error scenarios per persona in docs/persona-test-cases/tool-validation.md
- [X] T040 [P] Add seed data instructions for HW/SW inventory items (sample hardware, software, parent-child relationships, boundary linkages) in docs/persona-test-cases/test-data-setup.md
- [X] T041 [P] Add checklist items verifying inventory tools are registered, data is seeded, and eMASS export/import round-trip is functional in docs/persona-test-cases/environment-checklist.md
- [X] T042 [P] Add inventory-related rows to the results template for tracking pass/fail status in docs/persona-test-cases/results-template.md
- [X] T043 [P] Add test scenarios for ISSO inventory workflows (add/edit, completeness check, export, import) in docs/persona-test-cases/scripts/isso-test-script.md
- [X] T044 [P] Add test scenarios for Engineer registering components and updating versions in docs/persona-test-cases/scripts/engineer-test-script.md
- [X] T045 [P] Add test scenarios for SCA reviewing inventory completeness before assessment in docs/persona-test-cases/scripts/sca-test-script.md
- [X] T046 [P] Add test scenarios for ISSM reviewing inventory status across systems in portfolio in docs/persona-test-cases/scripts/issm-test-script.md
- [X] T047 [P] Add cross-persona inventory scenarios (Engineer registers → ISSO completeness check → SCA verifies → ISSO exports) in docs/persona-test-cases/scripts/cross-persona-test-script.md
- [X] T048 [P] Add HW/SW inventory steps to the unified RMF lifecycle test flow (Categorize/Implement/Assess phases) in docs/persona-test-cases/scripts/unified-rmf-test-script.md

---

## Phase 11: Polish & Cross-Cutting Concerns

**Purpose**: Final verification and validation

- [X] T049 Verify solution builds with zero errors and all existing + new unit tests pass
- [X] T050 Run quickstart.md 7-step workflow end-to-end validation (auto-seed → add HW → add SW → list → completeness → export → import)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup (Phase 1) — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Foundational (Phase 2) — MVP, implement first
- **US2 (Phase 4)**: Depends on Foundational (Phase 2) — can start after Phase 2, independent of US1
- **US3 (Phase 5)**: Depends on Foundational (Phase 2) — can start after Phase 2, independent of US1/US2
- **US6 (Phase 6)**: Depends on Phase 5 (US3) for round-trip format compatibility verification
- **US4 (Phase 7)**: Depends on Foundational (Phase 2) — can start after Phase 2, independent of other stories
- **US5 (Phase 8)**: Depends on Foundational (Phase 2) — can start after Phase 2, requires inventory data for SSP rendering
- **Testing (Phase 9)**: Depends on all user stories (Phases 3–8) being complete
- **Documentation (Phase 10)**: Depends on all user stories (Phases 3–8) being complete
- **Polish (Phase 11)**: Depends on Testing and Documentation (Phases 9–10)

### User Story Dependencies

- **US1 (P1)**: No dependencies on other stories — MVP deliverable
- **US2 (P2)**: Independent — uses same entity/service, no cross-story dependencies
- **US3 (P2)**: Independent — export reads from existing inventory data
- **US6 (P2)**: Soft dependency on US3 — uses same eMASS column format for round-trip compatibility
- **US4 (P3)**: Independent — completeness check reads inventory + boundary data
- **US5 (P3)**: Independent — SSP integration reads inventory data

### Within Each User Story

- Service methods before tools (tools call service methods)
- AddItemAsync first in US1 (establishes validation patterns used by other methods)
- Core CRUD (T007–T010) before auto-seed (T011) since auto-seed creates items

### Parallel Opportunities

- T001 and T002 can run in parallel (different files)
- All Phase 10 documentation tasks (T029–T048) can run in parallel (different files)
- T026, T027, and T028 can run in parallel (different test files)
- After Phase 2 completes, US1–US5 can theoretically start in parallel (if staffed), though sequential P1→P2→P3 is recommended for single-implementor flow

---

## Parallel Example: User Story 1

```text
After Phase 2 completes:

Sequential (same file — InventoryService.cs):
  T007 → T008 → T009 → T010 → T011

Sequential (same file — InventoryTools.cs):
  T012 → T013 → T014 → T015 → T016

These two sequences CAN run in parallel (different files):
  [InventoryService.cs] T007 → T008 → T009 → T010 → T011
  [InventoryTools.cs]   T012 → T013 → T014 → T015 → T016
```

---

## Implementation Strategy

- **MVP Scope**: Phase 1 + Phase 2 + Phase 3 (US1) — delivers core CRUD and auto-seed capability
- **Incremental Delivery**: Each user story phase produces an independently testable increment
- **Recommended Order**: Phases 1–8 sequentially (P1 → P2 → P3), then Phase 9 (tests), Phase 10 (docs), Phase 11 (polish)
- **Total Tasks**: 50
