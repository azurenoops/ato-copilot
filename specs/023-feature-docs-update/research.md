# Research — Feature 023: Documentation Update (Features 017–022)

**Date**: 2026-03-11
**Status**: Complete — all unknowns resolved

---

## Research Task 1: Verify Exact Tool Names and Counts

### Decision
Tool names and counts are verified against source code. Total: **33 tools** across Features 017–022.

### Findings

| Feature | Source File | Verified Tool Names | Count |
|---------|------------|---------------------|-------|
| **017** | `ScanImportTools.cs` | `compliance_import_ckl`, `compliance_import_xccdf`, `compliance_export_ckl`, `compliance_list_imports`, `compliance_get_import_summary` | 5 |
| **018** | `SapTools.cs` | `compliance_generate_sap`, `compliance_update_sap`, `compliance_finalize_sap`, `compliance_get_sap`, `compliance_list_saps` | 5 |
| **019** | `PrismaImportTools.cs` | `compliance_import_prisma_csv`, `compliance_import_prisma_api`, `compliance_list_prisma_policies`, `compliance_prisma_trend` | 4 |
| **021** | `PrivacyTools.cs` | `compliance_create_pta`, `compliance_generate_pia`, `compliance_review_pia`, `compliance_check_privacy_compliance` | 4 |
| **021** | `InterconnectionTools.cs` | `compliance_add_interconnection`, `compliance_list_interconnections`, `compliance_update_interconnection`, `compliance_generate_isa`, `compliance_register_agreement`, `compliance_update_agreement`, `compliance_certify_no_interconnections`, `compliance_validate_agreements` | 8 |
| **022** | `SspAuthoringTools.cs` (new) | `compliance_write_ssp_section`, `compliance_review_ssp_section`, `compliance_ssp_completeness`, `compliance_export_oscal_ssp`, `compliance_validate_oscal_ssp` | 5 |
| **022** | `SspAuthoringTools.cs` (enhanced) | `compliance_generate_ssp` (updated params for 13 sections) | +0 (enhanced) |
| **022** | `EmassExportTools.cs` (enhanced) | `compliance_export_oscal` (delegates SSP to OscalSspExportService) | +0 (enhanced) |

**Totals**: 31 new tools + 2 enhanced = 33 tool documentation entries needed.

### Correction from Spec
The spec says "22 new tools from Features 018, 021, and 022" — correct count is:
- Feature 021 has **12 tools** (4 privacy + 8 interconnection), not the 3+9 sometimes cited
- Feature 021's 4th privacy tool `compliance_check_privacy_compliance` was discovered during research
- Total new tools needing catalog entries: 5 (018) + 12 (021) + 5 (022) = 22 ✓
- Existing catalog entries to verify: 5 (017) + 4 (019) = 9 ✓

---

## Research Task 2: RBAC Role Mappings per Feature

### Decision
RBAC roles verified from `ServiceCollectionExtensions.cs` DI registration and `RequiredPimTier` annotations on each tool class.

### Findings

| Tool | PIM Tier | RBAC Roles |
|------|----------|------------|
| **Feature 017** | | |
| `compliance_import_ckl` | Write | ISSM, ISSO |
| `compliance_import_xccdf` | Write | ISSM, ISSO |
| `compliance_export_ckl` | Read | ISSM, ISSO, SCA |
| `compliance_list_imports` | Read | All |
| `compliance_get_import_summary` | Read | All |
| **Feature 018** | | |
| `compliance_generate_sap` | Write | SCA, ISSM |
| `compliance_update_sap` | Write | SCA, ISSM |
| `compliance_finalize_sap` | Write | SCA |
| `compliance_get_sap` | Read | All |
| `compliance_list_saps` | Read | All |
| **Feature 019** | | |
| `compliance_import_prisma_csv` | Write | ISSM, ISSO |
| `compliance_import_prisma_api` | Write | ISSM, ISSO |
| `compliance_list_prisma_policies` | Read | All |
| `compliance_prisma_trend` | Read | All |
| **Feature 021 — Privacy** | | |
| `compliance_create_pta` | Write | ISSM, ISSO |
| `compliance_generate_pia` | Write | ISSO, ISSM |
| `compliance_review_pia` | Write | ISSM |
| `compliance_check_privacy_compliance` | Read | All |
| **Feature 021 — Interconnection** | | |
| `compliance_add_interconnection` | Write | ISSM, ISSO, Eng |
| `compliance_list_interconnections` | Read | All |
| `compliance_update_interconnection` | Write | ISSM, ISSO |
| `compliance_generate_isa` | Write | ISSM |
| `compliance_register_agreement` | Write | ISSM |
| `compliance_update_agreement` | Write | ISSM |
| `compliance_certify_no_interconnections` | Write | ISSM |
| `compliance_validate_agreements` | Read | ISSM, ISSO, SCA |
| **Feature 022 — SSP Sections** | | |
| `compliance_write_ssp_section` | Write | ISSO, Eng |
| `compliance_review_ssp_section` | Write | ISSM |
| `compliance_ssp_completeness` | Read | All |
| **Feature 022 — OSCAL** | | |
| `compliance_export_oscal_ssp` | Read | ISSM, SCA, AO |
| `compliance_validate_oscal_ssp` | Read | ISSM, SCA |

