using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Compliance.Services.KnowledgeBase;

namespace Ato.Copilot.Tests.Unit.Agents;

public class DoDInstructionServiceTests
{
    private readonly DoDInstructionService _service;
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    public DoDInstructionServiceTests()
    {
        var logger = Mock.Of<ILogger<DoDInstructionService>>();
        _service = new DoDInstructionService(_cache, logger);
    }

    [Fact]
    public async Task GetInstructionAsync_Should_Return_Formatted_Text_For_Known_Control()
    {
        var result = await _service.GetInstructionAsync("CA-1");

        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("CA-1");
    }

    [Fact]
    public async Task GetInstructionAsync_Should_Return_Fallback_For_Unknown_Control()
    {
        var result = await _service.GetInstructionAsync("ZZ-99");

        result.Should().Contain("ZZ-99");
        result.Should().Contain("DoD Instruction 8510.01");
    }

    [Fact]
    public async Task ExplainInstructionAsync_Should_Return_Known_Instruction()
    {
        var result = await _service.ExplainInstructionAsync("DoDI 8510.01");

        result.Should().NotBeNull();
        result!.InstructionId.Should().Be("DoDI-8510.01");
        result.Title.Should().Contain("Risk Management Framework");
    }

    [Fact]
    public async Task ExplainInstructionAsync_Should_Handle_Bare_Number()
    {
        var result = await _service.ExplainInstructionAsync("8510.01");

        result.Should().NotBeNull();
        result!.InstructionId.Should().Be("DoDI-8510.01");
    }

    [Fact]
    public async Task ExplainInstructionAsync_Should_Return_Null_For_Unknown()
    {
        var result = await _service.ExplainInstructionAsync("DoDI 9999.99");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetInstructionsByControlAsync_Should_Return_Matching_Instructions()
    {
        var result = await _service.GetInstructionsByControlAsync("CA-1");

        result.Should().NotBeEmpty();
        result.Should().Contain(i => i.InstructionId == "DoDI-8510.01");
    }

    [Fact]
    public async Task GetInstructionsByControlAsync_Should_Return_Empty_For_Unknown()
    {
        var result = await _service.GetInstructionsByControlAsync("ZZ-99");

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("DoDI-8510.01", "DoDI-8510.01")]
    [InlineData("DoDI 8510.01", "DoDI-8510.01")]
    [InlineData("8510.01", "DoDI-8510.01")]
    [InlineData("CNSSI-1253", "CNSSI-1253")]
    [InlineData("CNSSI 1253", "CNSSI-1253")]
    public void NormalizeInstructionId_Should_Handle_Various_Formats(string input, string expected)
    {
        var result = DoDInstructionService.NormalizeInstructionId(input);

        result.Should().Be(expected);
    }

    [Fact]
    public void NormalizeInstructionId_Should_Handle_Empty_String()
    {
        var result = DoDInstructionService.NormalizeInstructionId("");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Loaded_Instructions_Should_Have_Control_Mappings()
    {
        var result = await _service.ExplainInstructionAsync("DoDI 8510.01");

        result.Should().NotBeNull();
        result!.ControlMappings.Should().NotBeEmpty();
    }
}
