# Template: Reference Page

## Contract

Reference pages serve as lookup resources. They MUST prioritize scannability over narrative.

---

```markdown
# {Reference Topic}

> {One-line description of what this reference covers.}

---

## Overview

{2-3 sentences explaining the scope and how to use this reference.}

## {Primary Content Section}

{Tables, definition lists, or alphabetical entries — NOT prose paragraphs.}

| Column 1 | Column 2 | Column 3 |
|----------|----------|----------|
| {data}   | {data}   | {data}   |

## {Additional Sections as needed}

## See Also

- [{Related reference}]({path})
- [{Related guide}]({path})
```

## Page-Specific Contracts

### Tool Inventory (`reference/tool-inventory.md`)

MUST include:
- All 114 MCP tools grouped by category (8 categories)
- Each tool: name, description, required RBAC role, RMF phase applicability
- Search-friendly format (one row per tool)

### Troubleshooting (`reference/troubleshooting.md`)

MUST include:
- 7 error categories from spec Appendix F
- Each entry: error message/symptom, cause, resolution
- 25+ error scenarios minimum
- Cross-references to relevant persona guides

### Quick Reference Cards (`reference/quick-reference-cards.md`)

MUST include:
- One card per persona (5 personas + admin)
- Each card: top 5 NL queries, key tools, phase responsibilities
- Designed for printing (clean formatting, no deep nesting)

### Glossary (`reference/glossary.md`)

MUST include:
- Alphabetical entries
- Each term: definition, context/usage, related terms
- All terms from spec Appendix C plus existing glossary entries

## Validation Rules

1. Reference pages MUST use tables or definition lists as primary format
2. No section should require reading previous sections to understand
3. All tool names MUST match codebase identifiers exactly
4. Cross-references MUST use relative links that resolve in MkDocs
