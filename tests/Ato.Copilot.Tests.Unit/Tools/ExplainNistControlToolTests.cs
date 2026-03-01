using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ato.Copilot.Agents.KnowledgeBase.Tools;
using Ato.Copilot.Agents.KnowledgeBase.Configuration;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Tools;

public class ExplainNistControlToolTests
{
    private readonly Mock<INistControlsService> _nistServiceMock = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly ExplainNistControlTool _tool;

    public ExplainNistControlToolTests()
    {
        var options = Options.Create(new KnowledgeBaseAgentOptions { CacheDurationMinutes = 60 });
        _tool = new ExplainNistControlTool(
            _nistServiceMock.Object,
            _cache,
            options,
            Mock.Of<ILogger<ExplainNistControlTool>>());
    }

    [Fact]
    public void Tool_Should_Have_Correct_Name()
    {
        _tool.Name.Should().Be("kb_explain_nist_control");
    }

    [Fact]
    public void Tool_Should_Have_Description()
    {
        _tool.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Tool_Should_Have_ControlId_Parameter()
    {
        _tool.Parameters.Should().ContainKey("control_id");
        _tool.Parameters["control_id"].Required.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteCoreAsync_WithValidControl_ReturnsFullResponse()
    {
        // Arrange
        var control = new NistControl
        {
            Id = "ac-2",
            Family = "AC",
            Title = "Account Management",
            Description = "The organization manages information system accounts.",
            ImpactLevel = "Low",
            AzureImplementation = "Use Azure RBAC and Entra ID for account management.",
            Baselines = ["Low", "Moderate", "High"],
            ControlEnhancements = [new NistControl { Id = "ac-2(1)", Title = "Automated System Account Management", Description = "Automated account management" }]
        };

        _nistServiceMock.Setup(s => s.GetControlAsync("AC-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(control);

        // Act
        var result = await _tool.ExecuteCoreAsync(new Dictionary<string, object?> { ["control_id"] = "AC-2" });

        // Assert
        result.Should().Contain("Account Management");
        result.Should().Contain("AC-2");
        result.Should().Contain("Disclaimer");
    }

    [Fact]
    public async Task ExecuteCoreAsync_WithEnhancement_FallsBackToBaseControl()
    {
        // Arrange — enhancement not found directly, but base control exists
        _nistServiceMock.Setup(s => s.GetControlAsync("AC-2(1)", It.IsAny<CancellationToken>()))
            .ReturnsAsync((NistControl?)null);

        var baseControl = new NistControl
        {
            Id = "ac-2",
            Family = "AC",
            Title = "Account Management",
            Description = "The organization manages information system accounts.",
            ControlEnhancements =
            [
                new NistControl { Id = "ac-2(1)", Title = "Automated System Account Management", Description = "Employ automated mechanisms." }
            ]
        };
        _nistServiceMock.Setup(s => s.GetControlAsync("AC-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseControl);

        // Act
        var result = await _tool.ExecuteCoreAsync(new Dictionary<string, object?> { ["control_id"] = "AC-2(1)" });

        // Assert
        result.Should().Contain("AC-2");
    }

    [Fact]
    public async Task ExecuteCoreAsync_WithNotFound_ReturnsFamilySuggestion()
    {
        // Arrange
        _nistServiceMock.Setup(s => s.GetControlAsync("ZZ-99", It.IsAny<CancellationToken>()))
            .ReturnsAsync((NistControl?)null);

        // Act
        var result = await _tool.ExecuteCoreAsync(new Dictionary<string, object?> { ["control_id"] = "ZZ-99" });

        // Assert
        result.Should().Contain("not found");
    }

    [Fact]
    public async Task ExecuteCoreAsync_WithAcFamily_IncludesAzureGuidance()
    {
        // Arrange
        var control = new NistControl
        {
            Id = "ac-2",
            Family = "AC",
            Title = "Account Management",
            Description = "Manage accounts.",
            AzureImplementation = "Use Entra ID"
        };
        _nistServiceMock.Setup(s => s.GetControlAsync("AC-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(control);

        // Act
        var result = await _tool.ExecuteCoreAsync(new Dictionary<string, object?> { ["control_id"] = "AC-2" });

        // Assert
        result.Should().Contain("Azure");
    }

    [Fact]
    public async Task ExecuteCoreAsync_CachesResult_ReturnsFromCache()
    {
        // Arrange
        var control = new NistControl
        {
            Id = "ac-2",
            Family = "AC",
            Title = "Account Management",
            Description = "Manage accounts."
        };
        _nistServiceMock.Setup(s => s.GetControlAsync("AC-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(control);

        // Act — first call
        var result1 = await _tool.ExecuteCoreAsync(new Dictionary<string, object?> { ["control_id"] = "AC-2" });
        // Act — second call (should be cached)
        var result2 = await _tool.ExecuteCoreAsync(new Dictionary<string, object?> { ["control_id"] = "AC-2" });

        // Assert — same result, service called once
        result1.Should().Be(result2);
        _nistServiceMock.Verify(s => s.GetControlAsync("AC-2", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteCoreAsync_NormalizesToUppercase()
    {
        // Arrange
        var control = new NistControl
        {
            Id = "ac-2",
            Family = "AC",
            Title = "Account Management",
            Description = "Manage accounts."
        };
        _nistServiceMock.Setup(s => s.GetControlAsync("AC-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(control);

        // Act
        var result = await _tool.ExecuteCoreAsync(new Dictionary<string, object?> { ["control_id"] = "ac-2" });

        // Assert
        _nistServiceMock.Verify(s => s.GetControlAsync("AC-2", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteCoreAsync_WithNoControlId_ReturnsError()
    {
        // Act
        var result = await _tool.ExecuteCoreAsync(new Dictionary<string, object?>());

        // Assert
        result.Should().Contain("control_id");
    }

    [Fact]
    public void AgentName_ShouldBeKnowledgeBase()
    {
        _tool.AgentName.Should().Be("knowledgebase");
    }
}
