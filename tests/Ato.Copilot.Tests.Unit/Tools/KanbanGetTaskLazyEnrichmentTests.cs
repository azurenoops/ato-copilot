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
/// Unit tests for lazy enrichment in <see cref="KanbanGetTaskTool"/> (Feature 012 — US4).
/// Covers: T054-T057 from tasks.md.
/// </summary>
public class KanbanGetTaskLazyEnrichmentTests : IDisposable
{
    private readonly Mock<IKanbanService> _kanbanService;
    private readonly Mock<ITaskEnrichmentService> _enrichmentService;
    private readonly DbContextOptions<AtoCopilotContext> _dbOptions;
    private readonly string _dbName;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KanbanGetTaskTool _tool;

    public KanbanGetTaskLazyEnrichmentTests()
    {
        _kanbanService = new Mock<IKanbanService>();
        _enrichmentService = new Mock<ITaskEnrichmentService>();
        _dbName = $"LazyEnrichTests_{Guid.NewGuid()}";
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
        _tool = new KanbanGetTaskTool(_scopeFactory, Mock.Of<ILogger<KanbanGetTaskTool>>());
    }

    public void Dispose()
    {
        using var ctx = new AtoCopilotContext(_dbOptions);
        ctx.Database.EnsureDeleted();
    }

    // ─── T054: Lazy enrichment triggers when script is null ──────────────

    [Fact]
    public async Task GetTask_NullScript_TriggersLazyEnrichment()
    {
        var task = CreateTask();
        SeedTask(task);
        SeedFinding(task.FindingId!);

        _kanbanService.Setup(s => s.GetTaskAsync("REM-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        _enrichmentService.Setup(s => s.EnrichTaskAsync(
                task, It.IsAny<ComplianceFinding?>(), ScriptType.AzureCli, false, It.IsAny<CancellationToken>()))
            .Callback<RemediationTask, ComplianceFinding?, ScriptType, bool, CancellationToken>((t, f, st, force, ct) =>
            {
                t.RemediationScript = "az resource update ...";
                t.RemediationScriptType = "AzureCli";
            })
            .ReturnsAsync(new TaskEnrichmentResult
            {
                TaskId = task.Id, TaskNumber = task.TaskNumber,
                ScriptGenerated = true, GenerationMethod = "AI", ScriptType = "AzureCli"
            });

        var args = new Dictionary<string, object?> { ["task_id"] = "REM-001" };
        var result = await _tool.ExecuteAsync(args);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("lazyEnriched").GetBoolean().Should().BeTrue();
        data.GetProperty("remediationScript").GetString().Should().Contain("az resource update");

        _enrichmentService.Verify(s => s.EnrichTaskAsync(
            task, It.IsAny<ComplianceFinding?>(), ScriptType.AzureCli, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── T055: Lazy enrichment skipped when script already exists ────────

    [Fact]
    public async Task GetTask_ExistingScript_SkipsLazyEnrichment()
    {
        var task = CreateTask(script: "existing script");
        _kanbanService.Setup(s => s.GetTaskAsync("REM-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var args = new Dictionary<string, object?> { ["task_id"] = "REM-001" };
        var result = await _tool.ExecuteAsync(args);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("lazyEnriched").GetBoolean().Should().BeFalse();
        data.GetProperty("remediationScript").GetString().Should().Be("existing script");

        _enrichmentService.Verify(s => s.EnrichTaskAsync(
            It.IsAny<RemediationTask>(), It.IsAny<ComplianceFinding?>(),
            It.IsAny<ScriptType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── T056: Lazy enrichment skipped when no FindingId ─────────────────

    [Fact]
    public async Task GetTask_NoFindingId_SkipsLazyEnrichment()
    {
        var task = CreateTask();
        task.FindingId = null;
        _kanbanService.Setup(s => s.GetTaskAsync("REM-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var args = new Dictionary<string, object?> { ["task_id"] = "REM-001" };
        var result = await _tool.ExecuteAsync(args);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("lazyEnriched").GetBoolean().Should().BeFalse();

        _enrichmentService.Verify(s => s.EnrichTaskAsync(
            It.IsAny<RemediationTask>(), It.IsAny<ComplianceFinding?>(),
            It.IsAny<ScriptType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── T057: Lazy enrichment failure is graceful ───────────────────────

    [Fact]
    public async Task GetTask_EnrichmentThrows_ReturnsTaskWithoutEnrichment()
    {
        var task = CreateTask();
        SeedTask(task);
        SeedFinding(task.FindingId!);

        _kanbanService.Setup(s => s.GetTaskAsync("REM-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        _enrichmentService.Setup(s => s.EnrichTaskAsync(
                task, It.IsAny<ComplianceFinding?>(), It.IsAny<ScriptType>(), false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("AI service unavailable"));

        var args = new Dictionary<string, object?> { ["task_id"] = "REM-001" };
        var result = await _tool.ExecuteAsync(args);
        var doc = JsonDocument.Parse(result);

        // Should still return success with the unenriched task
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("lazyEnriched").GetBoolean().Should().BeFalse();
    }

    // ─── Response includes remediationScriptType ─────────────────────────

    [Fact]
    public async Task GetTask_IncludesRemediationScriptType()
    {
        var task = CreateTask(script: "Set-AzVM ...");
        task.RemediationScriptType = "PowerShell";
        _kanbanService.Setup(s => s.GetTaskAsync("REM-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var args = new Dictionary<string, object?> { ["task_id"] = "REM-001" };
        var result = await _tool.ExecuteAsync(args);
        var doc = JsonDocument.Parse(result);

        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("remediationScriptType").GetString().Should().Be("PowerShell");
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
