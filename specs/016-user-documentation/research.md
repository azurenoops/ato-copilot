# Research: Feature 016 — Comprehensive User & Persona Documentation

**Date**: 2026-02-28

## Research Tasks

### R1: Documentation Site Generator Selection

**Decision**: MkDocs with Material theme

**Rationale**:
- MkDocs is Python-based, generates static HTML from Markdown — matches the existing `docs/` convention
- Material for MkDocs provides: search, responsive navigation, dark/light mode, admonitions, tabs, code annotations
- Static output works in air-gapped environments (serve from any web server, no runtime dependencies)
- `mkdocs build --strict` provides validation (broken links, missing pages) as a quality gate
- Alternative to DocFX (C#/.NET-native doc generator) was considered but rejected: DocFX is heavier, less flexible for pure Markdown sites, and has a smaller community for non-API docs

**Alternatives Considered**:
| Option | Rejected Because |
|--------|-----------------|
| DocFX | Better for API reference from XML docs; overkill for pure Markdown user guides |
| Docusaurus | React-based; adds JS runtime dependency unnecessary for static docs |
| Jekyll | Slower build times, Ruby dependency, less featureful search |
| Hugo | Go-based; fast but less Markdown extension support than MkDocs Material |
| Raw GitHub rendering | No search, no navigation structure, no offline bundle |

### R2: Existing Documentation Inventory

**Decision**: Preserve all existing files in-place; enhance with cross-links and air-gapped notes

**Rationale**:
- 15+ existing Markdown files across `docs/` already follow consistent conventions
- Moving files would break any existing links/bookmarks
- MkDocs `nav:` configuration can organize existing files into a coherent hierarchy without moving them
- Existing guide format (H1 → blockquote tagline → `---`-separated sections → tables → fenced code) will be maintained for consistency

**Inventory of Existing Docs**:

| File | Lines | Content | Enhancement Needed |
|------|-------|---------|-------------------|
| `docs/getting-started.md` | 332 | Prerequisites, build, run modes | Redirect to new `getting-started/` section |
| `docs/guides/issm-guide.md` | 726 | Full ISSM workflow (29 steps) | Add getting-started, air-gapped notes |
| `docs/guides/sca-guide.md` | 189 | SCA assessment workflow | Add getting-started, RBAC constraints |
| `docs/guides/ao-quick-reference.md` | 120+ | AO authorization decisions | Add getting-started, portfolio view |
| `docs/guides/engineer-guide.md` | 222 | SSP authoring workflow | Add getting-started, Kanban remediation |
| `docs/guides/compliance-watch.md` | 150+ | Monitoring, alerts, auto-remediation | Add air-gapped notes |
| `docs/guides/remediation-kanban.md` | 200+ | Kanban board management | Add error scenarios |
| `docs/guides/teams-bot-guide.md` | — | Teams bot setup | Preserve as-is |
| `docs/guides/chat-app.md` | — | Chat app guide | Preserve as-is |
| `docs/guides/knowledgebase.md` | — | Knowledge base guide | Preserve as-is |
| `docs/reference/glossary.md` | — | Alphabetical glossary | Expand with new terms |
| `docs/reference/rmf-process.md` | — | RMF process reference | Cross-link from rmf-phases/ |
| `docs/reference/nist-controls.md` | — | NIST control reference | Preserve |
| `docs/reference/nist-coverage.md` | — | Coverage report | Preserve |
| `docs/reference/impact-levels.md` | — | IL reference | Preserve |
| `docs/reference/stig-coverage.md` | — | STIG mapping | Preserve |
| `docs/architecture/agent-tool-catalog.md` | — | Tool catalog | Source for tool-inventory.md |
| `docs/architecture/overview.md` | — | Architecture overview | Preserve |
| `docs/deployment.md` | — | Deployment guide | Preserve |

### R3: New Pages Required

**Decision**: 14 new pages needed to fulfill spec requirements

| New Page | Source Spec Section | Priority |
|----------|-------------------|----------|
| `docs/index.md` | §1 Overview | P0 — Landing page |
| `docs/getting-started/index.md` | §3.2, §4.2, §5.2, §6.2, §7.2 | P0 — Onboarding |
| `docs/getting-started/issm.md` | §3.2 | P0 |
| `docs/getting-started/isso.md` | §4.2 | P0 |
| `docs/getting-started/sca.md` | §5.2 | P0 |
| `docs/getting-started/ao.md` | §6.2 | P0 |
| `docs/getting-started/engineer.md` | §7.2 | P0 |
| `docs/personas/index.md` | §2 | P1 — Organization |
| `docs/guides/portfolio-management.md` | §9.4 | P1 |
| `docs/guides/document-catalog.md` | §11 | P1 |
| `docs/guides/nl-query-reference.md` | §12 | P1 |
| `docs/reference/tool-inventory.md` | §14 Appendix A | P1 |
| `docs/reference/troubleshooting.md` | §14 Appendix F | P1 |
| `docs/reference/quick-reference-cards.md` | §14 Appendix D | P2 |
| `docs/scenarios/full-lifecycle.md` | §14 Appendix B | P2 |
| `mkdocs.yml` | N/A (config) | P0 — Site configuration |

### R4: Air-Gapped Documentation Delivery

**Decision**: MkDocs `mkdocs build` produces a self-contained `site/` directory that can be served from any static web server or browsed locally via `file://` protocol

**Rationale**:
- No external CDN dependencies when using `--no-directory-urls` and bundling assets
- Search index is a local JSON file — works offline
- CSS and JS are bundled inline (Material theme support)
- Can be distributed as a ZIP archive for air-gapped deployment

### R5: MkDocs Configuration Best Practices

**Decision**: Use Material theme with the following features

**Configuration Choices**:
| Feature | Setting | Rationale |
|---------|---------|-----------|
| Theme | `mkdocs-material` | Most popular MkDocs theme; search, nav, responsive |
| Search | Built-in (lunr.js) | Client-side search, works offline |
| Navigation | `nav:` with sections | Persona-based top-level, RMF phases second |
| Tabs | `navigation.tabs` | Top-level sections as tabs |
| TOC | `toc.integrate` | Integrate TOC into left navigation |
| Content tabs | `content.tabs.link` | Linked tabs for multi-persona examples |
| Admonitions | `admonition` extension | For air-gapped callouts (replaces blockquote convention) |
| Code blocks | `pymdownx.highlight` | Syntax highlighting for NL query examples |
| `use_directory_urls` | `false` | Enables offline browsing via `file://` |

### R6: Content Migration Strategy

**Decision**: In-place enhancement, not migration

**Rationale**:
- Existing `docs/guides/*.md` files are well-structured and actively used
- MkDocs `nav:` can reference files in any subdirectory — no moves needed
- New persona pages in `docs/personas/` will cross-link to existing guides via relative links
- Getting-started pages will be new standalone files
- Existing `docs/getting-started.md` will be updated to redirect to the new section
