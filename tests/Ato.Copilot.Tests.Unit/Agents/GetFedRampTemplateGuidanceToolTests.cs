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

public class GetFedRampTemplateGuidanceToolTests
{
    private readonly GetFedRampTemplateGuidanceTool _tool;
    private readonly Mock<IFedRampTemplateService> _serviceMock = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    public GetFedRampTemplateGuidanceToolTests()
    {
        var options = Options.Create(new KnowledgeBaseAgentOptions());
        var logger = Mock.Of<ILogger<GetFedRampTemplateGuidanceTool>>();
        _tool = new GetFedRampTemplateGuidanceTool(_serviceMock.Object, _cache, options, logger);
    }

    // ──────────────── Identity Tests ────────────────

    [Fact]
    public void Tool_Should_Have_Correct_Name()
    {
        _tool.Name.Should().Be("kb_get_fedramp_template_guidance");
    }

    [Fact]
    public void Tool_Should_Have_Description()
    {
        _tool.Description.Should().NotBeNullOrEmpty();
        _tool.Description.Should().Contain("FedRAMP");
    }

    [Fact]
    public void Tool_Should_Have_TemplateType_And_Baseline_Parameters()
    {
        _tool.Parameters.Should().ContainKey("template_type");
        _tool.Parameters.Should().ContainKey("baseline");
        _tool.Parameters["template_type"].Type.Should().Be("string");
        _tool.Parameters["baseline"].Type.Should().Be("string");
    }

    // ──────────────── SSP Template Tests ────────────────

