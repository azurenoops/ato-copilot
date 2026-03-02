using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Agents.Compliance.Agents;

namespace Ato.Copilot.Tests.Unit.Agents;

/// <summary>
/// Tests for ComplianceAgent.BuildResponseData and BuildSuggestions (T022e, FR-007a/d).
/// Exercises the private methods via reflection since they depend only on string inputs.
/// </summary>
public class ComplianceAgentResponseDataTests
{
    private readonly ComplianceAgent _agent;
    private readonly MethodInfo _buildResponseData;
    private readonly MethodInfo _buildSuggestions;

    public ComplianceAgentResponseDataTests()
    {
        // Use TestMockFactory to create a properly-constructed ComplianceAgent mock
        _agent = TestMockFactory.CreateComplianceAgentMock().Object;

        _buildResponseData = typeof(ComplianceAgent)
            .GetMethod("BuildResponseData", BindingFlags.NonPublic | BindingFlags.Instance)!;
        _buildSuggestions = typeof(ComplianceAgent)
            .GetMethod("BuildSuggestions", BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    // ─── BuildResponseData Tests ───────────────────────────────────────────

    [Theory]
    [InlineData("assess")]
    [InlineData("scan")]
    [InlineData("audit")]
    public void BuildResponseData_AssessmentActions_ReturnsAssessmentType(string action)
    {
        var json = """{"complianceScore": 0.85, "passedControls": 70, "failedControls": 5}""";

        var result = InvokeBuildResponseData(action, json);

        result.Should().NotBeNull();
        result!["type"].Should().Be("assessment");
        result.Should().ContainKey("complianceScore");
        result.Should().ContainKey("passedControls");
        result.Should().ContainKey("failedControls");
    }

    [Theory]
    [InlineData("finding")]
    [InlineData("control_family")]
    public void BuildResponseData_FindingActions_ReturnsFindingType(string action)
    {
        var json = """{"controlId": "AC-2", "severity": "High", "description": "Access control violation"}""";

        var result = InvokeBuildResponseData(action, json);

        result.Should().NotBeNull();
        result!["type"].Should().Be("finding");
        result.Should().ContainKey("controlId");
    }

    [Theory]
    [InlineData("remediate")]
    [InlineData("remediation_plan")]
    public void BuildResponseData_RemediationActions_ReturnsRemediationPlanType(string action)
    {
        var json = """{"steps": ["Step 1", "Step 2"], "riskReduction": 0.6}""";

        var result = InvokeBuildResponseData(action, json);

        result.Should().NotBeNull();
        result!["type"].Should().Be("remediationPlan");
    }

    [Theory]
    [InlineData("kanban_show")]
    [InlineData("kanban_list")]
    public void BuildResponseData_KanbanActions_ReturnsKanbanType(string action)
    {
        var json = """{"board": "main-board", "columns": ["Todo", "In Progress", "Done"]}""";

        var result = InvokeBuildResponseData(action, json);

        result.Should().NotBeNull();
        result!["type"].Should().Be("kanban");
    }

    [Fact]
    public void BuildResponseData_AlertAction_ReturnsAlertType()
    {
        var json = """{"alerts": [{"id": "a1", "severity": "High"}]}""";

        var result = InvokeBuildResponseData("alert", json);

        result.Should().NotBeNull();
        result!["type"].Should().Be("alert");
    }

    [Fact]
    public void BuildResponseData_TrendAction_ReturnsTrendType()
    {
        var json = """{"history": [{"date": "2024-01-01", "score": 0.8}]}""";

        var result = InvokeBuildResponseData("history", json);

        result.Should().NotBeNull();
        result!["type"].Should().Be("trend");
    }

    [Fact]
    public void BuildResponseData_EvidenceAction_ReturnsEvidenceType()
    {
        var json = """{"evidence": [{"id": "e1", "type": "screenshot"}]}""";

        var result = InvokeBuildResponseData("evidence", json);

        result.Should().NotBeNull();
        result!["type"].Should().Be("evidence");
    }

    [Fact]
    public void BuildResponseData_UnknownAction_ReturnsNull()
    {
        var result = InvokeBuildResponseData("unknown", """{"foo": "bar"}""");
        result.Should().BeNull();
    }

    [Fact]
    public void BuildResponseData_NoJsonProperties_ReturnsNull()
    {
        // Only "type" key → count ≤ 1 → returns null
        var result = InvokeBuildResponseData("assess", "plain text, no JSON");
        result.Should().BeNull();
    }

    // ─── BuildSuggestions Tests ────────────────────────────────────────────

    [Fact]
    public void BuildSuggestions_AssessWithFailures_IncludesRemediationSuggestion()
    {
        var json = """{"failedControls": 3}""";

        var result = InvokeBuildSuggestions("Assessment", json);

        result.Should().Contain(s => s.Title == "Generate Remediation Plan");
        result.Should().Contain(s => s.Title == "View Detailed Findings");
        result.Should().Contain(s => s.Title == "Show Kanban Board");
    }

    [Fact]
    public void BuildSuggestions_AssessWithoutFailures_IncludesExportSuggestion()
    {
        var json = """{"passedControls": 85}""";

        var result = InvokeBuildSuggestions("Assessment", json);

        result.Should().Contain(s => s.Title == "Export Compliance Report");
        result.Should().Contain(s => s.Title == "View Compliance Trend");
    }

    [Fact]
    public void BuildSuggestions_RemediationAction_IncludesAssessmentSuggestion()
    {
        var result = InvokeBuildSuggestions("Remediation", "{}");

        result.Should().Contain(s => s.Title == "Run Assessment");
        result.Should().Contain(s => s.Title == "Show Kanban Board");
    }

    [Fact]
    public void BuildSuggestions_UnknownAction_ReturnsDefaultSuggestions()
    {
        var result = InvokeBuildSuggestions("unknown_action", "{}");

        result.Should().Contain(s => s.Title == "Register a New System");
        result.Should().Contain(s => s.Title == "Show RMF Status");
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private Dictionary<string, object>? InvokeBuildResponseData(string actionType, string toolResult)
        => _buildResponseData.Invoke(_agent, [actionType, toolResult]) as Dictionary<string, object>;

    private List<AgentSuggestedAction> InvokeBuildSuggestions(string actionType, string toolResult)
        => (List<AgentSuggestedAction>)_buildResponseData.DeclaringType!
            .GetMethod("BuildSuggestions", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(_agent, [actionType, toolResult])!;
}
