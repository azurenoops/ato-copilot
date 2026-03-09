// ═══════════════════════════════════════════════════════════════════════════
// Feature 018 — SAP (Security Assessment Plan): MCP Tool Unit Tests
// Tests for GenerateSapTool.
// ═══════════════════════════════════════════════════════════════════════════

using System.Text.Json;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ato.Copilot.Tests.Unit.Tools;

// ─────────────────────────────────────────────────────────────────────────────
// GenerateSapTool Tests
// ─────────────────────────────────────────────────────────────────────────────

public class GenerateSapToolTests
{
    private readonly Mock<ISapService> _sapServiceMock = new();
    private readonly GenerateSapTool _tool;

    public GenerateSapToolTests()
    {
        _tool = new GenerateSapTool(
            _sapServiceMock.Object,
            NullLogger<GenerateSapTool>.Instance);
    }

    // ── Identity ─────────────────────────────────────────────────────────

    [Fact]
    public void Name_ReturnsCorrectToolName()
    {
        _tool.Name.Should().Be("compliance_generate_sap");
    }

    [Fact]
    public void Description_IsNotEmpty()
    {
        _tool.Description.Should().NotBeNullOrWhiteSpace();
        _tool.Description.Should().Contain("Security Assessment Plan");
    }

    [Fact]
    public void Parameters_ContainAllRequiredAndOptionalKeys()
    {
        _tool.Parameters.Should().ContainKey("system_id");
        _tool.Parameters.Should().ContainKey("assessment_id");
        _tool.Parameters.Should().ContainKey("schedule_start");
        _tool.Parameters.Should().ContainKey("schedule_end");
        _tool.Parameters.Should().ContainKey("team_members");
        _tool.Parameters.Should().ContainKey("scope_notes");
        _tool.Parameters.Should().ContainKey("method_overrides");
        _tool.Parameters.Should().ContainKey("rules_of_engagement");
        _tool.Parameters.Should().ContainKey("format");

        _tool.Parameters["system_id"].Required.Should().BeTrue();
        _tool.Parameters["assessment_id"].Required.Should().BeFalse();
        _tool.Parameters["schedule_start"].Required.Should().BeFalse();
        _tool.Parameters["schedule_end"].Required.Should().BeFalse();
        _tool.Parameters["team_members"].Required.Should().BeFalse();
        _tool.Parameters["scope_notes"].Required.Should().BeFalse();
        _tool.Parameters["method_overrides"].Required.Should().BeFalse();
        _tool.Parameters["rules_of_engagement"].Required.Should().BeFalse();
        _tool.Parameters["format"].Required.Should().BeFalse();
    }

    [Fact]
    public void Parameters_Has9Entries()
    {
        _tool.Parameters.Should().HaveCount(9);
    }

    // ── Input Validation ─────────────────────────────────────────────────

