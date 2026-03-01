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

namespace Ato.Copilot.Tests.Unit.Tools;

public class SearchStigsToolTests
{
    private readonly Mock<IStigKnowledgeService> _stigServiceMock = new();
    private readonly SearchStigsTool _tool;

    public SearchStigsToolTests()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new KnowledgeBaseAgentOptions());
        var logger = Mock.Of<ILogger<SearchStigsTool>>();
        _tool = new SearchStigsTool(_stigServiceMock.Object, cache, options, logger);
    }

    [Fact]
    public void Tool_Should_Have_Correct_Name()
    {
        _tool.Name.Should().Be("kb_search_stigs");
    }

    [Fact]
    public async Task Search_Should_Return_Matching_Stigs()
    {
        var stigs = new List<StigControl>
        {
            CreateStig("V-12345", "Audit policy enabled", StigSeverity.High),
            CreateStig("V-23456", "Password complexity", StigSeverity.Medium)
        };

        _stigServiceMock
            .Setup(s => s.SearchStigsAsync("security", null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stigs);

        var args = new Dictionary<string, object?> { ["search_term"] = "security" };
        var result = await _tool.ExecuteAsync(args);

        result.Should().Contain("V-12345");
        result.Should().Contain("V-23456");
        result.Should().Contain("2 results");
    }

    [Theory]
    [InlineData("high", StigSeverity.High)]
    [InlineData("High", StigSeverity.High)]
    [InlineData("cat1", StigSeverity.High)]
    [InlineData("CAT1", StigSeverity.High)]
    [InlineData("cati", StigSeverity.High)]
    [InlineData("CATI", StigSeverity.High)]
    [InlineData("cat I", StigSeverity.High)]
    public async Task Search_Should_Normalize_HighSeverity(string input, StigSeverity expected)
    {
        _stigServiceMock
            .Setup(s => s.SearchStigsAsync("test", expected, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StigControl>());

        var args = new Dictionary<string, object?> { ["search_term"] = "test", ["severity"] = input };
        await _tool.ExecuteAsync(args);

        _stigServiceMock.Verify(s => s.SearchStigsAsync("test", expected, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("medium", StigSeverity.Medium)]
    [InlineData("cat2", StigSeverity.Medium)]
    [InlineData("catii", StigSeverity.Medium)]
    [InlineData("cat II", StigSeverity.Medium)]
    public async Task Search_Should_Normalize_MediumSeverity(string input, StigSeverity expected)
    {
        _stigServiceMock
            .Setup(s => s.SearchStigsAsync("test", expected, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StigControl>());

        var args = new Dictionary<string, object?> { ["search_term"] = "test", ["severity"] = input };
        await _tool.ExecuteAsync(args);

        _stigServiceMock.Verify(s => s.SearchStigsAsync("test", expected, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("low", StigSeverity.Low)]
    [InlineData("cat3", StigSeverity.Low)]
    [InlineData("catiii", StigSeverity.Low)]
    [InlineData("cat III", StigSeverity.Low)]
    public async Task Search_Should_Normalize_LowSeverity(string input, StigSeverity expected)
    {
        _stigServiceMock
            .Setup(s => s.SearchStigsAsync("test", expected, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StigControl>());

        var args = new Dictionary<string, object?> { ["search_term"] = "test", ["severity"] = input };
        await _tool.ExecuteAsync(args);

        _stigServiceMock.Verify(s => s.SearchStigsAsync("test", expected, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Search_Should_Default_MaxResults_To_10()
    {
        _stigServiceMock
            .Setup(s => s.SearchStigsAsync("test", null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StigControl>());

        var args = new Dictionary<string, object?> { ["search_term"] = "test" };
        await _tool.ExecuteAsync(args);

        _stigServiceMock.Verify(s => s.SearchStigsAsync("test", null, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Search_Should_Return_NoResults_Message()
    {
        _stigServiceMock
            .Setup(s => s.SearchStigsAsync("nonexistent", null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StigControl>());

        var args = new Dictionary<string, object?> { ["search_term"] = "nonexistent" };
        var result = await _tool.ExecuteAsync(args);

        result.Should().Contain("No STIG findings found");
    }

    [Fact]
    public async Task Search_Should_Include_Disclaimer()
    {
        _stigServiceMock
            .Setup(s => s.SearchStigsAsync(It.IsAny<string>(), null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StigControl>
            {
                CreateStig("V-11111", "Test", StigSeverity.Low)
            });

        var args = new Dictionary<string, object?> { ["search_term"] = "test" };
        var result = await _tool.ExecuteAsync(args);

        result.Should().Contain("Disclaimer");
    }

    private static StigControl CreateStig(string id, string title, StigSeverity severity) => new(
        StigId: id,
        VulnId: id,
        RuleId: $"SV-{id[2..]}r1_rule",
        Title: title,
        Description: "Test description",
        Severity: severity,
        Category: "Test",
        StigFamily: "Test Family",
        NistControls: new List<string> { "AC-2" },
        CciRefs: new List<string> { "CCI-000001" },
        CheckText: "Check test",
        FixText: "Fix test",
        AzureImplementation: new Dictionary<string, string>(),
        ServiceType: "Azure"
    );
}
