using System.Text.Json;
using Moq;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Tools;

/// <summary>
/// Unit tests for Continuous Monitoring tools (Feature 015 — Phase 11 / US9).
/// Covers T138 (CreateConMonPlanTool), T139 (GenerateConMonReportTool),
/// T140 (ReportSignificantChangeTool), T141 (TrackAtoExpirationTool),
/// T142 (MultiSystemDashboardTool), plus ReauthorizationWorkflowTool
/// and NotificationDeliveryTool.
/// </summary>
public class ConMonToolTests
{
    private readonly Mock<IConMonService> _serviceMock = new();

    // ═══════════════════════════════════════════════════════════════════════
    // T138 — CreateConMonPlanTool Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateConMonPlan_ValidInput_ReturnsSuccess()
    {
        // Arrange
        var plan = CreateConMonPlan();
        _serviceMock
            .Setup(s => s.CreatePlanAsync(
                "sys-1", "Monthly", It.IsAny<DateTime>(),
                It.IsAny<List<string>?>(), It.IsAny<List<string>?>(),
                "system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var tool = CreateConMonPlanTool();

        // Act
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["assessment_frequency"] = "Monthly",
            ["annual_review_date"] = "2026-06-15"
        });

        // Assert
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("success");
        var data = root.GetProperty("data");
        data.GetProperty("system_id").GetString().Should().Be("sys-1");
        data.GetProperty("assessment_frequency").GetString().Should().Be("Monthly");
        data.GetProperty("annual_review_date").GetString().Should().Be("2026-06-15");
    }

