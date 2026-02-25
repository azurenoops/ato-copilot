# Data Model: KnowledgeBase Agent — "Compliance Library"

**Feature**: `010-knowledgebase-agent` | **Date**: 2026-02-24

## Entity Overview

```
┌─────────────────────┐      ┌──────────────────────┐
│  KnowledgeBaseAgent │──────│  KnowledgeBaseAgent   │
│  (BaseAgent)        │      │  Options              │
└────────┬────────────┘      └──────────────────────┘
         │ registers 7 tools
         ▼
┌─────────────────────┐
│  BaseTool (×7)      │─────────────────┐
│  - ExplainNist      │                 │
│  - SearchNist       │      ┌──────────┴──────────┐
│  - ExplainStig      │      │  Services            │
│  - SearchStigs      │──────│  - INistControlsSvc  │ (existing)
│  - ExplainRmf       │      │  - IStigKnowledgeSvc │ (expanded)
│  - ExplainImpact    │      │  - IRmfKnowledgeSvc  │ (expanded)
│  - GetFedRamp       │      │  - IDoDInstructionSvc│ (expanded)
│  Tools              │      │  - IDoDWorkflowSvc   │ (expanded)
└─────────────────────┘      │  - IImpactLevelSvc   │ (new)
                             │  - IFedRampTemplateSvc│ (new)
                             └──────────┬──────────┘
                                        │ loads from
                                        ▼
                             ┌──────────────────────┐
                             │  JSON Data Files (×9) │
                             │  via IMemoryCache     │
                             └──────────────────────┘
```

## New Entities (Ato.Copilot.Core/Models/Compliance/)

### StigControl

Represents a STIG finding from curated JSON data.

| Field | Type | Description |
|-------|------|-------------|
| StigId | string | STIG identifier (e.g., "V-12345") |
| VulnId | string | Vulnerability ID |
| RuleId | string | Rule identifier |
| Title | string | Short title |
| Description | string | Full description |
| Severity | StigSeverity (enum) | High (CAT I), Medium (CAT II), Low (CAT III) |
| Category | string | STIG category grouping |
| StigFamily | string | Parent STIG family |
| NistControls | List\<string\> | Mapped NIST 800-53 control IDs |
| CciRefs | List\<string\> | Control Correlation Identifier references |
| CheckText | string | Verification/check procedure |
| FixText | string | Remediation steps |
| AzureImplementation | Dictionary\<string, string\> | Azure-specific guidance (Service, Configuration, Policy, Automation) |
| ServiceType | string | Azure service type |

```csharp
public enum StigSeverity { High, Medium, Low }

public record StigControl(
    string StigId,
    string VulnId,
    string RuleId,
    string Title,
    string Description,
    StigSeverity Severity,
    string Category,
    string StigFamily,
    List<string> NistControls,
    List<string> CciRefs,
    string CheckText,
    string FixText,
    Dictionary<string, string> AzureImplementation,
    string ServiceType);
```

### RmfStep

Represents one step in the 6-step RMF process.

| Field | Type | Description |
|-------|------|-------------|
| Step | int | Step number (1-6) |
| Title | string | Step title (e.g., "Categorize") |
| Description | string | Step description |
| Activities | List\<string\> | Key activities in this step |
| Outputs | List\<string\> | Deliverables/outputs |
| Roles | List\<string\> | Responsible roles |
| DodInstruction | string | Governing DoD instruction |

```csharp
public record RmfStep(
    int Step,
    string Title,
    string Description,
    List<string> Activities,
    List<string> Outputs,
    List<string> Roles,
    string DodInstruction);
```

### RmfProcessData

Root container for RMF JSON data.

| Field | Type | Description |
|-------|------|-------------|
| Steps | List\<RmfStep\> | All 6 RMF steps |
| ServiceGuidance | Dictionary\<string, ServiceGuidance\> | Branch/service-specific guidance |
| DeliverablesOverview | List\<DeliverableInfo\> | Aggregated deliverables by step |

```csharp
public record RmfProcessData(
    List<RmfStep> Steps,
    Dictionary<string, ServiceGuidance> ServiceGuidance,
    List<DeliverableInfo> DeliverablesOverview);

public record ServiceGuidance(
    string Organization,
    string Description,
    List<string> Contacts,
    List<string> Requirements,
    string Timeline,
    List<string> Tools);

public record DeliverableInfo(int Step, string StepTitle, List<string> Deliverables);
```

### DoDInstruction

Represents a DoD Instruction document.

| Field | Type | Description |
|-------|------|-------------|
| InstructionId | string | e.g., "DoDI 8510.01" |
| Title | string | Full title |
| Description | string | Summary  |
| PublicationDate | string | Publication date |
| Applicability | string | Scope of applicability |
| Url | string | Reference URL |
| RelatedNistControls | List\<string\> | Mapped NIST control IDs |
| RelatedStigIds | List\<string\> | Mapped STIG IDs |
| ControlMappings | List\<ControlMapping\> | Detailed control-to-instruction mappings |

