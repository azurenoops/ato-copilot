# Quickstart — Feature 023: Documentation Update (Features 017–022)

**Date**: 2026-03-11
**Purpose**: Implementation guide for documentation updates covering Features 017–022.

---

## Prerequisites

- Branch `023-feature-docs-update` checked out
- Access to source files in `src/Ato.Copilot.Agents/Tools/` for parameter/response verification
- Familiarity with MkDocs Material theme markdown extensions (admonitions, tabbed, superfences)

---

## Implementation Order

Execute documentation tasks in dependency order. Later tasks reference content from earlier ones.

```
Phase 1 — Catalog & Inventory (foundation layer)
  ├── 1. Agent tool catalog (FR-001 → FR-006)
  └── 2. Tool inventory (FR-007 → FR-010)

Phase 2 — Persona Guides (workflow layer, references catalog)
  ├── 3. ISSM guide (FR-011)
  ├── 4. ISSO getting-started (FR-012)
  ├── 5. SCA guide (FR-013)
  ├── 6. Engineer guide (FR-014)
  └── 7. AO quick reference (FR-015)

Phase 3 — RMF Phases (cross-cutting, references guides + catalog)
  ├── 8. Prepare phase (FR-016)
  ├── 9. Assess phase (FR-017)
  ├── 10. Authorize phase (FR-018)
  ├── 11. Monitor phase (FR-019)
  └── 12. Categorize phase (FR-020)

Phase 4 — Reference & Architecture (detail layer)
  ├── 13. Data model reference (FR-021, FR-022)
  ├── 14. Glossary (FR-023)
  ├── 15. NL query reference (FR-024)
  └── 16. MCP server API reference (FR-025)

Phase 5 — Release Notes & Testing (standalone)
  ├── 17. Release notes v1.18.0 — Feature 017 (FR-026)
  ├── 18. Release notes v1.19.0 — Feature 018 (FR-027)
  ├── 19. Dev/testing guide — persona tests (FR-028)
  └── 20. mkdocs.yml nav update
```

---

## Format Patterns

All new content follows formats established in Feature 016. Key patterns:

### Tool Catalog Entry Format

```markdown
### `tool_name`

**Description**: One-sentence description
**RMF Phase**: Phase name(s)
**Roles**: Allowed roles | **Denied**: Denied roles

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| param_name | type | Yes/No | Description |

#### Response

```json
{
  "status": "success",
  "data": { ... },
  "metadata": { "tool": "tool_name", "timestamp": "..." }
}
```

#### Use Cases

> "Natural language query that maps to this tool"
```

### Tool Inventory Row Format

```markdown
| # | Tool Name | Description | Phase | Roles |
|---|-----------|-------------|-------|-------|
| 119 | `compliance_generate_sap` | Generate Security Assessment Plan | Assess | SCA, ISSM |
```

### Persona Guide Section Format

```markdown
## Section Title

!!! info "Prerequisites"
    List any required prior steps

### Workflow Steps

1. **Step name** — Description
   ```
   Tool invocation example
   ```
   Expected result description.

2. **Next step** — ...

!!! tip "Best Practice"
    Guidance note
```

### Release Notes Format

Follow `docs/release-notes/v1.20.0.md` structure:
- Header with version, date, branch, test counts
- New MCP Tools table (name, description, RBAC)
- Key Capabilities sections per tool group
- Data Model section (entities + enums tables)
- Cross-references to related features

---

## Source Files Reference

When documenting tools, verify parameters and responses against these source files:

| Feature | Source File | Path |
|---------|-----------|------|
| 017 | ScanImportTools.cs | `src/Ato.Copilot.Agents/Tools/ScanImportTools.cs` |
| 018 | SapTools.cs | `src/Ato.Copilot.Agents/Tools/SapTools.cs` |
| 019 | PrismaImportTools.cs | `src/Ato.Copilot.Agents/Tools/PrismaImportTools.cs` |
| 021 | PrivacyTools.cs | `src/Ato.Copilot.Agents/Tools/PrivacyTools.cs` |
| 021 | InterconnectionTools.cs | `src/Ato.Copilot.Agents/Tools/InterconnectionTools.cs` |
| 022 | SspAuthoringTools.cs | `src/Ato.Copilot.Agents/Tools/SspAuthoringTools.cs` |
| 022 (enhanced) | EmassExportTools.cs | `src/Ato.Copilot.Agents/Tools/EmassExportTools.cs` |

---

## Cross-Feature Dependencies

Document these relationships explicitly when they appear:

1. **F019 → F017**: Prisma import extends `ScanImportType` enum and reuses `ScanImportRecord` entity
2. **F022 → F021**: SSP §7 (System Interconnections) queries Feature 021 interconnection data
3. **F022 → F017**: SSP assessment results reference STIG scan import findings
4. **F018 → F017**: SAP test plan builder maps STIG benchmark IDs from Feature 017
5. **F022 → F015**: SSP authoring tools (write/review) coexist with Feature 015 narrative tools in same file
6. **F022 → F015**: `compliance_generate_ssp` enhanced from 5 to 13 sections (backward compatible)

---

## Verification Checklist

After each phase, verify:

- [ ] All tool names match source code `Name` properties exactly
- [ ] Parameter tables match source code `Parameters` dictionaries (name, type, required flag)
- [ ] RBAC roles match `ServiceCollectionExtensions.cs` DI registration
- [ ] Response JSON examples are structurally valid
- [ ] Cross-references between docs use correct anchor links
- [ ] New content follows the established format patterns above
- [ ] No orphan links (all referenced sections exist)

---

## Estimated Content Volume

| Target File | Existing Lines | Estimated New Lines | FRs |
|-------------|---------------|-------------------|-----|
| agent-tool-catalog.md | ~2,500 | ~800 | FR-001–FR-006 |
| tool-inventory.md | ~400 | ~80 | FR-007–FR-010 |
| issm-guide.md | ~300 | ~350 | FR-011 |
| isso getting-started | ~150 | ~100 | FR-012 |
| sca-guide.md | ~250 | ~200 | FR-013 |
| engineer-guide.md | ~200 | ~150 | FR-014 |
| ao-quick-reference.md | ~100 | ~60 | FR-015 |
| prepare.md | ~250 | ~200 | FR-016 |
| assess.md | ~200 | ~150 | FR-017 |
| authorize.md | ~150 | ~80 | FR-018 |
| monitor.md | ~200 | ~120 | FR-019 |
| categorize.md | ~150 | ~30 | FR-020 |
| data-model.md | ~400 | ~300 | FR-021–FR-022 |
| glossary.md | ~100 | ~60 | FR-023 |
| nl-query-reference.md | ~150 | ~80 | FR-024 |
| mcp-server.md | ~200 | ~60 | FR-025 |
| v1.18.0.md | 0 | ~200 | FR-026 |
| v1.19.0.md | 0 | ~200 | FR-027 |
| testing.md | ~150 | ~60 | FR-028 |
| mkdocs.yml | ~120 | ~10 | Nav update |
| **Total** | **~5,570** | **~3,290** | **30 FRs** |