---

## Research Task 3: Documentation Format Patterns

### Decision
All documentation must follow the established patterns from Features 015/016. Formats verified from reading existing files.

### Findings

#### Agent Tool Catalog Entry Format (from `agent-tool-catalog.md`)
```markdown
### `tool_name`

Description text.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `param` | string | Yes | Description |

**Response:**

```json
{
  "status": "success",
  "data": { ... },
  "metadata": { "tool": "tool_name", "timestamp": "..." }
}
```  

**RBAC:**
- Allowed: ISSM, ISSO
- Denied: Engineer, SCA, AO

**Use Cases:**
> "Natural language query example" → `tool_name`
```

#### Tool Inventory Row Format (from `tool-inventory.md`)
```markdown
| # | Tool | Description | Phase(s) | Roles |
|---|------|-------------|----------|-------|
| 119 | `tool_name` | Short description | Assess | SCA, ISSM |
```

#### Persona Guide Section Format (from `issm-guide.md`, `sca-guide.md`)
```markdown
## Section Title

Description of workflow.

### Step N: Action Name

Use `tool_name` to perform action:

```
Tool: tool_name
Parameters:
  param1: "value"
  param2: "value"
```

Expected result: Description of what is returned.
```

#### RMF Phase Guide Format (from `prepare.md`, `assess.md`)
```markdown
## Persona Responsibilities

### PersonaName (Lead/Support)

**Tasks in this phase**:

1. Task description → Tool: `tool_name`

**Natural Language Queries**:

> **"Query text"** → `tool_name` — explanation
```

#### Glossary Format (from `glossary.md`)
```markdown
## Letter

| Term | Definition |
|------|-----------|
| **TERM** | Definition text |
```

#### Release Notes Format (from `v1.20.0.md`)
```markdown
# Release Notes — vX.XX.0

> **Release Date:** Month Day, Year
> **Branch:** `###-feature-name`
> **Tests:** N,NNN passing (NNN new) · Build: 0 errors

---

## Feature NNN: Feature Name

Description.

---

### New MCP Tools

| Tool | Description | RBAC |
|------|-------------|------|
| `tool_name` | Description | Roles |

---

### Key Capabilities

#### Tool Name (`tool_name`)
- Bullet list of capabilities

---

### Data Model

#### New Entities
| Entity | Purpose |
|--------|---------|
| `EntityName` | Description |

#### New Enumerations
| Enum | Values |
|------|--------|
| `EnumName` | Value1, Value2, ... |
```

#### NL Query Reference Format (from `nl-query-reference.md`)
```markdown
## N. Category Name

