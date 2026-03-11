# Research: Hardware/Software Inventory

**Feature**: 025-hw-sw-inventory | **Date**: 2026-03-11

---

## R1: eMASS HW/SW Worksheet Column Format

**Decision**: Use standard DoD eMASS Excel column headers for both export and import (round-trip compatibility).

**Rationale**: The existing `EmassExportService` already uses eMASS-standard column headers for Controls (25 columns) and POA&M (24 columns). The HW/SW inventory export follows the same pattern. The eMASS HW/SW import template uses fixed column headers that vary slightly between eMASS versions but follow a stable core set.

**Hardware Worksheet Columns** (10 columns):
1. System Name
2. Hardware Name
3. Manufacturer
4. Model
5. Serial Number
6. Function (Server, Workstation, Network Device, Storage, Other)
7. IP Address
8. MAC Address
9. Location
10. Status (Active, Decommissioned)

**Software Worksheet Columns** (10 columns):
1. System Name
2. Software Name
3. Vendor
4. Version
5. Patch Level
6. Function (Operating System, Database, Middleware, Application, Security Tool, Other)
7. License Type
8. Installed On (hardware item name reference)
9. Status (Active, Decommissioned)

**Alternatives Considered**:
- CSV format: Rejected — eMASS expects .xlsx; ClosedXML already in use.
- Custom column schema: Rejected — would break eMASS import compatibility.

---

## R2: Entity Model Design — Single Table vs. Separate HW/SW Tables

**Decision**: Use a single `InventoryItem` entity with a `Type` discriminator (`Hardware` / `Software`).

**Rationale**: Hardware and software items share 80% of their fields (name, function, status, system FK, audit fields). The distinguishing fields (serial number, IP, MAC for HW; vendor, version, patch level, license type, parent HW FK for SW) are stored as nullable columns. This follows the existing codebase pattern where entities like `ComplianceFinding` use a type discriminator rather than separate tables. A single table simplifies queries (list all inventory), filtering (by type), export (single DbSet), and the parent-child relationship (self-referencing FK on same table).

**Alternatives Considered**:
- Separate `HardwareItem` and `SoftwareItem` tables with a shared base: Rejected — adds complexity for joined queries, requires two DbSets, complicates the parent-child FK (cross-table), and the EF Core TPH/TPT overhead isn't justified for this schema.
- EF Core TPH inheritance: Rejected — introduces discriminator column management complexity and limits EF query flexibility with no meaningful benefit over a simple `Type` enum field.

---

## R3: eMASS Excel Import Pattern

**Decision**: Extend the existing `EmassExportService` import pattern to support HW/SW worksheets with dry-run, partial import, and row-level error reporting.

**Rationale**: The existing `EmassExportService.ImportAsync()` already demonstrates the import pattern:
- Accepts `byte[]` file content + system ID + options (including `DryRun`)
- Opens `XLWorkbook(stream)`, detects worksheet type by header inspection
- Processes rows, maps columns to entities, validates, persists
- Returns a result object with counts and errors

The HW/SW import follows the same shape:
1. Open workbook and look for "Hardware" and "Software" worksheets (by name) or detect by column headers
2. Process "Hardware" worksheet first (so HW items exist when SW rows reference them via "Installed On")
3. Validate each row against FR-018 (function-based required fields)
4. In dry-run mode: collect results without calling `SaveChangesAsync`
5. Return `InventoryImportResult` with created/skipped counts and per-row errors

**Alternatives Considered**:
- CSV import: Rejected — eMASS uses xlsx; would require a separate parser.
- Streaming import (row-by-row save): Rejected — batch save is simpler, atomically consistent, and inventory sizes are small (< 1000 rows).

---

## R4: SSP Integration Strategy

**Decision**: Integrate HW/SW inventory data into the SSP by auto-generating content for the authorization boundary section (§11) when generating the SSP document.

**Rationale**: The existing `SspService` generates 13 sections per NIST 800-18. Section 11 ("Authorization Boundary") already describes the system boundary. HW/SW inventory tables are a natural sub-section of the boundary description. The `GenerateSspAsync` method assembles section content by querying the database — adding inventory queries follows the same pattern.

Implementation approach:
- When generating §11 (authorization_boundary), query `InventoryItems` for the system
- Render hardware items as a Markdown table within the section
- Render software items as a second Markdown table
- If no inventory items exist, include a placeholder: "Inventory not yet populated"

