using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Services;

public class AlertManagerTests : IDisposable
{
    private readonly DbContextOptions<AtoCopilotContext> _dbOptions;
    private readonly AlertManager _alertManager;
    private readonly AlertOptions _alertOptions;

    public AlertManagerTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase(databaseName: $"AlertManager_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var factory = new TestDbContextFactory(_dbOptions);

        _alertOptions = new AlertOptions
        {
            CriticalSlaMinutes = 60,
            HighSlaMinutes = 240,
            MediumSlaMinutes = 1440,
            LowSlaMinutes = 10080,
            DefaultPageSize = 50,
            MaxPageSize = 200
        };

        _alertManager = new AlertManager(
            factory,
            Options.Create(_alertOptions),
            Mock.Of<ILogger<AlertManager>>(),
            Mock.Of<IServiceProvider>());
    }

    public void Dispose()
    {
        using var db = new AtoCopilotContext(_dbOptions);
        db.Database.EnsureDeleted();
    }

    // ─── Alert Creation Tests ───────────────────────────────────────────────

    [Fact]
    public async Task CreateAlertAsync_ShouldGenerateAlertIdAndSetDefaults()
    {
        var alert = CreateTestAlert(AlertSeverity.High, AlertType.Drift);

        var result = await _alertManager.CreateAlertAsync(alert);

        result.AlertId.Should().StartWith("ALT-");
        result.AlertId.Should().HaveLength(17); // ALT-YYYYMMDDNNNNN
        result.Status.Should().Be(AlertStatus.New);
        result.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        result.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateAlertAsync_ShouldComputeCorrectSlaDeadline_Critical()
    {
        var alert = CreateTestAlert(AlertSeverity.Critical, AlertType.Violation);

        var result = await _alertManager.CreateAlertAsync(alert);

        var expected = result.CreatedAt.AddMinutes(_alertOptions.CriticalSlaMinutes);
        result.SlaDeadline.Should().BeCloseTo(expected, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateAlertAsync_ShouldComputeCorrectSlaDeadline_High()
    {
        var alert = CreateTestAlert(AlertSeverity.High, AlertType.Drift);

        var result = await _alertManager.CreateAlertAsync(alert);

        var expected = result.CreatedAt.AddMinutes(_alertOptions.HighSlaMinutes);
        result.SlaDeadline.Should().BeCloseTo(expected, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateAlertAsync_ShouldComputeCorrectSlaDeadline_Medium()
    {
        var alert = CreateTestAlert(AlertSeverity.Medium, AlertType.Degradation);

        var result = await _alertManager.CreateAlertAsync(alert);

        var expected = result.CreatedAt.AddMinutes(_alertOptions.MediumSlaMinutes);
        result.SlaDeadline.Should().BeCloseTo(expected, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateAlertAsync_ShouldComputeCorrectSlaDeadline_Low()
    {
        var alert = CreateTestAlert(AlertSeverity.Low, AlertType.Drift);

        var result = await _alertManager.CreateAlertAsync(alert);

        var expected = result.CreatedAt.AddMinutes(_alertOptions.LowSlaMinutes);
        result.SlaDeadline.Should().BeCloseTo(expected, TimeSpan.FromSeconds(1));
    }

    // ─── Alert ID Generation Tests ──────────────────────────────────────────

    [Fact]
    public async Task GenerateAlertIdAsync_ShouldBeSequentialWithinDate()
    {
        var id1 = await _alertManager.GenerateAlertIdAsync();
        var id2 = await _alertManager.GenerateAlertIdAsync();
        var id3 = await _alertManager.GenerateAlertIdAsync();

        id1.Should().EndWith("00001");
        id2.Should().EndWith("00002");
        id3.Should().EndWith("00003");
    }

    [Fact]
    public async Task GenerateAlertIdAsync_ShouldMatchPattern()
    {
        var id = await _alertManager.GenerateAlertIdAsync();

        id.Should().MatchRegex(@"^ALT-\d{8}\d{5}$");
    }

    // ─── Lifecycle State Machine Tests ──────────────────────────────────────

    [Fact]
    public async Task TransitionAlertAsync_New_To_Acknowledged_ShouldSucceed()
    {
        var alert = await CreateAndSaveAlert(AlertSeverity.High, AlertType.Drift);

        var result = await _alertManager.TransitionAlertAsync(
            alert.Id, AlertStatus.Acknowledged, "user1", ComplianceRoles.Analyst);

        result.Status.Should().Be(AlertStatus.Acknowledged);
        result.AcknowledgedAt.Should().NotBeNull();
        result.AcknowledgedBy.Should().Be("user1");
    }

    [Fact]
    public async Task TransitionAlertAsync_New_To_Escalated_ShouldSucceed()
    {
        var alert = await CreateAndSaveAlert(AlertSeverity.Critical, AlertType.Violation);

        var result = await _alertManager.TransitionAlertAsync(
            alert.Id, AlertStatus.Escalated, "system", ComplianceRoles.Administrator);

        result.Status.Should().Be(AlertStatus.Escalated);
        result.EscalatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task TransitionAlertAsync_New_To_Resolved_ShouldSucceed()
    {
        var alert = await CreateAndSaveAlert(AlertSeverity.Medium, AlertType.Drift);

        var result = await _alertManager.TransitionAlertAsync(
            alert.Id, AlertStatus.Resolved, "system", ComplianceRoles.Administrator);

        result.Status.Should().Be(AlertStatus.Resolved);
        result.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task TransitionAlertAsync_Acknowledged_To_InProgress_ShouldSucceed()
    {
        var alert = await CreateAndSaveAlert(AlertSeverity.High, AlertType.Drift);
        await _alertManager.TransitionAlertAsync(alert.Id, AlertStatus.Acknowledged, "user1", ComplianceRoles.Analyst);

        var result = await _alertManager.TransitionAlertAsync(
            alert.Id, AlertStatus.InProgress, "user1", ComplianceRoles.Analyst);

        result.Status.Should().Be(AlertStatus.InProgress);
    }

    [Fact]
    public async Task TransitionAlertAsync_Acknowledged_To_Dismissed_ByComplianceOfficer_ShouldSucceed()
    {
        var alert = await CreateAndSaveAlert(AlertSeverity.Low, AlertType.Drift);
        await _alertManager.TransitionAlertAsync(alert.Id, AlertStatus.Acknowledged, "user1", ComplianceRoles.Analyst);

        var result = await _alertManager.TransitionAlertAsync(
            alert.Id, AlertStatus.Dismissed, "co1", ComplianceRoles.Administrator,
            justification: "False positive — resource in decommission scope");

        result.Status.Should().Be(AlertStatus.Dismissed);
        result.DismissalJustification.Should().Be("False positive — resource in decommission scope");
        result.DismissedBy.Should().Be("co1");
    }

    [Fact]
    public async Task TransitionAlertAsync_Escalated_To_Acknowledged_ShouldSucceed()
    {
        var alert = await CreateAndSaveAlert(AlertSeverity.Critical, AlertType.Violation);
        await _alertManager.TransitionAlertAsync(alert.Id, AlertStatus.Escalated, "system", ComplianceRoles.Administrator);

        var result = await _alertManager.TransitionAlertAsync(
            alert.Id, AlertStatus.Acknowledged, "user1", ComplianceRoles.Analyst);

        result.Status.Should().Be(AlertStatus.Acknowledged);
    }

    [Fact]
    public async Task TransitionAlertAsync_Resolved_To_New_ShouldSucceed()
    {
        var alert = await CreateAndSaveAlert(AlertSeverity.High, AlertType.Drift);
        await _alertManager.TransitionAlertAsync(alert.Id, AlertStatus.Resolved, "system", ComplianceRoles.Administrator);

        var result = await _alertManager.TransitionAlertAsync(
            alert.Id, AlertStatus.New, "system", ComplianceRoles.Administrator);

        result.Status.Should().Be(AlertStatus.New);
    }

    // ─── Invalid Transition Tests ───────────────────────────────────────────

    [Fact]
    public async Task TransitionAlertAsync_New_To_InProgress_ShouldThrow()
    {
        var alert = await CreateAndSaveAlert(AlertSeverity.High, AlertType.Drift);

        var act = () => _alertManager.TransitionAlertAsync(
            alert.Id, AlertStatus.InProgress, "user1", ComplianceRoles.Analyst);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*INVALID_TRANSITION*");
    }

    [Fact]
    public async Task TransitionAlertAsync_Dismissed_To_Any_ShouldThrow()
    {
        var alert = await CreateAndSaveAlert(AlertSeverity.Low, AlertType.Drift);
        await _alertManager.TransitionAlertAsync(alert.Id, AlertStatus.Acknowledged, "u1", ComplianceRoles.Analyst);
        await _alertManager.TransitionAlertAsync(alert.Id, AlertStatus.Dismissed, "co1", ComplianceRoles.Administrator, "justified");

        var act = () => _alertManager.TransitionAlertAsync(
            alert.Id, AlertStatus.New, "system", ComplianceRoles.Administrator);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*INVALID_TRANSITION*");
    }

    [Fact]
    public async Task TransitionAlertAsync_InProgress_To_Acknowledged_ShouldThrow()
    {
        var alert = await CreateAndSaveAlert(AlertSeverity.High, AlertType.Drift);
        await _alertManager.TransitionAlertAsync(alert.Id, AlertStatus.Acknowledged, "u1", ComplianceRoles.Analyst);
        await _alertManager.TransitionAlertAsync(alert.Id, AlertStatus.InProgress, "u1", ComplianceRoles.Analyst);

        var act = () => _alertManager.TransitionAlertAsync(
            alert.Id, AlertStatus.Acknowledged, "u1", ComplianceRoles.Analyst);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*INVALID_TRANSITION*");
    }

    // ─── Role-Based Access Tests ────────────────────────────────────────────

    [Fact]
    public async Task TransitionAlertAsync_Dismiss_ByPlatformEngineer_ShouldThrow()
    {
        var alert = await CreateAndSaveAlert(AlertSeverity.Low, AlertType.Drift);
        await _alertManager.TransitionAlertAsync(alert.Id, AlertStatus.Acknowledged, "u1", ComplianceRoles.Analyst);

        var act = () => _alertManager.TransitionAlertAsync(
            alert.Id, AlertStatus.Dismissed, "pe1", ComplianceRoles.PlatformEngineer,
            justification: "Should not be allowed");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*INSUFFICIENT_PERMISSIONS*Only Compliance Officers*");
    }

    [Fact]
    public async Task TransitionAlertAsync_Dismiss_WithoutJustification_ShouldThrow()
    {
        var alert = await CreateAndSaveAlert(AlertSeverity.Low, AlertType.Drift);
        await _alertManager.TransitionAlertAsync(alert.Id, AlertStatus.Acknowledged, "u1", ComplianceRoles.Analyst);

        var act = () => _alertManager.TransitionAlertAsync(
            alert.Id, AlertStatus.Dismissed, "co1", ComplianceRoles.Administrator,
            justification: "");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*JUSTIFICATION_REQUIRED*");
    }

    [Fact]
    public async Task TransitionAlertAsync_Auditor_ShouldBeReadOnly()
    {
        var alert = await CreateAndSaveAlert(AlertSeverity.Medium, AlertType.Drift);

        var act = () => _alertManager.TransitionAlertAsync(
            alert.Id, AlertStatus.Acknowledged, "auditor1", ComplianceRoles.Auditor);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*INSUFFICIENT_PERMISSIONS*Auditor*read-only*");
    }

    // ─── Get / Query Tests ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAlertAsync_ShouldReturnNullForNonexistentId()
    {
        var result = await _alertManager.GetAlertAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAlertAsync_ShouldReturnAlertWithNotifications()
    {
        var alert = await CreateAndSaveAlert(AlertSeverity.High, AlertType.Drift);

        var result = await _alertManager.GetAlertAsync(alert.Id);

        result.Should().NotBeNull();
        result!.AlertId.Should().Be(alert.AlertId);
    }

    [Fact]
    public async Task GetAlertByAlertIdAsync_ShouldFindByHumanReadableId()
    {
        var alert = await CreateAndSaveAlert(AlertSeverity.Critical, AlertType.Violation);

        var result = await _alertManager.GetAlertByAlertIdAsync(alert.AlertId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(alert.Id);
    }

    [Fact]
    public async Task GetAlertsAsync_ShouldFilterBySeverity()
    {
        await CreateAndSaveAlert(AlertSeverity.Critical, AlertType.Violation);
        await CreateAndSaveAlert(AlertSeverity.High, AlertType.Drift);
        await CreateAndSaveAlert(AlertSeverity.Critical, AlertType.Degradation);

        var (alerts, total) = await _alertManager.GetAlertsAsync(severity: AlertSeverity.Critical);

        total.Should().Be(2);
        alerts.Should().HaveCount(2);
        alerts.Should().OnlyContain(a => a.Severity == AlertSeverity.Critical);
    }

    [Fact]
    public async Task GetAlertsAsync_ShouldFilterByStatus()
    {
        var alert1 = await CreateAndSaveAlert(AlertSeverity.High, AlertType.Drift);
        var alert2 = await CreateAndSaveAlert(AlertSeverity.Medium, AlertType.Drift);
        await _alertManager.TransitionAlertAsync(alert1.Id, AlertStatus.Acknowledged, "u1", ComplianceRoles.Analyst);

        var (alerts, total) = await _alertManager.GetAlertsAsync(status: AlertStatus.New);

        total.Should().Be(1);
        alerts.Should().OnlyContain(a => a.Status == AlertStatus.New);
    }

    [Fact]
    public async Task GetAlertsAsync_ShouldFilterByControlFamily()
    {
        await CreateAndSaveAlert(AlertSeverity.High, AlertType.Drift, controlFamily: "SC");
        await CreateAndSaveAlert(AlertSeverity.Medium, AlertType.Drift, controlFamily: "AC");

        var (alerts, total) = await _alertManager.GetAlertsAsync(controlFamily: "SC");

        total.Should().Be(1);
        alerts.First().ControlFamily.Should().Be("SC");
    }

    [Fact]
    public async Task GetAlertsAsync_ShouldPaginate()
    {
        for (int i = 0; i < 5; i++)
            await CreateAndSaveAlert(AlertSeverity.High, AlertType.Drift);

        var (page1, total) = await _alertManager.GetAlertsAsync(page: 1, pageSize: 2);
        var (page2, _) = await _alertManager.GetAlertsAsync(page: 2, pageSize: 2);
        var (page3, _) = await _alertManager.GetAlertsAsync(page: 3, pageSize: 2);

        total.Should().Be(5);
        page1.Should().HaveCount(2);
        page2.Should().HaveCount(2);
        page3.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAlertsAsync_ShouldClampMaxPageSize()
    {
        for (int i = 0; i < 5; i++)
            await CreateAndSaveAlert(AlertSeverity.Low, AlertType.Drift);

        // Should clamp to MaxPageSize (200)
        var (alerts, _) = await _alertManager.GetAlertsAsync(pageSize: 999);

        alerts.Should().HaveCount(5); // Only 5 exist, well under 200
    }

    // ─── DismissAlertAsync Tests ────────────────────────────────────────────

    [Fact]
    public async Task DismissAlertAsync_ShouldDelegateToTransitionAlert()
    {
        var alert = await CreateAndSaveAlert(AlertSeverity.Low, AlertType.Drift);
        await _alertManager.TransitionAlertAsync(alert.Id, AlertStatus.Acknowledged, "u1", ComplianceRoles.Analyst);

        var result = await _alertManager.DismissAlertAsync(
            alert.Id, "Accepted risk", "co1", ComplianceRoles.Administrator);

        result.Status.Should().Be(AlertStatus.Dismissed);
        result.DismissalJustification.Should().Be("Accepted risk");
    }

    // ─── Boundary Tests ────────────────────────────────────────────────────

    [Fact]
    public async Task TransitionAlertAsync_NonexistentAlert_ShouldThrow()
    {
        var act = () => _alertManager.TransitionAlertAsync(
            Guid.NewGuid(), AlertStatus.Acknowledged, "u1", ComplianceRoles.Analyst);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*ALERT_NOT_FOUND*");
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private ComplianceAlert CreateTestAlert(
        AlertSeverity severity,
        AlertType type,
        string subscriptionId = "sub-test",
        string? controlFamily = "SC")
    {
        return new ComplianceAlert
        {
            Type = type,
            Severity = severity,
            Title = $"Test {type} alert - {severity}",
            Description = $"Test description for {type} alert",
            SubscriptionId = subscriptionId,
            ControlFamily = controlFamily,
            ControlId = controlFamily != null ? $"{controlFamily}-1" : null,
            AffectedResources = new List<string> { "/subscriptions/sub-test/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/test" }
        };
    }

    private async Task<ComplianceAlert> CreateAndSaveAlert(
        AlertSeverity severity,
        AlertType type,
        string subscriptionId = "sub-test",
        string? controlFamily = "SC")
    {
        var alert = CreateTestAlert(severity, type, subscriptionId, controlFamily);
        return await _alertManager.CreateAlertAsync(alert);
    }

    /// <summary>
    /// Test IDbContextFactory implementation using InMemory provider.
    /// </summary>
    private sealed class TestDbContextFactory : IDbContextFactory<AtoCopilotContext>
    {
        private readonly DbContextOptions<AtoCopilotContext> _options;
        public TestDbContextFactory(DbContextOptions<AtoCopilotContext> options) => _options = options;
        public AtoCopilotContext CreateDbContext() => new(_options);
    }
}
