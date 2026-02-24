using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Services;

public class AlertCorrelationServiceTests
{
    private readonly AlertCorrelationService _service;

    public AlertCorrelationServiceTests()
    {
        _service = new AlertCorrelationService(
            Mock.Of<ILogger<AlertCorrelationService>>());
    }

    // ─── BuildCorrelationKeys ──────────────────────────────────────────────

    [Fact]
    public void BuildCorrelationKeys_WithResource_ShouldIncludeResourceKey()
    {
        var alert = CreateAlert(resourceId: "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/sa1");

        var keys = AlertCorrelationService.BuildCorrelationKeys(alert);

        keys.Should().Contain(k => k.StartsWith("resource:"));
    }

    [Fact]
    public void BuildCorrelationKeys_WithControlAndSubscription_ShouldIncludeControlKey()
    {
        var alert = CreateAlert(controlId: "SC-8", subscriptionId: "sub-1");

        var keys = AlertCorrelationService.BuildCorrelationKeys(alert);

        keys.Should().Contain("control:SC-8:sub-1");
    }

    [Fact]
    public void BuildCorrelationKeys_WithActor_ShouldIncludeActorKey()
    {
        var alert = CreateAlert(actorId: "user@example.com");

        var keys = AlertCorrelationService.BuildCorrelationKeys(alert);

        keys.Should().Contain("actor:user@example.com");
    }

    [Fact]
    public void BuildCorrelationKeys_NoMatchingFields_ShouldReturnEmpty()
    {
        var alert = new ComplianceAlert
        {
            Id = Guid.NewGuid(),
            AlertId = "ALT-001",
            Type = AlertType.Drift,
            Severity = AlertSeverity.Medium,
            Title = "Test",
            Description = "Test",
            SubscriptionId = ""
        };

        var keys = AlertCorrelationService.BuildCorrelationKeys(alert);

        keys.Should().BeEmpty();
    }

    // ─── Same-Resource Grouping ────────────────────────────────────────────

    [Fact]
    public async Task CorrelateAlert_SameResource_ShouldGroup()
    {
        var resourceId = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/sa1";

        var alert1 = CreateAlert(resourceId: resourceId);
        var result1 = await _service.CorrelateAlertAsync(alert1);

        result1.WasMerged.Should().BeFalse();
        result1.Alert.IsGrouped.Should().BeTrue();

        var alert2 = CreateAlert(resourceId: resourceId);
        var result2 = await _service.CorrelateAlertAsync(alert2);

        result2.WasMerged.Should().BeTrue();
        result2.Alert.ChildAlertCount.Should().Be(1);
        result2.CorrelationKey.Should().StartWith("resource:");
    }

    [Fact]
    public async Task CorrelateAlert_SameResource_ThreeAlerts_ShouldIncrementCount()
    {
        var resourceId = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/sa2";

        await _service.CorrelateAlertAsync(CreateAlert(resourceId: resourceId));
        await _service.CorrelateAlertAsync(CreateAlert(resourceId: resourceId));
        var result = await _service.CorrelateAlertAsync(CreateAlert(resourceId: resourceId));

        result.WasMerged.Should().BeTrue();
        result.Alert.ChildAlertCount.Should().Be(2);
    }

    // ─── Same-Control Grouping ─────────────────────────────────────────────

    [Fact]
    public async Task CorrelateAlert_SameControl_ShouldGroup()
    {
        var alert1 = CreateControlOnlyAlert("AC-2", "sub-ctrl");
        await _service.CorrelateAlertAsync(alert1);

        var alert2 = CreateControlOnlyAlert("AC-2", "sub-ctrl");
        var result = await _service.CorrelateAlertAsync(alert2);

        result.WasMerged.Should().BeTrue();
        result.CorrelationKey.Should().Be("control:AC-2:sub-ctrl");
    }

    [Fact]
    public async Task CorrelateAlert_DifferentControls_ShouldNotGroup()
    {
        await _service.CorrelateAlertAsync(CreateControlOnlyAlert("AC-2", "sub-1"));

        var result = await _service.CorrelateAlertAsync(
            CreateControlOnlyAlert("SC-8", "sub-1"));

        result.WasMerged.Should().BeFalse();
    }

    // ─── Actor Anomaly Detection ───────────────────────────────────────────

