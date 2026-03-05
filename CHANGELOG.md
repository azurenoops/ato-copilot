# Changelog

All notable changes to ATO Copilot are documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.19.0] - 2026-03-05

### Added

#### Feature 018: Security Assessment Plan (SAP) Generation

- **SAP Generation** (`compliance_generate_sap`) — Generate RMF Step 4 Security Assessment Plans from control baselines, OSCAL assessment objectives, STIG mappings, and evidence data. Auto-populates 15 sections with ~325 control entries (Moderate), assessment objectives, all three default methods (Examine/Interview/Test), STIG benchmark coverage, and evidence gap summaries. Supports Markdown, DOCX, and PDF output formats.
- **SAP Customization** (`compliance_update_sap`) — Update Draft SAP schedule, team, scope notes, rules of engagement, and per-control assessment method overrides with SCA justification tracking.
- **SAP Finalization** (`compliance_finalize_sap`) — Lock Draft SAPs with SHA-256 content integrity hash. Finalized SAPs are immutable and cannot be modified or re-finalized.
- **SAP Retrieval** (`compliance_get_sap`) — Retrieve a specific SAP by ID or the latest SAP for a system. System lookups prefer Finalized over Draft status.
- **SAP Listing** (`compliance_list_saps`) — List all SAPs for a system with status, dates, and scope summaries. Returns Draft and Finalized history ordered by generation date.
- **SAP Validation** — Completeness checks for assessment objectives, methods, team assignment, and schedule dates. Returns `SapValidationResult` with `IsComplete` flag, warning list, and coverage counts. Advisory only — does not block finalization.
- **SAP Status Summary** — `GetSapStatusAsync` returns latest SAP state (None/Draft/Finalized) with scope coverage for RMF lifecycle dashboard queries.
- **SAP-to-SAR Alignment** — `GetSapSarAlignmentAsync` cross-references finalized SAP controls with `ComplianceAssessment` findings to identify planned-but-unassessed and assessed-but-unplanned controls for SAR generation integration.
- **SAP Data Model** — `SecurityAssessmentPlan`, `SapControlEntry`, `SapTeamMember` entities with `SapStatus` enum, composite indexes on `(SystemId, Status)`, JSON columns for list properties, and cascade delete for team members.
- **SAP DTOs** — `SapGenerationInput`, `SapUpdateInput`, `SapDocument`, `SapFamilySummary`, `SapValidationResult`, `SapSarAlignmentResult`, `SapMethodOverrideInput`, `SapTeamMemberInput`.
- **DOCX/PDF Export** — `DocumentTemplateService` extended with `sap` merge field schema (15 fields) and `PopulateSapData` method for template-based document rendering.
- **170 Unit Tests** — 38 model tests, 67 service tests, 56 tool tests, 2 performance benchmarks (Moderate 325 controls < 15s, High 421 controls < 30s), plus 7 additional service tests for validation, status, and alignment.

### Fixed

- **ato-compliance-gate Action** — Fixed 6 bugs in `.github/actions/ato-compliance-gate/action.yml`:
  - `BLOCKING` and `ACCEPTED` counters were never incremented, causing the gate to always pass
  - `jq | while read` pipe ran findings loop in a subshell, losing counter state; replaced with process substitution
  - `compgen -G "**/*.tf"` required `globstar` which was never enabled; replaced with direct `find` commands
  - File list passed via `GITHUB_OUTPUT` risked size limits and word-splitting; now uses a temp file read line-by-line
  - `fail-on-error` input was declared but never referenced in the gate step
  - ARM template grep used double-escaped `$schema` pattern

---

## [1.18.0] - 2026-03-15

### Added

#### Feature 017: SCAP/STIG Viewer Import & Export