    [Fact]
    public async Task CreateConMonPlan_WithDistributionAndTriggers_ReturnsSuccess()
    {
        // Arrange
        var plan = CreateConMonPlan();
        plan.ReportDistribution = new List<string> { "ISSM", "AO" };
        plan.SignificantChangeTriggers = new List<string> { "New Interconnection", "Major Upgrade" };
        _serviceMock
            .Setup(s => s.CreatePlanAsync(
                "sys-1", "Quarterly", It.IsAny<DateTime>(),
                It.IsAny<List<string>?>(), It.IsAny<List<string>?>(),
                "system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var tool = CreateConMonPlanTool();

        // Act
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["assessment_frequency"] = "Quarterly",
            ["annual_review_date"] = "2026-12-01",
            ["report_distribution"] = JsonSerializer.Deserialize<JsonElement>("[\"ISSM\",\"AO\"]"),
            ["significant_change_triggers"] = JsonSerializer.Deserialize<JsonElement>("[\"New Interconnection\",\"Major Upgrade\"]")
        });

        // Assert
        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("report_distribution").GetArrayLength().Should().Be(2);
        data.GetProperty("significant_change_triggers").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task CreateConMonPlan_MissingSystemId_ReturnsError()
    {
        var tool = CreateConMonPlanTool();

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["assessment_frequency"] = "Monthly",
            ["annual_review_date"] = "2026-06-15"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task CreateConMonPlan_InvalidDate_ReturnsError()
    {
        var tool = CreateConMonPlanTool();

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["assessment_frequency"] = "Monthly",
            ["annual_review_date"] = "not-a-date"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // T139 — GenerateConMonReportTool Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GenerateConMonReport_Monthly_ReturnsSuccess()
    {
        // Arrange
        var report = CreateConMonReport("Monthly", "2026-02");
        _serviceMock
            .Setup(s => s.GenerateReportAsync(
                "sys-1", "Monthly", "2026-02",
                "system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var tool = CreateConMonReportTool();

        // Act
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["report_type"] = "Monthly",
            ["period"] = "2026-02"
        });

        // Assert
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("success");
        var data = root.GetProperty("data");
        data.GetProperty("report_type").GetString().Should().Be("Monthly");
        data.GetProperty("period").GetString().Should().Be("2026-02");
        data.GetProperty("compliance_score").GetDouble().Should().Be(92.5);
    }

    [Fact]
    public async Task GenerateConMonReport_WithBaselineDelta_ReturnsScoreDelta()
    {
        // Arrange
        var report = CreateConMonReport("Quarterly", "2026-Q1");
        report.AuthorizedBaselineScore = 95.0;
        report.ComplianceScore = 90.0;
        _serviceMock
            .Setup(s => s.GenerateReportAsync(
                "sys-1", "Quarterly", "2026-Q1",
                "system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var tool = CreateConMonReportTool();

        // Act
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["report_type"] = "Quarterly",
            ["period"] = "2026-Q1"
        });

        // Assert
        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("score_delta").GetDouble().Should().Be(-5.0);
        data.GetProperty("authorized_baseline_score").GetDouble().Should().Be(95.0);
    }

    [Fact]
    public async Task GenerateConMonReport_Annual_ReturnsSuccess()
    {
        // Arrange
        var report = CreateConMonReport("Annual", "2026");
        _serviceMock
            .Setup(s => s.GenerateReportAsync(
                "sys-1", "Annual", "2026",
                "system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var tool = CreateConMonReportTool();

        // Act
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["report_type"] = "Annual",
            ["period"] = "2026"
        });

        // Assert
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("report_type").GetString().Should().Be("Annual");
    }

    [Fact]
    public async Task GenerateConMonReport_MissingPeriod_ReturnsError()
    {
        var tool = CreateConMonReportTool();

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["report_type"] = "Monthly"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // T140 — ReportSignificantChangeTool Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ReportSignificantChange_ValidInput_ReturnsSuccess()
    {
        // Arrange
        var change = CreateSignificantChange("New Interconnection", true);
        _serviceMock
            .Setup(s => s.ReportChangeAsync(
                "sys-1", "New Interconnection", "Added VPN tunnel to partner org",
                "system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(change);

        var tool = CreateSignificantChangeTool();

        // Act
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["change_type"] = "New Interconnection",
            ["description"] = "Added VPN tunnel to partner org"
        });

        // Assert
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("success");
        var data = root.GetProperty("data");
        data.GetProperty("change_type").GetString().Should().Be("New Interconnection");
        data.GetProperty("requires_reauthorization").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ReportSignificantChange_NoReauthorizationNeeded_ReturnsFalse()
    {
        // Arrange
        var change = CreateSignificantChange("Minor Patch", false);
        _serviceMock
            .Setup(s => s.ReportChangeAsync(
                "sys-1", "Minor Patch", "Applied security patch KB12345",
                "system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(change);

        var tool = CreateSignificantChangeTool();

        // Act
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["change_type"] = "Minor Patch",
            ["description"] = "Applied security patch KB12345"
        });

        // Assert
        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("requires_reauthorization").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ReportSignificantChange_MissingDescription_ReturnsError()
    {
        var tool = CreateSignificantChangeTool();

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["change_type"] = "New Interconnection"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // T141 — TrackAtoExpirationTool Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TrackExpiration_90DayAlert_ReturnsInfo()
    {
        // Arrange
        var status = CreateExpirationStatus("Info", 85, false);
        _serviceMock
            .Setup(s => s.CheckExpirationAsync("sys-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var tool = CreateExpirationTool();

        // Act
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });

        // Assert
        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("alert_level").GetString().Should().Be("Info");
        data.GetProperty("days_until_expiration").GetInt32().Should().Be(85);
        data.GetProperty("is_expired").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task TrackExpiration_60DayAlert_ReturnsWarning()
    {
        var status = CreateExpirationStatus("Warning", 55, false);
        _serviceMock
            .Setup(s => s.CheckExpirationAsync("sys-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var tool = CreateExpirationTool();

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("alert_level").GetString().Should().Be("Warning");
        data.GetProperty("days_until_expiration").GetInt32().Should().Be(55);
    }

    [Fact]
    public async Task TrackExpiration_30DayAlert_ReturnsUrgent()
    {
        var status = CreateExpirationStatus("Urgent", 20, false);
        _serviceMock
            .Setup(s => s.CheckExpirationAsync("sys-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var tool = CreateExpirationTool();

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("alert_level").GetString().Should().Be("Urgent");
        data.GetProperty("days_until_expiration").GetInt32().Should().Be(20);
    }

    [Fact]
    public async Task TrackExpiration_Expired_ReturnsExpired()
    {
        var status = CreateExpirationStatus("Expired", -10, true);
        _serviceMock
            .Setup(s => s.CheckExpirationAsync("sys-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var tool = CreateExpirationTool();

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("alert_level").GetString().Should().Be("Expired");
        data.GetProperty("is_expired").GetBoolean().Should().BeTrue();
        data.GetProperty("days_until_expiration").GetInt32().Should().Be(-10);
    }

    [Fact]
    public async Task TrackExpiration_NoAuthorization_ReturnsNoAlert()
    {
        var status = new ExpirationStatus
        {
            SystemId = "sys-1",
            SystemName = "Test System",
            HasActiveAuthorization = false,
            AlertLevel = "None",
            AlertMessage = "No active authorization for this system.",
            IsExpired = false
        };
        _serviceMock
            .Setup(s => s.CheckExpirationAsync("sys-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var tool = CreateExpirationTool();

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("has_active_authorization").GetBoolean().Should().BeFalse();
        data.GetProperty("alert_level").GetString().Should().Be("None");
    }

    [Fact]
    public async Task TrackExpiration_MissingSystemId_ReturnsError()
    {
        var tool = CreateExpirationTool();

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>());

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // T142 — MultiSystemDashboardTool Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Dashboard_MultipleSystems_ReturnsAll()
    {
        // Arrange
        var dashboard = new DashboardResult
        {
            TotalSystems = 3,
            AuthorizedCount = 2,
            ExpiringCount = 1,
            ExpiredCount = 0,
            Systems = new List<DashboardSystemRow>
            {
                CreateDashboardRow("sys-1", "Alpha", "ALP", "High", "Monitor", "Authorized"),
                CreateDashboardRow("sys-2", "Bravo", "BRV", "Moderate", "Authorize", "Pending"),
                CreateDashboardRow("sys-3", "Charlie", "CHR", "Low", "Assess", "Authorized")
            }
        };
        _serviceMock
            .Setup(s => s.GetDashboardAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);

        var tool = CreateDashboardTool();

        // Act
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>());

        // Assert
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("success");
        var data = root.GetProperty("data");
        data.GetProperty("total_systems").GetInt32().Should().Be(3);
        data.GetProperty("authorized_count").GetInt32().Should().Be(2);
        data.GetProperty("expiring_count").GetInt32().Should().Be(1);
        data.GetProperty("systems").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task Dashboard_IncludeInactive_ShowsAll()
    {
        var dashboard = new DashboardResult
        {
            TotalSystems = 1,
            AuthorizedCount = 0,
            Systems = new List<DashboardSystemRow>
            {
                CreateDashboardRow("sys-1", "Decommissioned", null, "Low", "Prepare", "None")
            }
        };
        _serviceMock
            .Setup(s => s.GetDashboardAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);

        var tool = CreateDashboardTool();

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["active_only"] = "false"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("total_systems").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Dashboard_EmptyPortfolio_ReturnsZeros()
    {
        var dashboard = new DashboardResult
        {
            TotalSystems = 0,
            AuthorizedCount = 0,
            ExpiringCount = 0,
            ExpiredCount = 0,
            Systems = new List<DashboardSystemRow>()
        };
        _serviceMock
            .Setup(s => s.GetDashboardAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);

        var tool = CreateDashboardTool();

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>());

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("total_systems").GetInt32().Should().Be(0);
        data.GetProperty("systems").GetArrayLength().Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ReauthorizationWorkflowTool Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Reauthorization_Triggered_ReturnsTriggersAndInitiation()
    {
        var reauth = new ReauthorizationResult
        {
            SystemId = "sys-1",
            IsTriggered = true,
            Triggers = new List<string> { "ATO expiring in 25 days", "2 unreviewed significant changes require reauthorization" },
            WasInitiated = true,
            PreviousRmfStep = "Monitor",
            NewRmfStep = "Assess",
            UnreviewedChangeCount = 2
        };
        _serviceMock
            .Setup(s => s.CheckReauthorizationAsync("sys-1", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reauth);

        var tool = CreateReauthorizationTool();

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["initiate"] = "true"
        });

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("is_triggered").GetBoolean().Should().BeTrue();
        data.GetProperty("was_initiated").GetBoolean().Should().BeTrue();
        data.GetProperty("previous_rmf_step").GetString().Should().Be("Monitor");
        data.GetProperty("new_rmf_step").GetString().Should().Be("Assess");
        data.GetProperty("triggers").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Reauthorization_NotTriggered_ReturnsClean()
    {
        var reauth = new ReauthorizationResult
        {
            SystemId = "sys-1",
            IsTriggered = false,
            Triggers = new List<string>(),
            WasInitiated = false,
            UnreviewedChangeCount = 0
        };
        _serviceMock
            .Setup(s => s.CheckReauthorizationAsync("sys-1", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reauth);

        var tool = CreateReauthorizationTool();

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("is_triggered").GetBoolean().Should().BeFalse();
        data.GetProperty("triggers").GetArrayLength().Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NotificationDeliveryTool Tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Notification_Expiration_ReturnsDelivered()
    {
        var status = CreateExpirationStatus("Warning", 55, false);
        _serviceMock
            .Setup(s => s.CheckExpirationAsync("sys-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        var tool = CreateNotificationTool();

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["notification_type"] = "expiration"
        });

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("notification_type").GetString().Should().Be("expiration");
        data.GetProperty("delivered").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Notification_InvalidType_ReturnsError()
    {
        var tool = CreateNotificationTool();

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["notification_type"] = "invalid_type"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Factory Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private CreateConMonPlanTool CreateConMonPlanTool() =>
        new(_serviceMock.Object, Mock.Of<ILogger<CreateConMonPlanTool>>());

    private GenerateConMonReportTool CreateConMonReportTool() =>
        new(_serviceMock.Object, Mock.Of<ILogger<GenerateConMonReportTool>>());

    private ReportSignificantChangeTool CreateSignificantChangeTool() =>
        new(_serviceMock.Object, Mock.Of<ILogger<ReportSignificantChangeTool>>());

    private TrackAtoExpirationTool CreateExpirationTool() =>
        new(_serviceMock.Object, Mock.Of<ILogger<TrackAtoExpirationTool>>());

    private MultiSystemDashboardTool CreateDashboardTool() =>
        new(_serviceMock.Object, Mock.Of<ILogger<MultiSystemDashboardTool>>());

    private ReauthorizationWorkflowTool CreateReauthorizationTool() =>
        new(_serviceMock.Object, Mock.Of<ILogger<ReauthorizationWorkflowTool>>());

    private NotificationDeliveryTool CreateNotificationTool() =>
        new(_serviceMock.Object, Mock.Of<ILogger<NotificationDeliveryTool>>());

    // ═══════════════════════════════════════════════════════════════════════
    // Data Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private static ConMonPlan CreateConMonPlan() => new()
    {
        Id = Guid.NewGuid().ToString(),
        RegisteredSystemId = "sys-1",
        AssessmentFrequency = "Monthly",
        AnnualReviewDate = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
        ReportDistribution = new List<string>(),
        SignificantChangeTriggers = new List<string>(),
        CreatedBy = "system",
        CreatedAt = DateTime.UtcNow
    };

    private static ConMonReport CreateConMonReport(string reportType, string period) => new()
    {
        Id = Guid.NewGuid().ToString(),
        ConMonPlanId = Guid.NewGuid().ToString(),
        RegisteredSystemId = "sys-1",
        ReportPeriod = period,
        ReportType = reportType,
        ComplianceScore = 92.5,
        AuthorizedBaselineScore = null,
        NewFindings = 3,
        ResolvedFindings = 5,
        OpenPoamItems = 2,
        OverduePoamItems = 0,
        ReportContent = "# ConMon Report\n\nTest content",
        GeneratedAt = DateTime.UtcNow,
        GeneratedBy = "system"
    };

    private static SignificantChange CreateSignificantChange(string changeType, bool requiresReauth) => new()
    {
        Id = Guid.NewGuid().ToString(),
        RegisteredSystemId = "sys-1",
        ChangeType = changeType,
        Description = "Test change description",
        DetectedAt = DateTime.UtcNow,
        DetectedBy = "system",
        RequiresReauthorization = requiresReauth,
        ReauthorizationTriggered = false
    };

    private static ExpirationStatus CreateExpirationStatus(string alertLevel, int daysUntil, bool expired) => new()
    {
        SystemId = "sys-1",
        SystemName = "Test System",
        HasActiveAuthorization = !expired,
        DecisionType = "Ato",
        DecisionDate = DateTime.UtcNow.AddDays(-365),
        ExpirationDate = DateTime.UtcNow.AddDays(daysUntil),
        DaysUntilExpiration = daysUntil,
        AlertLevel = alertLevel,
        AlertMessage = $"ATO {(expired ? "expired" : $"expires in {daysUntil} days")}.",
        IsExpired = expired
    };

    private static DashboardSystemRow CreateDashboardRow(
        string id, string name, string? acronym, string impact,
        string rmfStep, string authStatus) => new()
    {
        SystemId = id,
        Name = name,
        Acronym = acronym,
        ImpactLevel = impact,
        CurrentRmfStep = rmfStep,
        AuthorizationStatus = authStatus
    };
}
