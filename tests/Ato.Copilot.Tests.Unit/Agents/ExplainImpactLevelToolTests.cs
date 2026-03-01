using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ato.Copilot.Agents.KnowledgeBase.Configuration;
using Ato.Copilot.Agents.KnowledgeBase.Tools;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Agents;

public class ExplainImpactLevelToolTests
{
    private readonly ExplainImpactLevelTool _tool;
    private readonly Mock<IImpactLevelService> _serviceMock = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    public ExplainImpactLevelToolTests()
    {
        var options = Options.Create(new KnowledgeBaseAgentOptions());
        var logger = Mock.Of<ILogger<ExplainImpactLevelTool>>();
        _tool = new ExplainImpactLevelTool(_serviceMock.Object, _cache, options, logger);
    }

    // ──────────────── Identity Tests ────────────────

    [Fact]
    public void Tool_Should_Have_Correct_Name()
    {
        _tool.Name.Should().Be("kb_explain_impact_level");
    }

    [Fact]
    public void Tool_Should_Have_Description()
    {
        _tool.Description.Should().NotBeNullOrEmpty();
        _tool.Description.Should().Contain("Impact Level");
    }

    [Fact]
    public void Tool_Should_Have_Level_Parameter()
    {
        _tool.Parameters.Should().ContainKey("level");
        _tool.Parameters["level"].Type.Should().Be("string");
    }

    // ──────────────── Single Level Tests ────────────────

    [Fact]
    public async Task ExecuteCoreAsync_IL5_Should_Return_Formatted_Level()
    {
        var il5 = CreateImpactLevel("IL5", "Impact Level 5");
        _serviceMock.Setup(s => s.GetImpactLevelAsync("IL5", It.IsAny<CancellationToken>()))
            .ReturnsAsync(il5);

        var args = new Dictionary<string, object> { ["level"] = "IL5" };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("IL5");
        result.Should().Contain("Impact Level 5");
        result.Should().Contain("Security Requirements");
        result.Should().Contain("Azure Implementation");
    }

    [Fact]
    public async Task ExecuteCoreAsync_Unknown_Level_Should_Return_Not_Found()
    {
        _serviceMock.Setup(s => s.GetImpactLevelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImpactLevel?)null);

