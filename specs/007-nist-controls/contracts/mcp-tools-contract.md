# MCP Tool Contracts: NIST Knowledge Base Tools

**Feature Branch**: `007-nist-controls` | **Date**: 2026-02-23

## Overview

Two new MCP tools are added to the Compliance Agent, extending `BaseTool` per Constitution Principle II. They expose NIST 800-53 control lookup capabilities to users via the chat interface and MCP protocol.

---

## NistControlSearchTool

### Tool Schema

```json
{
  "name": "search_nist_controls",
  "description": "Search NIST SP 800-53 Rev 5 controls by keyword. Returns matching controls with ID, title, and relevant excerpt.",
  "parameters": {
    "query": {
      "type": "string",
      "description": "Search term to match against control IDs, titles, and descriptions (e.g., 'encryption', 'access control', 'audit')",
      "required": true
    },
    "family": {
      "type": "string",
      "description": "Optional 2-letter control family filter (e.g., 'AC', 'SC', 'AU')",
      "required": false
    },
    "max_results": {
      "type": "integer",
      "description": "Maximum number of results to return (default: 10, max: 25)",
      "required": false
    }
  }
}
```

### Response Format

**Success (results found):**
```json
{
  "status": "success",
  "data": {
    "query": "encryption",
    "family_filter": null,
    "total_matches": 4,
    "controls": [
      {
        "id": "SC-13",
        "title": "Cryptographic Protection",
        "family": "System and Communications Protection",
        "excerpt": "Implement the following types of cryptography required for each specified cryptographic use..."
      },
      {
        "id": "SC-28",
        "title": "Protection of Information at Rest",
        "family": "System and Communications Protection",
        "excerpt": "Protect the confidentiality and integrity of the following information at rest..."
      }
    ]
  },
  "metadata": {
    "tool": "search_nist_controls",
    "execution_time_ms": 45,
    "timestamp": "2026-02-23T14:30:00Z"
  }
}
```

**Success (no results):**
```json
{
  "status": "success",
  "data": {
    "query": "blockchain",
    "family_filter": null,
    "total_matches": 0,
    "controls": [],
    "message": "No controls found matching your search for 'blockchain'. Try broader terms like 'cryptography', 'access', or 'audit'."
  },
  "metadata": {
    "tool": "search_nist_controls",
    "execution_time_ms": 12,
    "timestamp": "2026-02-23T14:31:00Z"
  }
}
```

**Error (catalog unavailable):**
```json
{
  "status": "error",
  "errorCode": "CATALOG_UNAVAILABLE",
  "message": "The NIST controls catalog is currently unavailable. Please try again later.",
  "suggestion": "The catalog may still be loading at startup. Wait 15 seconds and retry."
}
```

---

## NistControlExplainerTool

### Tool Schema

```json
{
  "name": "explain_nist_control",
  "description": "Get a detailed explanation of a specific NIST SP 800-53 Rev 5 control, including its statement, guidance, and assessment objectives.",
  "parameters": {
    "control_id": {
      "type": "string",
      "description": "The NIST control identifier (e.g., 'AC-2', 'SC-7', 'AU-6(1)')",
      "required": true
    }
  }
}
```

### Response Format

**Success:**
```json
{
  "status": "success",
  "data": {
    "control_id": "SC-7",
    "title": "Boundary Protection",
    "family": "System and Communications Protection",
    "statement": "a. Monitor and control communications at the external managed interfaces to the system and at key internal managed interfaces within the system; b. Implement subnetworks for publicly accessible system components that are physically or logically separated from internal organizational networks; and c. Connect to external networks or systems only through managed interfaces...",
    "guidance": "Managed interfaces include gateways, routers, firewalls, guards, network-based malicious code analysis, virtualization systems, or encrypted tunnels...",
    "objectives": [
      "Determine if communications at managed interfaces are monitored",
      "Determine if communications at managed interfaces are controlled",
      "Determine if subnetworks for publicly accessible components are implemented"
    ],
    "enhancement_count": 29,
    "catalog_version": "5.2.0"
  },
  "metadata": {
    "tool": "explain_nist_control",
    "execution_time_ms": 23,
    "timestamp": "2026-02-23T14:32:00Z"
  }
}
```

**Error (control not found):**
```json
{
  "status": "error",
  "errorCode": "CONTROL_NOT_FOUND",
  "message": "Control 'ZZ-99' was not found in the NIST SP 800-53 Rev 5 catalog.",
  "suggestion": "Check the control ID format (e.g., 'AC-2', 'SC-7', 'AU-6(1)'). Use 'search_nist_controls' to find controls by keyword."
}
```

---

## Health Check Contract

### Endpoint

`GET /health` (standard ASP.NET Core health check pipeline)

### Response

```json
{
  "status": "Healthy",
  "results": {
    "nist-controls": {
      "status": "Healthy",
      "description": "NIST SP 800-53 Rev 5 catalog is loaded and functional",
      "data": {
        "version": "5.2.0",
        "validTestControls": "3/3",
        "testControls": ["AC-3", "SC-13", "AU-2"],
        "responseTimeMs": 12,
        "timestamp": "2026-02-23T14:30:00Z",
        "cacheDurationHours": 24,
        "catalogSource": "remote"
      }
    }
  }
}
```

### Status Mapping

| Status | Condition |
|--------|-----------|
| `Healthy` | Version present + 3/3 test controls valid + response < 5s |
| `Degraded` | Version present + 1-2/3 test controls valid |
| `Unhealthy` | No version OR 0/3 valid OR exception OR response > 5s |