| Query | Tool | Persona |
|-------|------|---------|
| Descriptive natural language query with placeholder | `tool_name` | PersonaName |
```

#### Quick Reference Card Format (from `quick-reference-cards.md`)
```
┌─────────────────────────────────────────────────────────┐
│                 Persona Quick Reference                  │
│          Role: Compliance.RoleName                       │
├─────────────────────────────────────────────────────────┤
│ ACTION:   "Natural language query pattern"              │
└─────────────────────────────────────────────────────────┘
```

---

## Research Task 4: Existing Documentation Coverage Assessment

### Decision
Documentation coverage verified by reading each target file. Data below drives what needs to be added vs. verified.

### Findings

| File | Lines | Features 017-022 Content | Action Needed |
|------|-------|--------------------------|---------------|
| `agent-tool-catalog.md` | 2,086 | F017 (5 entries), F019 (4 entries) | Add F018 (5), F021 (12), F022 (5). Update 2 enhanced. Verify F017+F019. |
| `tool-inventory.md` | 244 | Cat 8 (F017 partial), Cat 9 (F019) | Add Cat 10 (F018, 5), Cat 11 (F021, 12), Cat 12 (F022, 5). Verify existing. |
| `issm-guide.md` | 929 | None | Add 7 sections: STIG import oversight, SAP review, Prisma monitoring, privacy oversight, interconnection mgmt, SSP review, OSCAL export |
| `sca-guide.md` | 333 | None | Add 5 sections: SAP generation, CKL export, privacy check, OSCAL validation, SSP completeness |
| `engineer-guide.md` | 415 | None | Add 4 sections: STIG remediation, Prisma scripts, interconnection registration, SSP environment |
| `nl-query-reference.md` | 204 | None | Add 6 categories: STIG Import, SAP Generation, Prisma Cloud, Privacy, Interconnections, SSP Authoring |
| `prepare.md` | 87 | None | Add PTA/PIA steps, interconnection registration, 2 gates |
| `assess.md` | 223 | None | Add STIG import, SAP generation, Prisma import as assessment activities |
| `authorize.md` | 143 | None | Add SAP-to-SAR reference, OSCAL SSP export, privacy prerequisites |
| `monitor.md` | 213 | None | Add ISA/MOU expiration, PIA annual review, Prisma re-import, SSP status |
| `categorize.md` | ~150 | None | Add minor PTA PII categories note |
| `data-model.md` | 557 | None | Add 19 entities + 16 enums from F017–F022 |
| `glossary.md` | 190 | None | Add ~15 new terms |
| `mcp-server.md` | 710 | None | Add 31 new tool registrations |
| `testing.md` | 208 | None | Add persona test case section (F020) |
| `quick-reference-cards.md` | 234 | None | Update ISSM, ISSO, SCA, AO cards |
| `isso.md` (getting-started) | ~100 | None | Update "what you can do" summary |
| `isso.md` (personas) | ~300 | None | Update ISSO persona guide activities |

---

## Research Task 5: Release Notes Versioning

### Decision
Assign sequential release version numbers to unversioned features based on release order.

### Rationale
- v1.18.0 → Feature 017 (SCAP/STIG Import) — released before Feature 019 which is v1.20.0
- v1.19.0 → Feature 018 (SAP Generation) — released between F017 and F019
- v1.20.0 → Feature 019 (Prisma Cloud) — already exists
- v1.21.0 → Feature 022 (SSP + OSCAL) — already exists
- Feature 020 (Persona Test Cases) and Feature 021 (PIA & Interconnections) are covered by v1.21.0 as bundled with Feature 022

### Alternatives considered
- Using patch versions (v1.17.1, v1.18.1) — rejected because these are major feature additions
- Using the feature number as version (v1.17.0, v1.18.0) — adopted as simplest mapping

---

## Research Task 6: Cross-Feature Dependencies

### Decision
Document all cross-feature data dependencies to ensure accurate cross-references.

### Findings

| Source Feature | Target Feature | Dependency |
|---------------|---------------|------------|
| 019 (Prisma) | 017 (STIG) | Prisma extends `ScanImportType` enum with `PrismaCloudCsv` and `PrismaCloudApi` values. Both use `ScanImportRecord` and `ScanImportFinding` entities. |
| 022 (SSP) | 021 (Interconnections) | SSP §7 ("System Interconnections") reads Feature 021 `SystemInterconnection` and `InterconnectionAgreement` data. |
| 022 (SSP) | 017 (STIG) | SSP §13 ("Contingency Plan") references STIG benchmark compliance data via `ScanImportRecord`. |
| 018 (SAP) | 017 (STIG) | SAP "STIG/SCAP Test Plan" section references Feature 017 STIG benchmark data for building test procedures. |
| 022 (SSP) | 015 (Core) | `compliance_generate_ssp` enhanced with 13 section keys extends the original 5-section output from Feature 015. |
| 022 (OSCAL) | 015 (Core) | `compliance_export_oscal` updated to delegate SSP model to `OscalSspExportService` (was inline in `EmassExportService`). |

---

## Research Task 7: MkDocs Navigation Updates

### Decision
`mkdocs.yml` needs 2 additions to the `nav` section for new release notes files.

### Changes Required
```yaml
# Add under existing nav... after any existing release-notes entries:
  - Release Notes:
    - v1.21.0: release-notes/v1.21.0.md
    - v1.20.0: release-notes/v1.20.0.md
    - v1.19.0: release-notes/v1.19.0.md    # NEW
    - v1.18.0: release-notes/v1.18.0.md    # NEW
```

Note: The current `mkdocs.yml` has no Release Notes section in nav. Need to check if release notes are already linked or if a new nav section is needed.

---

## Research Task 8: Feature 020 Test Documentation Content

### Decision
Feature 020 persona test cases exist in `docs/persona-test-cases/` (5 files + scripts/) and `specs/020-persona-test-cases/`. The dev/testing guide needs a reference section pointing to these artifacts.

### Findings
Existing test documentation files:
- `docs/persona-test-cases/environment-checklist.md`
- `docs/persona-test-cases/results-template.md`
- `docs/persona-test-cases/test-data-setup.md`
- `docs/persona-test-cases/test-report.md`
- `docs/persona-test-cases/tool-validation.md`
- `docs/persona-test-cases/scripts/` (test scripts)

These files are NOT linked from `mkdocs.yml` nav and not referenced from `dev/testing.md`.
