# RMF Phase 0: Prepare

> Establish the context and priority for managing security and privacy risk. Register the system, define the authorization boundary, and assign RMF roles.

---

## Phase Overview

| Attribute | Value |
|-----------|-------|
| **Phase Number** | 0 |
| **NIST Reference** | SP 800-37 Rev. 2, §3.1 |
| **Lead Persona** | ISSM |
| **Supporting Personas** | ISSO, Engineer |
| **Key Outcome** | System registered with boundary and roles assigned |

---

## Persona Responsibilities

### ISSM (Lead)

**Tasks in this phase**:

1. Register the system → Tool: `compliance_register_system`
2. Define the authorization boundary → Tool: `compliance_define_boundary`
3. Exclude shared/inherited resources → Tool: `compliance_exclude_from_boundary`
4. Assign RMF roles → Tool: `compliance_assign_rmf_role`
5. Verify role assignments → Tool: `compliance_list_rmf_roles`
6. Advance to Categorize → Tool: `compliance_advance_rmf_step`

**Natural Language Queries**:

> **"Register a new system called 'ACME Portal' as a Major Application with mission-critical designation in Azure Government"** → `compliance_register_system` — creates system entity with RMF step = Prepare

> **"Define the authorization boundary for system {id} — add the production VMs, SQL database, and Key Vault"** → `compliance_define_boundary` — adds Azure resource IDs to the boundary

> **"Assign Jane Smith as ISSM and Bob Jones as ISSO for system {id}"** → `compliance_assign_rmf_role` — assigns named personnel to RMF roles

> **"Show me all registered systems"** → `compliance_list_systems` — lists all systems with RMF phase and status

> **"What roles are assigned to system {id}?"** → `compliance_list_rmf_roles` — shows all role assignments

> **"Advance system {id} to the Categorize phase"** → `compliance_advance_rmf_step` — transitions to next phase (gate-checked)

### ISSO (Support)

- Assist with boundary definition by identifying Azure resources
- Verify role assignments are accurate

### Engineer (Support)

- Provide Azure resource inventory for boundary definition
- Confirm system type and hosting environment details

---

## Documents Produced

| Document | Owner | Format | Gate Dependency |
|----------|-------|--------|----------------|
| — | — | — | None (informational artifacts only) |

---

## Phase Gates

| Gate | Condition | Checked By |
|------|-----------|-----------|
| Roles assigned | At least one RMF role assigned to the system | `compliance_advance_rmf_step` |
| Boundary defined | At least one resource in the authorization boundary | `compliance_advance_rmf_step` |

---

## Transition to Next Phase

| Trigger | From Phase | To Phase | Handoff |
|---------|-----------|----------|---------|
| `compliance_advance_rmf_step` with gate pass | Prepare | Categorize | System ID with boundary and roles ready for categorization |

---

## See Also

- [Next Phase: Categorize](categorize.md)
- [ISSM Guide](../guides/issm-guide.md) — Full ISSM workflow documentation
- [Persona Overview](../personas/index.md) — Role definitions