```csharp
public record DoDInstruction(
    string InstructionId,
    string Title,
    string Description,
    string PublicationDate,
    string Applicability,
    string Url,
    List<string> RelatedNistControls,
    List<string> RelatedStigIds,
    List<ControlMapping> ControlMappings);

public record ControlMapping(string ControlId, string Requirement, string Guidance);
```

### DoDWorkflow

Represents a DoD authorization workflow.

| Field | Type | Description |
|-------|------|-------------|
| WorkflowId | string | Unique identifier |
| Name | string | Workflow name |
| Organization | string | Sponsoring org (e.g., "Navy") |
| ImpactLevel | string | Target impact level |
| Description | string | Workflow description |
| Steps | List\<WorkflowStep\> | Ordered steps |
| RequiredDocuments | List\<string\> | Required documentation |
| ApprovalAuthorities | List\<string\> | Approval chain |

```csharp
public record DoDWorkflow(
    string WorkflowId,
    string Name,
    string Organization,
    string ImpactLevel,
    string Description,
    List<WorkflowStep> Steps,
    List<string> RequiredDocuments,
    List<string> ApprovalAuthorities);

public record WorkflowStep(int Order, string Title, string Description, string Duration);
```

### ImpactLevel

Represents a DoD Impact Level (IL2-IL6) or FedRAMP baseline.

| Field | Type | Description |
|-------|------|-------------|
| Level | string | e.g., "IL5", "FedRAMP-High" |
| Name | string | Display name |
| DataClassification | string | Data classification description |
| SecurityRequirements | SecurityRequirements | Encryption, network, personnel details |
| AzureImplementation | AzureImpactGuidance | Azure-specific guidance |
| AdditionalControls | List\<string\> | Extra controls beyond baseline |

```csharp
public record ImpactLevel(
    string Level,
    string Name,
    string DataClassification,
    SecurityRequirements SecurityRequirements,
    AzureImpactGuidance AzureImplementation,
    List<string> AdditionalControls);

public record SecurityRequirements(
    string Encryption,
    string Network,
    string Personnel,
    string PhysicalSecurity);

public record AzureImpactGuidance(
    string Region,
    string Network,
    string Identity,
    string Encryption,
    List<string> Services);
```

### FedRampTemplate

Represents FedRAMP authorization package template guidance.

| Field | Type | Description |
|-------|------|-------------|
| TemplateType | string | e.g., "SSP", "POAM", "CRM" |
| Title | string | Template title |
| Description | string | Template purpose |
| Sections | List\<TemplateSection\> | Required sections |
| RequiredFields | List\<FieldDefinition\> | Required fields with examples |
| AzureMappings | Dictionary\<string, string\> | Azure service → template section |
| AuthorizationChecklist | List\<ChecklistItem\> | Package checklist |

```csharp
public record FedRampTemplate(
    string TemplateType,
    string Title,
    string Description,
    List<TemplateSection> Sections,
    List<FieldDefinition> RequiredFields,
    Dictionary<string, string> AzureMappings,
    List<ChecklistItem> AuthorizationChecklist);

public record TemplateSection(string Name, string Description, List<string> RequiredElements);
public record FieldDefinition(string Name, string Description, string Example, string AzureSource);
public record ChecklistItem(string Item, string Description, bool Required);
```

### KnowledgeBaseAgentOptions

Configuration entity bound to `AgentConfiguration:KnowledgeBaseAgent`.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Enabled | bool | true | Whether the agent participates in orchestration |
| MaxTokens | int | 4096 | Max tokens for AI responses |
| Temperature | double | 0.3 | AI temperature setting |
| ModelName | string | "gpt-4o" | Model identifier |
| CacheDurationMinutes | int | 60 | Tool-level cache TTL |
| KnowledgeBasePath | string | "KnowledgeBase/Data" | Relative path to JSON data files |
| DefaultSubscriptionId | string | "" | Default Azure subscription |
| MinimumConfidenceThreshold | double | 0.3 | Orchestrator routing threshold |

```csharp
public class KnowledgeBaseAgentOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxTokens { get; set; } = 4096;
    public double Temperature { get; set; } = 0.3;
    public string ModelName { get; set; } = "gpt-4o";
    public int CacheDurationMinutes { get; set; } = 60;
    public string KnowledgeBasePath { get; set; } = "KnowledgeBase/Data";
    public string DefaultSubscriptionId { get; set; } = "";
    public double MinimumConfidenceThreshold { get; set; } = 0.3;
}
```

## Expanded Interfaces

### IStigKnowledgeService (expanded)

```csharp
public interface IStigKnowledgeService
{
    // Existing (backward-compatible)
    Task<string> GetStigMappingAsync(string controlId, CancellationToken ct = default);

    // New methods for KB tools
    Task<StigControl?> GetStigControlAsync(string stigId, CancellationToken ct = default);
    Task<List<StigControl>> SearchStigsAsync(string query, StigSeverity? severity = null, int maxResults = 10, CancellationToken ct = default);
    Task<StigCrossReference?> GetStigCrossReferenceAsync(string stigId, CancellationToken ct = default);
}
```