    [Fact]
    public async Task CorrelateAlert_ActorAnomaly_At10_ShouldMarkAnomaly()
    {
        var actorId = "suspicious@example.com";

        // First alert creates the window (parent)
        await _service.CorrelateAlertAsync(CreateActorOnlyAlert(actorId));

        // 10 more alerts → 10 children → anomaly threshold met
        for (int i = 0; i < 10; i++)
        {
            await _service.CorrelateAlertAsync(CreateActorOnlyAlert(actorId));
        }

        // Verify the parent alert was marked as anomaly
        var window = await _service.GetCorrelationWindowAsync($"actor:{actorId}");
        window.Should().NotBeNull();
        window!.Count.Should().Be(10);
        window.ParentAlert.Title.Should().Contain("[ANOMALY]");
    }

    [Fact]
    public async Task CorrelateAlert_Actor_BelowThreshold_ShouldNotMarkAnomaly()
    {
        var actorId = "normal@example.com";

        await _service.CorrelateAlertAsync(CreateActorOnlyAlert(actorId));

        // 8 more → 8 children (below 10 threshold)
        for (int i = 0; i < 8; i++)
        {
            await _service.CorrelateAlertAsync(CreateActorOnlyAlert(actorId));
        }

        var window = await _service.GetCorrelationWindowAsync($"actor:{actorId}");
        window.Should().NotBeNull();
        window!.Count.Should().Be(8);
        window.ParentAlert.Title.Should().NotContain("[ANOMALY]");
    }

    // ─── Exactly 10 Actor Events (Boundary) ────────────────────────────────

    [Fact]
    public async Task CorrelateAlert_Exactly10ActorEvents_ShouldTriggerAnomaly()
    {
        var actorId = "boundary@example.com";

        // Creates window (1st alert as parent)
        await _service.CorrelateAlertAsync(CreateActorOnlyAlert(actorId));

        // 9 more → 9 children (below threshold)
        for (int i = 0; i < 9; i++)
        {
            await _service.CorrelateAlertAsync(CreateActorOnlyAlert(actorId));
        }

        var window = await _service.GetCorrelationWindowAsync($"actor:{actorId}");
        window.Should().NotBeNull();
        window!.Count.Should().Be(9);
        window.ParentAlert.Title.Should().NotContain("[ANOMALY]");

        // 10th child triggers anomaly
        await _service.CorrelateAlertAsync(CreateActorOnlyAlert(actorId));

        window = await _service.GetCorrelationWindowAsync($"actor:{actorId}");
        window!.Count.Should().Be(10);
        window.ParentAlert.Title.Should().Contain("[ANOMALY]");
    }

    // ─── Window Expiry and Finalization ────────────────────────────────────

    [Fact]
    public async Task GetCorrelationWindow_ActiveWindow_ShouldReturnWindow()
    {
        var alert = CreateAlert(resourceId: "/sub/rg/res1");
        await _service.CorrelateAlertAsync(alert);

        var window = await _service.GetCorrelationWindowAsync("resource:/sub/rg/res1");

        window.Should().NotBeNull();
        window!.Key.Should().Be("resource:/sub/rg/res1");
    }

    [Fact]
    public async Task GetCorrelationWindow_NoWindow_ShouldReturnNull()
    {
        var window = await _service.GetCorrelationWindowAsync("resource:nonexistent");

        window.Should().BeNull();
    }

    [Fact]
    public async Task FinalizeExpiredWindows_NoExpired_ShouldReturnZero()
    {
        await _service.CorrelateAlertAsync(CreateAlert(resourceId: "/sub/rg/active"));

        var count = await _service.FinalizeExpiredWindowsAsync();

        count.Should().Be(0);
        _service.ActiveWindowCount.Should().Be(1);
    }

    // ─── Alert Storm Detection ─────────────────────────────────────────────

    [Fact]
    public async Task CorrelateAlert_AlertStorm_50Plus_ShouldMarkStorm()
    {
        // Create 50+ alerts from different resources (no correlation grouping)
        for (int i = 0; i < 50; i++)
        {
            await _service.CorrelateAlertAsync(CreateAlert(
                resourceId: $"/sub/rg/unique-resource-{i}"));
        }

        // The 51st alert should reflect storm detection
        var stormAlert = CreateAlert(resourceId: "/sub/rg/storm-resource");
        var result = await _service.CorrelateAlertAsync(stormAlert);

        _service.IsAlertStorm().Should().BeTrue();
    }

