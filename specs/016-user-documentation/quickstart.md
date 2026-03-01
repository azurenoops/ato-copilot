# Quickstart: Feature 016 — Documentation Site

**Date**: 2026-02-28

## Prerequisites

- Python 3.9+
- pip (Python package manager)

## Install MkDocs

```bash
pip install mkdocs-material
```

This installs MkDocs, the Material theme, and all required extensions (admonition, pymdownx, etc.).

## Serve Locally

```bash
# From repo root
mkdocs serve
```

Opens at `http://127.0.0.1:8000`. Auto-reloads on file changes.

## Build for Production

```bash
mkdocs build --strict
```

Generates static site in `site/` directory. The `--strict` flag fails on warnings (broken links, missing pages).

## Build for Air-Gapped Delivery

```bash
mkdocs build --strict
# The site/ directory is self-contained — no external CDN dependencies
# Copy site/ to target environment and open index.html directly
```

Ensure `use_directory_urls: false` in `mkdocs.yml` so `file://` browsing works.

## Validate Links

```bash
mkdocs build --strict 2>&1 | grep -i "warning\|error"
```

Any output indicates broken internal links or missing pages.

## Content Authoring Workflow

1. Create/edit Markdown files in `docs/`
2. Run `mkdocs serve` to preview
3. Add new pages to `nav:` section in `mkdocs.yml`
4. Verify with `mkdocs build --strict`
5. Commit and push

## Page Template

Every new page should follow this structure:

```markdown
# Page Title

> One-line description of this page's purpose.

---

## Section Heading

Content here. Use tables for structured data:

| Column 1 | Column 2 |
|----------|----------|
| Data     | Data     |

## Natural Language Queries

> **"How do I...?"** → Tool: `tool_name` — Description of what happens.

## See Also

- [Related Page](../path/to/page.md)
```

## File Naming Conventions

- Use lowercase with hyphens: `nl-query-reference.md`
- Index files for sections: `getting-started/index.md`
- Match existing conventions in `docs/guides/`
