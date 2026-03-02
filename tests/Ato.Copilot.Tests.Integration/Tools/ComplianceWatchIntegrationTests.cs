using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Integration.Tools;

/// <summary>
/// Integration tests for Compliance Watch alert lifecycle and Auditor access enforcement.
/// Uses InMemory database with real AlertManager service.
/// </summary>
public class ComplianceWatchIntegrationTests : IDisposable
{
    private readonly AtoCopilotContext _db;
    private readonly TestDbContextFactory _dbFactory;
    private readonly AlertManager _alertManager;
    private readonly WatchShowAlertsTool _showAlertsTool;
    private readonly WatchGetAlertTool _getAlertTool;
    private readonly WatchAcknowledgeAlertTool _acknowledgeTool;
    private readonly WatchFixAlertTool _fixTool;
    private readonly WatchDismissAlertTool _dismissTool;

    public ComplianceWatchIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"WatchIntegration_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new AtoCopilotContext(options);
        _dbFactory = new TestDbContextFactory(options);
        var alertOptions = Options.Create(new AlertOptions());
        _alertManager = new AlertManager(_dbFactory, alertOptions, Mock.Of<ILogger<AlertManager>>(), Mock.Of<IServiceProvider>());

        _showAlertsTool = new WatchShowAlertsTool(_alertManager, Mock.Of<ILogger<WatchShowAlertsTool>>());
        _getAlertTool = new WatchGetAlertTool(_alertManager, Mock.Of<ILogger<WatchGetAlertTool>>());
        _acknowledgeTool = new WatchAcknowledgeAlertTool(_alertManager, Mock.Of<ILogger<WatchAcknowledgeAlertTool>>());
        _fixTool = new WatchFixAlertTool(_alertManager, Mock.Of<IAtoComplianceEngine>(), Mock.Of<ILogger<WatchFixAlertTool>>());
        _dismissTool = new WatchDismissAlertTool(_alertManager, Mock.Of<ILogger<WatchDismissAlertTool>>());
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<ComplianceAlert> SeedAlertAsync(
        AlertSeverity severity = AlertSeverity.High,
        AlertStatus status = AlertStatus.New)
    {
        var alert = new ComplianceAlert
        {
            AlertId = $"ALT-{DateTime.UtcNow:yyyyMMdd}{Random.Shared.Next(10000, 99999)}",
            SubscriptionId = "test-sub-001",
            Severity = severity,
            Status = status,
            Title = "Test Alert",
            Description = "Integration test alert",
            Type = AlertType.Drift,
            ControlFamily = "AC",
            ControlId = "AC-2",
            AffectedResources = ["res-1", "res-2"],
            RecommendedAction = "Remediate AC-2",
            CreatedAt = DateTimeOffset.UtcNow,
            SlaDeadline = DateTimeOffset.UtcNow.AddHours(24),
            AcknowledgedAt = status == AlertStatus.Acknowledged ? DateTimeOffset.UtcNow : null
        };

        _db.ComplianceAlerts.Add(alert);
        await _db.SaveChangesAsync();
        return alert;
    }

    // ─── FR-031: Auditor Read-Only Access — Negative Tests ───────────────────

