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
/// Unit tests for <see cref="KanbanGenerateScriptTool"/> (Feature 012 — US2).
/// Covers: T035-T040 from tasks.md.
/// </summary>
public class KanbanGenerateScriptToolTests : IDisposable
{
    private readonly Mock<IKanbanService> _kanbanService;
    private readonly Mock<ITaskEnrichmentService> _enrichmentService;
    private readonly DbContextOptions<AtoCopilotContext> _dbOptions;
    private readonly string _dbName;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KanbanGenerateScriptTool _tool;

    public KanbanGenerateScriptToolTests()
    {
        _kanbanService = new Mock<IKanbanService>();
        _enrichmentService = new Mock<ITaskEnrichmentService>();
        _dbName = $"GenerateScriptToolTests_{Guid.NewGuid()}";
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
        _tool = new KanbanGenerateScriptTool(_scopeFactory, Mock.Of<ILogger<KanbanGenerateScriptTool>>());
    }

    public void Dispose()
    {
        using var ctx = new AtoCopilotContext(_dbOptions);
        ctx.Database.EnsureDeleted();
    }

    // ─── T035: Tool returns TASK_NOT_FOUND for missing task ──────────────

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

    // ─── T036: Tool returns SCRIPT_EXISTS when not forced ────────────────

    [Fact]
    public async Task ExecuteAsync_ScriptExists_NoForce_ReturnsScriptExistsError()
    {
        var task = CreateTask(script: "existing script");
        _kanbanService.Setup(s => s.GetTaskAsync("REM-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var args = new Dictionary<string, object?> { ["task_id"] = "REM-001" };
        var result = await _tool.ExecuteAsync(args);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("SCRIPT_EXISTS");
    }

    // ─── T037: Tool generates script successfully ────────────────────────

    [Fact]
    public async Task ExecuteAsync_NoExistingScript_GeneratesScript()
    {
        var task = CreateTask();
        SeedTask(task);
        var findingId = task.FindingId!;
        SeedFinding(findingId);

        _kanbanService.Setup(s => s.GetTaskAsync("REM-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        _enrichmentService.Setup(s => s.EnrichTaskAsync(
                task, It.IsAny<ComplianceFinding?>(), ScriptType.AzureCli, false, It.IsAny<CancellationToken>()))
            .Callback<RemediationTask, ComplianceFinding?, ScriptType, bool, CancellationToken>((t, f, st, force, ct) =>
            {
                t.RemediationScript = "az vm update --set ...";
                t.RemediationScriptType = "AzureCli";
            })
            .ReturnsAsync(new TaskEnrichmentResult
            {
                TaskId = task.Id, TaskNumber = task.TaskNumber,
                ScriptGenerated = true, GenerationMethod = "AI",
                ScriptType = "AzureCli"
            });

        var args = new Dictionary<string, object?> { ["task_id"] = "REM-001" };
        var result = await _tool.ExecuteAsync(args);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("scriptGenerated").GetBoolean().Should().BeTrue();
        data.GetProperty("generationMethod").GetString().Should().Be("AI");
        data.GetProperty("remediationScript").GetString().Should().Contain("az vm update");
    }

    // ─── T038: Tool respects force flag ──────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ScriptExists_ForceTrue_RegeneratesScript()
    {
        var task = CreateTask(script: "old script");
        SeedTask(task);
        var findingId = task.FindingId!;
        SeedFinding(findingId);

        _kanbanService.Setup(s => s.GetTaskAsync("REM-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        _enrichmentService.Setup(s => s.EnrichTaskAsync(
                task, It.IsAny<ComplianceFinding?>(), ScriptType.PowerShell, true, It.IsAny<CancellationToken>()))
            .Callback<RemediationTask, ComplianceFinding?, ScriptType, bool, CancellationToken>((t, f, st, force, ct) =>
            {
                t.RemediationScript = "Set-AzVM -Force ...";
                t.RemediationScriptType = "PowerShell";
            })
            .ReturnsAsync(new TaskEnrichmentResult
            {
                TaskId = task.Id, TaskNumber = task.TaskNumber,
                ScriptGenerated = true, GenerationMethod = "AI",
                ScriptType = "PowerShell"
            });

        var args = new Dictionary<string, object?>
        {
            ["task_id"] = "REM-001", ["script_type"] = "PowerShell", ["force"] = true
        };
        var result = await _tool.ExecuteAsync(args);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("scriptType").GetString().Should().Be("PowerShell");
    }

    // ─── T039: Tool handles enrichment failure ───────────────────────────

    [Fact]
    public async Task ExecuteAsync_EnrichmentFails_ReturnsGenerationFailed()
    {
        var task = CreateTask();
        _kanbanService.Setup(s => s.GetTaskAsync("REM-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        _enrichmentService.Setup(s => s.EnrichTaskAsync(
                task, It.IsAny<ComplianceFinding?>(), It.IsAny<ScriptType>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaskEnrichmentResult
            {
                TaskId = task.Id, TaskNumber = task.TaskNumber,
                Error = "AI service unavailable"
            });

        var args = new Dictionary<string, object?> { ["task_id"] = "REM-001" };
        var result = await _tool.ExecuteAsync(args);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("GENERATION_FAILED");
    }

    // ─── T040: Tool name and parameters are correct ──────────────────────

    [Fact]
    public async Task ToolMetadata_IsCorrect()
    {
        await Task.CompletedTask;
        _tool.Name.Should().Be("kanban_generate_script");
        _tool.Parameters.Should().ContainKey("task_id");
        _tool.Parameters.Should().ContainKey("script_type");
        _tool.Parameters.Should().ContainKey("force");
        _tool.Parameters["task_id"].Required.Should().BeTrue();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static RemediationTask CreateTask(string? script = null) => new()
    {
        Id = Guid.NewGuid().ToString(),
        TaskNumber = "REM-001",
        Title = "Fix NSG rule",
        Description = "Update NSG to restrict inbound traffic",
        FindingId = Guid.NewGuid().ToString(),
        Status = TaskStatus.Backlog,
        Severity = FindingSeverity.High,
        RemediationScript = script,
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
