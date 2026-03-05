// ═══════════════════════════════════════════════════════════════════════════
// Feature 018 — Phase 7 (T052): SAP Performance Benchmark Tests
// Generates SAP for Moderate (~325 controls) and High (~421 controls) baselines
// and asserts generation completes within wall-clock limits.
// ═══════════════════════════════════════════════════════════════════════════

using System.Diagnostics;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Ato.Copilot.Tests.Unit.Performance;

/// <summary>
/// Performance benchmark tests for SAP generation operations.
/// Validates that baselines of various sizes complete within acceptable wall-clock limits.
/// </summary>
public class SapPerformanceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly SapService _service;
    private readonly Mock<INistControlsService> _nistServiceMock;
    private readonly Mock<IStigKnowledgeService> _stigServiceMock;

    private const string TestSystemId = "sys-perf-sap-001";

    // ─── NIST 800-53 control families for generating realistic baselines ──

    private static readonly string[] ControlFamilies =
    {
        "AC", "AT", "AU", "CA", "CM", "CP", "IA", "IR", "MA", "MP",
        "PE", "PL", "PM", "PS", "RA", "SA", "SC", "SI", "SR"
    };

    private readonly RegisteredSystem _testSystem = new()
    {
        Id = TestSystemId,
        Name = "Performance Test System",
        Acronym = "PTS",
        CurrentRmfStep = RmfPhase.Assess
    };

    public SapPerformanceTests(ITestOutputHelper output)
    {
        _output = output;

        var services = new ServiceCollection();
        var dbName = $"SapPerfTests_{Guid.NewGuid()}";
        services.AddDbContext<AtoCopilotContext>(options =>
            options.UseInMemoryDatabase(dbName));
        _serviceProvider = services.BuildServiceProvider();

        using var initScope = _serviceProvider.CreateScope();
        var initCtx = initScope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
        initCtx.Database.EnsureCreated();
        initCtx.RegisteredSystems.Add(_testSystem);
        initCtx.SaveChanges();

        _nistServiceMock = new Mock<INistControlsService>();
        _stigServiceMock = new Mock<IStigKnowledgeService>();

        // Default STIG mock — return empty list
        _stigServiceMock
            .Setup(s => s.GetStigsByCciChainAsync(It.IsAny<string>(), It.IsAny<StigSeverity?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StigControl>());

        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        _service = new SapService(
            scopeFactory,
            NullLogger<SapService>.Instance,
            _nistServiceMock.Object,
            _stigServiceMock.Object);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private List<string> GenerateControlIds(int count)
    {
        var controlIds = new List<string>();
        var perFamily = count / ControlFamilies.Length;
        var remainder = count % ControlFamilies.Length;

        for (var f = 0; f < ControlFamilies.Length; f++)
        {
            var familyCount = perFamily + (f < remainder ? 1 : 0);
            for (var i = 1; i <= familyCount; i++)
            {
                controlIds.Add($"{ControlFamilies[f]}-{i}");
            }
        }

        return controlIds;
    }

    private void SeedBaseline(int controlCount)
    {
        var controlIds = GenerateControlIds(controlCount);

        // Set up NIST mock for all control IDs
        foreach (var controlId in controlIds)
        {
            _nistServiceMock
                .Setup(s => s.GetControlEnhancementAsync(controlId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ControlEnhancement(
                    controlId,
                    $"{controlId} Title",
                    $"{controlId} statement text",
                    $"{controlId} guidance text",
                    new List<string> { $"{controlId}(a)", $"{controlId}(b)" },
                    DateTime.UtcNow));
        }

        using var scope = _serviceProvider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        var customerControls = (int)(controlCount * 0.6);
        var inheritedControls = (int)(controlCount * 0.2);
        var sharedControls = controlCount - customerControls - inheritedControls;

        ctx.ControlBaselines.Add(new ControlBaseline
        {
            RegisteredSystemId = TestSystemId,
            BaselineLevel = controlCount > 400 ? "High" : "Moderate",
            TotalControls = controlCount,
            CustomerControls = customerControls,
            InheritedControls = inheritedControls,
            SharedControls = sharedControls,
            ControlIds = controlIds
        });
        ctx.SaveChanges();
    }

    // ─── Benchmark Tests ─────────────────────────────────────────────────

    [Fact]
    public async Task GenerateSapAsync_ModerateBaseline325Controls_CompletesWithin15Seconds()
    {
        SeedBaseline(325);
        var input = new SapGenerationInput(SystemId: TestSystemId);

        var sw = Stopwatch.StartNew();
        var result = await _service.GenerateSapAsync(input);
        sw.Stop();

        _output.WriteLine($"Moderate baseline (325 controls): {sw.ElapsedMilliseconds}ms");

        result.Should().NotBeNull();
        result.TotalControls.Should().Be(325);
        result.Content.Should().NotBeNullOrWhiteSpace();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15),
            "GenerateSapAsync with Moderate baseline (~325 controls) should complete within 15 seconds");
    }

    [Fact]
    public async Task GenerateSapAsync_HighBaseline421Controls_CompletesWithin30Seconds()
    {
        SeedBaseline(421);
        var input = new SapGenerationInput(SystemId: TestSystemId);

        var sw = Stopwatch.StartNew();
        var result = await _service.GenerateSapAsync(input);
        sw.Stop();

        _output.WriteLine($"High baseline (421 controls): {sw.ElapsedMilliseconds}ms");

        result.Should().NotBeNull();
        result.TotalControls.Should().Be(421);
        result.Content.Should().NotBeNullOrWhiteSpace();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30),
            "GenerateSapAsync with High baseline (~421 controls) should complete within 30 seconds");
    }
}
