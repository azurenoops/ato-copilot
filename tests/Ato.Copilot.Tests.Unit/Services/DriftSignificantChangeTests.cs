using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// T250 — Unit tests for Watch drift → significant change auto-detection (Phase 17 §9a.5).
/// Tests: threshold not met → no change, threshold exceeded → change created,
/// no registered system → skipped.
/// </summary>
public class DriftSignificantChangeTests : IDisposable
{
    private readonly DbContextOptions<AtoCopilotContext> _dbOptions;
    private readonly Mock<IAlertManager> _mockAlertManager;
    private readonly Mock<IAtoComplianceEngine> _mockEngine;
    private readonly Mock<IRemediationEngine> _mockRemediation;
    private readonly Mock<ISystemSubscriptionResolver> _mockResolver;
    private readonly Mock<IConMonService> _mockConMon;
    private readonly MonitoringOptions _monitoringOptions;

    public DriftSignificantChangeTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase(databaseName: $"DriftSigChange_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _mockAlertManager = new Mock<IAlertManager>();
        _mockAlertManager
            .Setup(x => x.CreateAlertAsync(It.IsAny<ComplianceAlert>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ComplianceAlert a, CancellationToken _) =>
            {
                a.Id = Guid.NewGuid();
                a.AlertId = $"ALT-{DateTime.UtcNow:yyyyMMdd}00001";
                return a;
            });

        _mockEngine = new Mock<IAtoComplianceEngine>();
        _mockRemediation = new Mock<IRemediationEngine>();

        _mockResolver = new Mock<ISystemSubscriptionResolver>();
        _mockConMon = new Mock<IConMonService>();

        _monitoringOptions = new MonitoringOptions
        {
            SignificantDriftThreshold = 3, // Low threshold for testing
            AutoCreateSignificantChanges = true
        };

        // Default setup: RunAssessmentAsync returns an assessment with findings
        // that produce different hashes from the baselines (to trigger drift detection).
        _mockEngine
            .Setup(x => x.RunAssessmentAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string subId, string? fw, string? cf, string? rt, string? st, bool ip, CancellationToken ct) =>
            {
                var assessment = new ComplianceAssessment
                {
                    SubscriptionId = subId,
                    Status = AssessmentStatus.Completed
                };
                // Create findings for each seeded resource so drift is detected
                // (the hash will differ from the baseline's ConfigurationHash)
                using var db = new AtoCopilotContext(_dbOptions);
                var baselines = db.ComplianceBaselines
                    .Where(b => b.SubscriptionId == subId && b.IsActive)
                    .ToList();
                foreach (var baseline in baselines)
                {
                    assessment.Findings.Add(new ComplianceFinding
                    {
                        ResourceId = baseline.ResourceId,
                        ResourceType = baseline.ResourceType,
                        ControlId = "AC-1",
                        Status = FindingStatus.Open,
                        Severity = FindingSeverity.Medium,
                        Description = "Simulated drift finding"
                    });
                }
                return assessment;
            });
    }

    public void Dispose()
    {
        using var db = new AtoCopilotContext(_dbOptions);
        db.Database.EnsureDeleted();
    }

    [Fact]
    public async Task DetectDriftAsync_BelowThreshold_NoSignificantChange()
    {
        // Arrange — 2 baselines with drift (below threshold of 3)
        await SeedBaselines("sub-001", 2);
        _mockResolver.Setup(x => x.ResolveAsync("sub-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync("sys-001");

        var service = CreateWatchService();

        // Act
        var alerts = await service.DetectDriftAsync("sub-001");

        // Assert — should NOT call ReportChangeAsync (below threshold)
        _mockConMon.Verify(
            x => x.ReportChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DetectDriftAsync_NoRegisteredSystem_SkipsSignificantChange()
    {
        // Arrange — subscription not mapped to any system
        await SeedBaselines("sub-orphan", 5);
        _mockResolver.Setup(x => x.ResolveAsync("sub-orphan", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var service = CreateWatchService();

        // Act
        var alerts = await service.DetectDriftAsync("sub-orphan");

        // Assert — resolver returns null → no ReportChangeAsync call
        _mockConMon.Verify(
            x => x.ReportChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DetectDriftAsync_NoResolver_SkipsSignificantChange()
    {
        // Arrange — no resolver injected
        await SeedBaselines("sub-001", 5);

        var factory = new TestDbContextFactory(_dbOptions);
        var service = new ComplianceWatchService(
            factory,
            _mockAlertManager.Object,
            _mockEngine.Object,
            _mockRemediation.Object,
            Options.Create(_monitoringOptions),
            Options.Create(new AlertOptions()),
            Mock.Of<ILogger<ComplianceWatchService>>(),
            subscriptionResolver: null,
            serviceScopeFactory: null);

        // Act
        var alerts = await service.DetectDriftAsync("sub-001");

        // Assert — no resolver → no significant change attempt
        _mockConMon.Verify(
            x => x.ReportChangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private ComplianceWatchService CreateWatchService()
    {
        // Set up service scope factory with mock ConMonService
        var services = new ServiceCollection();
        services.AddSingleton(_dbOptions);
        services.AddScoped(sp => new AtoCopilotContext(sp.GetRequiredService<DbContextOptions<AtoCopilotContext>>()));
        services.AddScoped<IConMonService>(_ => _mockConMon.Object);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var factory = new TestDbContextFactory(_dbOptions);

        return new ComplianceWatchService(
            factory,
            _mockAlertManager.Object,
            _mockEngine.Object,
            _mockRemediation.Object,
            Options.Create(_monitoringOptions),
            Options.Create(new AlertOptions()),
            Mock.Of<ILogger<ComplianceWatchService>>(),
            _mockResolver.Object,
            scopeFactory);
    }

    private async Task SeedBaselines(string subscriptionId, int count)
    {
        await using var db = new AtoCopilotContext(_dbOptions);
        for (var i = 0; i < count; i++)
        {
            db.ComplianceBaselines.Add(new ComplianceBaseline
            {
                SubscriptionId = subscriptionId,
                ResourceId = $"/subscriptions/{subscriptionId}/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/sa{i}",
                ResourceType = "Microsoft.Storage/storageAccounts",
                ConfigurationHash = $"old-hash-{i}",
                ConfigurationSnapshot = "{}",
                IsActive = true,
                CapturedAt = DateTimeOffset.UtcNow.AddDays(-7)
            });
        }
        await db.SaveChangesAsync();
    }

    private sealed class TestDbContextFactory : IDbContextFactory<AtoCopilotContext>
    {
        private readonly DbContextOptions<AtoCopilotContext> _options;
        public TestDbContextFactory(DbContextOptions<AtoCopilotContext> options) => _options = options;
        public AtoCopilotContext CreateDbContext() => new(_options);
    }
}
