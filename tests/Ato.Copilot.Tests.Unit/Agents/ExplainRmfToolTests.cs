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

public class ExplainRmfToolTests
{
    private readonly ExplainRmfTool _tool;
    private readonly Mock<IRmfKnowledgeService> _rmfServiceMock = new();
    private readonly Mock<IDoDInstructionService> _dodInstructionServiceMock = new();
    private readonly Mock<IDoDWorkflowService> _dodWorkflowServiceMock = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    public ExplainRmfToolTests()
    {
        var options = Options.Create(new KnowledgeBaseAgentOptions());
        var logger = Mock.Of<ILogger<ExplainRmfTool>>();
        _tool = new ExplainRmfTool(
            _rmfServiceMock.Object,
            _dodInstructionServiceMock.Object,
            _dodWorkflowServiceMock.Object,
            _cache, options, logger);
    }

    [Fact]
    public void Tool_Should_Have_Correct_Name()
    {
        _tool.Name.Should().Be("kb_explain_rmf");
    }

    [Fact]
    public void Tool_Should_Have_Description()
    {
        _tool.Description.Should().Contain("RMF");
    }

    [Fact]
    public void Tool_Should_Have_Expected_Parameters()
    {
        _tool.Parameters.Should().ContainKey("topic");
        _tool.Parameters.Should().ContainKey("step_number");
        _tool.Parameters.Should().ContainKey("organization");
        _tool.Parameters.Should().ContainKey("instruction_id");
    }

