using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Kanban;
using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.Core.Models.Kanban;
using TaskStatus = Ato.Copilot.Core.Models.Kanban.TaskStatus;

namespace Ato.Copilot.Tests.Unit.Tools;

/// <summary>
/// Unit tests for <see cref="KanbanGenerateValidationTool"/> (Feature 012 — US3).
/// Covers: T045-T048 from tasks.md.
/// </summary>
public class KanbanGenerateValidationToolTests : IDisposable
{
    private readonly Mock<IKanbanService> _kanbanService;
    private readonly Mock<ITaskEnrichmentService> _enrichmentService;
    private readonly DbContextOptions<AtoCopilotContext> _dbOptions;
    private readonly string _dbName;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KanbanGenerateValidationTool _tool;

    public KanbanGenerateValidationToolTests()
    {
        _kanbanService = new Mock<IKanbanService>();
        _enrichmentService = new Mock<ITaskEnrichmentService>();
        _dbName = $"GenerateValidationToolTests_{Guid.NewGuid()}";
        _dbOptions = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        var services = new ServiceCollection();
        services.AddSingleton(_kanbanService.Object);
        services.AddSingleton(_enrichmentService.Object);
        services.AddDbContext<AtoCopilotContext>(opts =>
            opts.UseInMemoryDatabase(_dbName));
        var sp = services.BuildServiceProvider();

        _scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        _tool = new KanbanGenerateValidationTool(_scopeFactory, Mock.Of<ILogger<KanbanGenerateValidationTool>>());
    }

    public void Dispose()
    {
        using var ctx = new AtoCopilotContext(_dbOptions);
        ctx.Database.EnsureDeleted();
    }

    // ─── T045: Tool returns TASK_NOT_FOUND for missing task ──────────────

    [Fact]
    public async Task ExecuteAsync_MissingTaskId_ReturnsError()
    {
        var args = new Dictionary<string, object?> { ["task_id"] = "" };
        var result = await _tool.ExecuteAsync(args);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("TASK_NOT_FOUND");
    }

    [Fact]
    public async Task ExecuteAsync_TaskNotFound_ReturnsError()
    {
        _kanbanService.Setup(s => s.GetTaskAsync("REM-999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RemediationTask?)null);

        var args = new Dictionary<string, object?> { ["task_id"] = "REM-999" };
        var result = await _tool.ExecuteAsync(args);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("TASK_NOT_FOUND");
    }

    // ─── T046: Tool returns CRITERIA_EXISTS when not forced ──────────────

    [Fact]
    public async Task ExecuteAsync_CriteriaExists_NoForce_ReturnsCriteriaExistsError()
    {
        var task = CreateTask(validation: "existing criteria");
        _kanbanService.Setup(s => s.GetTaskAsync("REM-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var args = new Dictionary<string, object?> { ["task_id"] = "REM-001" };
        var result = await _tool.ExecuteAsync(args);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("CRITERIA_EXISTS");
    }

    // ─── T047: Tool returns FINDING_NOT_FOUND when no linked finding ─────

    [Fact]
    public async Task ExecuteAsync_NoLinkedFinding_ReturnsFindingNotFound()
    {
        var task = CreateTask();
        task.FindingId = null; // No linked finding

        _kanbanService.Setup(s => s.GetTaskAsync("REM-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var args = new Dictionary<string, object?> { ["task_id"] = "REM-001" };
        var result = await _tool.ExecuteAsync(args);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("FINDING_NOT_FOUND");
    }

    [Fact]
    public async Task ExecuteAsync_FindingNotInDb_ReturnsFindingNotFound()
    {
        var task = CreateTask();
        task.FindingId = Guid.NewGuid().ToString(); // Finding not seeded in DB

        _kanbanService.Setup(s => s.GetTaskAsync("REM-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var args = new Dictionary<string, object?> { ["task_id"] = "REM-001" };
        var result = await _tool.ExecuteAsync(args);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("FINDING_NOT_FOUND");
    }

    // ─── T048: Tool generates validation criteria successfully ───────────

    [Fact]
    public async Task ExecuteAsync_ValidTask_GeneratesValidationCriteria()
    {
        var task = CreateTask();
        SeedTask(task);
        var findingId = task.FindingId!;
        SeedFinding(findingId);

        _kanbanService.Setup(s => s.GetTaskAsync("REM-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        _enrichmentService.Setup(s => s.GenerateValidationCriteriaAsync(
                It.IsAny<ComplianceFinding>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("1. Verify NSG rule restricts inbound\n2. Run az network nsg show");

        var args = new Dictionary<string, object?> { ["task_id"] = "REM-001" };
        var result = await _tool.ExecuteAsync(args);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("validationCriteria").GetString().Should().Contain("Verify NSG");
    }

    [Fact]
    public async Task ExecuteAsync_ForceTrue_RegeneratesValidation()
    {
        var task = CreateTask(validation: "old criteria");
        SeedTask(task);
        var findingId = task.FindingId!;
        SeedFinding(findingId);

        _kanbanService.Setup(s => s.GetTaskAsync("REM-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        _enrichmentService.Setup(s => s.GenerateValidationCriteriaAsync(
                It.IsAny<ComplianceFinding>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Updated validation criteria");

        var args = new Dictionary<string, object?> { ["task_id"] = "REM-001", ["force"] = true };
        var result = await _tool.ExecuteAsync(args);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        task.ValidationCriteria.Should().Be("Updated validation criteria");
    }

    // ─── Tool metadata ──────────────────────────────────────────────────

    [Fact]
    public void ToolMetadata_IsCorrect()
    {
        _tool.Name.Should().Be("kanban_generate_validation");
        _tool.Parameters.Should().ContainKey("task_id");
        _tool.Parameters.Should().ContainKey("force");
        _tool.Parameters["task_id"].Required.Should().BeTrue();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static RemediationTask CreateTask(string? validation = null) => new()
    {
        Id = Guid.NewGuid().ToString(),
        TaskNumber = "REM-001",
        Title = "Fix NSG rule",
        Description = "Update NSG to restrict inbound traffic",
        FindingId = Guid.NewGuid().ToString(),
        Status = TaskStatus.Backlog,
        Severity = FindingSeverity.High,
        ValidationCriteria = validation,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private void SeedTask(RemediationTask task)
    {
        using var ctx = new AtoCopilotContext(_dbOptions);
        ctx.RemediationTasks.Add(task);
        ctx.SaveChanges();
    }

    private void SeedFinding(string findingId)
    {
        using var ctx = new AtoCopilotContext(_dbOptions);
        ctx.Findings.Add(new ComplianceFinding
        {
            Id = findingId,
            Title = "NSG too permissive",
            Description = "NSG allows all inbound traffic",
            Severity = FindingSeverity.High,
            Status = FindingStatus.Open,
            ResourceId = "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Network/networkSecurityGroups/nsg1",
            ResourceType = "Microsoft.Network/networkSecurityGroups",
            ControlId = "AC-4",
            ControlFamily = "Access Control",
            DiscoveredAt = DateTime.UtcNow
        });
        ctx.SaveChanges();
    }
}
