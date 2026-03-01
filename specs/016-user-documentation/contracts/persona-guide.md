# Template: Persona Guide Page

## Contract

Every persona guide page MUST include these sections in order.

---

```markdown
# {Persona Name} Guide

> One-line role description from NIST SP 800-37 or organizational definition.

---

## Role Overview

- **Full Title**: {e.g., Information System Security Manager}
- **Abbreviation**: {e.g., ISSM}
- **RBAC Role**: `{role_constant}` (from ComplianceRoles.cs)
- **Primary RMF Phases**: {comma-separated list}
- **Key Responsibility**: {one sentence}

## Permissions

| Capability | Allowed | Tool |
|-----------|---------|------|
| {action}  | ✅ / ❌  | `{tool_name}` |

## RMF Phase Workflows

### Phase N: {Phase Name}

**Objective**: {what the persona accomplishes in this phase}

**Step-by-Step**:

1. {Action} → Tool: `{tool_name}`
2. {Action} → Tool: `{tool_name}`

**Natural Language Queries**:

> **"{query}"** → `{tool_name}` — {result description}

**Documents Produced**:

| Document | Format | Purpose |
|----------|--------|---------|
| {name}   | {type} | {why}   |

🔒 **Air-Gapped Note**: {modifications for disconnected environments, if any}

## Cross-Persona Handoffs

| From | To | Trigger | Data |
|------|----|---------|------|
| {persona} | {persona} | {event} | {what transfers} |

## See Also

- [Getting Started]({path})
- [RMF Phase Reference]({path})
- [Quick Reference Card]({path})
```

## Validation Rules

1. Every persona guide MUST list all tools available to that persona's RBAC role
2. Every RMF phase section MUST include at least one NL query example
3. Air-gapped notes are REQUIRED for ISSO (Implement), ISSM (Monitor), SCA (Assess)
4. Cross-persona handoffs MUST be bidirectional (if ISSM hands to ISSO, ISSO guide shows receiving)
5. Tool names MUST match the exact MCP tool identifiers from the codebase
