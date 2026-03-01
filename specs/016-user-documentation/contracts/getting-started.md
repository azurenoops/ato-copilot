# Template: Getting Started Page

## Contract

Every per-persona getting-started page MUST include these sections in order.

---

```markdown
# Getting Started: {Persona Name}

> First-time setup and orientation for {persona abbreviation} users.

---

## Prerequisites

| Requirement | Details |
|------------|---------|
| **Access** | {what accounts/roles needed} |
| **Tools** | {VS Code, Teams, CLI — whichever applies} |
| **Knowledge** | {assumed background} |

## First-Time Setup

1. {Step 1 — e.g., verify RBAC role assignment}
   ```
   {example command or NL query}
   ```
2. {Step 2 — e.g., configure default system}
3. {Step 3 — e.g., verify connectivity}

## Your First 3 Commands

### 1. {Action Name}

> **"{natural language query}"**

Expected result: {what the user will see}

### 2. {Action Name}

> **"{natural language query}"**

Expected result: {what the user will see}

### 3. {Action Name}

> **"{natural language query}"**

Expected result: {what the user will see}

## What's Next

- [Full {Persona} Guide]({path}) — Complete workflow reference
- [RMF Phase Reference]({path}) — Phase-by-phase details
- [Quick Reference Card]({path}) — Printable cheat sheet

## Common First-Day Issues

| Issue | Cause | Fix |
|-------|-------|-----|
| {symptom} | {why} | {resolution} |
```

## Validation Rules

1. Prerequisites MUST specify the exact RBAC role name
2. First 3 commands MUST use natural language queries (not raw tool calls)
3. Expected results MUST be realistic
4. "What's Next" links MUST resolve to existing pages
5. Common issues MUST cover RBAC permission errors for the persona's role
