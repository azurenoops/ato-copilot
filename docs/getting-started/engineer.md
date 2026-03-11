# Getting Started: Platform Engineer / System Owner

> First-time setup and orientation for Platform Engineer and System Owner users.

---

## Prerequisites

| Requirement | Details |
|------------|---------|
| **Access** | CAC enrolled with `Compliance.PlatformEngineer` role (default if no explicit role mapping) |
| **Tools** | VS Code with GitHub Copilot Chat extension installed |
| **Infrastructure** | Azure subscription access for the system being authorized |

## First-Time Setup

1. **Verify your role**

    ```
    @ato "What role am I logged in as?"
    ```

    Expected result: `Compliance.PlatformEngineer`.

2. **Check your assigned tasks**

    ```
    @ato "Show my assigned remediation tasks"
    ```

    Expected result: Kanban board of assigned findings and remediation tasks with priorities and deadlines.

3. **Learn about your first control**

    ```
    @ato /knowledge "What does AC-2 mean for Azure?"
    ```

    Expected result: Plain-language explanation of the NIST control tailored to your Azure environment, with implementation guidance.

## Your First 3 Commands

### 1. Explain a Control

> **@ato /knowledge "What does AC-2 mean for Azure?"**

Expected result: NIST control description translated into Azure-specific implementation steps — what services to configure, what settings to enable, and what evidence to collect.

### 2. Scan Your IaC for Compliance

> **@ato "Scan my Bicep file for compliance issues"**

Expected result: List of compliance findings in your Infrastructure as Code (Bicep/Terraform/ARM) with CAT severity levels and suggested fixes.

### 3. Write a Control Narrative

> **@ato "Suggest a narrative for control SC-7 on system {id}"**

Expected result: AI-generated draft narrative with a confidence score. Review, edit if needed, then commit with:

```
@ato "Write the narrative for SC-7: 'Network boundary protection is implemented
using Azure Firewall with default-deny rules...'"
```

## VS Code Integration

ATO Copilot integrates directly into your VS Code workflow:

- **IaC Diagnostics** — Compliance findings appear as squiggly underlines (CAT I/II → Error, CAT III → Warning)
- **Quick Fix** — Lightbulb Code Actions to apply suggested fixes from STIG findings
- **Hover Info** — Shows NIST control + STIG rule + CAT severity on hover
- **`@ato` Chat Participant** — Ask compliance questions in natural language from the Copilot Chat panel
- **Narrative Governance** — View narrative version history, compare changes, and submit narratives for ISSM review (`compliance_narrative_history`, `compliance_narrative_diff`, `compliance_submit_narrative`)
- **HW/SW Inventory** — Register deployed software components and keep versions current (`inventory_add_item`, `inventory_update_item`)

## What's Next

- [Full Engineer Guide](../guides/engineer-guide.md) — Complete Implement/Assess/Monitor workflows
- [RMF Phase Reference](../rmf-phases/index.md) — Phase-by-phase details
- [Quick Reference Card](../reference/quick-reference-cards.md) — Printable Engineer cheat sheet

## Common First-Day Issues

| Issue | Cause | Fix |
|-------|-------|-----|
| "No assigned tasks" returned | ISSO or ISSM has not yet created Kanban tickets for your system | Ask your ISSO to assign findings from the assessment or check that your system is in the Implement phase |
| IaC scan finds no issues on an empty file | Scanner requires actual resource definitions to analyze | Add Bicep/Terraform resource blocks before scanning |
| "Access denied" on authorization or assessment tools | `Compliance.PlatformEngineer` cannot assess controls or issue authorization | Assessment is done by the SCA; authorization by the AO — focus on implementation and remediation |
