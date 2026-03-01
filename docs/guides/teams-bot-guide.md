# Teams Bot Guide

> Microsoft Teams bot installation, commands, Adaptive Cards, and notifications.

---

## Table of Contents

- [Overview](#overview)
- [Installation](#installation)
- [Commands](#commands)
- [Adaptive Cards](#adaptive-cards)
- [Card Routing](#card-routing)
- [Notifications](#notifications)
- [Configuration](#configuration)
- [Troubleshooting](#troubleshooting)

---

## Overview

The ATO Copilot Teams bot provides a conversational interface for RMF compliance workflows directly within Microsoft Teams. Users interact through natural language, and results are rendered as rich Adaptive Cards v1.5.

### Architecture

```
Teams Client → Bot Framework → M365 Extension → MCP Server → ComplianceAgent
                                    │
                                    ├─ Card Router
                                    ├─ 22 Adaptive Card Builders
                                    └─ Agent Attribution + Suggestion Buttons
```

---

## Installation

### Prerequisites

- Microsoft Teams (desktop or web)
- Azure Bot registration
- ATO Copilot MCP server running (HTTP mode, port 3001)

### Setup Steps

1. **Register Azure Bot** — Create a Bot Channels Registration in Azure
2. **Configure Messaging Endpoint** — Point to your Teams bot webhook URL
3. **Install App in Teams** — Upload the app manifest or install from the org catalog
4. **Configure MCP Backend** — Set `McpServer:BaseUrl` to your MCP server endpoint

---

## Commands

The Teams bot accepts natural language commands routed through the ComplianceAgent. Common patterns:

### RMF Lifecycle

| Command Pattern | Tool Invoked | Description |
|----------------|-------------|-------------|
| "Register a new system called..." | `compliance_register_system` | Create system |
| "Show my systems" | `compliance_list_systems` | List systems |
| "Show system {name}" | `compliance_get_system` | System details |
| "Advance {system} to Categorize" | `compliance_advance_rmf_step` | Phase transition |
| "Categorize {system}" | `compliance_categorize_system` | FIPS 199 |
| "Select baseline for {system}" | `compliance_select_baseline` | NIST 800-53 |
| "Generate SSP for {system}" | `compliance_generate_ssp` | SSP document |
| "Assess control AC-2" | `compliance_assess_control` | SCA assessment |
| "Issue ATO for {system}" | `compliance_issue_authorization` | AO decision |
| "Show dashboard" | `compliance_multi_system_dashboard` | Portfolio view |

### Compliance Operations

| Command Pattern | Tool Invoked | Description |
|----------------|-------------|-------------|
| "Scan resources" | `compliance_assess` | Compliance scan |
| "Show findings" | `compliance_status` | Finding summary |
| "Fix finding {id}" | `compliance_remediate` | Remediation |
| "Collect evidence" | `compliance_collect_evidence` | Evidence gathering |
| "Show alerts" | `watch_show_alerts` | Watch alerts |

### PIM & Authentication

| Command Pattern | Tool Invoked | Description |
|----------------|-------------|-------------|
| "Show my PIM roles" | `pim_list_eligible` | Eligible roles |
| "Activate Contributor role" | `pim_activate_role` | Role activation |
| "CAC status" | `cac_status` | Session check |

---

## Adaptive Cards

The bot renders responses as Adaptive Cards v1.5. The card system includes 22 card builders organized by domain.

### Feature 015 Cards

#### System Summary Card

Displays a registered system's key details:
- System name, acronym, type
- Current RMF step with color-coded icon (🔵 Prepare, 📋 Categorize, etc.)
- Compliance score with color-coded progress
- Mission criticality badge
- Active alerts count
- Action buttons: View Compliance, View Risks, Generate SSP

#### Categorization Card

Shows FIPS 199 security categorization:
- Formal notation: `SC System = {(confidentiality, Moderate), (integrity, High), (availability, Moderate)}`
- DoD Impact Level badge
- C/I/A impact indicators with color-coded icons (🔴 High, 🟡 Moderate, 🟢 Low)
- Information types table (name, SP 800-60 ID, C/I/A levels)
- Action buttons: Select Control Baseline, Edit Categorization

#### Authorization Card

Displays authorization decision details:
- Decision type with full label (e.g., "Authority to Operate (ATO)")
- Expiration countdown with urgency coloring:
  - 🟢 > 90 days remaining
  - 🟡 30-90 days remaining
  - 🔴 < 30 days remaining
  - ⚫ Expired
- Residual risk level with severity color
- Terms and conditions (for ATOwC)
- Conditions checklist with status icons
- Action buttons: View Auth Package, Review Risk Acceptances, Generate ConMon Report

#### Dashboard Card

Multi-system portfolio view:
- Average compliance score with icon
- RMF step distribution chart (7-phase count)
- Per-system rows (max 15): name, acronym, impact level, RMF step icon, ATO status, score, findings, POA&M items
- Action buttons: View All Systems, Compliance Summary, Expiring ATOs

### Core Cards

| Card | Purpose |
|------|---------|
| `complianceCard` | Compliance scan results with findings |
| `genericCard` | General text responses |
| `errorCard` | Error messages with recovery suggestions |
| `followUpCard` | Multi-option follow-up prompts |
| `knowledgeBaseCard` | NIST/STIG/RMF knowledge articles |
| `configurationCard` | Configuration status display |
| `findingDetailCard` | Individual finding with severity |
| `remediationPlanCard` | Remediation steps and priority |
| `alertLifecycleCard` | Alert status and history |
| `complianceTrendCard` | Trend visualization data |
| `evidenceCollectionCard` | Evidence collection results |
| `nistControlCard` | NIST control details |
| `clarificationCard` | Disambiguation prompts |
| `confirmationCard` | Action confirmation |
| `kanbanBoardCard` | Kanban board visualization |

### Shared Components

All cards include:
- **Agent Attribution** — "ATO Copilot" with version and timestamp
- **Suggestion Buttons** — Context-aware follow-up actions

---

## Card Routing

The `cardRouter` determines which card to render based on the response data:

### Priority Order

| Priority | Condition | Card |
|----------|----------|------|
| 0 | Error in response | Error Card |
| 1 | Follow-up options present | Follow-Up Card |
| 2 | Clarification needed | Clarification Card |
| 3 | `type === "systemSummary"` | System Summary Card |
| 4 | `type === "categorization"` | Categorization Card |
| 5 | `type === "authorization"` | Authorization Card |
| 6 | `type === "dashboard"` | Dashboard Card |
| 7 | `type === "finding"` | Finding Detail Card |
| 8 | `type === "remediationPlan"` | Remediation Plan Card |
| 9 | `type === "alert"` | Alert Lifecycle Card |
| 10 | `type === "trend"` | Compliance Trend Card |
| 11 | `type === "evidence"` | Evidence Collection Card |
| 12 | `type === "nistControl"` | NIST Control Card |
| 13 | `type === "kanbanBoard"` | Kanban Board Card |
| 14 | `type === "confirmation"` | Confirmation Card |
| 15 | Compliance data present | Compliance Card |
| 16 | Default | Generic Card |

### ResponseData Type Mapping

The ComplianceAgent's `BuildResponseData()` maps action types to card data:

```
Action "registered_system" → type: "systemSummary"
Action "categorization"    → type: "categorization"
Action "authorization"     → type: "authorization"
Action "dashboard"         → type: "dashboard"
```

---

## Notifications

### ConMon Notifications

The `compliance_notification_delivery` tool sends notifications via MCP response. Notification types:

| Type | Trigger | Content |
|------|---------|---------|
| ATO Expiration | Graduated alerts (90/60/30/0 days) | System name, expiration date, urgency level |
| Significant Change | Change requiring review | Change type, description, reauth requirement |
| ConMon Report | Report generated | Report period, compliance score, score delta |

### Future Channels

- **Teams Proactive Messages** — Push notifications to Teams channels
- **VS Code Information Messages** — Extension notifications in VS Code
- **Email** — Via MailKit SMTP integration

---

## Configuration

### MCP Server Settings

```json
{
  "McpServer": {
    "BaseUrl": "http://localhost:3001"
  }
}
```

### Feature Flags

| Flag | Default | Description |
|------|---------|-------------|
| `EnableAIProcessing` | true | Enable LLM-powered responses |
| `EnableAdaptiveCards` | true | Render Adaptive Cards (vs. text-only) |
| `EnablePimIntegration` | true | PIM role management features |
| `EnableCacAuth` | true | CAC/PIV authentication |

---

## Troubleshooting

| Issue | Cause | Resolution |
|-------|-------|------------|
| Bot not responding | MCP server unreachable | Verify `McpServer:BaseUrl` and server health |
| Generic card instead of rich card | Response type not mapped | Check `BuildResponseData()` in ComplianceAgent |
| "Authorization required" | Missing PIM role | Activate required PIM role first |
| Cards rendering as text | Teams version too old | Update Teams to support Adaptive Cards v1.5 |
| No follow-up buttons | Response has no structured data | Expected for simple text responses |