    [Fact]
    public async Task CorrelateAlert_BelowStormThreshold_ShouldNotMarkStorm()
    {
        for (int i = 0; i < 10; i++)
        {
            await _service.CorrelateAlertAsync(CreateAlert(
                resourceId: $"/sub/rg/normal-{i}"));
        }

        _service.IsAlertStorm().Should().BeFalse();
    }

    // ─── 5-Minute Window Edge ──────────────────────────────────────────────

    [Fact]
    public void WindowDuration_ShouldBe5Minutes()
    {
        AlertCorrelationService.WindowDuration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void ActorAnomalyThreshold_ShouldBe10()
    {
        AlertCorrelationService.ActorAnomalyThreshold.Should().Be(10);
    }

    [Fact]
    public void AlertStormThreshold_ShouldBe50()
    {
        AlertCorrelationService.AlertStormThreshold.Should().Be(50);
    }

    // ─── Multiple Correlation Keys ─────────────────────────────────────────

    [Fact]
    public async Task CorrelateAlert_ResourceTakesPriority_ShouldMatchResource()
    {
        var resourceId = "/sub/rg/overlapping";
        var controlId = "AC-5";
        var subId = "sub-overlap";

        var alert1 = CreateAlert(resourceId: resourceId, controlId: controlId, subscriptionId: subId);
        await _service.CorrelateAlertAsync(alert1);

        var alert2 = CreateAlert(resourceId: resourceId, controlId: controlId, subscriptionId: subId);
        var result = await _service.CorrelateAlertAsync(alert2);

        result.WasMerged.Should().BeTrue();
        result.CorrelationKey.Should().StartWith("resource:");
    }

    // ─── No Correlation Keys ───────────────────────────────────────────────

    [Fact]
    public async Task CorrelateAlert_NoKeys_ShouldReturnUnchanged()
    {
        var alert = new ComplianceAlert
        {
            Id = Guid.NewGuid(),
            AlertId = "ALT-NOKEY",
            Type = AlertType.Drift,
            Severity = AlertSeverity.Low,
            Title = "No keys",
            Description = "Test",
            SubscriptionId = ""
        };

        var result = await _service.CorrelateAlertAsync(alert);

        result.WasMerged.Should().BeFalse();
        result.CorrelationKey.Should().BeEmpty();
        result.Alert.Should().BeSameAs(alert);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static ComplianceAlert CreateAlert(
        string? resourceId = null,
        string? controlId = null,
        string? subscriptionId = null,
        string? actorId = null)
    {
        var alert = new ComplianceAlert
        {
            Id = Guid.NewGuid(),
            AlertId = $"ALT-{Guid.NewGuid():N}",
            Type = AlertType.Drift,
            Severity = AlertSeverity.Medium,
            Status = AlertStatus.New,
            Title = "Test alert",
            Description = "Test alert description",
            SubscriptionId = subscriptionId ?? "sub-default",
            ControlId = controlId,
            ActorId = actorId,
            AffectedResources = resourceId != null ? new List<string> { resourceId } : new List<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            SlaDeadline = DateTimeOffset.UtcNow.AddHours(4)
        };

        return alert;
    }

    /// <summary>Create an alert with only an actor key (no resource or control).</summary>
    private static ComplianceAlert CreateActorOnlyAlert(string actorId)
    {
        return new ComplianceAlert
        {
            Id = Guid.NewGuid(),
            AlertId = $"ALT-{Guid.NewGuid():N}",
            Type = AlertType.Drift,
            Severity = AlertSeverity.Medium,
            Status = AlertStatus.New,
            Title = "Test alert",
            Description = "Test alert description",
            SubscriptionId = "",
            ActorId = actorId,
            AffectedResources = new List<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            SlaDeadline = DateTimeOffset.UtcNow.AddHours(4)
        };
    }

    /// <summary>Create an alert with only a control key (no resource or actor).</summary>
    private static ComplianceAlert CreateControlOnlyAlert(string controlId, string subscriptionId)
    {
        return new ComplianceAlert
        {
            Id = Guid.NewGuid(),
            AlertId = $"ALT-{Guid.NewGuid():N}",
            Type = AlertType.Drift,
            Severity = AlertSeverity.Medium,
            Status = AlertStatus.New,
            Title = "Test alert",
            Description = "Test alert description",
            SubscriptionId = subscriptionId,
            ControlId = controlId,
            AffectedResources = new List<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            SlaDeadline = DateTimeOffset.UtcNow.AddHours(4)
        };
    }
}
