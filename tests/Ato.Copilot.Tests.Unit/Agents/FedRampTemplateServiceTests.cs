using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.KnowledgeBase.Services;

namespace Ato.Copilot.Tests.Unit.Agents;

public class FedRampTemplateServiceTests
{
    private readonly FedRampTemplateService _service;
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    public FedRampTemplateServiceTests()
    {
        var logger = Mock.Of<ILogger<FedRampTemplateService>>();
        _service = new FedRampTemplateService(_cache, logger);
    }

    // ──────────────── GetTemplateGuidanceAsync Tests ────────────────

    [Theory]
    [InlineData("SSP")]
    [InlineData("POAM")]
    [InlineData("CRM")]
    public async Task GetTemplateGuidanceAsync_Should_Return_Known_Templates(string templateType)
    {
        var result = await _service.GetTemplateGuidanceAsync(templateType);

        result.Should().NotBeNull();
        result!.TemplateType.Should().Be(templateType);
        result.Title.Should().NotBeNullOrEmpty();
        result.Description.Should().NotBeNullOrEmpty();
        result.Sections.Should().NotBeEmpty();
        result.RequiredFields.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTemplateGuidanceAsync_SSP_Should_Have_Sections_With_Elements()
    {
        var result = await _service.GetTemplateGuidanceAsync("SSP");

        result.Should().NotBeNull();
        result!.Sections.Should().HaveCountGreaterThanOrEqualTo(3);
        result.Sections.Should().AllSatisfy(s =>
        {
            s.Name.Should().NotBeNullOrEmpty();
            s.Description.Should().NotBeNullOrEmpty();
            s.RequiredElements.Should().NotBeEmpty();
        });
    }

    [Fact]
    public async Task GetTemplateGuidanceAsync_SSP_Should_Have_AzureMappings()
    {
        var result = await _service.GetTemplateGuidanceAsync("SSP");

        result.Should().NotBeNull();
        result!.AzureMappings.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTemplateGuidanceAsync_SSP_Should_Have_AuthorizationChecklist()
    {
        var result = await _service.GetTemplateGuidanceAsync("SSP");

        result.Should().NotBeNull();
        result!.AuthorizationChecklist.Should().NotBeEmpty();
        result.AuthorizationChecklist.Should().Contain(c => c.Required);
    }

    [Fact]
    public async Task GetTemplateGuidanceAsync_POAM_Should_Have_RequiredFields()
    {
        var result = await _service.GetTemplateGuidanceAsync("POAM");

        result.Should().NotBeNull();
        result!.RequiredFields.Should().NotBeEmpty();
        result.RequiredFields.Should().AllSatisfy(f =>
        {
            f.Name.Should().NotBeNullOrEmpty();
            f.Description.Should().NotBeNullOrEmpty();
            f.Example.Should().NotBeNullOrEmpty();
            f.AzureSource.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task GetTemplateGuidanceAsync_Unknown_Template_Should_Return_Null()
    {
        var result = await _service.GetTemplateGuidanceAsync("UNKNOWN");

        result.Should().BeNull();
    }

    // ──────────────── Template Type Normalization Tests ────────────────

    [Theory]
    [InlineData("POA&M", "POAM")]
    [InlineData("poa&m", "POAM")]
    [InlineData("CONMON", "CRM")]
    [InlineData("conmon", "CRM")]
    [InlineData("continuous monitoring", "CRM")]
    [InlineData("ssp", "SSP")]
    public void NormalizeTemplateType_Should_Return_Canonical_Form(string input, string expected)
    {
        var result = FedRampTemplateService.NormalizeTemplateType(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void NormalizeTemplateType_Empty_Should_Return_Empty()
    {
        var result = FedRampTemplateService.NormalizeTemplateType("");

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("POA&M")]
    [InlineData("CONMON")]
    public async Task GetTemplateGuidanceAsync_Should_Normalize_Input(string input)
    {
        var result = await _service.GetTemplateGuidanceAsync(input);

        result.Should().NotBeNull();
    }

    // ──────────────── GetAllTemplatesAsync Tests ────────────────

    [Fact]
    public async Task GetAllTemplatesAsync_Should_Return_Three_Templates()
    {
        var result = await _service.GetAllTemplatesAsync();

        result.Should().HaveCount(3);
        result.Select(t => t.TemplateType).Should().Contain(new[] { "SSP", "POAM", "CRM" });
    }

    [Fact]
    public async Task GetAllTemplatesAsync_Should_Return_Complete_Templates()
    {
        var result = await _service.GetAllTemplatesAsync();

        result.Should().AllSatisfy(t =>
        {
            t.TemplateType.Should().NotBeNullOrEmpty();
            t.Title.Should().NotBeNullOrEmpty();
            t.Description.Should().NotBeNullOrEmpty();
            t.Sections.Should().NotBeEmpty();
        });
    }
}
