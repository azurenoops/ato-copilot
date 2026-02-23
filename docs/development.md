# Development Guide

> Contributing, testing, code conventions, and adding new features to ATO Copilot.

## Table of Contents

- [Development Setup](#development-setup)
- [Project Dependencies](#project-dependencies)
- [Building](#building)
- [Testing](#testing)
  - [Test Stack](#test-stack)
  - [Running Tests](#running-tests)
  - [Writing Unit Tests](#writing-unit-tests)
  - [Writing Integration Tests](#writing-integration-tests)
  - [Test Conventions](#test-conventions)
- [Code Conventions](#code-conventions)
  - [C# Standards](#c-standards)
  - [Project Layering](#project-layering)
  - [Naming](#naming)
- [Adding a New Tool](#adding-a-new-tool)
- [Adding a New Service](#adding-a-new-service)
- [Adding a New Entity](#adding-a-new-entity)
- [Constitution Principles](#constitution-principles)
- [Specify Toolkit Workflow](#specify-toolkit-workflow)
- [Branching Strategy](#branching-strategy)

---

## Development Setup

1. Install [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
2. Clone the repository
3. Build: `dotnet build Ato.Copilot.sln`
4. Test: `dotnet test Ato.Copilot.sln`
5. Run: `cd src/Ato.Copilot.Mcp && dotnet run -- --http`

No external dependencies are needed for development — the system defaults to SQLite and in-memory state. Azure credentials are only required for live compliance assessments.

---

## Project Dependencies

```
Ato.Copilot.Mcp
  ├── Ato.Copilot.Agents
  │     ├── Ato.Copilot.Core
  │     └── Ato.Copilot.State
  └── Ato.Copilot.Core

Ato.Copilot.Tests.Unit
  ├── Ato.Copilot.Agents
  ├── Ato.Copilot.Core
  ├── Ato.Copilot.State
  └── Ato.Copilot.Mcp

Ato.Copilot.Tests.Integration
  └── Ato.Copilot.Mcp
```

Key NuGet packages:

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.EntityFrameworkCore` | 9.0.x | ORM / data access |
| `Microsoft.EntityFrameworkCore.Sqlite` | 9.0.x | Development database |
| `Microsoft.EntityFrameworkCore.SqlServer` | 9.0.x | Production database |
| `Azure.ResourceManager` | Latest | Azure ARM SDK |
| `Azure.Identity` | Latest | Azure credential flow |
| `Serilog.AspNetCore` | Latest | Structured logging |
| `Microsoft.Identity.Web` | 3.5.x | Azure AD / Entra ID integration |

Test packages:

| Package | Version | Purpose |
|---|---|---|
| `xunit` | 2.9.x | Test framework |
| `FluentAssertions` | 7.0.x | Assertion library |
| `Moq` | 4.20.x | Mocking framework |
| `Microsoft.AspNetCore.Mvc.Testing` | 9.0.x | Integration test server |

---

## Building

```bash
# Full build
dotnet build Ato.Copilot.sln

# Single project
dotnet build src/Ato.Copilot.Agents/Ato.Copilot.Agents.csproj

# Release build
dotnet build Ato.Copilot.sln -c Release

# Publish (for deployment)
dotnet publish src/Ato.Copilot.Mcp/Ato.Copilot.Mcp.csproj -c Release -o ./publish
```

The build should complete with **0 errors and 0 code warnings**. NuGet advisory warnings (NU1902) for transitive dependencies are expected and not actionable.

---

## Testing

### Test Stack

- **xUnit 2.9.x** — test framework
- **FluentAssertions 7.0.x** — readable assertions (`result.Should().BeTrue()`)
- **Moq 4.20.x** — interface mocking (`Mock<IKanbanService>()`)

### Running Tests

```bash
# All tests
dotnet test Ato.Copilot.sln

# Unit tests only
dotnet test tests/Ato.Copilot.Tests.Unit/

# Integration tests only
dotnet test tests/Ato.Copilot.Tests.Integration/

# Specific test class
dotnet test --filter "FullyQualifiedName~ComplianceAgentTests"

# With verbose output
dotnet test --verbosity detailed

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Writing Unit Tests

Unit tests go in `tests/Ato.Copilot.Tests.Unit/`. Follow this pattern:

```csharp
public class MyToolTests
{
    private readonly Mock<IServiceScopeFactory> _scopeFactory;
    private readonly Mock<ILogger<MyTool>> _logger;
    private readonly MyTool _sut;

    public MyToolTests()
    {
        _scopeFactory = new Mock<IServiceScopeFactory>();
        _logger = new Mock<ILogger<MyTool>>();
        _sut = new MyTool(_scopeFactory.Object, _logger.Object);
    }

    [Fact]
    public async Task ExecuteCoreAsync_WithValidInput_ReturnsSuccess()
    {
        // Arrange
        var args = new Dictionary<string, object?>
        {
            ["param_name"] = "value"
        };

        // Act
        var result = await _sut.ExecuteCoreAsync(args);

        // Assert
        result.Should().Contain("\"status\":\"success\"");
    }

    [Fact]
    public async Task ExecuteCoreAsync_WithMissingRequired_ReturnsError()
    {
        // Arrange
        var args = new Dictionary<string, object?>();

        // Act
        var result = await _sut.ExecuteCoreAsync(args);

        // Assert
        result.Should().Contain("\"status\":\"error\"");
    }
}
```

### Writing Integration Tests

Integration tests use `WebApplicationFactory` to spin up a real HTTP server:

```csharp
public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

### Test Conventions

| Convention | Rule |
|---|---|
| **File naming** | `{ClassUnderTest}Tests.cs` |
| **Method naming** | `MethodName_Scenario_ExpectedResult` |
| **Arrange/Act/Assert** | Use explicit sections with comments |
| **One assertion per test** | Preferred; multiple related assertions acceptable |
| **No test interdependencies** | Each test is fully isolated |
| **Mock boundaries** | Mock at service interface boundaries, not internal methods |

---

## Code Conventions

### C# Standards

- **Target**: C# 13 / .NET 9.0
- **Nullable**: Enabled project-wide (`<Nullable>enable</Nullable>`)
- **Implicit usings**: Enabled
- **File-scoped namespaces**: Preferred
- **Primary constructors**: Use for simple DI injection
- **`var`**: Use when type is obvious from the right-hand side
- **XML docs**: Required on public APIs, service interfaces, and tools

### Project Layering

```
Mcp → Agents → Core
              → State
```

- **Core** has NO dependencies on other projects (pure domain)
- **State** has NO dependencies on other projects
- **Agents** depends on Core + State
- **Mcp** depends on Agents + Core (entry point)
- **Never** add upward dependencies (Core → Agents would be a violation)

### Naming

| Element | Convention | Example |
|---|---|---|
| Interfaces | `I` prefix | `IKanbanService` |
| Tools | `{Domain}{Action}Tool` | `KanbanCreateBoardTool` |
| MCP tool names | `snake_case` | `kanban_create_board` |
| Services | `{Domain}Service` | `ComplianceWatchService` |
| Hosted services | `{Purpose}HostedService` | `RetentionCleanupHostedService` |
| Options | `{Section}Options` | `GatewayOptions` |
| Entities | PascalCase | `ComplianceAssessment` |
| Enums | PascalCase | `AlertSeverity`, `TaskStatus` |
| Constants | PascalCase in static class | `ComplianceRoles.Administrator` |
| Test classes | `{ClassUnderTest}Tests` | `AlertManagerTests` |

---

## Adding a New Tool

1. **Create the tool class** in `src/Ato.Copilot.Agents/Compliance/Tools/`:

```csharp
public class MyNewTool : BaseTool  // or a domain-specific base like KanbanToolBase
{
    public MyNewTool(IServiceScopeFactory scopeFactory, ILogger<MyNewTool> logger)
        : base() { /* store deps */ }

    public override string Name => "my_new_tool";
    public override string Description => "Does something useful.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters =>
        new Dictionary<string, ToolParameter>
        {
            ["param1"] = new() { Name = "param1", Description = "...", Type = "string", Required = true }
        };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        var param1 = GetArg<string>(arguments, "param1");
        if (string.IsNullOrWhiteSpace(param1))
            return Error("param1 is required.", "MISSING_PARAMETER");

        // Tool logic here
        return Success(new { result = "done" });
    }
}
```

2. **Register in DI** — add to `ServiceCollectionExtensions.AddComplianceAgent()`:

```csharp
services.AddSingleton<MyNewTool>();
services.AddSingleton<BaseTool>(sp => sp.GetRequiredService<MyNewTool>());
```

3. **Register in ComplianceAgent** — inject in constructor and call `RegisterTool()`:

```csharp
RegisterTool(myNewTool);
```

4. **Add MCP wrapper** in `ComplianceMcpTools.cs`:

```csharp
[Description("Does something useful")]
public async Task<string> MyNewToolAsync(string param1)
{
    var args = new Dictionary<string, object?> { ["param1"] = param1 };
    return await _myNewTool.ExecuteAsync(args, CancellationToken.None);
}
```

5. **Update the system prompt** in `ComplianceAgent.prompt.txt` to document the new tool.

6. **Write unit tests** in `tests/Ato.Copilot.Tests.Unit/`.

7. **Update documentation** in the relevant `docs/*.md` file.

---

## Adding a New Service

1. **Define the interface** in `src/Ato.Copilot.Core/Interfaces/`:

```csharp
public interface IMyService
{
    Task<Result> DoSomethingAsync(string input, CancellationToken ct = default);
}
```

2. **Implement** in `src/Ato.Copilot.Agents/Compliance/Services/`:

```csharp
public class MyService : IMyService
{
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;

    public MyService(IDbContextFactory<AtoCopilotContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<Result> DoSomethingAsync(string input, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        // ...
    }
}
```

3. **Register in DI** — choose the correct lifetime:
   - **Singleton** for stateless services (use `IDbContextFactory` for DB access)
   - **Scoped** for services with per-request state (use `AtoCopilotContext` directly)

---

## Adding a New Entity

1. **Define the model** in `src/Ato.Copilot.Core/Models/`:

```csharp
public class MyEntity : ConcurrentEntity  // if optimistic concurrency needed
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

2. **Add DbSet** to `AtoCopilotContext`:

```csharp
public DbSet<MyEntity> MyEntities { get; set; } = null!;
```

3. **Configure** in `OnModelCreating()`:

```csharp
modelBuilder.Entity<MyEntity>(e =>
{
    e.HasKey(x => x.Id);
    e.HasIndex(x => x.Name);
    // Add concurrency token if extending ConcurrentEntity
    e.Property(x => x.RowVersion).IsConcurrencyToken();
});
```

4. **Add migration**: Migrations are auto-applied at startup. If using EF migrations tooling:

```bash
dotnet ef migrations add AddMyEntity --project src/Ato.Copilot.Core --startup-project src/Ato.Copilot.Mcp
```

---

## Constitution Principles

The project follows a set of Constitution Principles that govern design decisions:

| Principle | Rule |
|---|---|
| **I** | Documentation as Source of Truth — new features MUST update relevant `/docs/*.md` |
| **II** | Agent/Tool Inheritance — all agents extend `BaseAgent`, all tools extend `BaseTool` |
| **III** | DI Registration — all services registered via extension methods |
| **IV** | Configuration Binding — use `IOptions<T>` pattern, never raw `IConfiguration` |
| **V** | Database Access — use `IDbContextFactory<AtoCopilotContext>` in singletons, direct injection in scoped |
| **VI** | Error Handling — tools return structured error responses, never throw to caller |
| **VII** | Standard Envelope — `ToolResponse<T>` / JSON success/error envelope on all tool outputs |
| **VIII** | Audit Trail — security-sensitive operations MUST log to `AuditLogEntry` |

---

## Specify Toolkit Workflow

Features are developed using the Specify spec-driven workflow:

```bash
# 1. Create a new feature spec
.specify/scripts/bash/create-new-feature.sh "Feature Name"

# 2. Write the spec (user stories, acceptance scenarios, FRs)
# Edit specs/<nnn>-feature-name/spec.md

# 3. Set up the implementation plan
.specify/scripts/bash/setup-plan.sh

# 4. Plan review (architecture decisions, task breakdown)
# Edit specs/<nnn>-feature-name/plan.md

# 5. Generate tasks
# Edit specs/<nnn>-feature-name/tasks.md

# 6. Implement tasks (T001, T002, ..., T0nn)

# 7. Update agent context
.specify/scripts/bash/update-agent-context.sh copilot
```

Each feature gets a dedicated branch (`<nnn>-feature-name`) and a spec directory under `specs/`.

---

## Branching Strategy

| Branch | Purpose |
|---|---|
| `main` | Production-ready code |
| `<nnn>-feature-name` | Feature development (e.g., `005-compliance-watch`) |

Feature branches are merged to `main` after all tasks are complete and tests pass.
