# Changelog

All notable changes to ATO Copilot are documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.15.0] - 2025-01-15

### Added

#### Feature 015: Persona-Driven RMF Workflows

- **RMF Lifecycle Tools (56 new MCP tools)**
  - Prepare: `compliance_register_system` — system registration with metadata
  - Categorize: `compliance_categorize_system` — FIPS 199 categorization with SP 800-60 info types
  - Select: `compliance_select_baseline`, `compliance_tailor_baseline`, `compliance_set_inheritance` — baseline selection, tailoring, and CRM inheritance
  - Implement: `compliance_write_narrative`, `compliance_batch_populate`, `compliance_generate_ssp` — control narratives, batch SSP population, QuestPDF/ClosedXML SSP generation
  - Assess: `compliance_assess_control`, `compliance_record_effectiveness`, `compliance_generate_sar` — control assessment, effectiveness tracking, Security Assessment Report
  - Authorize: `compliance_issue_authorization`, `compliance_accept_risk`, `compliance_create_poam`, `compliance_update_poam`, `compliance_generate_rar`, `compliance_bundle_authorization_package` — ATO/IATT/DATO decisions, risk acceptance, POA&M management, Risk Assessment Report, authorization package bundling
  - Monitor: `compliance_create_conmon_plan`, `compliance_generate_conmon_report`, `compliance_track_ato_expiration`, `compliance_report_significant_change`, `compliance_reauthorization_workflow`, `compliance_multi_system_dashboard`, `compliance_send_notification` — continuous monitoring plans, periodic reports, graduated expiration alerts, significant change detection, reauthorization triggers, portfolio dashboard

- **Interoperability Tools**
  - `compliance_emass_export_controls`, `compliance_emass_export_poam`, `compliance_emass_import`, `compliance_emass_export_oscal` — eMASS and OSCAL import/export
  - `compliance_show_stig_mapping` — NIST-to-STIG cross-reference lookup

- **Template & Report Tools**
  - `compliance_list_templates`, `compliance_generate_from_template`, `compliance_save_template` — customizable document templates for SSP, SAR, POA&M
  - QuestPDF-based PDF generation for SSP and authorization packages
  - ClosedXML-based Excel export for POA&M and control matrices

- **18 New EF Core Entities**
  - RegisteredSystem, SecurityCategorization, InformationType, AuthorizationBoundary
  - ControlBaseline, ControlTailoring, ControlInheritance, ControlImplementation
  - ControlEffectiveness, AssessmentRecord
  - AuthorizationDecision, RiskAcceptance, PoamItem, PoamMilestone
  - ConMonPlan, ConMonReport, SignificantChange
  - RmfRoleAssignment

- **AuthorizingOfficial RBAC Role**
  - New role with authorization decision, risk acceptance, and reauthorization permissions
  - Integrated into PIM eligible roles and compliance authorization middleware

- **Adaptive Cards (4 new for Teams bot)**
  - System Summary Card — registered system overview
  - Categorization Card — FIPS 199 security categories
  - Authorization Card — ATO decision details
  - Dashboard Card — multi-system portfolio view

- **VS Code Extension Enhancements**
  - RMF Overview webview panel with system status, timeline, and metrics
  - IaC compliance diagnostics with CAT severity mapping
  - Code actions for STIG remediation suggestions

- **GitHub Actions Compliance Gate**
  - `.github/actions/ato-compliance-gate/action.yml` — composite action for PR-level IaC scanning
  - Blocks on CAT I/II findings, respects risk acceptances

- **Cross-Cutting Quality**
  - Structured logging tests (AuditLoggingMiddleware validation)
  - Progress indicator tests (BaseTool ExecuteAsync instrumentation)

### Changed

- **PimService** — Replaced hardcoded eligible roles with Microsoft Graph PIM API integration (falls back to simulated data when Graph client not configured)
- **RemediationScriptExecutor** — Replaced `Task.Delay` simulation with real subprocess execution via `Process.Start` (PowerShell/bash)
- **Deployment docs** — Added Feature 015 configuration section with new entities, packages, and Azure permissions
- **Agent-tool-catalog** — Updated with all 56 Feature 015 tools

### Fixed

- AsyncLocal context propagation in singleton ComplianceAgent
- DATO test assertion for expiration tracking (returns None alert level)
- VS Code extension test imports for compliance diagnostics

---

## [1.14.0] - 2024-12-15

### Added

- Feature 014: Agent UI Enrichment — rich tool output formatting and Adaptive Card rendering

## [1.13.0] - 2024-12-01

### Added

- Feature 013: Copilot Everywhere — multi-channel deployment (VS Code, Teams, CLI)

## [1.12.0] - 2024-11-15

### Added

- Feature 012: Task Enrichment — Kanban task scripts, validation, and remediation integration

## [1.11.0] - 2024-11-01

### Added

- Feature 011: Azure OpenAI Agents SDK integration

## [1.10.0] - 2024-10-15

### Added

- Feature 010: Knowledge Base Agent — RMF, STIG, DoD, impact level services

## [1.9.0] - 2024-10-01

### Added

- Feature 009: Remediation Engine v2 — AI-powered remediation planning and script execution

## [1.8.0] - 2024-09-15

### Added

- Feature 008: Compliance Engine — automated scanning, evidence collection, assessment persistence

## [1.7.0] - 2024-09-01

### Added

- Feature 007: NIST Controls Service — 800-53 Rev 5 catalog with baseline selection

## [1.6.0] - 2024-08-15

### Added

- Feature 006: Chat Application — web-based chat interface with conversation management

## [1.5.0] - 2024-08-01

### Added

- Feature 005: Compliance Watch — real-time monitoring, alerting, and auto-remediation rules

## [1.4.0] - 2024-07-15

### Added

- Feature 004: Kanban User Context — user-scoped task boards with assignment tracking

## [1.3.0] - 2024-07-01

### Added

- Feature 003: CAC Authentication & PIM — smart card auth, privileged role management, JIT access

## [1.2.0] - 2024-06-15

### Added

- Feature 002: Remediation Kanban — task board with workflow states and comment system

## [1.1.0] - 2024-06-01

### Added

- Feature 001: Core Compliance — MCP server, compliance assessment, document generation, evidence collection