- **CKL Checklist Import** (`compliance_import_ckl`) — Import DISA STIG Viewer `.ckl` checklist files with automatic STIG rule resolution, CCI-to-NIST mapping, finding creation, and control effectiveness determination. Supports `Skip`/`Overwrite`/`Merge` conflict resolution strategies and dry-run preview mode.
- **XCCDF Results Import** (`compliance_import_xccdf`) — Import SCAP Compliance Checker XCCDF result files (1.1 and 1.2 namespaces) with automated rule ID resolution, benchmark score capture, and error/unknown/notchecked result handling.
- **CKL Checklist Export** (`compliance_export_ckl`) — Export assessment data as DISA STIG Viewer-compatible CHECKLIST XML for eMASS upload or offline review. Generates ASSET, STIG_INFO, and VULN elements with finding status mapping.
- **Import History** (`compliance_list_imports`) — Paginated import history with filtering by benchmark, import type, date range, and dry-run inclusion.
- **Import Summary** (`compliance_get_import_summary`) — Detailed per-finding breakdown of any import operation including unmatched rules, conflict resolution actions, and NIST control mappings.
- **CKL Parser** — Parses DISA STIG Viewer CHECKLIST XML with SI_DATA/VULN_DATA extraction, host metadata, and STIG info parsing. Includes `CklParseException` for structured error reporting.
- **XCCDF Parser** — Parses SCAP Compliance Checker XCCDF results with XCCDF 1.1/1.2 namespace detection, Benchmark-wrapped TestResult handling, score extraction, and rule ID prefix stripping.
- **CKL Generator** — Generates DISA STIG Viewer-compatible CHECKLIST XML from assessment data with proper STATUS, FINDING_DETAILS, COMMENTS, and SEVERITY_OVERRIDE elements.
- **Import Data Model** — `ScanImportRecord` and `ScanImportFinding` entities with SHA-256 duplicate detection, `ImportFindingAction` enum (Created/Updated/Skipped/Unmatched/NotApplicable/NotReviewed/Error), and `ScanImportStatus` lifecycle tracking.
- **STIG Resolution** — Control lookup by VulnId and RuleId with CCI cross-reference to NIST 800-53 controls. Benchmark-based bulk control retrieval for export.
- **Effectiveness Aggregation** — Auto-determines per-control effectiveness (Satisfied/OtherThanSatisfied) based on aggregate STIG finding status across all imports.
- **Evidence Capture** — SHA-256 hashed import evidence with benchmark metadata, finding counts, and XCCDF score data.
- **Performance** — CKL import with 500 VULNs completes in < 10 seconds; XCCDF import with 500 rule-results in < 5 seconds.
- **170+ Unit Tests** — Comprehensive test coverage for parsers, service logic, MCP tools, models, CKL generation, and integration scenarios.

### Documentation

- **Agent Tool Catalog** — 5 new tool entries with parameter tables, response schemas, RBAC roles, and RMF step mapping.
- **ISSM Guide** — STIG import workflow section with dry-run, conflict resolution, and eMASS export workflow.
- **SCA Guide** — SCAP import for assessment section with imported data usage and SAR integration.
- **Engineer Guide** — CKL import/export from VS Code via `@ato` chat participant.
- **STIG Coverage Reference** — Import processing pipeline, status mapping tables, conflict resolution, and duplicate detection.

---

## [1.17.0] - 2026-03-01

### Added

#### Feature 016: AI-Powered Agent Intelligence

- **Intelligent Tool Selection** — `BaseAgent.SelectToolsForMessage()` dynamically selects relevant tools per request, keeping within OpenAI's 128-tool limit (selects ~72 from 130 available based on message keywords and core compliance prefixes)
- **Tool Category Routing** — `ToolCategoryKeywords` maps tool prefixes (`kanban_`, `cac_`, `pim_`, `jit_`, `watch_`) to message keywords for context-aware tool inclusion; `AlwaysIncludePrefixes` ensures 20 core compliance prefixes are always available
- **Multi-Turn Conversation Support** — Enhanced system prompt instructs the AI to immediately execute tools when users provide requested data on follow-up turns, eliminating "I will route your request" non-actions
- **System Name Resolution** — AI automatically calls `compliance_list_systems` to resolve human-friendly system names (e.g., "Eagle Eye") to UUIDs before executing dependent tool calls
- **AI Path Suggestion Buttons** — `TryProcessWithAiAsync` now populates `Suggestions` via `BuildSuggestions()` so quick-action buttons appear on AI-generated responses
- **VS Code Chat Participant Icon** — Added `iconPath` to `package.json` chatParticipants definition for proper ATO Copilot branding in VS Code chat

### Changed

- **BaseAgent.BuildToolDefinitions** — Now accepts optional `message` parameter to enable context-aware tool filtering when tool count exceeds `MaxToolsPerRequest` (128)
- **ComplianceAgent AI Response Path** — AI responses now include contextual follow-up suggestions matching the keyword routing path behavior

### Fixed

- **OpenAI 400 Error** — Resolved HTTP 400 "Expected maximum length 128, but got 130" by adding intelligent tool selection that caps tool definitions per request
- **Duplicate Attribution Text** — Removed explicit "Processed by: Compliance Agent" rendering in VS Code extension; agent context is conveyed through the Tools Used summary table
- **Missing Suggestion Buttons** — AI path responses now include suggestion buttons (`Register a New System`, `Define Authorization Boundary`, `Assign RMF Roles`, etc.)
- **Multi-Turn Tool Execution** — AI no longer summarizes user input back or says "I will route" on follow-up turns; directly executes the appropriate tool with gathered parameters

