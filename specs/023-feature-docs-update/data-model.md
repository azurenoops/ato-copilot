# Data Model — Feature 023: Documentation Update (Features 017–022)

**Date**: 2026-03-11
**Purpose**: Define the documentation entities and their relationships for the implementation plan.

---

## Documentation Domain Model

This feature operates on **documentation entities**, not database entities. The "data model" for this feature describes the content objects being created/updated and their relationships.

---

## Entity: Tool Catalog Entry

A complete documentation block for one MCP tool in `docs/architecture/agent-tool-catalog.md`.

| Field | Type | Source | Notes |
|-------|------|--------|-------|
| Tool Name | string | Source code `Name` property | Exact match required |
| MCP Method | string | Source code class name suffix | e.g., `GenerateSapAsync` |
| Description | text | Source code `Description` property | Can be paraphrased for clarity |
| Parameters | table | Source code `Parameters` dictionary | Name, Type, Required, Description |
| Response JSON | code block | Source code `ExecuteCoreAsync` return | Standard envelope: status/data/metadata |
| RBAC Roles | list | `ServiceCollectionExtensions.cs` + `RequiredPimTier` | Allowed and Denied roles |
| Use Cases | blockquote list | NL query patterns from persona guides | At least 2 per tool |

**Relationships**: Each entry belongs to a Feature Section header. Each entry produces corresponding rows in Tool Inventory, NL Query Reference, Persona Guides, and MCP Server Reference.

---

## Entity: Tool Inventory Row

A single row in the tool inventory table in `docs/reference/tool-inventory.md`.

