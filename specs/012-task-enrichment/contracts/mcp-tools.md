# MCP Tool Contracts: Task Enrichment

**Feature**: 012-task-enrichment  
**Date**: 2026-02-25  
**Status**: Complete

---

## New Tool: `kanban_generate_script`

**Class**: `KanbanGenerateScriptTool` extends `BaseTool`  
**Location**: `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`

### Schema

```json
{
  "name": "kanban_generate_script",
  "description": "Generate an AI-powered remediation script for a remediation task. Uses Azure OpenAI when available, falls back to NIST-template-based scripts. Supports Azure CLI, PowerShell, Bicep, and Terraform output formats.",
  "parameters": {
    "type": "object",
    "properties": {
      "task_id": {
        "type": "string",
        "description": "Task ID (GUID) or task number (e.g., 'REM-001')"
      },
      "script_type": {
        "type": "string",
        "enum": ["AzureCli", "PowerShell", "Bicep", "Terraform"],
        "description": "Target script language. Default: AzureCli"
      },
      "force": {
        "type": "boolean",
        "description": "If true, regenerate even if a script already exists. Default: false"
      }
    },
    "required": ["task_id"]
  }
}
```

### Response Envelope

**Success**:
```json
{
  "status": "success",
  "data": {
    "taskId": "a1b2c3d4-...",
    "taskNumber": "REM-005",
    "controlId": "AC-2.1",
    "scriptType": "AzureCli",
    "generationMethod": "AI",
    "remediationScript": "#!/bin/bash\n# Remediation for AC-2.1: Enable MFA\naz ad user list --query ...",
    "estimatedDuration": "00:05:00",
    "scriptLength": 342
  },
  "metadata": {
    "tool": "kanban_generate_script",
    "executionTime": "3.2s",
    "timestamp": "2026-02-25T14:30:00Z"
  }
}
```

**Error (task not found)**:
```json
{
  "status": "error",
  "data": null,
  "metadata": {
    "tool": "kanban_generate_script",
    "errorCode": "TASK_NOT_FOUND",
    "message": "Task 'REM-999' not found on any board.",
    "suggestion": "Use kanban_task_list to see available tasks, or provide a valid task ID."
  }
}
```

**Error (already has script, force=false)**:
```json
{
  "status": "error",
  "data": null,
  "metadata": {
    "tool": "kanban_generate_script",
    "errorCode": "SCRIPT_EXISTS",
    "message": "Task 'REM-005' already has a remediation script.",
    "suggestion": "Set force=true to regenerate, or use kanban_get_task to view the existing script."
  }
}
```

---

## New Tool: `kanban_generate_validation`

**Class**: `KanbanGenerateValidationTool` extends `BaseTool`  
**Location**: `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`

### Schema

```json
{
  "name": "kanban_generate_validation",
  "description": "Generate validation criteria for verifying a remediation task was applied correctly. Uses AI when available, falls back to template-based validation steps.",
  "parameters": {
    "type": "object",
    "properties": {
      "task_id": {
        "type": "string",
        "description": "Task ID (GUID) or task number (e.g., 'REM-001')"
      },
      "force": {
        "type": "boolean",
        "description": "If true, regenerate even if validation criteria already exists. Default: false"
      }
    },
    "required": ["task_id"]
  }
}
```

### Response Envelope

**Success**:
```json
{
  "status": "success",
  "data": {
    "taskId": "a1b2c3d4-...",
    "taskNumber": "REM-005",
    "controlId": "AC-2.1",
    "generationMethod": "AI",
    "validationCriteria": "1. Verify MFA is enabled for all privileged accounts via Azure AD portal\n2. Run `az ad user list --filter \"userType eq 'Member'\"` and confirm MFA status\n3. Re-scan subscription for AC-2.1 compliance and verify finding status changed to Remediated"
  },
  "metadata": {
    "tool": "kanban_generate_validation",
    "executionTime": "2.8s",
    "timestamp": "2026-02-25T14:31:00Z"
  }
}
```

**Error (task not found)**:
```json
{
  "status": "error",
  "data": null,
  "metadata": {
    "tool": "kanban_generate_validation",
    "errorCode": "TASK_NOT_FOUND",
    "message": "Task 'REM-999' not found on any board.",
    "suggestion": "Use kanban_task_list to see available tasks, or provide a valid task ID."
  }
}
```

**Error (criteria already exists, force=false)**:
```json
{
  "status": "error",
  "data": null,
  "metadata": {
    "tool": "kanban_generate_validation",
    "errorCode": "CRITERIA_EXISTS",
    "message": "Task 'REM-005' already has validation criteria.",
    "suggestion": "Set force=true to regenerate, or use kanban_get_task to view the existing criteria."
  }
}
```

---

## Modified Tool: `kanban_get_task` (Lazy Enrichment)

**Class**: `KanbanGetTaskTool` (existing)  
**Location**: `src/Ato.Copilot.Agents/Compliance/Tools/KanbanTools.cs`

### Behavioral Change

Before returning task details, if `task.RemediationScript` is null and `ITaskEnrichmentService` is resolvable from the scoped `IServiceProvider`:

1. Look up linked finding via `task.FindingId` → `context.Findings.FirstOrDefaultAsync(f => f.Id == task.FindingId)`
2. If finding found: call `enrichmentService.EnrichTaskAsync(task, finding)`
3. Persist changes via `context.SaveChangesAsync()`
4. Return the enriched task details

If `ITaskEnrichmentService` is not registered (e.g., during tests), skip enrichment silently.

### Schema

No changes to existing schema. Parameters remain: `task_id` (required).

### Response Change

The response `data` object will now include populated values for:
- `remediationScript`: Previously always null → now AI or template content
- `validationCriteria`: Previously always null → now AI or template content
- `remediationScriptType`: **NEW field** — "AzureCli", "PowerShell", etc. (null if unenriched)

---

## System Prompt Update

**File**: `src/Ato.Copilot.Agents/Compliance/Prompts/ComplianceAgent.prompt.txt`

Add to Kanban Board tools section (after `kanban_archive_board`):

```
19. **kanban_generate_script** — Generate an AI-powered remediation script for a task. Supports Azure CLI (default), PowerShell, Bicep, and Terraform output formats. Use when a task has no script or when the user wants a different script type.
20. **kanban_generate_validation** — Generate validation criteria for verifying a task's remediation was applied correctly. Produces step-by-step verification instructions specific to the task's control and resources.
```
