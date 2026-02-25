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

public class SearchNistControlsToolTests
{
    private readonly Mock<INistControlsService> _nistServiceMock = new();
    private readonly SearchNistControlsTool _tool;

    public SearchNistControlsToolTests()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new KnowledgeBaseAgentOptions());
        var logger = Mock.Of<ILogger<SearchNistControlsTool>>();
        _tool = new SearchNistControlsTool(_nistServiceMock.Object, cache, options, logger);
    }

    [Fact]
    public void Tool_Should_Have_Correct_Name()
    {
        _tool.Name.Should().Be("kb_search_nist_controls");
    }

    [Fact]
    public void Tool_Should_Have_Search_Term_Parameter()
    {
        _tool.Parameters.Should().ContainKey("search_term");
        _tool.Parameters["search_term"].Required.Should().BeTrue();
    }

    [Fact]
    public void Tool_Should_Have_Optional_Family_Parameter()
    {
        _tool.Parameters.Should().ContainKey("family");
        _tool.Parameters["family"].Required.Should().BeFalse();
    }

    [Fact]
    public void Tool_Should_Have_Optional_MaxResults_Parameter()
    {
        _tool.Parameters.Should().ContainKey("max_results");
        _tool.Parameters["max_results"].Required.Should().BeFalse();
    }

    [Fact]
    public async Task Search_Should_Return_Matching_Controls()
    {
        var controls = new List<NistControl>
        {
            new() { Id = "SC-8", Family = "SC", Title = "Transmission Confidentiality and Integrity", Description = "Protect transmitted information" },
            new() { Id = "SC-12", Family = "SC", Title = "Cryptographic Key Establishment and Management", Description = "Establish and manage cryptographic keys" },
            new() { Id = "SC-13", Family = "SC", Title = "Cryptographic Protection", Description = "Implement cryptographic mechanisms" }
        };

        _nistServiceMock
            .Setup(s => s.SearchControlsAsync("encryption", null, null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(controls);

        var args = new Dictionary<string, object?> { ["search_term"] = "encryption" };
        var result = await _tool.ExecuteAsync(args);

        result.Should().Contain("SC-8");
        result.Should().Contain("SC-12");
        result.Should().Contain("SC-13");
        result.Should().Contain("Transmission Confidentiality");
        result.Should().Contain("3 results");
    }

    [Fact]
    public async Task Search_Should_Filter_By_Family()
    {
        var controls = new List<NistControl>
        {
            new() { Id = "AC-3", Family = "AC", Title = "Access Enforcement", Description = "Enforce access control policies" }
        };

        _nistServiceMock
            .Setup(s => s.SearchControlsAsync("access", "AC", null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(controls);

        var args = new Dictionary<string, object?>
        {
            ["search_term"] = "access",
            ["family"] = "AC"
        };
        var result = await _tool.ExecuteAsync(args);

        result.Should().Contain("AC-3");
        result.Should().Contain("AC");
    }

    [Fact]
    public async Task Search_Should_Respect_MaxResults_Override()
    {
        var controls = new List<NistControl>
        {
            new() { Id = "AU-2", Family = "AU", Title = "Event Logging", Description = "Audit relevant events" }
        };

        _nistServiceMock
            .Setup(s => s.SearchControlsAsync("audit", null, null, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(controls);

        var args = new Dictionary<string, object?>
        {
            ["search_term"] = "audit",
            ["max_results"] = 5
        };
        var result = await _tool.ExecuteAsync(args);

        result.Should().Contain("AU-2");
        _nistServiceMock.Verify(s => s.SearchControlsAsync("audit", null, null, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Search_Should_Return_No_Results_Message_For_Empty_Results()
    {
        _nistServiceMock
            .Setup(s => s.SearchControlsAsync("nonexistentterm", null, null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NistControl>());

        var args = new Dictionary<string, object?> { ["search_term"] = "nonexistentterm" };
        var result = await _tool.ExecuteAsync(args);

        result.Should().Contain("No controls found");
    }

    [Fact]
    public async Task Search_Should_Default_MaxResults_To_10()
    {
        _nistServiceMock
            .Setup(s => s.SearchControlsAsync("test", null, null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NistControl>());

        var args = new Dictionary<string, object?> { ["search_term"] = "test" };
        await _tool.ExecuteAsync(args);

        _nistServiceMock.Verify(s => s.SearchControlsAsync("test", null, null, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Search_Should_Include_Disclaimer()
    {
        _nistServiceMock
            .Setup(s => s.SearchControlsAsync(It.IsAny<string>(), null, null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NistControl>
            {
                new() { Id = "AC-1", Family = "AC", Title = "Policy and Procedures", Description = "Develop access control policy" }
            });

        var args = new Dictionary<string, object?> { ["search_term"] = "policy" };
        var result = await _tool.ExecuteAsync(args);

        result.Should().Contain("Disclaimer");
    }
}
