# MCP Tool Contracts: KnowledgeBase Agent

**Feature**: `010-knowledgebase-agent` | **Date**: 2026-02-24

All 7 KB tools are exposed via MCP protocol with `kb_` prefixed tool IDs. Each tool follows the standard MCP tool contract format used by existing Compliance tools.

**Error response convention**: All error responses include `errorCode` (machine-readable string) per Constitution VII. Tool-level errors use codes like `CONTROL_NOT_FOUND`, `STIG_NOT_FOUND`, `INVALID_STEP`, `DATA_LOAD_ERROR`. The orchestrator fallback uses `NO_AGENT_MATCH`. The `success: false` boolean aligns with the existing agent-layer `AgentResponse.Success` pattern used throughout the codebase.

---

## kb_explain_nist_control

**Description**: Explain a NIST 800-53 control with statement, guidance, Azure implementation advice, and related controls.

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "control_id": {
      "type": "string",
      "description": "NIST 800-53 control identifier (e.g., 'AC-2', 'IA-2(1)')"
    }
  },
  "required": ["control_id"]
}
```

### Output Schema (success)

```json
{
  "success": true,
  "data": {
    "control_id": "AC-2",
    "title": "Account Management",
    "family": "Access Control",
    "statement": "...",
    "supplemental_guidance": "...",
    "azure_implementation": {
      "service": "Microsoft Entra ID / Azure RBAC",
      "guidance": "..."
    },
    "related_controls": ["AC-3", "AC-6", "IA-2"],
    "disclaimer": "This is informational guidance only..."
  },
  "metadata": {
    "tool": "kb_explain_nist_control",
    "timestamp": "2026-02-24T10:00:00Z",
    "duration_ms": 45,
    "cached": false
  }
}
```

### Output Schema (error)

```json
{
  "success": false,
  "error": "Control 'ZZ-99' not found",
  "errorCode": "CONTROL_NOT_FOUND",
  "suggestion": "Valid control families: AC, AT, AU, CA, CM, CP, IA, IR, MA, MP, PE, PL, PM, PS, RA, SA, SC, SI, SR",
  "metadata": {
    "tool": "kb_explain_nist_control",
    "timestamp": "2026-02-24T10:00:00Z",
    "duration_ms": 12
  }
}
```

---

## kb_search_nist_controls

**Description**: Search NIST 800-53 controls by keyword, optionally filtered by family.

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "search_term": {
      "type": "string",
      "description": "Keyword to search across control titles, statements, and guidance"
    },
    "family": {
      "type": "string",
      "description": "Optional control family filter (e.g., 'AC', 'SC')"
    },
    "max_results": {
      "type": "integer",
      "description": "Maximum results to return (default: 10)",
      "default": 10
    }
  },
  "required": ["search_term"]
}
```

### Output Schema (success)

```json
{
  "success": true,
  "data": {
    "search_term": "encryption",
    "family_filter": null,
    "total_matches": 15,
    "results_returned": 10,
    "results": [
      {
        "control_id": "SC-8",
        "title": "Transmission Confidentiality and Integrity",
        "family": "System and Communications Protection",
        "description": "..."
      }
    ]
  },
  "metadata": { "tool": "kb_search_nist_controls", "timestamp": "...", "duration_ms": 120 }
}
```

---

## kb_explain_stig

**Description**: Explain a STIG control with severity, check/fix procedures, NIST mappings, and Azure guidance.

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "stig_id": {
      "type": "string",
      "description": "STIG vulnerability identifier (e.g., 'V-12345')"
    }
  },
  "required": ["stig_id"]
}
```

### Output Schema (success)

```json
{
  "success": true,
  "data": {
    "stig_id": "V-12345",
    "title": "Windows Server must enforce password complexity",
    "severity": "High",
    "category": "CAT I",
    "description": "...",
    "check_text": "...",
    "fix_text": "...",
    "nist_controls": ["IA-5", "IA-5(1)"],
    "cci_refs": ["CCI-000192", "CCI-000193"],
    "azure_implementation": {
      "service": "Microsoft Entra ID",
      "configuration": "Password Protection policies",
      "policy": "azure-policy-id",
      "automation": "az ad policy update ..."
    },
    "cross_references": {
      "dod_instructions": ["DoDI 8510.01"]
    }
  },
  "metadata": { "tool": "kb_explain_stig", "timestamp": "...", "duration_ms": 35 }
}
```

---

## kb_search_stigs

**Description**: Search STIG controls by keyword and/or severity.

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "search_term": {
      "type": "string",
      "description": "Keyword to search across STIG titles, descriptions, and categories"
    },
    "severity": {
      "type": "string",
      "description": "Optional severity filter: 'high'/'cat1'/'cati', 'medium'/'cat2'/'catii', 'low'/'cat3'/'catiii'"
    },
    "max_results": {
      "type": "integer",
      "description": "Maximum results to return (default: 10)",
      "default": 10
    }
  },
  "required": ["search_term"]
}
```

### Output Schema (success)

