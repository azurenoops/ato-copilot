# Feature 018 — Research

**Date**: 2026-03-04 | **Branch**: `018-sap-generation`

---

## R1: Document Generation Service Pattern

**Decision**: Follow `SspService` pattern — `ISapService`/`SapService` with `IServiceScopeFactory` + `ILogger` constructor injection, scoped `AtoCopilotContext` per operation via `_scopeFactory.CreateScope()`, `StringBuilder`-based Markdown assembly, optional `IProgress<string>` for streaming updates.

**Rationale**: The SSP service (Feature 015) is the closest analogue — both are multi-section document assemblies that query multiple entities and render structured Markdown. The SAR service follows a simpler pattern (single assessment → single document) that doesn't match SAP's complexity. The SSP pattern also supports opt-in section rendering and progress reporting, which SAP will need for large baselines.

**Alternatives considered**:
- `DocumentGenerationService` pattern (single `GenerateDocumentAsync` method): Rejected — too generic, doesn't support per-control method assembly or STIG test plan building
- Repository pattern with separate data layer: Rejected — Constitution II prohibits service-locator patterns and no other compliance service uses repository abstraction

**Key references**:
- Interface: [ISspService.cs](../../../src/Ato.Copilot.Core/Interfaces/Compliance/ISspService.cs)
- Implementation: [SspService.cs](../../../src/Ato.Copilot.Agents/Compliance/Services/SspService.cs) (lines 367–585 for `GenerateSspAsync`)

---

## R2: SAR Generation Pattern for SAP Return DTO

**Decision**: `SapDocument` DTO follows `SarDocument` pattern — structured class with both Markdown `Content` string and typed metadata fields (`TotalControls`, `FamilySummaries[]`, etc.). Return from `GenerateSapAsync` as single object. Tool wrapper serializes to JSON envelope.

**Rationale**: `SarDocument` successfully combines rendered content with machine-readable aggregates for downstream consumption. SAP needs the same: AI agents need structured family counts and method breakdowns; human users need the rendered Markdown.

**Alternatives considered**:
- Return only Markdown string: Rejected — loses structured metadata needed for tool response envelope
- Return separate content + metadata objects: Rejected — violates established single-DTO-return pattern

**Key references**:
- SAR service: [AssessmentArtifactService.cs](../../../src/Ato.Copilot.Agents/Compliance/Services/AssessmentArtifactService.cs) (lines 459–610)
- SAR tool: [AssessmentArtifactTools.cs](../../../src/Ato.Copilot.Agents/Compliance/Tools/AssessmentArtifactTools.cs) (lines 481–573)

---

## R3: OSCAL Assessment Objective Extraction

**Decision**: Call `NistControlsService.GetControlEnhancementAsync(controlId)` per control to obtain the `Objectives` list. No new OSCAL extraction method is needed — the existing method already recursively extracts `assessment-objective` parts via `ExtractObjectives(parts)`. Assessment **methods** default to all three (Examine, Interview, Test) per clarification Q2.

**Rationale**: `GetControlEnhancementAsync` returns a `ControlEnhancement` record with a `List<string> Objectives` field that contains exactly the assessment objective prose needed for SAP Section 4. The OSCAL catalog is cached in memory, so ~325 lookups for Moderate baseline will be fast. The `ExtractObjectives` method (line 557–580) recursively walks `assessment-objective` parts and collects all `Prose` strings.

**Alternatives considered**:
- Add new `ExtractAssessmentMethodsAsync` method: Rejected per clarification Q2 — Rev 5 catalog lacks `assessment-method` named parts
- Parse OSCAL 800-53A catalog separately: Rejected per clarification Q2 — adds complexity for marginal benefit when default-all-three is correct for DoD
- Batch extraction (all controls at once): Not needed — catalog is cached in-memory, individual lookups are fast

**Key references**:
- `GetControlEnhancementAsync`: [NistControlsService.cs](../../../src/Ato.Copilot.Agents/Compliance/Services/NistControlsService.cs) (lines 203–250)
- `ExtractObjectives`: [NistControlsService.cs](../../../src/Ato.Copilot.Agents/Compliance/Services/NistControlsService.cs) (lines 557–580)
- Return type: [OscalTypes.cs](../../../src/Ato.Copilot.Core/Models/Compliance/OscalTypes.cs) — `ControlEnhancement(Id, Title, Statement, Guidance, List<string> Objectives, DateTime LastUpdated)`

