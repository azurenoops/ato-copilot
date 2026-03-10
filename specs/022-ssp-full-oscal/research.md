# Research: Feature 022 — SSP 800-18 Full Sections + OSCAL Output

**Date**: 2026-03-10
**Status**: Complete — all unknowns resolved

---

## R1: OSCAL 1.1.2 SSP JSON Schema Requirements

**Decision**: Target OSCAL 1.1.2 JSON with 6 required top-level sections under `system-security-plan`.
**Rationale**: OSCAL 1.1.2 is the current stable release. FedRAMP and eMASS accept this version. The current codebase produces OSCAL 1.0.6 (only metadata + system-characteristics + control-implementation). Upgrading to 1.1.2 requires adding `import-profile`, `system-implementation`, and `back-matter`.
**Alternatives considered**: OSCAL 1.0.6 (current, but missing required sections and outdated), OSCAL 2.0 (not yet released), XML/YAML output (rejected per spec — JSON only).

### Required OSCAL SSP Sections (1.1.2)

| Section | Current Status | Action |
|---------|----------------|--------|
| `metadata` | ✅ Exists (v1.0.6) | Upgrade: add `roles`, `parties`, `responsible-parties`, bump oscal-version to "1.1.2" |
| `import-profile` | ❌ Missing | New: static constant profile URIs per baseline level |
| `system-characteristics` | ✅ Exists (partial) | Enhance: add `authorization-boundary`, `network-architecture`, `data-flow` |
| `system-implementation` | ❌ Missing | New: `components` (from AuthorizationBoundary), `inventory-items`, `users` (from RmfRoleAssignment), `leveraged-authorizations` |
| `control-implementation` | ✅ Exists | Enhance: add `by-components`, `statements`, `responsible-roles` |
| `back-matter` | ❌ Missing | New: `resources` array with document references (ISAs, PIAs, contingency plans) |

---

## R2: OSCAL Baseline Profile URI Constants

**Decision**: Static constant strings compiled into the service — no runtime HTTP fetching or configuration lookup.
**Rationale**: Profile URIs are stable NIST-published references. Runtime fetching adds latency, failure modes, and network dependency in air-gapped Azure Government environments. Constants are simple, testable, and deterministic.
**Alternatives considered**: Runtime HTTP verification (rejected — network dependency), appsettings.json configuration (rejected — unnecessary indirection for stable URIs).

### URI Constants

```
Low:      https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json/NIST_SP-800-53_rev5_LOW-baseline_profile.json
Moderate: https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json/NIST_SP-800-53_rev5_MODERATE-baseline_profile.json
High:     https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json/NIST_SP-800-53_rev5_HIGH-baseline_profile.json
```

---

## R3: SspSection Lifecycle State Machine

**Decision**: Strict sequential transitions: NotStarted → Draft → UnderReview → Approved.
**Rationale**: Federal documentation review workflows require auditable progression. Prevents premature approval of unreviewed content. Auto-generated sections start at Draft since they have content immediately.
**Alternatives considered**: Relaxed transitions allowing skipping (rejected — audit trail gaps), strict with rejection loop (deferred — Approved→Draft on content change is implicit since updates reset status to Draft via Cap 1.2).

### State Transition Matrix

| From | To | Trigger | Valid? |
|------|----|---------|--------|
| NotStarted | Draft | First content write | ✅ |
| Draft | UnderReview | Submit for review | ✅ |
| UnderReview | Approved | Reviewer approves | ✅ |
| UnderReview | Draft | Reviewer rejects | ✅ |
| Approved | Draft | Content updated (auto-reset) | ✅ |
| NotStarted | Approved | — | ❌ |
| NotStarted | UnderReview | — | ❌ |
| Draft | Approved | — | ❌ |

---

## R4: OSCAL Export Incomplete Data Handling

**Decision**: Partial export — produce OSCAL JSON with schema-valid placeholders for missing data, return warnings listing gaps.
**Rationale**: Teams iteratively build SSPs and export throughout the process to check progress. Blocking export until 100% complete cripples the workflow. The validation tool (Cap 4.1) catches structural issues separately.
**Alternatives considered**: Strict blocking (rejected — impractical for iterative workflow), tiered minimum (rejected — still blocks early-stage exports).

### Placeholder Strategy

| Missing Data | OSCAL Placeholder |
|-------------|-------------------|
| No SecurityCategorization | `security-sensitivity-level: "not-yet-determined"`, impact levels default to `"low"` |
| No ControlBaseline | `import-profile.href` omitted, warning issued |
| No AuthorizationBoundary resources | `system-implementation.components` = empty array |
| No RmfRoleAssignment | `metadata.roles` and `system-implementation.users` = empty arrays |
| Authored section not written | Excluded from OSCAL narrative fields, warning issued |
| No ControlImplementation narratives | `implemented-requirements` = empty array, warning issued |

---

## R5: Auto-Generated vs. Authored Section Classification