```json
{
  "success": true,
  "data": {
    "search_term": "password",
    "severity_filter": "High",
    "total_matches": 8,
    "results": [
      {
        "stig_id": "V-12345",
        "title": "...",
        "severity": "High",
        "category": "CAT I",
        "nist_controls": ["IA-5"]
      }
    ]
  },
  "metadata": { "tool": "kb_search_stigs", "timestamp": "...", "duration_ms": 95 }
}
```

---

## kb_explain_rmf

**Description**: Explain the RMF process — full overview, specific step, service-specific guidance, or deliverables.

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "topic": {
      "type": "string",
      "description": "Optional topic: 'navy', 'army', 'deliverables', or general RMF topic"
    },
    "step": {
      "type": "integer",
      "description": "Optional specific RMF step number (1-6)",
      "minimum": 1,
      "maximum": 6
    }
  },
  "required": []
}
```

### Output Schema (success — full overview)

```json
{
  "success": true,
  "data": {
    "topic": null,
    "step": null,
    "overview": "Risk Management Framework (RMF) — 6-Step Process",
    "steps": [
      {
        "step": 1,
        "title": "Categorize",
        "description": "...",
        "activities": ["..."],
        "outputs": ["..."],
        "roles": ["..."]
      }
    ]
  },
  "metadata": { "tool": "kb_explain_rmf", "timestamp": "...", "duration_ms": 20 }
}
```

---

## kb_explain_impact_level

**Description**: Explain a DoD Impact Level (IL2-IL6) or FedRAMP baseline with security requirements and Azure guidance.

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "level": {
      "type": "string",
      "description": "Impact level ('IL2', 'IL4', 'IL5', 'IL6', '5', 'IL-5') or FedRAMP baseline ('High', 'Moderate', 'Low', 'FedRAMP-High') or 'compare'/'all' for comparison"
    }
  },
  "required": []
}
```

### Output Schema (success — single level)

```json
{
  "success": true,
  "data": {
    "level": "IL5",
    "name": "Impact Level 5",
    "data_classification": "CUI and National Security Systems",
    "security_requirements": {
      "encryption": "FIPS 140-2 Level 1 minimum",
      "network": "Dedicated connections, CAP required",
      "personnel": "Favorable SSBI adjudication",
      "physical_security": "..."
    },
    "azure_implementation": {
      "region": "Azure Government Secret regions",
      "network": "ExpressRoute with encryption",
      "identity": "CAC/PIV authentication",
      "encryption": "Customer-managed keys via FIPS 140-2 L2+ HSM",
      "services": ["Azure Government Secret", "Isolated compute"]
    },
    "additional_controls": ["SC-28(1)", "AC-2(7)"]
  },
  "metadata": { "tool": "kb_explain_impact_level", "timestamp": "...", "duration_ms": 15 }
}
```

---

## kb_get_fedramp_template_guidance

**Description**: Get FedRAMP authorization template guidance (SSP, POA&M, continuous monitoring, package overview).

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "template_type": {
      "type": "string",
      "description": "Template type: 'SSP', 'POAM'/'POA&M', 'CRM'/'CONMON', or omit for package overview"
    },
    "baseline": {
      "type": "string",
      "description": "FedRAMP baseline (default: 'High')",
      "default": "High"
    }
  },
  "required": []
}
```

### Output Schema (success)

```json
{
  "success": true,
  "data": {
    "template_type": "SSP",
    "baseline": "High",
    "title": "System Security Plan",
    "description": "...",
    "sections": [
      {
        "name": "System Information",
        "description": "...",
        "required_elements": ["System name", "System owner", "..."]
      }
    ],
    "azure_mappings": {
      "System Inventory": "Azure Resource Graph",
      "Network Diagram": "Azure Network Watcher"
    },
    "authorization_checklist": [
      { "item": "SSP completed", "description": "...", "required": true }
    ]
  },
  "metadata": { "tool": "kb_get_fedramp_template_guidance", "timestamp": "...", "duration_ms": 25 }
}
```

---

## Orchestrator Routing Contract

### Agent Selection Flow

```
User Message
    │
    ▼
AgentOrchestrator.SelectAgent(message)
    │
    ├── KnowledgeBaseAgent.CanHandle(message) → 0.0–1.0
    ├── ComplianceAgent.CanHandle(message) → 0.0–1.0
    ├── ConfigurationAgent.CanHandle(message) → 0.0–1.0
    │
    ▼
Select highest score ≥ 0.3 threshold
    │
    ├── Found → agent.ProcessAsync(message, context, ct)
    └── None above threshold → graceful fallback response
```

### Fallback Response

```json
{
  "success": false,
  "response": "I'm not sure how to help with that. I can assist with:\n- **Compliance knowledge**: NIST controls, STIGs, RMF process, impact levels, FedRAMP templates\n- **Compliance assessments**: Scanning, monitoring, remediation\n- **Configuration**: Setting up subscriptions, frameworks, baselines",
  "errorCode": "NO_AGENT_MATCH",
  "agent_name": "Orchestrator"
}
```
