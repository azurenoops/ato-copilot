using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Compliance.Services.KnowledgeBase;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Agents;

public class DoDWorkflowServiceTests
{
    private readonly DoDWorkflowService _service;
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    public DoDWorkflowServiceTests()
    {
        var logger = Mock.Of<ILogger<DoDWorkflowService>>();
        _service = new DoDWorkflowService(_cache, logger);
    }

    [Fact]
    public async Task GetWorkflowAsync_Should_Return_Steps()
    {
        var result = await _service.GetWorkflowAsync("Moderate");

        result.Should().NotBeEmpty();
        result[0].Should().Contain("Step");
    }

    [Fact]
    public async Task GetWorkflowDetailAsync_Should_Return_Moderate_Navy_Workflow()
    {
        var result = await _service.GetWorkflowDetailAsync("NAVY-ATO-MODERATE");

        result.Should().NotBeNull();
        result!.WorkflowId.Should().Be("NAVY-ATO-MODERATE");
        result.Organization.Should().Be("Navy");
        result.Steps.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetWorkflowDetailAsync_Should_Return_Null_For_Unknown()
    {
        var result = await _service.GetWorkflowDetailAsync("NONEXISTENT-WORKFLOW");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetWorkflowsByOrganizationAsync_Should_Return_Navy_Workflows()
    {
        var result = await _service.GetWorkflowsByOrganizationAsync("Navy");

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(w => w.Organization == "Navy");
    }

    [Fact]
    public async Task GetWorkflowsByOrganizationAsync_Should_Return_Empty_For_Unknown()
    {
        var result = await _service.GetWorkflowsByOrganizationAsync("Marines");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Workflow_Steps_Should_Have_Duration()
    {
        var result = await _service.GetWorkflowDetailAsync("NAVY-ATO-MODERATE");

        result.Should().NotBeNull();
        result!.Steps.Should().OnlyContain(s => s.Duration.Contains("days"));
    }

    [Fact]
    public async Task GetWorkflowsByOrganizationAsync_Case_Insensitive()
    {
        var result = await _service.GetWorkflowsByOrganizationAsync("navy");

        // Case-insensitive comparison
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Multiple_Organizations_Should_Have_Workflows()
    {
        var army = await _service.GetWorkflowsByOrganizationAsync("Army");
        var af = await _service.GetWorkflowsByOrganizationAsync("Air Force");

        army.Should().NotBeEmpty();
        af.Should().NotBeEmpty();
    }
}
