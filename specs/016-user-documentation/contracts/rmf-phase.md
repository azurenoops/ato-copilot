# Template: RMF Phase Page

## Contract

Every RMF phase page MUST include these sections in order.

---

```markdown
# RMF Phase {N}: {Phase Name}

> {NIST SP 800-37 definition of this phase}

---

## Phase Overview

| Attribute | Value |
|-----------|-------|
| **Phase Number** | {0–6} |
| **NIST Reference** | SP 800-37 Rev. 2, §{section} |
| **Lead Persona** | {primary persona} |
| **Supporting Personas** | {comma-separated} |
| **Key Outcome** | {one sentence} |

## Persona Responsibilities

### {Persona Name} ({Abbreviation})

**Tasks in this phase**:

1. {Task} → Tool: `{tool_name}`
2. {Task} → Tool: `{tool_name}`

**Natural Language Queries**:

> **"{query}"** → `{tool_name}` — {result}

### {Next Persona}...

## Documents Produced

| Document | Owner | Format | Gate Dependency |
|----------|-------|--------|----------------|
| {name}   | {persona} | {type} | {which gate requires this} |

## Phase Gates

| Gate | Condition | Checked By |
|------|-----------|-----------|
| {gate name} | {what must be true} | `{tool_name}` |

## Transition to Next Phase

| Trigger | From Phase | To Phase | Handoff |
|---------|-----------|----------|---------|
| {event} | {current} | {next}   | {what transfers} |

🔒 **Air-Gapped Considerations**: {if applicable}

## See Also

- [Previous Phase: {name}]({path})
- [Next Phase: {name}]({path})
- [{Lead Persona} Guide]({path})
```

## Validation Rules

1. Every persona with responsibilities in this phase MUST have a subsection
2. Phase gates MUST reference the actual tool that enforces them
3. Document ownership MUST align with the persona's RBAC permissions
4. Transition triggers MUST match the gate conditions
5. Previous/Next phase links MUST be present (except Prepare has no previous, Monitor has no next)
