# Testing Guide

> Test structure, naming conventions, mock patterns, and coverage requirements.

---

## Test Projects

| Project | Framework | Runner | Location |
|---------|-----------|--------|----------|
| `Ato.Copilot.Tests.Unit` | xUnit + FluentAssertions + Moq | `dotnet test` | `tests/Ato.Copilot.Tests.Unit/` |
| M365 Extension Tests | Mocha + Chai | `npm test` | `extensions/m365/` |
| VS Code Extension Tests | Mocha + Chai | `npm test` | `extensions/vscode/` |

---

## .NET Unit Tests

### Naming Convention

```
MethodName_StateUnderTest_ExpectedBehavior
```

Examples:
```csharp
[Fact]
public async Task ExecuteCoreAsync_ValidSystemId_ReturnsRegisteredSystem()

[Fact]
public async Task ExecuteCoreAsync_MissingRequiredParameter_ThrowsArgumentException()

[Fact]
public async Task ExecuteCoreAsync_SystemNotFound_ReturnsNotFoundMessage()
```

### Test Class Organization

```
tests/Ato.Copilot.Tests.Unit/
├── Agents/
│   └── ComplianceAgentTests.cs          # Agent registration & tool discovery
├── Tools/
│   ├── ComplianceAssessmentToolTests.cs # Per-tool tests
│   ├── KanbanCreateBoardToolTests.cs
│   └── ...
├── Services/
│   ├── NistControlsServiceTests.cs
│   └── ...
├── Models/
│   └── EntityValidationTests.cs
└── CrossCutting/
    ├── StructuredLoggingTests.cs
    └── ProgressIndicatorTests.cs
```

### Database Setup

Use EF Core In-Memory provider for unit tests:

```csharp
private static AtoCopilotContext CreateContext()
{
    var options = new DbContextOptionsBuilder<AtoCopilotContext>()
        .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
        .Options;
    return new AtoCopilotContext(options);
}
```

For tools that require `IServiceScopeFactory`:

```csharp
private static IServiceScopeFactory CreateScopeFactory(AtoCopilotContext context)
{
    var services = new ServiceCollection();
    services.AddSingleton(context);
    services.AddSingleton<IDesignTimeDbContextFactory<AtoCopilotContext>>(
        new InMemoryDbContextFactory(
            new DbContextOptionsBuilder<AtoCopilotContext>()
                .UseInMemoryDatabase($"Test_{Guid.NewGuid()}")
                .Options));
    return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
}
```

### Mock Patterns

Use `Moq` for service interfaces:

```csharp
// Simple mock
var logger = Mock.Of<ILogger<MyTool>>();

// Mock with setup
var serviceMock = new Mock<IMyService>();
serviceMock.Setup(s => s.GetDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(expectedData);

var tool = new MyTool(serviceMock.Object, logger);
```

### Assertion Patterns

Use FluentAssertions for readable assertions:

```csharp
// String result from tool
var result = await tool.ExecuteCoreAsync(args);
result.Should().Contain("success");

// JSON result
var json = JsonDocument.Parse(result);
json.RootElement.GetProperty("status").GetString().Should().Be("completed");

// Collection
items.Should().HaveCount(3);
items.Should().Contain(x => x.Name == "Expected");

// Exception
var act = () => tool.ExecuteCoreAsync(invalidArgs);
await act.Should().ThrowAsync<ArgumentException>()
    .WithMessage("*required*");
```

### Test Data Builders

For complex entity setup, use builder methods:

```csharp
private static RegisteredSystem CreateTestSystem(
    string name = "Test System",
    RmfStep step = RmfStep.Prepare)
{
    return new RegisteredSystem
    {
        Id = Guid.NewGuid(),
        Name = name,
        CurrentRmfStep = step,
        CreatedAt = DateTime.UtcNow,
    };
}
```

---

## TypeScript Tests (M365 / VS Code)

### Naming Convention

```typescript
describe('ComponentName', () => {
    it('should do expected behavior when condition', async () => {
        // Arrange, Act, Assert
    });

    it('should handle error case gracefully', async () => {
        // ...
    });
});
```

### Running Tests

```bash
# M365 extension
cd extensions/m365
npm test

# VS Code extension
cd extensions/vscode
npm test
```

---

## Coverage Requirements

| Metric | Target |
|--------|--------|
| Line coverage | ≥ 80% |
| Branch coverage | ≥ 70% |
| Critical paths (auth, assessment, authorization) | 100% |

### Generating Coverage

```bash
dotnet test --collect:"XPlat Code Coverage" \
    --results-directory ./coverage
```

---

## Test Categories

Tag tests for selective execution:

```csharp
[Trait("Category", "Unit")]
[Trait("Feature", "015")]
public class MyToolTests { }
```

Run by category:

```bash
dotnet test --filter "Category=Unit&Feature=015"
```
