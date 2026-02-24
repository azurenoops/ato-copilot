# Quickstart: ATO Remediation Engine

**Feature**: 009-remediation-engine | **Branch**: `009-remediation-engine`

## Prerequisites

- .NET 9.0 SDK
- Git (on branch `009-remediation-engine`)
- Features 002, 005, 007, 008 merged

## Build & Test

```bash
# Build
dotnet build Ato.Copilot.sln

# Run all tests
dotnet test tests/Ato.Copilot.Tests.Unit/Ato.Copilot.Tests.Unit.csproj

# Run only remediation engine tests
dotnet test tests/Ato.Copilot.Tests.Unit/Ato.Copilot.Tests.Unit.csproj --filter "FullyQualifiedName~Remediation"
```

## Key Entry Points

### Engine (primary)

| File | Purpose |
|------|---------|
| `src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/AtoRemediationEngine.cs` | Main engine — 17 `IRemediationEngine` methods |
| `src/Ato.Copilot.Core/Interfaces/Compliance/IComplianceInterfaces.cs` | `IRemediationEngine` interface (4 existing + 13 new methods) |

### Supporting Services

| File | Purpose |
|------|---------|
| `AiRemediationPlanGenerator.cs` | AI script/guidance/prioritization via `IChatClient` |
| `RemediationScriptExecutor.cs` | Script execution with timeout + retry |
| `NistRemediationStepsService.cs` | Curated NIST steps + regex parsing |
| `AzureArmRemediationService.cs` | 8 legacy ARM operations + snapshots |
| `ComplianceRemediationService.cs` | Tier 2 structured remediation |
| `ScriptSanitizationService.cs` | Script safety validation |

All located in `src/Ato.Copilot.Agents/Compliance/Services/Engines/Remediation/`.

### Models

| File | Key Types |
|------|-----------|
| `src/Ato.Copilot.Core/Models/Compliance/ComplianceModels.cs` | RemediationExecution, BatchRemediationResult, RemediationValidationResult, RemediationRollbackResult, etc. |
| `src/Ato.Copilot.Core/Models/Compliance/RemediationScript.cs` | RemediationScript, ScriptType |
| `src/Ato.Copilot.Core/Models/Compliance/RemediationGuidance.cs` | RemediationGuidance |
| `src/Ato.Copilot.Core/Models/Compliance/PrioritizedFinding.cs` | PrioritizedFinding |

### Configuration

| File | Key Property |
|------|-------------|
| `src/Ato.Copilot.Agents/Compliance/Configuration/ComplianceAgentOptions.cs` | `EnableAutomatedRemediation` (master gate), `Remediation` (RemediationOptions sub-class) |

### DI Registration

| File | Registrations |
|------|--------------|
| `src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs` | `IRemediationEngine` → `AtoRemediationEngine` (Singleton), 6 supporting services (Singleton) |

### MCP Tools

| Tool Name | File | Line |
|-----------|------|------|
| `compliance_remediate` | `ComplianceTools.cs` | L398 |
| `compliance_validate_remediation` | `ComplianceTools.cs` | L455 |
| `compliance_generate_plan` | `ComplianceTools.cs` | L494 |

### Tests

| File | Tests | Focus |
|------|-------|-------|
| `AtoRemediationEngineTests.cs` | ~85 | Core engine (plan, execute, batch, workflow, AI) |
| `NistRemediationStepsServiceTests.cs` | ~10 | Step lookup + regex parsing |
| `ScriptSanitizationServiceTests.cs` | ~10 | Safe/unsafe script detection |
| `AiRemediationPlanGeneratorTests.cs` | ~10 | AI with mock IChatClient + null fallback |
| `RemediationScriptExecutorTests.cs` | ~10 | Script execution + timeout + retry |
| `AzureArmRemediationServiceTests.cs` | ~10 | ARM operations + snapshots |
| `ComplianceRemediationServiceTests.cs` | ~10 | Tier 2 orchestration |

## Architecture

```
MCP Tools (compliance_remediate, etc.)
    │
    ▼
IRemediationEngine (AtoRemediationEngine)
    │
    ├── IAiRemediationPlanGenerator (Tier 1: AI Script)
    │       └── IChatClient? (optional)
    │
    ├── IComplianceRemediationService (Tier 2: Structured)
    │       └── IRemediationScriptExecutor
    │               └── IScriptSanitizationService
    │
    ├── IAzureArmRemediationService (Tier 3: Legacy ARM)
    │       └── ArmClient (singleton)
    │
    ├── INistRemediationStepsService (step lookup)
    │
    ├── IAtoComplianceEngine (finding/assessment data)
    │
    ├── IKanbanService? (optional, post-execution task sync)
    │
    └── IDbContextFactory<AtoCopilotContext> (persistence)
```

## Common Patterns

### Test setup
```csharp
var dbFactory = new InMemoryDbContextFactory();
var mockCompliance = new Mock<IAtoComplianceEngine>();
var mockArm = new Mock<IAzureArmRemediationService>();
var mockAi = new Mock<IAiRemediationPlanGenerator>();
// ... other mocks
var engine = new AtoRemediationEngine(
    mockCompliance.Object, dbFactory,
    mockArm.Object, mockAi.Object, /* ... */
    Mock.Of<ILogger<AtoRemediationEngine>>());
```

### Creating test findings
```csharp
private static ComplianceFinding CreateFinding(
    string id = "finding-1", string severity = "High",
    string controlId = "AC-2", bool autoRemediable = true) => new()
{
    Id = id, Severity = severity, ControlId = controlId,
    AutoRemediable = autoRemediable, Status = "Open",
    // ... standard test values
};
```
