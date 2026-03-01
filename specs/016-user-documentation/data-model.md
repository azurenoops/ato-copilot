# Data Model: Feature 016 — Documentation Site Structure

**Date**: 2026-02-28

## Overview

This feature produces no runtime data models. The "data model" for a documentation-only feature is the **site structure** — the hierarchy of pages, their relationships, and content types.

## Entity: Documentation Page

Each page in the documentation site follows this structure:

| Field | Type | Description |
|-------|------|-------------|
| `path` | string | File path relative to `docs/` |
| `title` | string | H1 heading — one per file |
| `section` | enum | Top-level section: `getting-started`, `personas`, `rmf-phases`, `guides`, `reference`, `scenarios` |
| `persona_scope` | enum[] | Which personas this page serves: `ISSM`, `ISSO`, `SCA`, `AO`, `Engineer`, `Administrator`, `All` |
| `status` | enum | `existing` (preserved), `enhanced` (existing + new content), `new` (created from scratch) |
| `priority` | enum | `P0` (launch-blocking), `P1` (full coverage), `P2` (nice-to-have) |

## Site Navigation Model

```yaml
nav:
  - Home: index.md
  - Getting Started:
    - Overview: getting-started/index.md
    - ISSM: getting-started/issm.md
    - ISSO: getting-started/isso.md
    - SCA: getting-started/sca.md
    - AO: getting-started/ao.md
    - Engineer: getting-started/engineer.md
  - Personas:
    - Overview: personas/index.md
    - ISSM Guide: guides/issm-guide.md          # existing file
    - ISSO Guide: personas/isso.md               # new
    - SCA Guide: guides/sca-guide.md             # existing file
    - AO Guide: guides/ao-quick-reference.md     # existing file
    - Engineer Guide: guides/engineer-guide.md   # existing file
    - Administrator: personas/administrator.md   # new
  - RMF Phases:
    - Overview: rmf-phases/index.md
    - Prepare: rmf-phases/prepare.md
    - Categorize: rmf-phases/categorize.md
    - Select: rmf-phases/select.md
    - Implement: rmf-phases/implement.md
    - Assess: rmf-phases/assess.md
    - Authorize: rmf-phases/authorize.md
    - Monitor: rmf-phases/monitor.md
  - Guides:
    - Compliance Watch: guides/compliance-watch.md
    - Remediation Kanban: guides/remediation-kanban.md
    - Portfolio Management: guides/portfolio-management.md
    - Document Catalog: guides/document-catalog.md
    - NL Query Reference: guides/nl-query-reference.md
    - Teams Bot: guides/teams-bot-guide.md
    - Chat App: guides/chat-app.md
    - Knowledge Base: guides/knowledgebase.md
  - Reference:
    - Glossary: reference/glossary.md
    - Tool Inventory: reference/tool-inventory.md
    - Troubleshooting: reference/troubleshooting.md
    - Quick Reference Cards: reference/quick-reference-cards.md
    - RMF Process: reference/rmf-process.md
    - NIST Controls: reference/nist-controls.md
    - NIST Coverage: reference/nist-coverage.md
    - Impact Levels: reference/impact-levels.md
    - STIG Coverage: reference/stig-coverage.md
  - Scenarios:
    - Full RMF Lifecycle: scenarios/full-lifecycle.md
  - Developer:
    - Code Style: dev/code-style.md
    - Contributing: dev/contributing.md
    - Testing: dev/testing.md
    - Release: dev/release.md
  - Architecture:
    - Overview: architecture/overview.md
    - Data Model: architecture/data-model.md
    - Agent & Tool Catalog: architecture/agent-tool-catalog.md
    - RMF Step Map: architecture/rmf-step-map.md
    - Security: architecture/security.md
  - API:
    - MCP Server: api/mcp-server.md
    - VS Code Extension: api/vscode-extension.md
  - Deployment: deployment.md
```

## Page Inventory

### New Pages (16 files)

