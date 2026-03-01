using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Agents.KnowledgeBase.Agents;
using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.State.Abstractions;

namespace Ato.Copilot.Tests.Unit.Agents;

/// <summary>
/// Tests for KnowledgeBaseAgent and ConfigurationAgent ResponseData/Suggestions (T022f, FR-007b/c/d).
/// </summary>
public class AgentResponseDataTests
{
    // ─── KnowledgeBaseAgent.GetKnowledgeBaseSuggestions Tests ───────────────

    [Theory]
    [InlineData(KnowledgeQueryType.NistControl, "View related controls")]
    [InlineData(KnowledgeQueryType.NistSearch, "View related controls")]
    [InlineData(KnowledgeQueryType.Stig, "View STIG fix guidance")]
    [InlineData(KnowledgeQueryType.StigSearch, "View STIG fix guidance")]
    [InlineData(KnowledgeQueryType.Rmf, "View RMF step details")]
    [InlineData(KnowledgeQueryType.ImpactLevel, "Compare impact levels")]
    [InlineData(KnowledgeQueryType.FedRamp, "Generate SSP document")]
    [InlineData(KnowledgeQueryType.GeneralKnowledge, "Search NIST controls")]
    public void GetKnowledgeBaseSuggestions_ReturnsExpectedSuggestion(
        KnowledgeQueryType queryType, string expectedSuggestion)
    {
        var method = typeof(KnowledgeBaseAgent)
            .GetMethod("GetKnowledgeBaseSuggestions", BindingFlags.NonPublic | BindingFlags.Static)!;

        var suggestions = (List<string>)method.Invoke(null, [queryType])!;

        suggestions.Should().Contain(expectedSuggestion);
        suggestions.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Theory]
    [InlineData(KnowledgeQueryType.NistControl)]
    [InlineData(KnowledgeQueryType.Stig)]
    [InlineData(KnowledgeQueryType.Rmf)]
    [InlineData(KnowledgeQueryType.FedRamp)]
    public void GetKnowledgeBaseSuggestions_AlwaysContainsAssessmentSuggestion(KnowledgeQueryType queryType)
    {
        var method = typeof(KnowledgeBaseAgent)
            .GetMethod("GetKnowledgeBaseSuggestions", BindingFlags.NonPublic | BindingFlags.Static)!;

        var suggestions = (List<string>)method.Invoke(null, [queryType])!;

        suggestions.Should().Contain("Run compliance assessment");
    }

    // ─── AgentResponse Defaults Tests ──────────────────────────────────────

    [Fact]
    public void AgentResponse_NewInstance_HasEmptyDefaults()
    {
        var response = new AgentResponse();

        response.Suggestions.Should().BeEmpty();
        response.RequiresFollowUp.Should().BeFalse();
        response.FollowUpPrompt.Should().BeNull();
        response.MissingFields.Should().BeEmpty();
        response.ResponseData.Should().BeNull();
        response.ToolsExecuted.Should().BeEmpty();
    }

    [Fact]
    public void AgentResponse_WithEnrichment_PreservesAllFields()
    {
        var response = new AgentResponse
        {
            Suggestions = new List<string> { "Do something" },
            RequiresFollowUp = true,
            FollowUpPrompt = "What framework?",
            MissingFields = new List<string> { "framework" },
            ResponseData = new Dictionary<string, object> { ["type"] = "test" }
        };

        response.Suggestions.Should().ContainSingle().Which.Should().Be("Do something");
        response.RequiresFollowUp.Should().BeTrue();
        response.FollowUpPrompt.Should().Be("What framework?");
        response.MissingFields.Should().ContainSingle().Which.Should().Be("framework");
        response.ResponseData!["type"].Should().Be("test");
    }
}