    [Fact]
    public async Task ExecuteCoreAsync_SSP_Should_Return_Formatted_Template()
    {
        var ssp = CreateTemplate("SSP", "System Security Plan");
        _serviceMock.Setup(s => s.GetTemplateGuidanceAsync("SSP", "High", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ssp);

        var args = new Dictionary<string, object> { ["template_type"] = "SSP" };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("SSP");
        result.Should().Contain("System Security Plan");
        result.Should().Contain("Required Sections");
        result.Should().Contain("Required Fields");
    }

    [Fact]
    public async Task ExecuteCoreAsync_SSP_Should_Include_Azure_Mappings()
    {
        var ssp = CreateTemplate("SSP", "System Security Plan");
        _serviceMock.Setup(s => s.GetTemplateGuidanceAsync("SSP", "High", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ssp);

        var args = new Dictionary<string, object> { ["template_type"] = "SSP" };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("Azure Integration Mappings");
    }

    // ──────────────── POAM Template Tests ────────────────

    [Fact]
    public async Task ExecuteCoreAsync_POAM_Should_Return_Fields_With_Examples()
    {
        var poam = CreateTemplate("POAM", "Plan of Action and Milestones");
        _serviceMock.Setup(s => s.GetTemplateGuidanceAsync("POAM", "High", It.IsAny<CancellationToken>()))
            .ReturnsAsync(poam);

        var args = new Dictionary<string, object> { ["template_type"] = "POAM" };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("POAM");
        result.Should().Contain("Example");
        result.Should().Contain("Azure Source");
    }

    // ──────────────── CRM Template Tests ────────────────

    [Fact]
    public async Task ExecuteCoreAsync_CRM_Should_Return_Template()
    {
        var crm = CreateTemplate("CRM", "Continuous Monitoring");
        _serviceMock.Setup(s => s.GetTemplateGuidanceAsync("CRM", "High", It.IsAny<CancellationToken>()))
            .ReturnsAsync(crm);

        var args = new Dictionary<string, object> { ["template_type"] = "CRM" };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("CRM");
        result.Should().Contain("Continuous Monitoring");
    }

    // ──────────────── Package Overview Tests ────────────────

    [Fact]
    public async Task ExecuteCoreAsync_No_Type_Should_Return_Package_Overview()
    {
        var templates = new List<FedRampTemplate>
        {
            CreateTemplate("SSP", "System Security Plan"),
            CreateTemplate("POAM", "Plan of Action and Milestones"),
            CreateTemplate("CRM", "Continuous Monitoring")
        };
        _serviceMock.Setup(s => s.GetAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        var args = new Dictionary<string, object>(); // No template_type
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("Package Overview");
        result.Should().Contain("SSP");
        result.Should().Contain("POAM");
        result.Should().Contain("CRM");
        result.Should().Contain("|"); // Table format
    }

    [Fact]
    public async Task ExecuteCoreAsync_Empty_Templates_Should_Return_Unavailable()
    {
        _serviceMock.Setup(s => s.GetAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FedRampTemplate>());

        var args = new Dictionary<string, object>();
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("not currently available");
    }

    // ──────────────── Template Not Found Tests ────────────────

    [Fact]
    public async Task ExecuteCoreAsync_Unknown_Template_Should_Return_Not_Found()
    {
        _serviceMock.Setup(s => s.GetTemplateGuidanceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FedRampTemplate?)null);

        var args = new Dictionary<string, object> { ["template_type"] = "UNKNOWN" };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("not found");
        result.Should().Contain("SSP");
    }

    // ──────────────── Baseline Parameter Tests ────────────────

    [Fact]
    public async Task ExecuteCoreAsync_With_Baseline_Should_Pass_To_Service()
    {
        var ssp = CreateTemplate("SSP", "System Security Plan");
        _serviceMock.Setup(s => s.GetTemplateGuidanceAsync("SSP", "Moderate", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ssp);

        var args = new Dictionary<string, object>
        {
            ["template_type"] = "SSP",
            ["baseline"] = "Moderate"
        };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("SSP");
        _serviceMock.Verify(s => s.GetTemplateGuidanceAsync("SSP", "Moderate", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────── Caching Tests ────────────────

    [Fact]
    public async Task ExecuteCoreAsync_Should_Cache_Results()
    {
        var ssp = CreateTemplate("SSP", "System Security Plan");
        _serviceMock.Setup(s => s.GetTemplateGuidanceAsync("SSP", "High", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ssp);

        var args = new Dictionary<string, object> { ["template_type"] = "SSP" };

        var result1 = await _tool.ExecuteCoreAsync(args);
        var result2 = await _tool.ExecuteCoreAsync(args);

        result1.Should().Be(result2);
        _serviceMock.Verify(s => s.GetTemplateGuidanceAsync("SSP", "High", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────── Authorization Checklist Tests ────────────────

    [Fact]
    public async Task ExecuteCoreAsync_Should_Include_Authorization_Checklist()
    {
        var ssp = CreateTemplate("SSP", "System Security Plan");
        _serviceMock.Setup(s => s.GetTemplateGuidanceAsync("SSP", "High", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ssp);

        var args = new Dictionary<string, object> { ["template_type"] = "SSP" };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("Authorization Checklist");
        result.Should().Contain("[Required]");
    }

    // ──────────────── Helper ────────────────

    private static FedRampTemplate CreateTemplate(string type, string title)
    {
        return new FedRampTemplate(
            TemplateType: type,
            Title: title,
            Description: $"Test description for {type}",
            Sections: new List<TemplateSection>
            {
                new("Section 1", "First section description", new List<string> { "Element A", "Element B" }),
                new("Section 2", "Second section description", new List<string> { "Element C" })
            },
            RequiredFields: new List<FieldDefinition>
            {
                new("Field 1", "Field description", "Example value", "Azure Service A"),
                new("Field 2", "Another field", "Another example", "Azure Service B")
            },
            AzureMappings: new Dictionary<string, string>
            {
                ["Capability A"] = "Azure Service X",
                ["Capability B"] = "Azure Service Y"
            },
            AuthorizationChecklist: new List<ChecklistItem>
            {
                new("Item 1", "First checklist item", true),
                new("Item 2", "Second checklist item", false)
            });
    }
}
