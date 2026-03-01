using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.KnowledgeBase.Services;

namespace Ato.Copilot.Tests.Unit.Agents;

public class ImpactLevelServiceTests
{
    private readonly ImpactLevelService _service;
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    public ImpactLevelServiceTests()
    {
        var logger = Mock.Of<ILogger<ImpactLevelService>>();
        _service = new ImpactLevelService(_cache, logger);
    }

    // ──────────────── GetImpactLevelAsync Tests ────────────────

    [Theory]
    [InlineData("IL2")]
    [InlineData("IL4")]
    [InlineData("IL5")]
    [InlineData("IL6")]
    public async Task GetImpactLevelAsync_Should_Return_Known_Levels(string level)
    {
        var result = await _service.GetImpactLevelAsync(level);

        result.Should().NotBeNull();
        result!.Level.Should().Be(level);
        result.Name.Should().NotBeNullOrEmpty();
        result.DataClassification.Should().NotBeNullOrEmpty();
        result.SecurityRequirements.Should().NotBeNull();
        result.AzureImplementation.Should().NotBeNull();
    }

    [Theory]
    [InlineData("IL-5", "IL5")]
    [InlineData("5", "IL5")]
    [InlineData("il5", "IL5")]
    [InlineData("IMPACT LEVEL 5", "IL5")]
    public async Task GetImpactLevelAsync_Should_Normalize_Input(string input, string expectedLevel)
    {
        var result = await _service.GetImpactLevelAsync(input);

        result.Should().NotBeNull();
        result!.Level.Should().Be(expectedLevel);
    }

    [Fact]
    public async Task GetImpactLevelAsync_Unknown_Level_Should_Return_Null()
    {
        var result = await _service.GetImpactLevelAsync("IL9");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetImpactLevelAsync_IL5_Should_Have_SecurityRequirements()
    {
        var result = await _service.GetImpactLevelAsync("IL5");

        result.Should().NotBeNull();
        result!.SecurityRequirements.Encryption.Should().NotBeNullOrEmpty();
        result.SecurityRequirements.Network.Should().NotBeNullOrEmpty();
        result.SecurityRequirements.Personnel.Should().NotBeNullOrEmpty();
        result.SecurityRequirements.PhysicalSecurity.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetImpactLevelAsync_IL5_Should_Have_AzureGuidance()
    {
        var result = await _service.GetImpactLevelAsync("IL5");

        result.Should().NotBeNull();
        result!.AzureImplementation.Region.Should().NotBeNullOrEmpty();
        result.AzureImplementation.Network.Should().NotBeNullOrEmpty();
        result.AzureImplementation.Identity.Should().NotBeNullOrEmpty();
        result.AzureImplementation.Encryption.Should().NotBeNullOrEmpty();
        result.AzureImplementation.Services.Should().NotBeEmpty();
    }

    // ──────────────── GetAllImpactLevelsAsync Tests ────────────────

    [Fact]
    public async Task GetAllImpactLevelsAsync_Should_Return_Four_Levels()
    {
        var result = await _service.GetAllImpactLevelsAsync();

        result.Should().HaveCount(4);
        result.Select(il => il.Level).Should().Contain(new[] { "IL2", "IL4", "IL5", "IL6" });
    }

    [Fact]
    public async Task GetAllImpactLevelsAsync_Should_Not_Include_FedRamp_Baselines()
    {
        var result = await _service.GetAllImpactLevelsAsync();

        result.Should().NotContain(il => il.Level.StartsWith("FedRAMP"));
    }

    // ──────────────── GetFedRampBaselineAsync Tests ────────────────

    [Theory]
    [InlineData("Low")]
    [InlineData("Moderate")]
    [InlineData("High")]
    public async Task GetFedRampBaselineAsync_Should_Return_Known_Baselines(string baseline)
    {
        var result = await _service.GetFedRampBaselineAsync(baseline);

        result.Should().NotBeNull();
        result!.Level.Should().StartWith("FedRAMP-");
        result.Name.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("high", "FedRAMP-High")]
    [InlineData("FedRAMP-High", "FedRAMP-High")]
    [InlineData("FEDRAMP-HIGH", "FedRAMP-High")]
    [InlineData("fedramp high", "FedRAMP-High")]
    public async Task GetFedRampBaselineAsync_Should_Normalize_Input(string input, string expectedLevel)
    {
        var result = await _service.GetFedRampBaselineAsync(input);

        result.Should().NotBeNull();
        result!.Level.Should().Be(expectedLevel);
    }

    [Fact]
    public async Task GetFedRampBaselineAsync_Unknown_Should_Return_Null()
    {
        var result = await _service.GetFedRampBaselineAsync("Critical");

        result.Should().BeNull();
    }

    // ──────────────── NormalizeLevel Tests ────────────────

    [Theory]
    [InlineData("IL5", "IL5")]
    [InlineData("IL-5", "IL5")]
    [InlineData("il5", "IL5")]
    [InlineData("5", "IL5")]
    [InlineData("IMPACT LEVEL 5", "IL5")]
    [InlineData("IL2", "IL2")]
    [InlineData("2", "IL2")]
    public void NormalizeLevel_Should_Return_Canonical_Form(string input, string expected)
    {
        var result = ImpactLevelService.NormalizeLevel(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void NormalizeLevel_Empty_Should_Return_Empty()
    {
        var result = ImpactLevelService.NormalizeLevel("");

        result.Should().BeEmpty();
    }

    // ──────────────── NormalizeBaseline Tests ────────────────

    [Theory]
    [InlineData("High", "FedRAMP-High")]
    [InlineData("FEDRAMP-HIGH", "FedRAMP-High")]
    [InlineData("fedramp high", "FedRAMP-High")]
    [InlineData("Moderate", "FedRAMP-Moderate")]
    [InlineData("low", "FedRAMP-Low")]
    public void NormalizeBaseline_Should_Return_Canonical_Form(string input, string expected)
    {
        var result = ImpactLevelService.NormalizeBaseline(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void NormalizeBaseline_Empty_Should_Return_Empty()
    {
        var result = ImpactLevelService.NormalizeBaseline("");

        result.Should().BeEmpty();
    }
}
