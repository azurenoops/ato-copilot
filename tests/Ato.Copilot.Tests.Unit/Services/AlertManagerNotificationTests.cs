using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
/// T235 — Unit tests for AlertManager notification injection (Phase 17 §9a.2).
/// Tests that CreateAlertAsync calls IAlertNotificationService when available,
/// handles notification failure gracefully, and works without notification service.
/// </summary>
public class AlertManagerNotificationTests : IDisposable
{
    private readonly DbContextOptions<AtoCopilotContext> _dbOptions;
    private readonly AlertOptions _alertOptions;

    public AlertManagerNotificationTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase(databaseName: $"AlertMgrNotif_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _alertOptions = new AlertOptions
        {
            CriticalSlaMinutes = 60,
            HighSlaMinutes = 240,
            MediumSlaMinutes = 1440,
            LowSlaMinutes = 10080,
            DefaultPageSize = 50,
            MaxPageSize = 200
        };
    }

    public void Dispose()
    {
        using var db = new AtoCopilotContext(_dbOptions);
        db.Database.EnsureDeleted();
    }

    [Fact]
    public async Task CreateAlertAsync_WithNotificationService_ShouldCallSendNotification()
    {
        // Arrange
        var mockNotification = new Mock<IAlertNotificationService>();
        mockNotification
            .Setup(x => x.SendNotificationAsync(It.IsAny<ComplianceAlert>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var alertManager = CreateAlertManager(notificationService: mockNotification.Object);
        var alert = CreateTestAlert();

        // Act
        var result = await alertManager.CreateAlertAsync(alert);

        // Assert
        result.AlertId.Should().StartWith("ALT-");
        mockNotification.Verify(
            x => x.SendNotificationAsync(It.Is<ComplianceAlert>(a => a.Id == result.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAlertAsync_WithoutNotificationService_ShouldSucceed()
    {
        // Arrange — no notification service injected
        var alertManager = CreateAlertManager(notificationService: null);
        var alert = CreateTestAlert();

        // Act
        var result = await alertManager.CreateAlertAsync(alert);

        // Assert
        result.AlertId.Should().StartWith("ALT-");
        result.Status.Should().Be(AlertStatus.New);
    }

    [Fact]
    public async Task CreateAlertAsync_NotificationFailure_ShouldNotFailAlertCreation()
    {
        // Arrange — notification service throws
        var mockNotification = new Mock<IAlertNotificationService>();
        mockNotification
            .Setup(x => x.SendNotificationAsync(It.IsAny<ComplianceAlert>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Notification channel unavailable"));

        var alertManager = CreateAlertManager(notificationService: mockNotification.Object);
        var alert = CreateTestAlert();

        // Act
        var result = await alertManager.CreateAlertAsync(alert);

        // Assert — alert should still be created despite notification failure
        result.AlertId.Should().StartWith("ALT-");
        result.Status.Should().Be(AlertStatus.New);

        // Verify alert persisted in DB
        await using var db = new AtoCopilotContext(_dbOptions);
        var persisted = await db.ComplianceAlerts.FindAsync(result.Id);
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAlertAsync_ShouldNotifyAfterPersistence()
    {
        // Arrange — notification verifies the alert has an ID (set during creation)
        var mockNotification = new Mock<IAlertNotificationService>();
        mockNotification
            .Setup(x => x.SendNotificationAsync(
                It.Is<ComplianceAlert>(a => a.Id != Guid.Empty && a.AlertId != null),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var alertManager = CreateAlertManager(notificationService: mockNotification.Object);

        // Act
        await alertManager.CreateAlertAsync(CreateTestAlert());

        // Assert — notification received an alert with populated ID
        mockNotification.Verify(
            x => x.SendNotificationAsync(
                It.Is<ComplianceAlert>(a => a.Id != Guid.Empty),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private AlertManager CreateAlertManager(IAlertNotificationService? notificationService)
    {
        return new AlertManager(
            new TestDbContextFactory(_dbOptions),
            Options.Create(_alertOptions),
            Mock.Of<ILogger<AlertManager>>(),
            correlationService: null,
            notificationService: notificationService);
    }

    private static ComplianceAlert CreateTestAlert() => new()
    {
        Type = AlertType.Drift,
        Severity = AlertSeverity.High,
        Title = "Test Drift Alert",
        Description = "Test description",
        SubscriptionId = "sub-test-001"
    };

    private sealed class TestDbContextFactory : IDbContextFactory<AtoCopilotContext>
    {
        private readonly DbContextOptions<AtoCopilotContext> _options;
        public TestDbContextFactory(DbContextOptions<AtoCopilotContext> options) => _options = options;
        public AtoCopilotContext CreateDbContext() => new(_options);
    }
}