**Decision**: Cap 1.2/Part 3 classification is authoritative.
**Rationale**: Post-renumbering, §7 (Interconnections) auto-generates from `SystemInterconnection` entities and §9 (Minimum Security Controls) auto-generates from `ControlBaseline` + `ControlImplementation` data. The original Q6 clarification used pre-renumbering references.

### Final Classification

| Type | Sections | Data Source |
|------|----------|-------------|
| **Auto-generated** | §1, §2, §3, §4, §7, §9, §10, §11 | RegisteredSystem, SecurityCategorization, RmfRoleAssignment, SystemInterconnection, ControlBaseline, ControlTailoring, ControlImplementation, AuthorizationBoundary |
| **Authored** | §5, §8, §12, §13 | ISSO-authored markdown content |
| **Hybrid** | §6 | HostingEnvironment auto-populated + ISSO narrative |

---

## R6: Concurrency Handling

**Decision**: Optimistic concurrency via EF Core concurrency token on the `Version` field.
**Rationale**: Standard EF Core pattern. Prevents silent data loss when two users edit the same section concurrently. Low implementation cost — the `[ConcurrencyCheck]` attribute handles it.
**Alternatives considered**: Last-write-wins (rejected — risks data loss for compliance-critical content).

### Implementation Pattern

```csharp
// Entity
public class SspSection
{
    [ConcurrencyCheck]
    public int Version { get; set; }
}

// Service method
public async Task<SspSection> WriteSspSectionAsync(
    string systemId, int sectionNumber, string content, int expectedVersion, ...)
{
    var section = await context.SspSections.FindAsync(systemId, sectionNumber);
    if (section.Version != expectedVersion)
        throw new DbUpdateConcurrencyException("Section modified by another user");
    section.Content = content;
    section.Version++;
    await context.SaveChangesAsync();
}
```

---

## R7: Existing Service Patterns for New Services

**Decision**: Follow the singleton-with-scope-factory pattern used by `SspService` and `EmassExportService`.
**Rationale**: All MCP tool services are singletons (discovered via `IEnumerable<BaseTool>`). Services use `IServiceScopeFactory` internally to create scoped `AtoCopilotContext` for each operation. This is the established pattern across the codebase.

### Registration Pattern

```csharp
// In ServiceCollectionExtensions.cs
services.AddSingleton<IOscalSspExportService, OscalSspExportService>();
services.AddSingleton<IOscalValidationService, OscalValidationService>();

// Tools
services.AddSingleton<WriteSspSectionTool>();
services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<WriteSspSectionTool>());
// ... repeat for each new tool
```

---

## R8: OSCAL JSON Serialization

**Decision**: Continue using `Dictionary<string, object>` with `System.Text.Json` and `JsonNamingPolicy.KebabCaseLower`.
**Rationale**: The existing `EmassExportService` uses this pattern. Creating strongly-typed OSCAL model classes would be cleaner but adds significant entity count for a single export format. The dictionary approach is proven in the codebase and matches the OSCAL spec's dynamic structure.
**Alternatives considered**: Strongly-typed OSCAL classes (rejected — OSCAL schema has 200+ types; overkill for export-only), `Newtonsoft.Json` (rejected — `System.Text.Json` 9.0.5 already in use).

### Serialization Options (Existing)

```csharp
private static readonly JsonSerializerOptions OscalJsonOpts = new()
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower
};
```

---

## R9: Section Renumbering Strategy

**Decision**: Map current section keys to new NIST 800-18 section numbers with backward compatibility.
**Rationale**: Current section keys (`system_information`, `categorization`, `baseline`, `controls`, `interconnections`) must continue working while new keys are added for the full 13-section mapping.

### Key Mapping

| Old Key | New Key | NIST § |
|---------|---------|--------|
| `system_information` | `system_identification` | §1 |
| `categorization` | `categorization` (unchanged) | §2 |
| — | `personnel` | §3 |
| — | `system_type` | §4 |
| — | `description` | §5 |
| — | `environment` | §6 |
| `interconnections` | `interconnections` (unchanged) | §7 |
| — | `laws_regulations` | §8 |
| `baseline` | `minimum_controls` | §9 |
| `controls` | `control_implementations` | §10 |
| — | `authorization_boundary` | §11 |
| — | `personnel_security` | §12 |
| — | `contingency_plan` | §13 |

---

## R10: ControlTailoring Entity — Already Exists

**Decision**: Use existing `ControlTailoring` entity for §9 (Minimum Security Controls) section generation.
**Rationale**: `ControlTailoring` was implemented in Feature 015 Phase 3 with `Action` (Added/Removed), `Rationale`, `IsOverlayRequired`, and audit fields. The `ControlBaseline` entity has a `Tailorings` navigation property. No new entity needed.

### Existing Schema

- `ControlBaselineId` (FK → ControlBaseline)
- `ControlId` (string, the control identifier)
- `Action` (TailoringAction enum: Added, Removed)
- `Rationale` (string, required, max 2000 chars)
- `IsOverlayRequired` (bool)
- `TailoredBy` (string), `TailoredAt` (DateTime)
