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

public class ExplainStigToolTests
{
    private readonly Mock<IStigKnowledgeService> _stigServiceMock = new();
    private readonly ExplainStigTool _tool;

    public ExplainStigToolTests()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new KnowledgeBaseAgentOptions());
        var logger = Mock.Of<ILogger<ExplainStigTool>>();
        _tool = new ExplainStigTool(_stigServiceMock.Object, cache, options, logger);
    }

    [Fact]
    public void Tool_Should_Have_Correct_Name()
    {
        _tool.Name.Should().Be("kb_explain_stig");
    }

    [Fact]
    public async Task ExecuteCoreAsync_WithFoundStig_ReturnsSeverityAndCategory()
    {
        var stig = CreateTestStig();
        _stigServiceMock
            .Setup(s => s.GetStigControlAsync("V-12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stig);

        var args = new Dictionary<string, object?> { ["stig_id"] = "V-12345" };
        var result = await _tool.ExecuteAsync(args);

        result.Should().Contain("V-12345");
        result.Should().Contain("CAT I (High)");
        result.Should().Contain("Test STIG Control");
    }

    [Fact]
    public async Task ExecuteCoreAsync_WithFoundStig_ReturnsNistMappings()
    {
        var stig = CreateTestStig();
        _stigServiceMock
            .Setup(s => s.GetStigControlAsync("V-12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stig);

        var args = new Dictionary<string, object?> { ["stig_id"] = "V-12345" };
        var result = await _tool.ExecuteAsync(args);

        result.Should().Contain("AU-2");
        result.Should().Contain("AU-3");
        result.Should().Contain("NIST 800-53 Control Mappings");
    }

    [Fact]
    public async Task ExecuteCoreAsync_WithFoundStig_ReturnsCciRefs()
    {
        var stig = CreateTestStig();
        _stigServiceMock
            .Setup(s => s.GetStigControlAsync("V-12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stig);

        var args = new Dictionary<string, object?> { ["stig_id"] = "V-12345" };
        var result = await _tool.ExecuteAsync(args);

        result.Should().Contain("CCI-000130");
        result.Should().Contain("CCI References");
    }

    [Fact]
    public async Task ExecuteCoreAsync_WithFoundStig_ReturnsAzureGuidance()
    {
        var stig = CreateTestStig();
        _stigServiceMock
            .Setup(s => s.GetStigControlAsync("V-12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stig);

        var args = new Dictionary<string, object?> { ["stig_id"] = "V-12345" };
        var result = await _tool.ExecuteAsync(args);

        result.Should().Contain("Azure Monitor");
        result.Should().Contain("Azure Implementation Guidance");
    }

    [Fact]
    public async Task ExecuteCoreAsync_WithNotFoundStig_ReturnsSuggestion()
    {
        _stigServiceMock
            .Setup(s => s.GetStigControlAsync("V-99999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((StigControl?)null);

        var args = new Dictionary<string, object?> { ["stig_id"] = "V-99999" };
        var result = await _tool.ExecuteAsync(args);

        result.Should().Contain("Not Found");
        result.Should().Contain("kb_search_stigs");
    }

    [Fact]
    public async Task ExecuteCoreAsync_WithCheckFixText_ReturnsCheckAndFix()
    {
        var stig = CreateTestStig();
        _stigServiceMock
            .Setup(s => s.GetStigControlAsync("V-12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stig);

        var args = new Dictionary<string, object?> { ["stig_id"] = "V-12345" };
        var result = await _tool.ExecuteAsync(args);

        result.Should().Contain("Check Procedure");
        result.Should().Contain("Fix / Remediation");
        result.Should().Contain("Verify audit policy");
        result.Should().Contain("Enable audit policy");
    }

    [Fact]
    public async Task ExecuteCoreAsync_NormalizesSvPrefix()
    {
        var stig = CreateTestStig();
        _stigServiceMock
            .Setup(s => s.GetStigControlAsync("V-12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stig);

        var args = new Dictionary<string, object?> { ["stig_id"] = "SV-12345r1" };
        var result = await _tool.ExecuteAsync(args);

        result.Should().Contain("V-12345");
        _stigServiceMock.Verify(s => s.GetStigControlAsync("V-12345", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteCoreAsync_IncludesDisclaimer()
    {
        var stig = CreateTestStig();
        _stigServiceMock
            .Setup(s => s.GetStigControlAsync("V-12345", It.IsAny<CancellationToken>()))
            .ReturnsAsync(stig);

        var args = new Dictionary<string, object?> { ["stig_id"] = "V-12345" };
        var result = await _tool.ExecuteAsync(args);

        result.Should().Contain("Disclaimer");
    }

    private static StigControl CreateTestStig() => new(
        StigId: "V-12345",
        VulnId: "V-12345",
        RuleId: "SV-12345r1_rule",
        Title: "Test STIG Control",
        Description: "Test description",
        Severity: StigSeverity.High,
        Category: "Windows Server",
        StigFamily: "Audit and Accountability",
        NistControls: new List<string> { "AU-2", "AU-3" },
        CciRefs: new List<string> { "CCI-000130" },
        CheckText: "Verify audit policy is enabled",
        FixText: "Enable audit policy",
        AzureImplementation: new Dictionary<string, string>
        {
            ["Service"] = "Azure Monitor"
        },
        ServiceType: "Azure Virtual Machines"
    );
}
