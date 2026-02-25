using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Compliance.Services.KnowledgeBase;

namespace Ato.Copilot.Tests.Unit.Agents;

public class RmfKnowledgeServiceTests
{
    private readonly RmfKnowledgeService _service;
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    public RmfKnowledgeServiceTests()
    {
        var logger = Mock.Of<ILogger<RmfKnowledgeService>>();
        _service = new RmfKnowledgeService(_cache, logger);
    }

    [Fact]
    public async Task GetGuidanceAsync_Should_Return_Non_Empty_Text()
    {
        var result = await _service.GetGuidanceAsync("AC-1");

        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("AC-1");
    }

    [Fact]
    public async Task GetRmfProcessAsync_Should_Return_Six_Steps()
    {
        var result = await _service.GetRmfProcessAsync();

        result.Should().NotBeNull();
        result!.Steps.Should().HaveCount(6);
        result.Steps[0].Title.Should().Be("Categorize");
        result.Steps[5].Title.Should().Be("Monitor");
    }

    [Fact]
    public async Task GetRmfStepAsync_Should_Return_Specific_Step()
    {
        var result = await _service.GetRmfStepAsync(3);

        result.Should().NotBeNull();
        result!.Step.Should().Be(3);
        result.Title.Should().Be("Implement");
        result.Activities.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetRmfStepAsync_Invalid_Step_Should_Return_Null()
    {
        var result = await _service.GetRmfStepAsync(99);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetServiceGuidanceAsync_Should_Return_Navy_Guidance()
    {
        var result = await _service.GetServiceGuidanceAsync("navy");

        result.Should().NotBeNull();
        result!.Organization.Should().Be("Navy");
        result.Tools.Should().Contain("eMASS");
    }

    [Fact]
    public async Task GetServiceGuidanceAsync_Should_Be_Case_Insensitive()
    {
        var result = await _service.GetServiceGuidanceAsync("NAVY");

        result.Should().NotBeNull();
        result!.Organization.Should().Be("Navy");
    }

    [Fact]
    public async Task GetServiceGuidanceAsync_Unknown_Should_Return_Null()
    {
        var result = await _service.GetServiceGuidanceAsync("spacforce");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRmfProcessAsync_Should_Cache_Results()
    {
        var first = await _service.GetRmfProcessAsync();
        var second = await _service.GetRmfProcessAsync();

        first.Should().BeSameAs(second);
    }

    [Fact]
    public async Task Process_Should_Include_Service_Guidance()
    {
        var result = await _service.GetRmfProcessAsync();

        result.Should().NotBeNull();
        result!.ServiceGuidance.Should().ContainKey("navy");
        result.ServiceGuidance.Should().ContainKey("army");
    }

    [Fact]
    public async Task Process_Should_Include_Deliverables()
    {
        var result = await _service.GetRmfProcessAsync();

        result.Should().NotBeNull();
        result!.DeliverablesOverview.Should().HaveCount(6);
    }
}