| Field | Type | Source |
|-------|------|--------|
| Sequential Number | integer | Auto-increment from last entry (currently #118) |
| Tool Name | string | Must match Tool Catalog Entry |
| Description | string | Short (< 100 chars) |
| RMF Phase(s) | string | Prepare, Categorize, Select, Implement, Assess, Authorize, Monitor |
| RBAC Roles | string | Abbreviations: ISSM, ISSO, SCA, AO, Eng, Admin, All |

**Relationships**: Belongs to a Category. Category maps to Feature.

---

## Entity: Persona Guide Section

A workflow-oriented section within a persona guide markdown file.

| Field | Type | Source |
|-------|------|--------|
| Section Heading | string (## or ###) | Workflow name |
| Prerequisites | list | Required prior steps/tools |
| Steps | numbered list | Step → Tool invocation → Expected result |
| Tool Invocations | code blocks | Tool name + parameters + expected response |
| RBAC Notes | admonition | Any access restrictions for this persona |

**Relationships**: References Tool Catalog Entries. May cross-reference other Persona Guide Sections.

---

## Entity: RMF Phase Section

Content block within an RMF phase guide file.

| Field | Type | Source |
|-------|------|--------|
| Phase | enum | Prepare, Categorize, Assess, Authorize, Monitor |
| Persona Responsibilities | sub-sections | Tasks per persona with tool references |
| NL Query Examples | blockquotes | Natural language → tool mapping |
| Documents Produced | table | Document name, owner, format, gate dependency |
| Gate Requirements | sub-section | Prerequisites for advancing to next phase |

**Relationships**: References Tool Catalog Entries and Persona Guide Sections.

---

## Entity: Release Notes File

Version-stamped changelog file in `docs/release-notes/`.

| Field | Type | Source |
|-------|------|--------|
| Version | string | Semantic version (v1.18.0, v1.19.0) |
| Release Date | date | Feature completion date |
| Branch | string | Feature branch name |
| Test Counts | string | Passing tests at time of release |
| New MCP Tools | table | Tool name, Description, RBAC |
| Key Capabilities | sections | Per-tool capability bullet lists |
| Data Model | tables | New entities and enumerations |

---

## Category Assignments

### Category 10: SAP Generation (Feature 018)

| # | Tool | Phase | Roles |
|---|------|-------|-------|
| 119 | `compliance_generate_sap` | Assess | SCA, ISSM |
| 120 | `compliance_update_sap` | Assess | SCA, ISSM |
| 121 | `compliance_finalize_sap` | Assess | SCA |
| 122 | `compliance_get_sap` | Assess | All |
| 123 | `compliance_list_saps` | Assess | All |

### Category 11: Privacy & Interconnections (Feature 021)

| # | Tool | Phase | Roles |
|---|------|-------|-------|
| 124 | `compliance_create_pta` | Prepare | ISSM, ISSO |
| 125 | `compliance_generate_pia` | Prepare | ISSO, ISSM |
| 126 | `compliance_review_pia` | Prepare | ISSM |
| 127 | `compliance_check_privacy_compliance` | Prepare | All |
| 128 | `compliance_add_interconnection` | Prepare | ISSM, ISSO, Eng |
| 129 | `compliance_list_interconnections` | Prepare | All |
| 130 | `compliance_update_interconnection` | Prepare | ISSM, ISSO |
| 131 | `compliance_generate_isa` | Prepare | ISSM |
| 132 | `compliance_register_agreement` | Prepare | ISSM |
| 133 | `compliance_update_agreement` | Prepare | ISSM |
| 134 | `compliance_certify_no_interconnections` | Prepare | ISSM |
| 135 | `compliance_validate_agreements` | Prepare | ISSM, ISSO, SCA |

### Category 12: SSP Authoring & OSCAL (Feature 022)

| # | Tool | Phase | Roles |
|---|------|-------|-------|
| 136 | `compliance_write_ssp_section` | Implement | ISSO, Eng |
| 137 | `compliance_review_ssp_section` | Implement | ISSM |
| 138 | `compliance_ssp_completeness` | Implement | All |
| 139 | `compliance_export_oscal_ssp` | Assess | ISSM, SCA, AO |
| 140 | `compliance_validate_oscal_ssp` | Assess | ISSM, SCA |

**Updated Totals**: 140 tools across 12 categories.

---

## Source Data Entities (for data-model.md documentation)

These are the EF Core entities that need to be documented in `docs/architecture/data-model.md`:

### Feature 017 Entities
- `ScanImportRecord` — Tracks each CKL/XCCDF/Prisma file import
- `ScanImportFinding` — Individual finding from a scan import
- `StigControl` — DISA STIG rule-to-CCI-to-NIST mapping lookup
- `ComplianceFinding` (enhanced) — Added `ScanSourceType` and `ImportRecordId` fields

### Feature 018 Entities
- `SecurityAssessmentPlan` — SAP header with status, schedule, SHA-256 hash
- `SapControlEntry` — Per-control assessment method assignments
- `SapTeamMember` — Assessment team roster
- `SapMethodOverride` — SCA-customized assessment methods per control

### Feature 019 Entities
- `ScanImportFinding` (enhanced) — Added Prisma-specific fields (policy ID, cloud type, resource ARN)
- `PrismaPolicy` — Prisma Cloud policy catalog cache
- `PrismaTrendRecord` — Periodic trend data for ConMon

### Feature 021 Entities
- `PrivacyThresholdAnalysis` — PTA records with PII classification
- `PrivacyImpactAssessment` — PIA header with version, status, reviewer
- `PiaSection` — Individual PIA section (8 OMB M-03-22 sections)
- `SystemInterconnection` — System-to-system connection metadata
- `InterconnectionAgreement` — ISA/MOU agreement tracking

### Feature 022 Entities
- `SspSection` — Individual SSP section (13 NIST 800-18 sections) with status lifecycle
- `ContingencyPlanReference` — ISCP reference for SSP §13
- `RegisteredSystem` (enhanced) — Added `HasNoExternalInterconnections`, `OperationalStatus`, `SystemStartDate`

### Feature 017 Enumerations
- `ScanImportType` — CKl, Xccdf (extended by F019 with PrismaCloudCsv, PrismaCloudApi)
- `ScanImportStatus` — Pending, Completed, Failed, PartiallyCompleted
- `ImportFindingAction` — Created, Updated, Skipped, Error
- `ScanSourceType` — Manual, StigViewer, ScapComplianceChecker, PrismaCloud

### Feature 018 Enumerations
- `SapStatus` — Draft, InReview, Approved, Active, Completed
- `AssessmentMethod` — Test, Interview, Examine
- `TeamMemberRole` — Assessor, Lead, Observer, SystemOwner, Isso, Issm

### Feature 019 Enumerations
- `ScanImportType` (extended) — PrismaCloudCsv, PrismaCloudApi values added
- `ScanSourceType` (extended) — PrismaCloud value added

### Feature 021 Enumerations
- `PtaDetermination` — PiaRequired, PiaNotRequired, Exempt
- `PiaStatus` — Draft, InReview, Approved, Expired, Archived
- `InterconnectionType` — Direct, Vpn, Api, Federated, Wireless, RemoteAccess
- `DataFlowDirection` — Inbound, Outbound, Bidirectional
- `InterconnectionStatus` — Proposed, Active, Suspended, Terminated
- `AgreementType` — Isa, Mou, Moa, Sla
- `AgreementStatus` — Draft, Pending, Active, Expired, Revoked

### Feature 022 Enumerations
- `SspSectionStatus` — NotStarted, Draft, InReview, Approved, NeedsRevision
- `OperationalStatus` — Operational, UnderDevelopment, MajorModification, Disposition, Other
