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
/// T247 — Unit tests for ConMon → Alert pipeline (Phase 17 §9a.4).
/// Tests: expiration alert at each severity level, significant change alert,
/// alert creation with RegisteredSystemId populated.
/// </summary>
public class ConMonAlertPipelineTests : IDisposable
{
    private readonly DbContextOptions<AtoCopilotContext> _dbOptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Mock<IAlertManager> _mockAlertManager;
    private readonly List<ComplianceAlert> _createdAlerts = new();

    public ConMonAlertPipelineTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase(databaseName: $"ConMonAlert_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var services = new ServiceCollection();
        services.AddSingleton(_dbOptions);
        services.AddScoped(sp => new AtoCopilotContext(sp.GetRequiredService<DbContextOptions<AtoCopilotContext>>()));
        var provider = services.BuildServiceProvider();
        _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        _mockAlertManager = new Mock<IAlertManager>();
        _mockAlertManager
            .Setup(x => x.CreateAlertAsync(It.IsAny<ComplianceAlert>(), It.IsAny<CancellationToken>()))
            .Callback<ComplianceAlert, CancellationToken>((alert, _) =>
            {
                alert.Id = Guid.NewGuid();
                alert.AlertId = $"ALT-TEST-{_createdAlerts.Count + 1:D3}";
                _createdAlerts.Add(alert);
            })
            .ReturnsAsync((ComplianceAlert alert, CancellationToken _) => alert);
    }

    public void Dispose()
    {
        using var db = new AtoCopilotContext(_dbOptions);
        db.Database.EnsureDeleted();
    }

    // ─── Expiration Alert Tests (T244) ──────────────────────────────────────

    [Theory]
    [InlineData(15, "Urgent", AlertSeverity.High)]     // 15 days → Urgent → High
    [InlineData(45, "Warning", AlertSeverity.Medium)]   // 45 days → Warning → Medium
    [InlineData(80, "Info", AlertSeverity.Low)]          // 80 days → Info → Low
    public async Task CheckExpirationAsync_AlertLevel_CreatesAlertWithCorrectSeverity(
        int daysUntilExpiration, string expectedLevel, AlertSeverity expectedSeverity)
    {
        // Arrange
        var systemId = await SeedSystemWithAuth(daysUntilExpiration);
        var service = CreateConMonService();

        // Act
        var status = await service.CheckExpirationAsync(systemId);

        // Assert
        status.AlertLevel.Should().Be(expectedLevel);
        _createdAlerts.Should().ContainSingle();
        _createdAlerts[0].Severity.Should().Be(expectedSeverity);
        _createdAlerts[0].RegisteredSystemId.Should().Be(systemId);
    }

    [Fact]
    public async Task CheckExpirationAsync_Expired_CreatesCriticalAlert()
    {
        // Arrange — expired 5 days ago
        var systemId = await SeedSystemWithAuth(-5);
        var service = CreateConMonService();

        // Act
        var status = await service.CheckExpirationAsync(systemId);

        // Assert
        status.AlertLevel.Should().Be("Expired");
        _createdAlerts.Should().ContainSingle();
        _createdAlerts[0].Severity.Should().Be(AlertSeverity.Critical);
        _createdAlerts[0].Title.Should().Contain("Expired");
    }

    [Fact]
    public async Task CheckExpirationAsync_NoAlert_WhenStatusNone()
    {
        // Arrange — 365 days until expiration → "None"
        var systemId = await SeedSystemWithAuth(365);
        var service = CreateConMonService();

        // Act
        var status = await service.CheckExpirationAsync(systemId);

        // Assert
        status.AlertLevel.Should().Be("None");
        _createdAlerts.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckExpirationAsync_NoAlertManager_SkipsGracefully()
    {
        // Arrange — no alert manager injected
        var systemId = await SeedSystemWithAuth(15);
        var service = new ConMonService(
            _scopeFactory,
            Mock.Of<ILogger<ConMonService>>(),
            watchService: null,
            alertManager: null); // No alert manager

        // Act
        var status = await service.CheckExpirationAsync(systemId);

        // Assert — still returns status, no alerts created
        status.AlertLevel.Should().Be("Urgent");
        _mockAlertManager.Verify(
            x => x.CreateAlertAsync(It.IsAny<ComplianceAlert>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── Significant Change Alert Tests (T245) ─────────────────────────────

    [Fact]
    public async Task ReportChangeAsync_RequiresReauth_CreatesHighSeverityAlert()
    {
        // Arrange
        var systemId = await SeedSystem();
        var service = CreateConMonService();

        // Act
        var change = await service.ReportChangeAsync(
            systemId, "New Interconnection", "VPN to partner org", "issm-user");

        // Assert
        change.RequiresReauthorization.Should().BeTrue();
        _createdAlerts.Should().ContainSingle();
        _createdAlerts[0].Severity.Should().Be(AlertSeverity.High);
        _createdAlerts[0].Type.Should().Be(AlertType.Violation);
        _createdAlerts[0].RegisteredSystemId.Should().Be(systemId);
        _createdAlerts[0].Title.Should().Contain("New Interconnection");
    }

    [Fact]
    public async Task ReportChangeAsync_NoReauth_NoAlert()
    {
        // Arrange — change type that doesn't require reauthorization
        var systemId = await SeedSystem();
        var service = CreateConMonService();

        // Act
        var change = await service.ReportChangeAsync(
            systemId, "Minor Patch", "Security patch applied", "issm-user");

        // Assert
        change.RequiresReauthorization.Should().BeFalse();
        _createdAlerts.Should().BeEmpty();
    }

    private ConMonService CreateConMonService()
    {
        return new ConMonService(
            _scopeFactory,
            Mock.Of<ILogger<ConMonService>>(),
            watchService: null,
            alertManager: _mockAlertManager.Object);
    }

    private async Task<string> SeedSystem()
    {
        await using var db = new AtoCopilotContext(_dbOptions);
        var systemId = Guid.NewGuid().ToString();
        db.RegisteredSystems.Add(new RegisteredSystem
        {
            Id = systemId,
            Name = "Test System",
            SystemType = SystemType.MajorApplication,
            MissionCriticality = MissionCriticality.MissionCritical,
            HostingEnvironment = "Azure Government",
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();
        return systemId;
    }

    private async Task<string> SeedSystemWithAuth(int daysUntilExpiration)
    {
        await using var db = new AtoCopilotContext(_dbOptions);
        var systemId = Guid.NewGuid().ToString();
        db.RegisteredSystems.Add(new RegisteredSystem
        {
            Id = systemId,
            Name = "Test System",
            SystemType = SystemType.MajorApplication,
            MissionCriticality = MissionCriticality.MissionCritical,
            HostingEnvironment = "Azure Government",
            CreatedBy = "test"
        });

        db.AuthorizationDecisions.Add(new AuthorizationDecision
        {
            RegisteredSystemId = systemId,
            DecisionType = AuthorizationDecisionType.Ato,
            DecisionDate = DateTime.UtcNow.AddDays(-30),
            ExpirationDate = DateTime.UtcNow.AddDays(daysUntilExpiration),
            IssuedByName = "AO Test",
            IsActive = true,
            IssuedBy = "test"
        });

        await db.SaveChangesAsync();
        return systemId;
    }
}
