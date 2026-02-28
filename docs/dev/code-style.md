# Code Style Guide

> C# and TypeScript conventions, naming rules, and folder structure standards.

---

## C# Conventions

### Language Version

- **C# 13** / **.NET 9.0**
- `<ImplicitUsings>enable</ImplicitUsings>`
- `<Nullable>enable</Nullable>`

### Naming

| Element | Convention | Example |
|---------|-----------|---------|
| Classes | PascalCase | `ComplianceAssessmentTool` |
| Interfaces | `I` + PascalCase | `IAtoComplianceEngine` |
| Methods | PascalCase | `ExecuteCoreAsync` |
| Properties | PascalCase | `RequiredPimTier` |
| Private fields | `_camelCase` | `_service` |
| Parameters | camelCase | `systemId` |
| Constants | PascalCase | `MaxRetryCount` |
| Enums | PascalCase (singular) | `RmfStep`, `DecisionType` |
| Enum values | PascalCase | `RmfStep.Categorize` |
| Tool names | `snake_case` | `"compliance_register_system"` |

### File Organization

- One primary class per file
- File name matches class name: `ComplianceAssessmentTool.cs`
- Group related models in a single file when they're small records/enums: `RmfModels.cs`

### Code Patterns

**Records for Value Objects:**
```csharp
public record SecurityRequirements(
    string Encryption,
    string Network,
    string Personnel,
    string PhysicalSecurity);
```

**Entity Classes with Navigation:**
```csharp
public class RegisteredSystem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
    public List<PoamItem> PoamItems { get; set; } = [];
}
```

**Async Methods:**
- Suffix with `Async`: `GetDataAsync`, `ExecuteCoreAsync`
- Always accept `CancellationToken` as last parameter
- Use `ConfigureAwait(false)` in library code

**Null Handling:**
```csharp
// Prefer null-coalescing over if-null checks
var value = arguments["key"]?.ToString()
    ?? throw new ArgumentException("key is required.");
```

### Tool Implementation Pattern

```csharp
public class MyTool : BaseTool
{
    // 1. Private readonly fields for injected dependencies
    private readonly IMyService _service;

    // 2. Constructor: dependencies + logger (passed to base)
    public MyTool(IMyService service, ILogger<MyTool> logger) : base(logger)
    {
        _service = service;
    }

    // 3. Required overrides — Name, Description, Parameters
    public override string Name => "compliance_my_tool";
    public override string Description => "Brief description.";
    public override IReadOnlyDictionary<string, ToolParameter> Parameters =>
        new Dictionary<string, ToolParameter>
        {
            ["required_param"] = new("Description.", true),
            ["optional_param"] = new("Description.", false),
        };

    // 4. Optional: PIM tier override
    public override PimTier RequiredPimTier => PimTier.None;

    // 5. Core logic
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        // Parse arguments → call service → serialize result
    }
}
```

### DI Registration Pattern

```csharp
// Services: Singleton for stateless, Scoped for DB-dependent
services.AddSingleton<IMyService, MyService>();
services.AddScoped<IMyDbService, MyDbService>();

// Tools: Always Singleton, dual registration
services.AddSingleton<MyTool>();
services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<MyTool>());
```

---

## TypeScript Conventions

### Framework

- **TypeScript 5.x** with strict mode
- **Extensions**: VS Code Extension API, Bot Framework SDK
- **Tests**: Mocha + Chai

### Naming

| Element | Convention | Example |
|---------|-----------|---------|
| Classes / Interfaces | PascalCase | `SystemSummaryCard` |
| Functions | camelCase | `buildSystemSummaryCard` |
| Variables | camelCase | `cardData` |
| Constants | UPPER_SNAKE_CASE | `MAX_RETRY_COUNT` |
| File names | kebab-case | `system-summary-card.ts` |
| Test files | `*.test.ts` | `system-summary-card.test.ts` |

### Adaptive Card Builders

```typescript
export function buildMyCard(data: MyCardData): Attachment {
    const card = {
        type: 'AdaptiveCard',
        $schema: 'http://adaptivecards.io/schemas/adaptive-card.json',
        version: '1.5',
        body: [/* ... */],
        actions: [/* ... */],
    };
    return CardFactory.adaptiveCard(card);
}
```

### Import Order

1. Node.js built-ins
2. External packages (`botbuilder`, `vscode`)
3. Internal modules (relative paths)

---

## Folder Structure Rules

### Tool Files

Tools live in `src/Ato.Copilot.Agents/Compliance/Tools/` and are grouped by domain:

| File | Domain |
|------|--------|
| `RmfTools.cs` | RMF lifecycle (Prepare–Monitor) |
| `SspTools.cs` | SSP generation and narratives |
| `AssessmentTools.cs` | Assessment and SAR |
| `AuthorizationTools.cs` | Authorization decisions, POA&M, RAR |
| `ConMonTools.cs` | Continuous monitoring |
| `InteropTools.cs` | eMASS, multi-system dashboard |
| `TemplateTools.cs` | Template management |
| `StigTools.cs` | STIG lookup and mapping |
| `AuthCacTools.cs` | CAC/PIV authentication |
| `AuthPimTools.cs` | PIM/JIT privilege management |
| `KanbanTools.cs` | Task board management |
| `WatchTools.cs` | Compliance monitoring/alerts |

### Model Files

Models live in `src/Ato.Copilot.Core/Models/Compliance/` and are grouped by domain:

| File | Contains |
|------|----------|
| `RmfModels.cs` | RegisteredSystem, SecurityCategorization, InformationType, AuthorizationBoundary |
| `BaselineModels.cs` | ControlBaseline, ControlTailoring, ControlInheritance |
| `ImplementModels.cs` | ControlImplementation |
| `AssessModels.cs` | ControlEffectiveness, AssessmentRecord |
| `AuthorizeModels.cs` | AuthorizationDecision, RiskAcceptance, PoamItem, PoamMilestone |
| `ConMonModels.cs` | ConMonPlan, ConMonReport, SignificantChange |

---

## Error Handling

- Tools return JSON-formatted error messages (never throw to MCP):
  ```csharp
  return JsonSerializer.Serialize(new { error = "System not found.", system_id = id });
  ```
- Services throw typed exceptions caught by tool `ExecuteCoreAsync`
- Middleware logs and wraps exceptions for protocol responses

## Logging

Use structured logging with Serilog:

```csharp
Logger.LogInformation("Processing system {SystemId} for {Operation}", systemId, "assessment");
```

- Use named parameters (not string interpolation)
- Include correlation IDs where available
- Log at appropriate levels: `Debug` for internal state, `Information` for operations, `Warning` for recoverable issues, `Error` for failures