### IRmfKnowledgeService (expanded)

```csharp
public interface IRmfKnowledgeService
{
    // Existing (backward-compatible)
    Task<string> GetGuidanceAsync(string controlId, CancellationToken ct = default);

    // New methods for KB tools
    Task<RmfProcessData?> GetRmfProcessAsync(CancellationToken ct = default);
    Task<RmfStep?> GetRmfStepAsync(int step, CancellationToken ct = default);
    Task<ServiceGuidance?> GetServiceGuidanceAsync(string topic, CancellationToken ct = default);
}
```

### IDoDInstructionService (expanded)

```csharp
public interface IDoDInstructionService
{
    // Existing (backward-compatible)
    Task<string> GetInstructionAsync(string controlId, CancellationToken ct = default);

    // New methods for KB tools
    Task<DoDInstruction?> ExplainInstructionAsync(string instructionId, CancellationToken ct = default);
    Task<List<DoDInstruction>> GetInstructionsByControlAsync(string controlId, CancellationToken ct = default);
}
```

### IDoDWorkflowService (expanded)

```csharp
public interface IDoDWorkflowService
{
    // Existing (backward-compatible)
    Task<List<string>> GetWorkflowAsync(string assessmentType, CancellationToken ct = default);

    // New methods for KB tools
    Task<DoDWorkflow?> GetWorkflowDetailAsync(string workflowId, CancellationToken ct = default);
    Task<List<DoDWorkflow>> GetWorkflowsByOrganizationAsync(string organization, CancellationToken ct = default);
}
```

### IImpactLevelService (new)

```csharp
public interface IImpactLevelService
{
    Task<ImpactLevel?> GetImpactLevelAsync(string level, CancellationToken ct = default);
    Task<List<ImpactLevel>> GetAllImpactLevelsAsync(CancellationToken ct = default);
    Task<ImpactLevel?> GetFedRampBaselineAsync(string baseline, CancellationToken ct = default);
}
```

### IFedRampTemplateService (new)

```csharp
public interface IFedRampTemplateService
{
    Task<FedRampTemplate?> GetTemplateGuidanceAsync(string templateType, string baseline = "High", CancellationToken ct = default);
    Task<List<FedRampTemplate>> GetAllTemplatesAsync(CancellationToken ct = default);
}
```

## Supporting Types

### StigCrossReference

```csharp
public record StigCrossReference(
    string StigId,
    StigControl Stig,
    List<string> NistControlIds,
    List<DoDInstruction> RelatedInstructions);
```

### QueryType (enum for AnalyzeQueryType)

```csharp
public enum KnowledgeQueryType
{
    NistControl,
    NistSearch,
    Stig,
    StigSearch,
    Rmf,
    ImpactLevel,
    FedRamp,
    GeneralKnowledge
}
```

## Relationships

```
StigControl ──→ NistControls (List<string>) ──→ INistControlsService
StigControl ──→ CciRefs (List<string>)
StigControl ──→ DoDInstruction (via StigCrossReference)
RmfStep ──→ DodInstruction (string reference)
DoDInstruction ──→ RelatedNistControls, RelatedStigIds
DoDWorkflow ──→ ImpactLevel (string reference)
ImpactLevel ←→ FedRampTemplate (shared baseline scope)
```

## Validation Rules

- `StigId` must match pattern `V-\d+` (normalized to uppercase)
- `Severity` input normalization: "high"/"cat1"/"cati" → High, "medium"/"cat2"/"catii" → Medium, "low"/"cat3"/"catiii" → Low
- Control IDs normalized to uppercase (e.g., "ac-2" → "AC-2")
- Impact level normalized: "IL-5"/"5"/"il5" → "IL5"; "HIGH"/"FEDRAMP-HIGH" → "FedRAMP-High"
- `max_results` defaults to 10 if not specified; must be > 0
- Template types normalized: "POAM"/"POA&M" → "POAM"; "CRM"/"CONMON" → "CRM"

## State Keys (IAgentStateManager)

| Key | Written By | Format | Purpose |
|-----|-----------|--------|---------|
| `kb_last_nist_control` | ExplainNistControlTool | `{ query, result }` JSON | Cross-agent NIST sharing |
| `kb_last_stig` | ExplainStigTool | `{ query, result }` JSON | Cross-agent STIG sharing |
| `last_operation` | KnowledgeBaseAgent | string | Last operation type |
| `last_operation_at` | KnowledgeBaseAgent | ISO 8601 timestamp | When last operation ran |
| `operation_count` | KnowledgeBaseAgent | int | Cumulative operation counter |
| `last_query` | KnowledgeBaseAgent | string | Raw query text |
| `last_query_success` | KnowledgeBaseAgent | bool | Success/failure flag |
| `last_query_duration_ms` | KnowledgeBaseAgent | long | Execution duration in ms |