**Alternatives Considered**:
- Add new SSP sections (§14, §15) for HW/SW: Rejected — NIST 800-18 specifies 13 sections; adding more breaks the standard structure.
- Generate a separate HW/SW appendix document: Rejected — the spec requires integration within the SSP, not a separate document.

---

## R5: Auto-Seed from Authorization Boundary

**Decision**: Map `AuthorizationBoundary` resources to `InventoryItem` entries using a direct FK (`BoundaryResourceId`) for idempotency.

**Rationale**: The `AuthorizationBoundary` entity has: `ResourceId` (Azure resource ID), `ResourceType` (Azure resource type string), `ResourceName` (display name). Auto-seeding creates one HW `InventoryItem` per in-scope boundary resource:

| Boundary Field | → InventoryItem Field |
|----------------|----------------------|
| `ResourceName` | `ItemName` |
| `ResourceType` → mapped | `Function` (Server, Network Device, Storage, etc.) |
| `Id` | `BoundaryResourceId` (FK for idempotency) |

Azure resource type mapping heuristic:
- `Microsoft.Compute/virtualMachines` → Server
- `Microsoft.Network/*` → Network Device
- `Microsoft.Storage/*` → Storage
- Everything else → Other

The `BoundaryResourceId` FK ensures re-running auto-seed only creates items for new/unmatched boundary resources (FR-014).

**Alternatives Considered**:
- Match by name to detect duplicates: Rejected — names can be changed; FK gives stable identity.
- Store mapping in a separate join table: Rejected — unnecessary complexity for a simple optional FK.

---

## R6: Function Classification Enums

**Decision**: Create two enums — `HardwareFunction` and `SoftwareFunction` — with string-backed EF Core value conversion.

**Rationale**: The spec defines fixed function categories for hardware (Server, Workstation, Network Device, Storage, Other) and software (Operating System, Database, Middleware, Application, Security Tool, Other). These map directly to DoD SSP inventory templates. Using enums provides compile-time safety and consistent serialization. String-backed storage (via EF Core `EnumToStringConverter`) ensures database readability and avoids breaking changes when enum values are reordered.

**Enum Definitions**:
```csharp
public enum HardwareFunction { Server, Workstation, NetworkDevice, Storage, Other }
public enum SoftwareFunction { OperatingSystem, Database, Middleware, Application, SecurityTool, Other }
public enum InventoryItemType { Hardware, Software }
public enum InventoryItemStatus { Active, Decommissioned }
```

**Alternatives Considered**:
- Free-text function field: Rejected — would produce inconsistent values, complicating export column mapping.
- Single `InventoryFunction` enum combining HW + SW: Rejected — conflates two distinct classification domains; a Server is not the same dimension as an Operating System.

---

## R7: Completeness Check Design

**Decision**: Implement as a computed result (not persisted) returned by `IInventoryService.CheckCompletenessAsync()`.

**Rationale**: The completeness check aggregates three dimensions:
1. **Missing required fields** — per FR-018 validation rules applied to all items
2. **Unmatched boundary resources** — boundary resources with no corresponding inventory item (by `BoundaryResourceId` FK)
3. **Hardware without software** — HW items with no SW children

This is a read-only analysis that should reflect current state, not a cached snapshot. It returns an `InventoryCompleteness` result type with counts, a pass/fail flag, and per-item issue lists.

**Alternatives Considered**:
- Persist completeness results: Rejected — would become stale immediately after any inventory change.
- Trigger completeness check on every mutation: Rejected — unnecessary overhead; check is on-demand per the spec.

---

## R8: Service Architecture — New Service vs. Extend Existing

**Decision**: Create a new `IInventoryService` / `InventoryService` rather than extending `IEmassExportService` or `IBoundaryService`.

**Rationale**: The inventory capability is a distinct domain concern with its own CRUD operations, validation rules, query patterns, and export format. It touches boundary data (auto-seed) and eMASS export (HW/SW worksheets) but has its own lifecycle. A dedicated service keeps responsibilities clean:
- `IInventoryService`: CRUD, query, completeness check, auto-seed, import, export
- `IEmassExportService`: Controls + POA&M export/import (unchanged)
- `IBoundaryService`: Boundary resource management (unchanged)

The inventory service follows the same pattern as other services: constructor injection of `IServiceScopeFactory` + `ILogger<T>`, singleton registration, scoped `AtoCopilotContext` access.

**Alternatives Considered**:
- Extend `IEmassExportService` with inventory methods: Rejected — SRP violation; export service shouldn't own CRUD.
- Extend `IBoundaryService`: Rejected — boundary service manages Azure resource scoping, not DoD inventory metadata.