---

## R4: Control Inheritance Scope Assembly

**Decision**: Load `ControlBaseline` with `.Include(b => b.Inheritances)`. Build `Dictionary<string, ControlInheritance>` keyed by `ControlId`. For each control in `ControlBaseline.ControlIds`, look up inheritance designation — default to `InheritanceType.Customer` if no explicit record exists.

**Rationale**: This is the exact pattern used by `SspService` (line 528) and has proven reliable. The dictionary lookup is O(1) per control. Controls without explicit `ControlInheritance` records are assumed to be customer responsibility (the most common case and safest default).

**Alternatives considered**:
- Direct query per control: Rejected — N+1 query problem
- Separate `ControlInheritances` query: Used by eMASS export but less idiomatic than Include

**Key references**:
- Entity: [RmfModels.cs](../../../src/Ato.Copilot.Core/Models/Compliance/RmfModels.cs) (lines 548–590)
- SSP usage: [SspService.cs](../../../src/Ato.Copilot.Agents/Compliance/Services/SspService.cs) (line 528)
- Enum: [ComplianceModels.cs](../../../src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs) (line 580) — `Inherited`, `Shared`, `Customer`

---

## R5: STIG/SCAP Reverse Mapping

**Decision**: Use `IStigKnowledgeService.GetStigsByCciChainAsync(controlId)` to find STIG controls mapped to each NIST control via CCI chain. Group results by `BenchmarkId` for the SAP STIG Testing Plan section (§3.1 Section 8).

**Rationale**: The CCI chain method (line 44–195 in StigKnowledgeService) is the richest reverse mapping — it goes NIST → CCI → STIG with deduplication and severity ordering. It handles both `CciRefs` and `NistControls` fields on `StigControl`. The simpler `GetStigMappingAsync` returns only comma-delimited IDs without benchmark grouping.

**Alternatives considered**:
- `GetStigMappingAsync`: Rejected — returns flat string, not structured data needed for benchmark grouping
- Direct DB query: Not applicable — STIG data is loaded from JSON knowledge base, not EF entities
- Skip STIG mapping for inherited controls: Include with note "Provider-managed" — transparency for assessors

**Key references**:
- Service: [StigKnowledgeService.cs](../../../src/Ato.Copilot.Agents/Compliance/Services/KnowledgeBase/StigKnowledgeService.cs) (lines 44–195)
- CCI mapping data: `cci-nist-mapping.json` (7029 entries)

---

## R6: Evidence Gap Summary

**Decision**: Query `context.Evidence.Where(e => controlIds.Contains(e.ControlId)).GroupBy(e => e.ControlId)` to get per-control evidence counts. Populate `SapControlEntry.EvidenceExpected` (derived from method count — 1 per method) and `EvidenceCollected` (actual evidence record count) to produce gap indicators like "2/3 artifacts collected."

**Rationale**: The `CheckEvidenceCompletenessAsync` pattern (AssessmentArtifactService lines 370–457) demonstrates the group-by-control query pattern. The SAP doesn't need the full verification status (verified/unverified/missing) — just a count-based gap indicator. Using `EvidenceExpected` based on method count (each method implies one class of evidence) gives a simple denominator.

**Alternatives considered**:
- Full evidence completeness report per control: Rejected per clarification Q5 — bloats SAP without adding value; use `compliance_check_evidence_completeness` for that
- No evidence information: Rejected — SCA needs assessment readiness context
- Live evidence cross-reference: Rejected per Q5 — static prose + gap count is sufficient

**Key references**:
- Evidence entity: [ComplianceModels.cs](../../../src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs) (lines 1148–1199)
- Completeness check: [AssessmentArtifactService.cs](../../../src/Ato.Copilot.Agents/Compliance/Services/AssessmentArtifactService.cs) (lines 370–457)

---

## R7: DOCX/PDF Export Integration

