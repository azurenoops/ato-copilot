# Research: Narrative Governance — Version Control + Approval Workflow

**Feature**: 024-narrative-governance
**Date**: 2026-03-11

---

## R-001: Version History Storage Strategy

**Decision**: Store `NarrativeVersion` records as a separate entity table with FK to `ControlImplementation`, rather than embedding version history as a JSON column or using temporal tables.

**Rationale**:
- EF Core 9.0 supports temporal tables for SQL Server but not SQLite (the project uses dual-provider strategy with SQLite as default for dev). A separate entity works with both providers.
- The existing `TaskHistoryEntry` pattern in `Ato.Copilot.Core/Models/Kanban/KanbanModels.cs` uses the same approach: separate immutable records linked to a parent entity via FK.
- Separate records enable LINQ queries for filtering, sorting, and pagination — critical for the `compliance_narrative_history` tool (FR-002).
- Append-only semantics are easier to enforce at the application layer (no UPDATE operations on `NarrativeVersion` rows).

**Alternatives considered**:
- *Temporal tables*: SQL Server only; SQLite dev workflow would break. Rejected.
- *JSON array column on ControlImplementation*: Would grow unbounded (spec requires unlimited retention), cause EF tracking complexity, and prevent indexed queries on version metadata. Rejected.
- *Event sourcing*: Over-engineered for this scope; no event bus infrastructure exists. Rejected.

---

## R-002: Line-Level Diff Implementation

**Decision**: Use the `DiffPlex` NuGet package (MIT license, .NET Standard 2.0) for line-level unified diff generation.

**Rationale**:
- No diff library currently exists in the project (`grep` of all `.csproj` files confirms zero diff dependencies).
- `DiffPlex` is the most widely used .NET diff library (~16M downloads), battle-tested, provides `InlineDiffBuilder` and `UnifiedDiffBuilder` out of the box.
- The `Differ` class produces clean line-by-line diffs with `+`/`-`/` ` prefixes matching the spec requirement (clarification Q4: line-level unified diff like `git diff`).
- Alternative: implement a custom LCS-based diff algorithm. Rejected — unnecessary complexity when a well-maintained library exists.

**Alternatives considered**:
- *Custom LCS diff*: Additional code to write, test, and maintain. No advantage over DiffPlex. Rejected.
- *Google's diff-match-patch*: Word-level by default, requires configuration for line-level. Less idiomatic .NET API. Rejected.
- *System.Text diff*: No built-in .NET diff capability. Rejected.

---

## R-003: Approval Status Reuse vs. New Enum

**Decision**: Reuse the existing `SspSectionStatus` enum (`NotStarted`, `Draft`, `InReview`, `Approved`, `NeedsRevision`) for narrative approval status rather than creating a new enum.

**Rationale**:
- The spec explicitly states (FR-007): "This mirrors the existing `SspSectionStatus` enum."
- Clarification Q3 confirmed that the narrative and SSP section status lifecycles should be conceptually aligned.
- Reuse prevents enum proliferation and ensures consistent status reporting across `compliance_ssp_completeness` and `compliance_narrative_approval_progress`.
- The `SspSectionStatus` values map exactly to the spec's lifecycle: Draft → InReview (=UnderReview) → Approved / NeedsRevision.
- Note: The spec uses "UnderReview" in prose, but the existing enum value is `InReview`. Implementation will use `InReview` (the enum value) and map to "UnderReview" in serialized tool responses for consistency with existing `compliance_write_ssp_section` output.

**Alternatives considered**:
- *New `NarrativeApprovalStatus` enum*: Would duplicate the same values. Rejected — principles VI (no duplication) and I (follow existing patterns).
- *String-based status*: Loses type safety. Rejected.

---

## R-004: `ControlImplementation` Enhancement Strategy

**Decision**: Add new fields to the existing `ControlImplementation` entity (`ApprovalStatus`, `CurrentVersion`, `ApprovedVersionId`) rather than replacing it with a new entity.

**Rationale**:
- FR-006 requires backward compatibility — existing callers of `compliance_write_narrative` must continue to work.
- The `Narrative` field on `ControlImplementation` will continue to hold the latest version's content for backward compatibility (existing code reads `.Narrative` directly).
- New fields are additive: `ApprovalStatus` (default `Draft`), `CurrentVersion` (default 1), `ApprovedVersionId` (nullable FK to `NarrativeVersion`).
- EF Core migration will add these columns with default values, so existing data remains valid.
- The `ControlImplementation` → `NarrativeVersion` relationship is one-to-many (one per control, many versions).