    [Fact]
    public async Task Execute_MissingSystemId_ReturnsInvalidInputError()
    {
        var args = new Dictionary<string, object?> { ["system_id"] = "" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
        json.RootElement.GetProperty("message").GetString().Should().Contain("system_id");
    }

    [Fact]
    public async Task Execute_NullSystemId_ReturnsInvalidInputError()
    {
        var args = new Dictionary<string, object?> { ["system_id"] = null };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task Execute_InvalidFormat_ReturnsInvalidFormatError()
    {
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["format"] = "html"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_FORMAT");
        json.RootElement.GetProperty("message").GetString().Should().Contain("html");
    }

    [Fact]
    public async Task Execute_InvalidScheduleStartDate_ReturnsInvalidInputError()
    {
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["schedule_start"] = "not-a-date"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
        json.RootElement.GetProperty("message").GetString().Should().Contain("schedule_start");
    }

    [Fact]
    public async Task Execute_InvalidScheduleEndDate_ReturnsInvalidInputError()
    {
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["schedule_end"] = "bad-date"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
        json.RootElement.GetProperty("message").GetString().Should().Contain("schedule_end");
    }

    [Fact]
    public async Task Execute_MalformedTeamMembersJson_ReturnsInvalidInputError()
    {
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["team_members"] = "not-valid-json"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
        json.RootElement.GetProperty("message").GetString().Should().Contain("team_members");
    }

    [Fact]
    public async Task Execute_MalformedMethodOverridesJson_ReturnsInvalidInputError()
    {
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["method_overrides"] = "{bad json!"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
        json.RootElement.GetProperty("message").GetString().Should().Contain("method_overrides");
    }

    // ── Domain Error Codes ───────────────────────────────────────────────

    [Fact]
    public async Task Execute_SystemNotFound_ReturnsSystemNotFoundError()
    {
        _sapServiceMock
            .Setup(s => s.GenerateSapAsync(
                It.IsAny<SapGenerationInput>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("System 'sys-99' not found."));

        var args = new Dictionary<string, object?> { ["system_id"] = "sys-99" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("SYSTEM_NOT_FOUND");
        json.RootElement.GetProperty("message").GetString().Should().Contain("sys-99");
    }

    [Fact]
    public async Task Execute_BaselineNotFound_ReturnsBaselineNotFoundError()
    {
        _sapServiceMock
            .Setup(s => s.GenerateSapAsync(
                It.IsAny<SapGenerationInput>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No control baseline found for system 'sys-1'. Select a baseline before generating a SAP."));

        var args = new Dictionary<string, object?> { ["system_id"] = "sys-1" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("BASELINE_NOT_FOUND");
    }

    [Fact]
    public async Task Execute_InvalidMethod_ReturnsInvalidMethodError()
    {
        _sapServiceMock
            .Setup(s => s.GenerateSapAsync(
                It.IsAny<SapGenerationInput>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Invalid assessment method 'Scan' for control AC-1."));

        var args = new Dictionary<string, object?> { ["system_id"] = "sys-1" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_METHOD");
    }

    [Fact]
    public async Task Execute_GenericInvalidOperation_ReturnsGenerateSapFailedError()
    {
        _sapServiceMock
            .Setup(s => s.GenerateSapAsync(
                It.IsAny<SapGenerationInput>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Something unexpected happened."));

        var args = new Dictionary<string, object?> { ["system_id"] = "sys-1" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("GENERATE_SAP_FAILED");
    }

    [Fact]
    public async Task Execute_UnexpectedException_ReturnsGenerateSapFailedError()
    {
        _sapServiceMock
            .Setup(s => s.GenerateSapAsync(
                It.IsAny<SapGenerationInput>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Boom"));

        var args = new Dictionary<string, object?> { ["system_id"] = "sys-1" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("GENERATE_SAP_FAILED");
    }

    // ── Success Response ─────────────────────────────────────────────────

    [Fact]
    public async Task Execute_ValidInput_ReturnsSuccessWithSapDocument()
    {
        var sapDocument = new SapDocument
        {
            SapId = "sap-001",
            SystemId = "sys-1",
            AssessmentId = "asmt-1",
            Title = "SAP — Test System — Moderate",
            Status = "Draft",
            Format = "markdown",
            BaselineLevel = "Moderate",
            TotalControls = 25,
            CustomerControls = 20,
            InheritedControls = 4,
            SharedControls = 1,
            StigBenchmarkCount = 3,
            ControlsWithObjectives = 18,
            EvidenceGaps = 2,
            Content = "# Security Assessment Plan\n\nTest content",
            GeneratedAt = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc),
            Warnings = new List<string> { "System is not in Assess phase" },
            FamilySummaries = new List<SapFamilySummary>
            {
                new()
                {
                    Family = "Access Control (AC)",
                    ControlCount = 10,
                    CustomerCount = 8,
                    InheritedCount = 2,
                    Methods = new List<string> { "Examine", "Interview", "Test" }
                }
            }
        };

        _sapServiceMock
            .Setup(s => s.GenerateSapAsync(
                It.Is<SapGenerationInput>(i =>
                    i.SystemId == "sys-1" &&
                    i.AssessmentId == "asmt-1"),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sapDocument);

        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["assessment_id"] = "asmt-1"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        // Top-level status
        json.RootElement.GetProperty("status").GetString().Should().Be("success");

        // Data envelope
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("sap_id").GetString().Should().Be("sap-001");
        data.GetProperty("system_id").GetString().Should().Be("sys-1");
        data.GetProperty("assessment_id").GetString().Should().Be("asmt-1");
        data.GetProperty("title").GetString().Should().Contain("Test System");
        data.GetProperty("status").GetString().Should().Be("Draft");
        data.GetProperty("format").GetString().Should().Be("markdown");
        data.GetProperty("baseline_level").GetString().Should().Be("Moderate");
        data.GetProperty("total_controls").GetInt32().Should().Be(25);
        data.GetProperty("customer_controls").GetInt32().Should().Be(20);
        data.GetProperty("inherited_controls").GetInt32().Should().Be(4);
        data.GetProperty("shared_controls").GetInt32().Should().Be(1);
        data.GetProperty("stig_benchmark_count").GetInt32().Should().Be(3);
        data.GetProperty("controls_with_objectives").GetInt32().Should().Be(18);
        data.GetProperty("evidence_gaps").GetInt32().Should().Be(2);
        data.GetProperty("content").GetString().Should().StartWith("# Security Assessment Plan");
        data.GetProperty("generated_at").GetString().Should().StartWith("2025-01-15");

        // Warnings
        var warnings = data.GetProperty("warnings");
        warnings.GetArrayLength().Should().Be(1);
        warnings[0].GetString().Should().Contain("Assess phase");

        // Family summaries
        var families = data.GetProperty("family_summaries");
        families.GetArrayLength().Should().Be(1);
        var firstFamily = families[0];
        firstFamily.GetProperty("family").GetString().Should().Be("Access Control (AC)");
        firstFamily.GetProperty("control_count").GetInt32().Should().Be(10);
        firstFamily.GetProperty("customer_count").GetInt32().Should().Be(8);
        firstFamily.GetProperty("inherited_count").GetInt32().Should().Be(2);
        firstFamily.GetProperty("methods").GetArrayLength().Should().Be(3);

        // Metadata
        var meta = json.RootElement.GetProperty("metadata");
        meta.GetProperty("tool").GetString().Should().Be("compliance_generate_sap");
        meta.GetProperty("duration_ms").GetInt64().Should().BeGreaterOrEqualTo(0);
        meta.GetProperty("timestamp").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Execute_MinimalArgs_PassesCorrectDefaults()
    {
        var sapDocument = new SapDocument
        {
            SapId = "sap-min",
            SystemId = "sys-1",
            Title = "SAP — Minimal",
            TotalControls = 5,
            Content = "# SAP",
            GeneratedAt = DateTime.UtcNow
        };

        _sapServiceMock
            .Setup(s => s.GenerateSapAsync(
                It.Is<SapGenerationInput>(i =>
                    i.SystemId == "sys-1" &&
                    i.AssessmentId == null &&
                    i.ScheduleStart == null &&
                    i.ScheduleEnd == null &&
                    i.ScopeNotes == null &&
                    i.RulesOfEngagement == null &&
                    i.TeamMembers == null &&
                    i.MethodOverrides == null &&
                    i.Format == "markdown"),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sapDocument);

        var args = new Dictionary<string, object?> { ["system_id"] = "sys-1" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("sap_id").GetString().Should().Be("sap-min");
        json.RootElement.GetProperty("data").GetProperty("format").GetString().Should().Be("markdown");
    }

    [Fact]
    public async Task Execute_WithScheduleDates_ParsesIso8601Correctly()
    {
        var sapDocument = new SapDocument
        {
            SapId = "sap-dates",
            SystemId = "sys-1",
            Content = "# SAP",
            GeneratedAt = DateTime.UtcNow
        };

        _sapServiceMock
            .Setup(s => s.GenerateSapAsync(
                It.Is<SapGenerationInput>(i =>
                    i.ScheduleStart.HasValue &&
                    i.ScheduleStart.Value.Year == 2025 &&
                    i.ScheduleStart.Value.Month == 3 &&
                    i.ScheduleEnd.HasValue &&
                    i.ScheduleEnd.Value.Year == 2025 &&
                    i.ScheduleEnd.Value.Month == 4),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sapDocument);

        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["schedule_start"] = "2025-03-01",
            ["schedule_end"] = "2025-04-30"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task Execute_WithTeamMembersJson_DeserializesCorrectly()
    {
        var sapDocument = new SapDocument
        {
            SapId = "sap-team",
            SystemId = "sys-1",
            Content = "# SAP",
            GeneratedAt = DateTime.UtcNow
        };

        _sapServiceMock
            .Setup(s => s.GenerateSapAsync(
                It.Is<SapGenerationInput>(i =>
                    i.TeamMembers != null &&
                    i.TeamMembers.Count == 1 &&
                    i.TeamMembers[0].Name == "Jane Doe" &&
                    i.TeamMembers[0].Organization == "DISA" &&
                    i.TeamMembers[0].Role == "SCA Lead"),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sapDocument);

        var teamJson = JsonSerializer.Serialize(new[]
        {
            new { Name = "Jane Doe", Organization = "DISA", Role = "SCA Lead", ContactInfo = "jane@disa.mil" }
        });

        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["team_members"] = teamJson
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task Execute_WithMethodOverridesJson_DeserializesCorrectly()
    {
        var sapDocument = new SapDocument
        {
            SapId = "sap-overrides",
            SystemId = "sys-1",
            Content = "# SAP",
            GeneratedAt = DateTime.UtcNow
        };

        _sapServiceMock
            .Setup(s => s.GenerateSapAsync(
                It.Is<SapGenerationInput>(i =>
                    i.MethodOverrides != null &&
                    i.MethodOverrides.Count == 1 &&
                    i.MethodOverrides[0].ControlId == "AC-1" &&
                    i.MethodOverrides[0].Methods.Contains("Examine")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sapDocument);

        var overridesJson = JsonSerializer.Serialize(new[]
        {
            new { ControlId = "AC-1", Methods = new[] { "Examine" }, Rationale = "Document review only" }
        });

        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["method_overrides"] = overridesJson
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Theory]
    [InlineData("markdown")]
    [InlineData("docx")]
    [InlineData("pdf")]
    public async Task Execute_ValidFormats_AreAccepted(string format)
    {
        var sapDocument = new SapDocument
        {
            SapId = $"sap-{format}",
            SystemId = "sys-1",
            Format = format,
            Content = "# SAP",
            GeneratedAt = DateTime.UtcNow
        };

        _sapServiceMock
            .Setup(s => s.GenerateSapAsync(
                It.Is<SapGenerationInput>(i => i.Format == format),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sapDocument);

        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["format"] = format
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Theory]
    [InlineData("html")]
    [InlineData("csv")]
    [InlineData("XML")]
    [InlineData("json")]
    public async Task Execute_InvalidFormats_AreRejected(string format)
    {
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["format"] = format
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_FORMAT");
    }

    // ── RBAC Note ────────────────────────────────────────────────────────
    // RBAC enforcement occurs at the MCP middleware layer
    // (ComplianceAuthorizationMiddleware), not in BaseTool.
    // Role-based access tests covering Viewer, Auditor, ISSO,
    // PlatformEngineer, and AuthorizingOfficial are validated in
    // the middleware/MCP integration tests, not in tool unit tests.
}

// ─────────────────────────────────────────────────────────────────────────────
// T032: UpdateSapTool Tests
// ─────────────────────────────────────────────────────────────────────────────

public class UpdateSapToolTests
{
    private readonly Mock<ISapService> _sapServiceMock = new();
    private readonly UpdateSapTool _tool;

    public UpdateSapToolTests()
    {
        _tool = new UpdateSapTool(
            _sapServiceMock.Object,
            NullLogger<UpdateSapTool>.Instance);
    }

    [Fact]
    public void Name_ReturnsCorrectToolName()
    {
        _tool.Name.Should().Be("compliance_update_sap");
    }

    [Fact]
    public void Parameters_ContainAllKeys()
    {
        _tool.Parameters.Should().ContainKey("sap_id");
        _tool.Parameters.Should().ContainKey("system_id");
        _tool.Parameters.Should().ContainKey("schedule_start");
        _tool.Parameters.Should().ContainKey("schedule_end");
        _tool.Parameters.Should().ContainKey("scope_notes");
        _tool.Parameters.Should().ContainKey("rules_of_engagement");
        _tool.Parameters.Should().ContainKey("team_members");
        _tool.Parameters.Should().ContainKey("method_overrides");

        _tool.Parameters["sap_id"].Required.Should().BeFalse();
        _tool.Parameters["system_id"].Required.Should().BeFalse();
        _tool.Parameters.Should().HaveCount(8);
    }

    [Fact]
    public async Task Execute_MissingSapId_ReturnsInvalidInputError()
    {
        var args = new Dictionary<string, object?> { ["sap_id"] = "" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task Execute_ValidUpdate_ReturnsSuccessEnvelope()
    {
        var sapDocument = new SapDocument
        {
            SapId = "sap-001",
            SystemId = "sys-1",
            Title = "SAP — Test",
            Status = "Draft",
            Content = "# Updated SAP",
            GeneratedAt = DateTime.UtcNow
        };

        _sapServiceMock
            .Setup(s => s.UpdateSapAsync(
                It.Is<SapUpdateInput>(i => i.SapId == "sap-001"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sapDocument);

        var args = new Dictionary<string, object?>
        {
            ["sap_id"] = "sap-001",
            ["scope_notes"] = "Updated scope"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("sap_id").GetString().Should().Be("sap-001");
        data.GetProperty("status").GetString().Should().Be("Draft");
        data.GetProperty("content").GetString().Should().NotBeNullOrWhiteSpace();

        var meta = json.RootElement.GetProperty("metadata");
        meta.GetProperty("tool").GetString().Should().Be("compliance_update_sap");
    }

    [Fact]
    public async Task Execute_SapFinalized_ReturnsSapFinalizedError()
    {
        _sapServiceMock
            .Setup(s => s.UpdateSapAsync(It.IsAny<SapUpdateInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SAP 'sap-001' is finalized and cannot be modified."));

        var args = new Dictionary<string, object?> { ["sap_id"] = "sap-001" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("SAP_FINALIZED");
    }

    [Fact]
    public async Task Execute_SapNotFound_ReturnsSapNotFoundError()
    {
        _sapServiceMock
            .Setup(s => s.UpdateSapAsync(It.IsAny<SapUpdateInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SAP 'sap-999' not found."));

        var args = new Dictionary<string, object?> { ["sap_id"] = "sap-999" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("SAP_NOT_FOUND");
    }

    [Fact]
    public async Task Execute_InvalidMethod_ReturnsInvalidMethodError()
    {
        _sapServiceMock
            .Setup(s => s.UpdateSapAsync(It.IsAny<SapUpdateInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Invalid assessment method 'Scan' for control AC-1."));

        var args = new Dictionary<string, object?>
        {
            ["sap_id"] = "sap-001",
            ["method_overrides"] = "[{\"ControlId\":\"AC-1\",\"Methods\":[\"Scan\"]}]"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_METHOD");
    }

    [Fact]
    public async Task Execute_MalformedTeamMembersJson_ReturnsInvalidInputError()
    {
        var args = new Dictionary<string, object?>
        {
            ["sap_id"] = "sap-001",
            ["team_members"] = "not-json"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// T032: FinalizeSapTool Tests
// ─────────────────────────────────────────────────────────────────────────────

public class FinalizeSapToolTests
{
    private readonly Mock<ISapService> _sapServiceMock = new();
    private readonly FinalizeSapTool _tool;

    public FinalizeSapToolTests()
    {
        _tool = new FinalizeSapTool(
            _sapServiceMock.Object,
            NullLogger<FinalizeSapTool>.Instance);
    }

    [Fact]
    public void Name_ReturnsCorrectToolName()
    {
        _tool.Name.Should().Be("compliance_finalize_sap");
    }

    [Fact]
    public void Parameters_ContainsSapIdAndSystemId()
    {
        _tool.Parameters.Should().ContainKey("sap_id");
        _tool.Parameters.Should().ContainKey("system_id");
        _tool.Parameters["sap_id"].Required.Should().BeFalse();
        _tool.Parameters["system_id"].Required.Should().BeFalse();
        _tool.Parameters.Should().HaveCount(2);
    }

    [Fact]
    public async Task Execute_MissingSapId_ReturnsInvalidInputError()
    {
        var args = new Dictionary<string, object?> { ["sap_id"] = "" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task Execute_ValidFinalize_ReturnsSuccessEnvelope()
    {
        var sapDocument = new SapDocument
        {
            SapId = "sap-001",
            SystemId = "sys-1",
            Title = "SAP — Test — Finalized",
            Status = "Finalized",
            ContentHash = "abc123def456",
            Content = "# SAP",
            TotalControls = 25,
            FinalizedAt = DateTime.UtcNow,
            GeneratedAt = DateTime.UtcNow.AddDays(-1)
        };

        _sapServiceMock
            .Setup(s => s.FinalizeSapAsync("sap-001", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sapDocument);

        var args = new Dictionary<string, object?> { ["sap_id"] = "sap-001" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("sap_id").GetString().Should().Be("sap-001");
        data.GetProperty("status").GetString().Should().Be("Finalized");
        data.GetProperty("content_hash").GetString().Should().Be("abc123def456");
        data.GetProperty("total_controls").GetInt32().Should().Be(25);
        data.GetProperty("title").GetString().Should().Contain("Finalized");

        var meta = json.RootElement.GetProperty("metadata");
        meta.GetProperty("tool").GetString().Should().Be("compliance_finalize_sap");
    }

    [Fact]
    public async Task Execute_AlreadyFinalized_ReturnsSapFinalizedError()
    {
        _sapServiceMock
            .Setup(s => s.FinalizeSapAsync("sap-001", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SAP 'sap-001' is already finalized and cannot be re-finalized."));

        var args = new Dictionary<string, object?> { ["sap_id"] = "sap-001" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("SAP_FINALIZED");
    }

    [Fact]
    public async Task Execute_SapNotFound_ReturnsSapNotFoundError()
    {
        _sapServiceMock
            .Setup(s => s.FinalizeSapAsync("sap-999", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SAP 'sap-999' not found."));

        var args = new Dictionary<string, object?> { ["sap_id"] = "sap-999" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("SAP_NOT_FOUND");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// T046: GetSapTool Tests
// ─────────────────────────────────────────────────────────────────────────────

public class GetSapToolTests
{
    private readonly Mock<ISapService> _sapServiceMock = new();
    private readonly GetSapTool _tool;

    public GetSapToolTests()
    {
        _tool = new GetSapTool(
            _sapServiceMock.Object,
            NullLogger<GetSapTool>.Instance);
    }

    [Fact]
    public void Name_ReturnsCorrectToolName()
    {
        _tool.Name.Should().Be("compliance_get_sap");
    }

    [Fact]
    public void Parameters_ContainCorrectKeys()
    {
        _tool.Parameters.Should().ContainKey("sap_id");
        _tool.Parameters.Should().ContainKey("system_id");
        _tool.Parameters["sap_id"].Required.Should().BeFalse();
        _tool.Parameters["system_id"].Required.Should().BeFalse();
    }

    [Fact]
    public async Task Execute_BySapId_ReturnsSuccessEnvelope()
    {
        var doc = CreateTestSapDocument();
        _sapServiceMock
            .Setup(s => s.GetSapAsync("sap-001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var args = new Dictionary<string, object?> { ["sap_id"] = "sap-001" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("sap_id").GetString().Should().Be("sap-001");
        data.GetProperty("system_id").GetString().Should().Be("sys-001");
        data.GetProperty("content").GetString().Should().NotBeNullOrEmpty();
        json.RootElement.GetProperty("metadata").GetProperty("tool").GetString()
            .Should().Be("compliance_get_sap");
    }

    [Fact]
    public async Task Execute_BySystemId_ReturnsSuccessEnvelope()
    {
        var doc = CreateTestSapDocument();
        _sapServiceMock
            .Setup(s => s.GetSapAsync(null, "sys-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var args = new Dictionary<string, object?> { ["system_id"] = "sys-001" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task Execute_NeitherIdProvided_ReturnsMissingParameterError()
    {
        var args = new Dictionary<string, object?>();

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("MISSING_PARAMETER");
    }

    [Fact]
    public async Task Execute_SapNotFound_ReturnsSapNotFoundError()
    {
        _sapServiceMock
            .Setup(s => s.GetSapAsync("nonexistent", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SAP 'nonexistent' not found."));

        var args = new Dictionary<string, object?> { ["sap_id"] = "nonexistent" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("SAP_NOT_FOUND");
    }

    [Fact]
    public async Task Execute_SystemNotFound_ReturnsSapNotFoundError()
    {
        _sapServiceMock
            .Setup(s => s.GetSapAsync(null, "bad-sys", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SAP not found for system 'bad-sys'."));

        var args = new Dictionary<string, object?> { ["system_id"] = "bad-sys" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("SAP_NOT_FOUND");
    }

    [Fact]
    public async Task Execute_IncludesFamilySummaries()
    {
        var doc = CreateTestSapDocument();
        _sapServiceMock
            .Setup(s => s.GetSapAsync("sap-001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var args = new Dictionary<string, object?> { ["sap_id"] = "sap-001" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        var families = json.RootElement.GetProperty("data").GetProperty("family_summaries");
        families.GetArrayLength().Should().BeGreaterThan(0);
        families[0].GetProperty("family").GetString().Should().Be("Access Control (AC)");
    }

    private static SapDocument CreateTestSapDocument() => new()
    {
        SapId = "sap-001",
        SystemId = "sys-001",
        Title = "Security Assessment Plan — Test System — FY26",
        Status = "Finalized",
        Format = "markdown",
        BaselineLevel = "Moderate",
        Content = "# Security Assessment Plan...",
        ContentHash = "abc123",
        TotalControls = 10,
        CustomerControls = 6,
        InheritedControls = 2,
        SharedControls = 2,
        StigBenchmarkCount = 3,
        ControlsWithObjectives = 9,
        EvidenceGaps = 2,
        FamilySummaries = new List<SapFamilySummary>
        {
            new()
            {
                Family = "Access Control (AC)",
                ControlCount = 5,
                CustomerCount = 3,
                InheritedCount = 1,
                Methods = new List<string>{ "Examine", "Interview", "Test" }
            }
        },
        GeneratedAt = DateTime.UtcNow.AddHours(-1),
        FinalizedAt = DateTime.UtcNow
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// T046: ListSapsTool Tests
// ─────────────────────────────────────────────────────────────────────────────

public class ListSapsToolTests
{
    private readonly Mock<ISapService> _sapServiceMock = new();
    private readonly ListSapsTool _tool;

    public ListSapsToolTests()
    {
        _tool = new ListSapsTool(
            _sapServiceMock.Object,
            NullLogger<ListSapsTool>.Instance);
    }

    [Fact]
    public void Name_ReturnsCorrectToolName()
    {
        _tool.Name.Should().Be("compliance_list_saps");
    }

    [Fact]
    public void Parameters_ContainSystemId()
    {
        _tool.Parameters.Should().ContainKey("system_id");
        _tool.Parameters["system_id"].Required.Should().BeTrue();
    }

    [Fact]
    public async Task Execute_ReturnsSuccessEnvelope_WithSapList()
    {
        var saps = new List<SapDocument>
        {
            new()
            {
                SapId = "sap-001", SystemId = "sys-001", Title = "SAP 1",
                Status = "Finalized", Format = "markdown", BaselineLevel = "Moderate",
                TotalControls = 10, CustomerControls = 6, InheritedControls = 2, SharedControls = 2,
                GeneratedAt = DateTime.UtcNow, FinalizedAt = DateTime.UtcNow,
                FamilySummaries = new List<SapFamilySummary>()
            },
            new()
            {
                SapId = "sap-002", SystemId = "sys-001", Title = "SAP 2",
                Status = "Draft", Format = "markdown", BaselineLevel = "Moderate",
                TotalControls = 10, CustomerControls = 6, InheritedControls = 2, SharedControls = 2,
                GeneratedAt = DateTime.UtcNow,
                FamilySummaries = new List<SapFamilySummary>()
            }
        };
        _sapServiceMock
            .Setup(s => s.ListSapsAsync("sys-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(saps);

        var args = new Dictionary<string, object?> { ["system_id"] = "sys-001" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("system_id").GetString().Should().Be("sys-001");
        data.GetProperty("sap_count").GetInt32().Should().Be(2);
        data.GetProperty("saps").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Execute_EmptyList_ReturnsZeroCount()
    {
        _sapServiceMock
            .Setup(s => s.ListSapsAsync("sys-empty", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SapDocument>());

        var args = new Dictionary<string, object?> { ["system_id"] = "sys-empty" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("sap_count").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Execute_MissingSystemId_ReturnsInvalidInputError()
    {
        var args = new Dictionary<string, object?>();

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task Execute_IncludesMetadata()
    {
        _sapServiceMock
            .Setup(s => s.ListSapsAsync("sys-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SapDocument>());

        var args = new Dictionary<string, object?> { ["system_id"] = "sys-001" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("metadata").GetProperty("tool").GetString()
            .Should().Be("compliance_list_saps");
        json.RootElement.GetProperty("metadata").GetProperty("duration_ms").ValueKind
            .Should().Be(JsonValueKind.Number);
    }
}