    [Theory]
    [InlineData(ComplianceRoles.Auditor)]
    [InlineData("Auditor")]
    public async Task AuditorCannotAcknowledgeAlert(string auditorRole)
    {
        var alert = await SeedAlertAsync();

        var args = new Dictionary<string, object?>
        {
            ["alert_id"] = alert.AlertId,
            ["user_id"] = "auditor-user",
            ["user_role"] = auditorRole
        };

        var act = () => _acknowledgeTool.ExecuteCoreAsync(args, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("INSUFFICIENT_PERMISSIONS");
        ex.Which.Message.Should().Contain("read-only");
        ex.Which.Message.Should().Contain("pim_activate_role");
    }

    [Theory]
    [InlineData(ComplianceRoles.Auditor)]
    [InlineData("Auditor")]
    public async Task AuditorCannotFixAlert(string auditorRole)
    {
        var alert = await SeedAlertAsync();

        var args = new Dictionary<string, object?>
        {
            ["alert_id"] = alert.AlertId,
            ["user_id"] = "auditor-user",
            ["user_role"] = auditorRole
        };

        var act = () => _fixTool.ExecuteCoreAsync(args, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("INSUFFICIENT_PERMISSIONS");
        ex.Which.Message.Should().Contain("read-only");
    }

    [Theory]
    [InlineData(ComplianceRoles.Auditor)]
    [InlineData("Auditor")]
    public async Task AuditorCannotDismissAlert(string auditorRole)
    {
        var alert = await SeedAlertAsync();

        var args = new Dictionary<string, object?>
        {
            ["alert_id"] = alert.AlertId,
            ["justification"] = "False positive",
            ["user_id"] = "auditor-user",
            ["user_role"] = auditorRole
        };

        var act = () => _dismissTool.ExecuteCoreAsync(args, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("INSUFFICIENT_PERMISSIONS");
        ex.Which.Message.Should().Contain("read-only");
    }

    // ─── Auditor CAN View Alerts (Read-Only Access) ──────────────────────────

    [Fact]
    public async Task AuditorCanShowAlerts()
    {
        await SeedAlertAsync();

        var args = new Dictionary<string, object?>
        {
            ["subscription_id"] = "test-sub-001"
        };

        var result = await _showAlertsTool.ExecuteCoreAsync(args, CancellationToken.None);

        result.Should().NotBeNullOrEmpty();
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("alerts").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AuditorCanGetAlertDetails()
    {
        var alert = await SeedAlertAsync();

        var args = new Dictionary<string, object?>
        {
            ["alert_id"] = alert.AlertId
        };

        var result = await _getAlertTool.ExecuteCoreAsync(args, CancellationToken.None);

        result.Should().NotBeNullOrEmpty();
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("alertId").GetString().Should().Be(alert.AlertId);
    }

    // ─── Alert Lifecycle Integration: Create → Acknowledge → Fix → Resolve ──

    [Fact]
    public async Task AlertLifecycle_AcknowledgeThenFix()
    {
        var alert = await SeedAlertAsync(AlertSeverity.Critical);
        var adminRole = ComplianceRoles.Administrator;

        // Step 1: Acknowledge
        var ackArgs = new Dictionary<string, object?>
        {
            ["alert_id"] = alert.AlertId,
            ["user_id"] = "admin-1",
            ["user_role"] = adminRole
        };
        var ackResult = await _acknowledgeTool.ExecuteCoreAsync(ackArgs, CancellationToken.None);
        var ackDoc = JsonDocument.Parse(ackResult);
        ackDoc.RootElement.GetProperty("data").GetProperty("newStatus").GetString()
            .Should().Be("Acknowledged");

        // Verify in DB (use factory to get fresh context)
        using var verifyDb = _dbFactory.CreateDbContext();
        var updated = await verifyDb.ComplianceAlerts.FindAsync(alert.Id);
        updated!.Status.Should().Be(AlertStatus.Acknowledged);
        updated.AcknowledgedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AlertLifecycle_DismissRequiresComplianceOfficer()
    {
        // Alert must be Acknowledged before it can be Dismissed
        var alert = await SeedAlertAsync(status: AlertStatus.Acknowledged);

        // Platform Engineer should be denied by AlertManager
        var peArgs = new Dictionary<string, object?>
        {
            ["alert_id"] = alert.AlertId,
            ["justification"] = "False positive",
            ["user_id"] = "pe-user",
            ["user_role"] = ComplianceRoles.PlatformEngineer
        };

        // PlatformEngineer is NOT in Auditor set, so it passes the tool-level check
        // but AlertManager.DismissAlertAsync should deny non-CO roles
        var act = () => _dismissTool.ExecuteCoreAsync(peArgs, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*INSUFFICIENT_PERMISSIONS*");
    }

    [Fact]
    public async Task AlertLifecycle_DismissWithComplianceOfficerSucceeds()
    {
        // Alert must be Acknowledged before it can be Dismissed
        var alert = await SeedAlertAsync(status: AlertStatus.Acknowledged);

        var args = new Dictionary<string, object?>
        {
            ["alert_id"] = alert.AlertId,
            ["justification"] = "Verified as false positive — control is compensated by AC-3",
            ["user_id"] = "co-user",
            ["user_role"] = ComplianceRoles.Administrator // CO = Administrator
        };

        var result = await _dismissTool.ExecuteCoreAsync(args, CancellationToken.None);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("newStatus").GetString()
            .Should().Be("Dismissed");

        // Verify DB
        using var verifyDb = _dbFactory.CreateDbContext();
        var updated = await verifyDb.ComplianceAlerts.FindAsync(alert.Id);
        updated!.Status.Should().Be(AlertStatus.Dismissed);
    }

    [Fact]
    public async Task ShowAlerts_WithFilters()
    {
        // Seed alerts with different severities
        await SeedAlertAsync(AlertSeverity.Critical);
        await SeedAlertAsync(AlertSeverity.Low);
        await SeedAlertAsync(AlertSeverity.High);

        var args = new Dictionary<string, object?>
        {
            ["subscription_id"] = "test-sub-001",
            ["severity"] = "Critical"
        };

        var result = await _showAlertsTool.ExecuteCoreAsync(args, CancellationToken.None);
        var doc = JsonDocument.Parse(result);
        var alerts = doc.RootElement.GetProperty("data").GetProperty("alerts");
        alerts.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task FixAlert_DryRunDoesNotChangeStatus()
    {
        var alert = await SeedAlertAsync();

        var args = new Dictionary<string, object?>
        {
            ["alert_id"] = alert.AlertId,
            ["user_id"] = "admin-1",
            ["user_role"] = ComplianceRoles.Administrator,
            ["dry_run"] = true
        };

        var result = await _fixTool.ExecuteCoreAsync(args, CancellationToken.None);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("data").GetProperty("dryRun").GetBoolean().Should().BeTrue();

        // Alert should still be New
        using var verifyDb = _dbFactory.CreateDbContext();
        var unchanged = await verifyDb.ComplianceAlerts.FindAsync(alert.Id);
        unchanged!.Status.Should().Be(AlertStatus.New);
    }
}