| # | Path | Section | Personas | Source from Spec |
|---|------|---------|----------|-----------------|
| 1 | `docs/index.md` | Home | All | §1 Overview |
| 2 | `docs/getting-started/index.md` | Getting Started | All | §3.2–§7.2 |
| 3 | `docs/getting-started/issm.md` | Getting Started | ISSM | §3.2 |
| 4 | `docs/getting-started/isso.md` | Getting Started | ISSO | §4.2 |
| 5 | `docs/getting-started/sca.md` | Getting Started | SCA | §5.2 |
| 6 | `docs/getting-started/ao.md` | Getting Started | AO | §6.2 |
| 7 | `docs/getting-started/engineer.md` | Getting Started | Engineer | §7.2 |
| 8 | `docs/personas/index.md` | Personas | All | §2 |
| 9 | `docs/personas/isso.md` | Personas | ISSO | §4 |
| 10 | `docs/personas/administrator.md` | Personas | Admin | §8 |
| 11 | `docs/rmf-phases/index.md` | RMF Phases | All | §10 |
| 12 | `docs/rmf-phases/prepare.md` | RMF Phases | ISSM, ISSO | §3.4 Phase 0 |
| 13 | `docs/rmf-phases/categorize.md` | RMF Phases | ISSM | §3.4 Phase 1 |
| 14 | `docs/rmf-phases/select.md` | RMF Phases | ISSM | §3.4 Phase 2 |
| 15 | `docs/rmf-phases/implement.md` | RMF Phases | ISSO, Eng | §3.4 Phase 3, §4.4, §7.4 |
| 16 | `docs/rmf-phases/assess.md` | RMF Phases | SCA, ISSO | §5.5, §4.4 |
| 17 | `docs/rmf-phases/authorize.md` | RMF Phases | AO, ISSM | §6.4 |
| 18 | `docs/rmf-phases/monitor.md` | RMF Phases | ISSM, ISSO | §3.4 Phase 6, §4.4 |
| 19 | `docs/guides/portfolio-management.md` | Guides | ISSM, AO | §9.4 |
| 20 | `docs/guides/document-catalog.md` | Guides | All | §11 |
| 21 | `docs/guides/nl-query-reference.md` | Guides | All | §12 |
| 22 | `docs/reference/tool-inventory.md` | Reference | All | Appendix A |
| 23 | `docs/reference/troubleshooting.md` | Reference | All | Appendix F |
| 24 | `docs/reference/quick-reference-cards.md` | Reference | All | Appendix D |
| 25 | `docs/scenarios/full-lifecycle.md` | Scenarios | All | Appendix B |
| 26 | `mkdocs.yml` | Config | N/A | Research R5 |

### Enhanced Pages (6 files)

| # | Path | Enhancement |
|---|------|-------------|
| 1 | `docs/guides/issm-guide.md` | Add air-gapped notes, cross-links to getting-started and rmf-phases |
| 2 | `docs/guides/sca-guide.md` | Add RBAC constraints table, evidence integrity notes |
| 3 | `docs/guides/ao-quick-reference.md` | Add portfolio view section, risk expiration queries |
| 4 | `docs/guides/engineer-guide.md` | Add Kanban remediation, VS Code IaC diagnostics |
| 5 | `docs/guides/compliance-watch.md` | Add air-gapped monitoring notes |
| 6 | `docs/reference/glossary.md` | Add ~10 new terms from spec Appendix C |

### Preserved Pages (9 files — no changes)

All files in `docs/architecture/`, `docs/api/`, `docs/dev/`, and `docs/deployment.md` are included in the MkDocs navigation but require no content changes.

## Cross-Reference Map

| From Page | Links To | Relationship |
|-----------|----------|-------------|
| `index.md` | All getting-started pages | Entry point |
| `getting-started/issm.md` | `guides/issm-guide.md` | "Next: Full Guide" |
| `getting-started/sca.md` | `guides/sca-guide.md` | "Next: Full Guide" |
| `personas/index.md` | All persona guides | Hub page |
| `rmf-phases/prepare.md` | `guides/issm-guide.md` | ISSM lead |
| `rmf-phases/assess.md` | `guides/sca-guide.md` | SCA lead |
| `rmf-phases/authorize.md` | `guides/ao-quick-reference.md` | AO lead |
| `rmf-phases/implement.md` | `guides/engineer-guide.md` | Engineer lead |
| `rmf-phases/monitor.md` | `guides/compliance-watch.md` | Watch integration |
| `guides/portfolio-management.md` | `reference/tool-inventory.md` | Bulk tool references |
| `reference/troubleshooting.md` | All persona guides | Error resolution |
| `reference/quick-reference-cards.md` | All persona guides | Cheat sheets |
