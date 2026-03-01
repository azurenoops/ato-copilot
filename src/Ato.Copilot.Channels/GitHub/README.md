# GitHub Copilot Extension — ATO Copilot

This directory contains the scaffold for the GitHub Copilot Extension that integrates ATO Copilot as a `@ato` chat participant.

## Structure

```
GitHub/
├── README.md                  # This file
├── manifest.json              # Copilot extension manifest
├── ATOParticipant.cs     # Chat participant implementation
└── InlineComplianceChecker.cs # Inline compliance checking (FR-064)
```

## Features (FR-064)

- **@ato chat participant**: Users type `@ato` in GitHub Copilot Chat to interact with the ATO Copilot
- **Inline compliance checking**: Real-time compliance annotations on IaC files (Bicep, Terraform, ARM)
- **Control explanations**: Hover tooltips with NIST 800-53 control descriptions
- **Assessment triggers**: Run compliance assessments directly from the editor

## Integration

The extension communicates with the ATO Copilot MCP server via stdio transport:
```
@ato assess my environment against FedRAMP High
@ato explain control AC-2
@ato generate a compliant AKS template
```

## Status

**Scaffold only** — Full implementation requires GitHub Copilot Extension SDK GA release.