### Documentation

- **User Documentation** — Feature 016 user guide with mkdocs (`mkdocs build --strict` passing)

---

## [1.16.0] - 2026-02-20

### Added

#### Feature 015 Phase 17: Monitoring & Alert Pipeline Integration

- **ComplianceAlert → RegisteredSystem FK** — nullable `RegisteredSystemId` FK on `ComplianceAlert` with `SetNull` delete behavior and indexed lookup (T231, T232)
- **AlertManager → Notification Pipeline** — optional `IAlertNotificationService` injection; sends notification after alert persistence with graceful failure handling (T234, T236)
- **SystemSubscriptionResolver** — new service resolving `subscriptionId` → `RegisteredSystemId` via `AzureProfile.SubscriptionIds` reverse lookup with 5-minute in-memory cache (T237)
- **Watch → Alert Enrichment** — `ComplianceWatchService` populates `ComplianceAlert.RegisteredSystemId` before all alert creation using `ISystemSubscriptionResolver` (T238)
- **ConMon Report Watch Data Enrichment** — `GenerateReportAsync()` now includes monitoring enabled status, active drift alert count, auto-remediation rule count, and last monitoring check timestamp (T240, T241, T242)
- **ConMon → Alert Pipeline** — `CheckExpirationAsync()` auto-creates graduated alerts (Info@90d→Low, Warning@60d→Medium, Urgent@30d→High, Expired→Critical); `ReportChangeAsync()` auto-creates High severity alert when `RequiresReauthorization = true` (T244, T245)
- **NotificationDeliveryTool Enhancement** — `compliance_send_notification` now routes through `IAlertManager` for alert pipeline integration with `alert_pipeline` channel (T246)
- **Drift → Significant Change** — `DetectDriftAsync()` auto-creates ConMon significant change when drifted resource count exceeds configurable `SignificantDriftThreshold` (default: 5) via `IServiceScopeFactory` scoped resolution (T248, T249)
- **MonitoringOptions Expansion** — added `SignificantDriftThreshold`, `AutoCreateSignificantChanges`, `MaxDriftAlertsPerReport` configuration properties (T248)
- **27 New Unit Tests** — AlertManager notification (4), SystemSubscriptionResolver (7), ConMon report enrichment (4), ConMon alert pipeline (7), drift significant change (3), plus integration test coverage (T235, T239, T243, T247, T250)
- **Documentation Updates** — monitoring pipeline ASCII diagram in `overview.md`, Phase 17 enhancement notes in `agent-tool-catalog.md` and `issm-guide.md` (T251)

## [1.15.0] - 2026-02-15

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

## [1.14.0] - 2026-1-15

### Added

- Feature 014: Agent UI Enrichment — rich tool output formatting and Adaptive Card rendering

## [1.13.0] - 2026-1-12

### Added

- Feature 013: Copilot Everywhere — multi-channel deployment (VS Code, Teams, CLI)

## [1.12.0] - 2026-1-10

### Added

- Feature 012: Task Enrichment — Kanban task scripts, validation, and remediation integration

## [1.11.0] - 2026-1-07

### Added

- Feature 011: Azure OpenAI Agents SDK integration

## [1.10.0] - 2026-1-07

### Added

- Feature 010: Knowledge Base Agent — RMF, STIG, DoD, impact level services

## [1.9.0] - 2026-1-05

### Added

- Feature 009: Remediation Engine v2 — AI-powered remediation planning and script execution

## [1.8.0] - 2026-1-05

### Added

- Feature 008: Compliance Engine — automated scanning, evidence collection, assessment persistence

## [1.7.0] - 2026-1-05

### Added

- Feature 007: NIST Controls Service — 800-53 Rev 5 catalog with baseline selection

## [1.6.0] - 2026-1-05

### Added

- Feature 006: Chat Application — web-based chat interface with conversation management

## [1.5.0] - 2026-1-05

### Added

- Feature 005: Compliance Watch — real-time monitoring, alerting, and auto-remediation rules

## [1.4.0] - 2026-1-05

### Added

- Feature 004: Kanban User Context — user-scoped task boards with assignment tracking

## [1.3.0] - 2026-1-02

### Added

- Feature 003: CAC Authentication & PIM — smart card auth, privileged role management, JIT access

## [1.2.0] - 2026-1-02

### Added

- Feature 002: Remediation Kanban — task board with workflow states and comment system

## [1.1.0] - 2026-1-02

### Added

- Feature 001: Core Compliance — MCP server, compliance assessment, document generation, evidence collection