**Alternatives considered**:
- *New entity replacing ControlImplementation*: Would break 10+ existing tool references, DbContext configuration, indexes, and FK relationships. Rejected.
- *Separate approval status table*: Adds join complexity for every narrative query. Rejected — single table is simpler.

---

## R-005: Concurrency Control Pattern

**Decision**: Use the same optimistic concurrency pattern as `SspSection` — integer `Version` field with `[ConcurrencyCheck]` attribute and explicit `expected_version` parameter on write operations.

**Rationale**:
- `SspSection` already implements this exact pattern (confirmed in `RmfModels.cs` line 716: `[ConcurrencyCheck] public int Version { get; set; } = 1;`).
- The `compliance_write_ssp_section` tool already exposes `expected_version` as an optional parameter (confirmed in `SspAuthoringTools.cs` line 511).
- Reuses the validated concurrency handling in `SaveChangesAsync` (which regenerates Guid-based row versions per research R-001 from the kanban feature).

**Alternatives considered**:
- *ETag-based concurrency*: HTTP-centric; MCP tools don't use HTTP headers. Rejected.
- *Pessimistic locking*: Would block concurrent reads. Over-engineered for narrative authoring. Rejected.

---

## R-006: Tool File Organization

**Decision**: Create a new `NarrativeGovernanceTools.cs` file in `src/Ato.Copilot.Agents/Compliance/Tools/` for the 8 new tool classes, rather than adding to the existing `SspAuthoringTools.cs`.

**Rationale**:
- `SspAuthoringTools.cs` already contains 8 tool classes (T070–T077) and is ~700 lines. Adding 8 more tools would make it unwieldy.
- The existing codebase uses topical grouping: `SspAuthoringTools.cs` for SSP tools, `AssessmentArtifactTools.cs` for assessment tools, `KanbanTools.cs` for kanban tools.
- A separate `NarrativeGovernanceTools.cs` maintains single-responsibility per file while keeping the tools in the same namespace.

**Alternatives considered**:
- *Add to SspAuthoringTools.cs*: Would create a 1400+ line file violating constitution principle VI (maintainability). Rejected.
- *One file per tool*: 8 new files is excessive for closely related tools. Rejected.

---

## R-007: Service Layer Design

**Decision**: Create a new `INarrativeGovernanceService` interface in `Ato.Copilot.Core/Interfaces/Compliance/` and implement as `NarrativeGovernanceService` in `Ato.Copilot.Agents/Compliance/Services/`, following the existing service pattern.

**Rationale**:
- Existing `ISspService` handles SSP authoring (write narrative, suggest, batch populate, progress, generate SSP, write/review section, completeness). Adding 8+ new methods would bloat it.
- The codebase splits services by domain: `ISspService` (SSP), `IAssessmentArtifactService` (assessment), `IEmassExportService` (eMASS), `ISapService` (SAP).
- New service keeps `ISspService` unchanged (backward compatibility) and isolates governance logic.
- The `WriteNarrativeAsync` method in `ISspService` will be enhanced to call into `INarrativeGovernanceService` for version creation (service composition, not replacement).

**Alternatives considered**:
- *Extend ISspService*: Would grow the interface from 7 to 15+ methods. Rejected.
- *Put all logic in tools directly*: Violates constitution principle II (services behind tools). Rejected.

---

## R-008: SSP Generation Integration

**Decision**: Modify `GenerateSspAsync` in `SspService` to query the latest Approved `NarrativeVersion` (via `ApprovedVersionId` on `ControlImplementation`) instead of reading `ControlImplementation.Narrative` directly, with fallback to the latest Draft.

**Rationale**:
- FR-024 requires SSP generation to use approved content, falling back to draft with warnings.
- The `ApprovedVersionId` field on `ControlImplementation` provides a direct lookup (no version table scan needed).
- When `ApprovedVersionId` is null, fall back to `ControlImplementation.Narrative` (latest version content), preserving existing behavior for systems that don't use the approval workflow.

**Alternatives considered**:
- *Always query NarrativeVersion table for latest approved*: Adds N+1 query risk for 325 controls. Using the denormalized `ApprovedVersionId` FK avoids this. Rejected.
- *Separate approved content column*: Redundant with NarrativeVersion storage. Rejected.