**Decision**: Add `"sap"` merge-field schema to `DocumentTemplateService.MergeFieldSchemas` dictionary. Add `case "sap":` in `BuildMergeDataAsync` switch dispatching to new `PopulateSapData()` method. Merge fields: `SystemName`, `SystemAcronym`, `BaselineLevel`, `AssessmentScope`, `TotalControls`, `CustomerControls`, `InheritedControls`, `SharedControls`, `StigBenchmarks`, `AssessmentTeam`, `ScheduleStart`, `ScheduleEnd`, `RulesOfEngagement`, `ControlMatrix`, `PreparedBy`, `PreparedDate`.

**Rationale**: All four existing document types (SSP, SAR, POA&M, RAR) follow the same pattern — schema entry + `BuildMergeDataAsync` case + `Populate*Data` method. The DOCX rendering uses simple `{{FieldName}}` replacement in Word XML. PDF rendering delegates to QuestPDF.

**Alternatives considered**:
- Separate SAP template service: Rejected — violates existing single-service pattern; over-engineering
- Direct OpenXML manipulation: Rejected — `DocumentTemplateService` already handles all DOCX complexity

**Key references**:
- Merge schemas: [DocumentTemplateService.cs](../../../src/Ato.Copilot.Agents/Compliance/Services/DocumentTemplateService.cs) (line 56)
- `BuildMergeDataAsync`: [DocumentTemplateService.cs](../../../src/Ato.Copilot.Agents/Compliance/Services/DocumentTemplateService.cs) (lines 273–350)
- `RenderDocxAsync`: [DocumentTemplateService.cs](../../../src/Ato.Copilot.Agents/Compliance/Services/DocumentTemplateService.cs) (lines 350–450)

---

## R8: DI Registration Pattern

**Decision**: Two-step Singleton registration:
1. Register service + concrete tools: `services.AddSingleton<ISapService, SapService>()`, `services.AddSingleton<GenerateSapTool>()`, etc.
2. Forward-register as `BaseTool`: `services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<GenerateSapTool>())` for each tool.

Place in `ServiceCollectionExtensions.cs` near the existing SSP/SAR registration block (lines 257–271) with a `// SAP Generation service and tools (Feature 018)` comment.

**Rationale**: Exact match for existing registration pattern. All compliance services use `AddSingleton` with internal `IServiceScopeFactory` for scoped DbContext. The forward-registration enables `ComplianceAgent` to discover tools via `IEnumerable<BaseTool>`.

**Key references**:
- Step 1: [ServiceCollectionExtensions.cs](../../../src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs) (lines 257–271)
- Step 2: [ServiceCollectionExtensions.cs](../../../src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs) (lines 395–406)

---

## R9: ComplianceMcpTools Registration Pattern

**Decision**: Add 5 new tools to `ComplianceMcpTools.cs`:
1. Private fields: `_generateSapTool`, `_getSapTool`, `_updateSapTool`, `_listSapsTool`, `_finalizeSapTool`
2. Constructor parameters + assignments
3. Wrapper methods with `[Description("...")]` attributes accepting typed parameters
4. Each wrapper builds `Dictionary<string, object?>` args and delegates to `tool.ExecuteAsync(args, ct)`

**Rationale**: Exact match for existing MCP tool registration pattern. The `[Description]` attribute provides MCP tool discoverability. Method parameter types (string, string?, CancellationToken) follow established conventions.

**Key references**:
- Field declaration: [ComplianceMcpTools.cs](../../../src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs) (line 159)
- Constructor: [ComplianceMcpTools.cs](../../../src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs) (lines 297–428)
- Wrapper method: [ComplianceMcpTools.cs](../../../src/Ato.Copilot.Mcp/Tools/ComplianceMcpTools.cs) (lines 1902–1914)

---

## R10: Greenfield Status Confirmation

**Decision**: All SAP artifacts are new — no existing code to modify beyond DI registrations, ComplianceMcpTools wrappers, AtoCopilotContext DbSets, and DocumentTemplateService schema extension.

**Rationale**: Searched for `SecurityAssessmentPlan`, `ISapService`, `GenerateSapTool`, `SapDocument`, `SapModels` — no production code matches. The only reference is `rmf-process.json` which mentions SAP as an RMF Step 4 output. This confirms a clean greenfield implementation with well-established patterns to follow.

**Alternatives considered**: N/A — this is a factual finding, not a design decision.