        var args = new Dictionary<string, object> { ["level"] = "IL99" };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("not found");
        result.Should().Contain("IL2");
    }

    // ──────────────── FedRAMP Baseline Tests ────────────────

    [Theory]
    [InlineData("FedRAMP-High")]
    [InlineData("high")]
    [InlineData("moderate")]
    [InlineData("low")]
    public async Task ExecuteCoreAsync_FedRamp_Should_Call_GetFedRampBaselineAsync(string input)
    {
        var baseline = CreateImpactLevel($"FedRAMP-{char.ToUpper(input.Replace("FedRAMP-", "")[0])}{input.Replace("FedRAMP-", "")[1..].ToLower()}", "FedRAMP Baseline");
        _serviceMock.Setup(s => s.GetFedRampBaselineAsync(input, It.IsAny<CancellationToken>()))
            .ReturnsAsync(baseline);

        var args = new Dictionary<string, object> { ["level"] = input };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("FedRAMP");
        _serviceMock.Verify(s => s.GetFedRampBaselineAsync(input, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteCoreAsync_FedRamp_Unknown_Should_Return_Not_Found()
    {
        _serviceMock.Setup(s => s.GetFedRampBaselineAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImpactLevel?)null);

        var args = new Dictionary<string, object> { ["level"] = "FedRAMP-Ultra" };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("not found");
    }

    // ──────────────── Comparison Tests ────────────────

    [Theory]
    [InlineData("compare")]
    [InlineData("all")]
    [InlineData("comparison")]
    public async Task ExecuteCoreAsync_Should_Return_Comparison_Table(string keyword)
    {
        var levels = new List<ImpactLevel>
        {
            CreateImpactLevel("IL2", "Impact Level 2"),
            CreateImpactLevel("IL4", "Impact Level 4"),
            CreateImpactLevel("IL5", "Impact Level 5")
        };
        _serviceMock.Setup(s => s.GetAllImpactLevelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(levels);

        var args = new Dictionary<string, object> { ["level"] = keyword };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("Comparison");
        result.Should().Contain("IL2");
        result.Should().Contain("IL4");
        result.Should().Contain("IL5");
        result.Should().Contain("|"); // Table format
    }

    [Fact]
    public async Task ExecuteCoreAsync_Comparison_Empty_Should_Return_Unavailable()
    {
        _serviceMock.Setup(s => s.GetAllImpactLevelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ImpactLevel>());

        var args = new Dictionary<string, object> { ["level"] = "compare" };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("not currently available");
    }

    // ──────────────── No Level Provided Tests ────────────────

    [Fact]
    public async Task ExecuteCoreAsync_No_Level_Should_Default_To_Comparison()
    {
        var levels = new List<ImpactLevel>
        {
            CreateImpactLevel("IL2", "Impact Level 2"),
            CreateImpactLevel("IL5", "Impact Level 5")
        };
        _serviceMock.Setup(s => s.GetAllImpactLevelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(levels);

        var args = new Dictionary<string, object>(); // No "level" key
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("Comparison");
        _serviceMock.Verify(s => s.GetAllImpactLevelsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────── Caching Tests ────────────────

    [Fact]
    public async Task ExecuteCoreAsync_Should_Cache_Results()
    {
        var il5 = CreateImpactLevel("IL5", "Impact Level 5");
        _serviceMock.Setup(s => s.GetImpactLevelAsync("IL5", It.IsAny<CancellationToken>()))
            .ReturnsAsync(il5);

        var args = new Dictionary<string, object> { ["level"] = "IL5" };

        // First call
        var result1 = await _tool.ExecuteCoreAsync(args);
        // Second call should come from cache
        var result2 = await _tool.ExecuteCoreAsync(args);

        result1.Should().Be(result2);
        // Service called once, second call served from cache
        _serviceMock.Verify(s => s.GetImpactLevelAsync("IL5", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────── Formatting Tests ────────────────

    [Fact]
    public async Task ExecuteCoreAsync_Should_Include_AdditionalControls_When_Present()
    {
        var il5 = new ImpactLevel(
            "IL5", "Impact Level 5", "CUI and NOFORN data",
            new SecurityRequirements("FIPS 140-2", "Isolated", "Secret clearance", "SCIF"),
            new AzureImpactGuidance("Gov", "ExpressRoute", "CAC", "CMK", new List<string> { "AKS" }),
            new List<string> { "SC-28(1)", "AC-2(7)" });
        _serviceMock.Setup(s => s.GetImpactLevelAsync("IL5", It.IsAny<CancellationToken>()))
            .ReturnsAsync(il5);

        var args = new Dictionary<string, object> { ["level"] = "IL5" };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("Additional Required Controls");
        result.Should().Contain("SC-28(1)");
        result.Should().Contain("AC-2(7)");
    }

    // ──────────────── Helper ────────────────

    private static ImpactLevel CreateImpactLevel(string level, string name)
    {
        return new ImpactLevel(
            Level: level,
            Name: name,
            DataClassification: "Test data classification",
            SecurityRequirements: new SecurityRequirements(
                Encryption: "FIPS 140-2 Level 1",
                Network: "Dedicated network",
                Personnel: "Background check",
                PhysicalSecurity: "Standard facility"),
            AzureImplementation: new AzureImpactGuidance(
                Region: "Azure Government",
                Network: "VNet isolation",
                Identity: "Azure AD",
                Encryption: "Platform-managed keys",
                Services: new List<string> { "Azure VM", "Azure SQL" }),
            AdditionalControls: new List<string>());
    }
}