    [Fact]
    public async Task Overview_Topic_Should_Return_RMF_Steps()
    {
        var processData = CreateSampleProcessData();
        _rmfServiceMock.Setup(s => s.GetRmfProcessAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(processData);

        var args = new Dictionary<string, object> { ["topic"] = "overview" };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("RMF");
        result.Should().Contain("Categorize");
        result.Should().Contain("Step 1");
    }

    [Fact]
    public async Task Step_Topic_Should_Return_Step_Details()
    {
        var step = new RmfStep(3, "Implement",
            "Implement the selected security controls.",
            new List<string> { "Deploy controls", "Configure STIGs" },
            new List<string> { "Updated SSP" },
            new List<string> { "System Owner", "ISSO" },
            "DoDI 8510.01, Enclosure 6");

        _rmfServiceMock.Setup(s => s.GetRmfStepAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(step);

        var args = new Dictionary<string, object> { ["topic"] = "step", ["step_number"] = 3 };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("Step 3");
        result.Should().Contain("Implement");
        result.Should().Contain("Deploy controls");
        result.Should().Contain("ISSO");
    }

    [Fact]
    public async Task Step_Topic_Without_Number_Should_Return_Prompt()
    {
        var args = new Dictionary<string, object> { ["topic"] = "step" };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("step_number");
    }

    [Fact]
    public async Task Invalid_Step_Number_Should_Return_Error()
    {
        var args = new Dictionary<string, object> { ["topic"] = "step", ["step_number"] = 9 };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("Invalid");
    }

    [Fact]
    public async Task Service_Topic_Should_Return_Organization_Guidance()
    {
        var guidance = new ServiceGuidance(
            "Navy",
            "Navy-specific RMF guidance.",
            new List<string> { "NAVCYBERFOR" },
            new List<string> { "Register in eMASS" },
            "6-12 months",
            new List<string> { "eMASS", "ACAS" });

        _rmfServiceMock.Setup(s => s.GetServiceGuidanceAsync("Navy", It.IsAny<CancellationToken>()))
            .ReturnsAsync(guidance);
        _dodWorkflowServiceMock.Setup(s => s.GetWorkflowsByOrganizationAsync("Navy", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DoDWorkflow>());

        var args = new Dictionary<string, object> { ["topic"] = "service", ["organization"] = "Navy" };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("Navy");
        result.Should().Contain("NAVCYBERFOR");
        result.Should().Contain("eMASS");
    }

    [Fact]
    public async Task Deliverables_Topic_Should_Return_Deliverables_By_Step()
    {
        var processData = CreateSampleProcessData();
        _rmfServiceMock.Setup(s => s.GetRmfProcessAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(processData);

        var args = new Dictionary<string, object> { ["topic"] = "deliverables" };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("Deliverables");
        result.Should().Contain("System Security Plan");
    }

    [Fact]
    public async Task Instruction_Topic_Should_Return_DoD_Instruction()
    {
        var instruction = new DoDInstruction(
            "DoDI-8510.01", "Risk Management Framework for DoD Systems",
            "Establishes the RMF.", "2022-07-19", "All DoD IT",
            "https://example.com",
            new List<string> { "CA-1", "CA-2" },
            new List<string>(),
            new List<ControlMapping>
            {
                new("CA-1", "Security Assessment Policy", "Implement per DoDI 8510.01.")
            });

        _dodInstructionServiceMock.Setup(s => s.ExplainInstructionAsync("DoDI 8510.01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(instruction);

        var args = new Dictionary<string, object> { ["topic"] = "instruction", ["instruction_id"] = "DoDI 8510.01" };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("DoDI-8510.01");
        result.Should().Contain("Risk Management Framework");
        result.Should().Contain("CA-1");
    }

    [Fact]
    public async Task Workflow_Topic_Should_Return_Organization_Workflows()
    {
        var workflows = new List<DoDWorkflow>
        {
            new("NAVY-ATO-MODERATE", "Navy ATO — Moderate", "Navy", "Moderate",
                "Standard Navy workflow.",
                new List<WorkflowStep>
                {
                    new(1, "Registration", "Register system in eMASS.", "14 days"),
                    new(2, "Categorization", "Complete FIPS 199.", "21 days")
                },
                new List<string> { "SSP", "SAR" },
                new List<string> { "NAVCYBERFOR AO" })
        };

        _dodWorkflowServiceMock.Setup(s => s.GetWorkflowsByOrganizationAsync("Navy", It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflows);

        var args = new Dictionary<string, object> { ["topic"] = "workflow", ["organization"] = "Navy" };
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("Navy");
        result.Should().Contain("Registration");
        result.Should().Contain("14 days");
        result.Should().Contain("SSP");
    }

    [Fact]
    public async Task Default_Topic_Should_Return_Overview()
    {
        var processData = CreateSampleProcessData();
        _rmfServiceMock.Setup(s => s.GetRmfProcessAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(processData);

        var args = new Dictionary<string, object>();
        var result = await _tool.ExecuteCoreAsync(args);

        result.Should().Contain("RMF");
        result.Should().Contain("Categorize");
    }

    [Fact]
    public async Task Results_Should_Be_Cached()
    {
        var processData = CreateSampleProcessData();
        _rmfServiceMock.Setup(s => s.GetRmfProcessAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(processData);

        var args = new Dictionary<string, object> { ["topic"] = "overview" };

        await _tool.ExecuteCoreAsync(args);
        await _tool.ExecuteCoreAsync(args);

        _rmfServiceMock.Verify(s => s.GetRmfProcessAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static RmfProcessData CreateSampleProcessData()
    {
        return new RmfProcessData(
            new List<RmfStep>
            {
                new(1, "Categorize", "Categorize the system.", new List<string> { "Identify info types" }, new List<string> { "System categorization" }, new List<string> { "System Owner" }, "DoDI 8510.01"),
                new(2, "Select", "Select controls.", new List<string> { "Identify baseline" }, new List<string> { "Control baseline" }, new List<string> { "ISSO" }, "DoDI 8510.01")
            },
            new Dictionary<string, ServiceGuidance>
            {
                ["navy"] = new("Navy", "Navy guidance.", new List<string> { "NAVCYBERFOR" }, new List<string> { "Register in eMASS" }, "6-12 months", new List<string> { "eMASS" })
            },
            new List<DeliverableInfo>
            {
                new(1, "Categorize", new List<string> { "System Security Plan (SSP)" }),
                new(2, "Select", new List<string> { "Control Baseline" })
            });
    }
}
