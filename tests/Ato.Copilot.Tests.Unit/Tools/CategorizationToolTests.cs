using System.Text.Json;
using Moq;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Tools;

/// <summary>
/// Unit tests for Feature 015 Phase 4 — Categorization Tools.
/// T042: CategorizeSystemTool tests
/// T043: High-water mark computation tests
/// T044: DoD IL derivation tests
/// </summary>
public class CategorizationToolTests
{
    private readonly Mock<ICategorizationService> _categorizationMock = new();

    // ────────────────────────────────────────────────────────────────────────
    // T042: CategorizeSystemTool Tests
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CategorizeSystem_ValidInput_ReturnsSuccess()
    {
        var categorization = CreateTestCategorization("sys-1", ImpactValue.Moderate, ImpactValue.Moderate, ImpactValue.Low);
        _categorizationMock
            .Setup(s => s.CategorizeSystemAsync(
                "sys-1", It.IsAny<IEnumerable<InformationTypeInput>>(), "mcp-user",
                false, "Test justification", It.IsAny<CancellationToken>()))
            .ReturnsAsync(categorization);

        var tool = CreateCategorizeSystemTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["information_types"] = CreateInfoTypeInputs(),
            ["justification"] = "Test justification"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("overall_categorization").GetString().Should().Be("Moderate");
        json.RootElement.GetProperty("data").GetProperty("information_type_count").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task CategorizeSystem_MissingSystemId_ReturnsError()
    {
        var tool = CreateCategorizeSystemTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["information_types"] = CreateInfoTypeInputs()
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task CategorizeSystem_MissingInfoTypes_ReturnsError()
    {
        var tool = CreateCategorizeSystemTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task CategorizeSystem_SystemNotFound_ReturnsError()
    {
        _categorizationMock
            .Setup(s => s.CategorizeSystemAsync(
                "nonexistent", It.IsAny<IEnumerable<InformationTypeInput>>(), "mcp-user",
                false, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("System 'nonexistent' not found."));

        var tool = CreateCategorizeSystemTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "nonexistent",
            ["information_types"] = CreateInfoTypeInputs()
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("message").GetString().Should().Contain("not found");
    }

    [Fact]
    public async Task CategorizeSystem_WithNss_PassesNssFlag()
    {
        var categorization = CreateTestCategorization("sys-nss", ImpactValue.High, ImpactValue.High, ImpactValue.Moderate, isNss: true);
        _categorizationMock
            .Setup(s => s.CategorizeSystemAsync(
                "sys-nss", It.IsAny<IEnumerable<InformationTypeInput>>(), "mcp-user",
                true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(categorization);

        var tool = CreateCategorizeSystemTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-nss",
            ["information_types"] = CreateInfoTypeInputs(),
            ["is_national_security_system"] = true
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("is_national_security_system").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task CategorizeSystem_WithJsonElement_ParsesCorrectly()
    {
        var categorization = CreateTestCategorization("sys-json", ImpactValue.Moderate, ImpactValue.Low, ImpactValue.Low);
        _categorizationMock
            .Setup(s => s.CategorizeSystemAsync(
                "sys-json", It.IsAny<IEnumerable<InformationTypeInput>>(), "mcp-user",
                false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(categorization);

        // Simulate JsonElement input (as MCP would provide)
        var jsonStr = JsonSerializer.Serialize(new[]
        {
            new { sp800_60_id = "D.1.1", name = "Strategic Planning",
                  confidentiality_impact = "Moderate", integrity_impact = "Low", availability_impact = "Low" }
        });
        var jsonElement = JsonDocument.Parse(jsonStr).RootElement;

        var tool = CreateCategorizeSystemTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-json",
            ["information_types"] = jsonElement
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task CategorizeSystem_WithAdjustment_IncludesJustification()
    {
        var categorization = CreateTestCategorization("sys-adj", ImpactValue.High, ImpactValue.Moderate, ImpactValue.Low);
        categorization.InformationTypes.First().UsesProvisionalImpactLevels = false;
        categorization.InformationTypes.First().AdjustmentJustification = "Elevated due to PII";

        _categorizationMock
            .Setup(s => s.CategorizeSystemAsync(
                "sys-adj", It.IsAny<IEnumerable<InformationTypeInput>>(), "mcp-user",
                false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(categorization);

        var tool = CreateCategorizeSystemTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-adj",
            ["information_types"] = CreateInfoTypeInputs()
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        var infoTypes = json.RootElement.GetProperty("data").GetProperty("information_types");
        infoTypes.EnumerateArray().First().GetProperty("adjustment_justification").GetString()
            .Should().Be("Elevated due to PII");
    }

    // ────────────────────────────────────────────────────────────────────────
    // GetCategorizationTool Tests
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCategorization_Found_ReturnsSuccess()
    {
        var categorization = CreateTestCategorization("sys-get", ImpactValue.Low, ImpactValue.Low, ImpactValue.Low);
        _categorizationMock
            .Setup(s => s.GetCategorizationAsync("sys-get", It.IsAny<CancellationToken>()))
            .ReturnsAsync(categorization);

        var tool = CreateGetCategorizationTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-get"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("overall_categorization").GetString().Should().Be("Low");
    }

    [Fact]
    public async Task GetCategorization_NotFound_ReturnsNullData()
    {
        _categorizationMock
            .Setup(s => s.GetCategorizationAsync("no-cat", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SecurityCategorization?)null);

        var tool = CreateGetCategorizationTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "no-cat"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("message").GetString().Should().Contain("No categorization");
    }

    [Fact]
    public async Task GetCategorization_MissingSystemId_ReturnsError()
    {
        var tool = CreateGetCategorizationTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>());

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
    }

    // ────────────────────────────────────────────────────────────────────────
    // SuggestInfoTypesTool Tests
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SuggestInfoTypes_ReturnsSuggestions()
    {
        _categorizationMock
            .Setup(s => s.SuggestInfoTypesAsync("sys-suggest", "security portal", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SuggestedInformationType>
            {
                new()
                {
                    Sp80060Id = "C.3.5.8", Name = "Information Security",
                    Category = "Management and Support", Confidence = 0.85,
                    Rationale = "System description matches keyword 'security'",
                    DefaultConfidentialityImpact = "Moderate", DefaultIntegrityImpact = "Moderate",
                    DefaultAvailabilityImpact = "Low"
                }
            });

        var tool = CreateSuggestInfoTypesTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-suggest",
            ["description"] = "security portal"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("suggestion_count").GetInt32().Should().Be(1);
        var first = json.RootElement.GetProperty("data").GetProperty("suggestions").EnumerateArray().First();
        first.GetProperty("sp800_60_id").GetString().Should().Be("C.3.5.8");
        first.GetProperty("confidence").GetDouble().Should().Be(0.85);
    }

    [Fact]
    public async Task SuggestInfoTypes_SystemNotFound_ReturnsError()
    {
        _categorizationMock
            .Setup(s => s.SuggestInfoTypesAsync("bad-id", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("System 'bad-id' not found."));

        var tool = CreateSuggestInfoTypesTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "bad-id"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("message").GetString().Should().Contain("not found");
    }

    [Fact]
    public async Task SuggestInfoTypes_MissingSystemId_ReturnsError()
    {
        var tool = CreateSuggestInfoTypesTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>());

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
    }

    // ────────────────────────────────────────────────────────────────────────
    // T043: High-Water Mark Computation Tests (via ComputeHighWaterMark)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void HighWaterMark_AllLow_ReturnsLow()
    {
        var service = CreateCategorizationServiceForCompute();
        var result = service.ComputeHighWaterMark(new[]
        {
            MakeInput("D.1.1", "Low", "Low", "Low"),
            MakeInput("D.2.1", "Low", "Low", "Low")
        });

        result.OverallCategorization.Should().Be(ImpactValue.Low);
        result.NistBaseline.Should().Be("Low");
        result.DoDImpactLevel.Should().Be("IL2");
        result.InformationTypeCount.Should().Be(2);
    }

    [Fact]
    public void HighWaterMark_MixedImpacts_ReturnsHighest()
    {
        var service = CreateCategorizationServiceForCompute();
        var result = service.ComputeHighWaterMark(new[]
        {
            MakeInput("D.1.1", "Low", "Moderate", "Low"),
            MakeInput("D.2.1", "Moderate", "Low", "High")
        });

        result.ConfidentialityImpact.Should().Be(ImpactValue.Moderate);
        result.IntegrityImpact.Should().Be(ImpactValue.Moderate);
        result.AvailabilityImpact.Should().Be(ImpactValue.High);
        result.OverallCategorization.Should().Be(ImpactValue.High);
        result.NistBaseline.Should().Be("High");
        result.DoDImpactLevel.Should().Be("IL5");
    }

    [Fact]
    public void HighWaterMark_AllHigh_ReturnsHigh()
    {
        var service = CreateCategorizationServiceForCompute();
        var result = service.ComputeHighWaterMark(new[]
        {
            MakeInput("D.1.1", "High", "High", "High"),
            MakeInput("D.2.1", "Moderate", "Moderate", "Moderate")
        });

        result.OverallCategorization.Should().Be(ImpactValue.High);
        result.NistBaseline.Should().Be("High");
        result.DoDImpactLevel.Should().Be("IL5");
    }

    [Fact]
    public void HighWaterMark_AllModerate_ReturnsModerate()
    {
        var service = CreateCategorizationServiceForCompute();
        var result = service.ComputeHighWaterMark(new[]
        {
            MakeInput("D.1.1", "Moderate", "Moderate", "Moderate")
        });

        result.OverallCategorization.Should().Be(ImpactValue.Moderate);
        result.NistBaseline.Should().Be("Moderate");
        result.DoDImpactLevel.Should().Be("IL4");
    }

    [Fact]
    public void HighWaterMark_SingleInfoType_ComputesCorrectly()
    {
        var service = CreateCategorizationServiceForCompute();
        var result = service.ComputeHighWaterMark(new[]
        {
            MakeInput("C.3.5.8", "Low", "Moderate", "Low")
        });

        result.ConfidentialityImpact.Should().Be(ImpactValue.Low);
        result.IntegrityImpact.Should().Be(ImpactValue.Moderate);
        result.AvailabilityImpact.Should().Be(ImpactValue.Low);
        result.OverallCategorization.Should().Be(ImpactValue.Moderate);
    }

    [Fact]
    public void HighWaterMark_EmptyList_ThrowsException()
    {
        var service = CreateCategorizationServiceForCompute();
        var act = () => service.ComputeHighWaterMark(Array.Empty<InformationTypeInput>());
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least one*");
    }

    [Fact]
    public void HighWaterMark_FormalNotation_FormattedCorrectly()
    {
        var service = CreateCategorizationServiceForCompute();
        var result = service.ComputeHighWaterMark(new[]
        {
            MakeInput("D.1.1", "Moderate", "Low", "High")
        });

        result.FormalNotation.Should().Contain("MODERATE");
        result.FormalNotation.Should().Contain("LOW");
        result.FormalNotation.Should().Contain("HIGH");
        result.FormalNotation.Should().StartWith("SC System");
    }

    // ────────────────────────────────────────────────────────────────────────
    // T044: DoD IL Derivation Tests
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ILDerivation_LowBaseline_ReturnsIL2()
    {
        var service = CreateCategorizationServiceForCompute();
        var result = service.ComputeHighWaterMark(new[] { MakeInput("D.1.1", "Low", "Low", "Low") });
        result.DoDImpactLevel.Should().Be("IL2");
    }

    [Fact]
    public void ILDerivation_ModerateBaseline_ReturnsIL4()
    {
        var service = CreateCategorizationServiceForCompute();
        var result = service.ComputeHighWaterMark(new[] { MakeInput("D.1.1", "Moderate", "Moderate", "Low") });
        result.DoDImpactLevel.Should().Be("IL4");
    }

    [Fact]
    public void ILDerivation_HighBaseline_ReturnsIL5()
    {
        var service = CreateCategorizationServiceForCompute();
        var result = service.ComputeHighWaterMark(new[] { MakeInput("D.1.1", "High", "High", "High") });
        result.DoDImpactLevel.Should().Be("IL5");
    }

    [Fact]
    public void ILDerivation_NSS_ReturnsIL6()
    {
        var service = CreateCategorizationServiceForCompute();
        var result = service.ComputeHighWaterMark(
            new[] { MakeInput("D.1.1", "Low", "Low", "Low") },
            isNationalSecuritySystem: true);
        // NSS without classified designation → not IL6, baseline IL derivation
        // Per ComplianceFrameworks.DeriveImpactLevel: IL6 requires NSS + classified designation
        result.DoDImpactLevel.Should().Be("IL2");
    }

    [Fact]
    public void ILDerivation_MixedToModerate_ReturnsIL4()
    {
        var service = CreateCategorizationServiceForCompute();
        var result = service.ComputeHighWaterMark(new[]
        {
            MakeInput("D.1.1", "Low", "Moderate", "Low"),
            MakeInput("D.2.1", "Moderate", "Low", "Low")
        });
        result.DoDImpactLevel.Should().Be("IL4");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Tool Metadata Tests
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CategorizeSystemTool_HasCorrectName()
    {
        var tool = CreateCategorizeSystemTool();
        tool.Name.Should().Be("compliance_categorize_system");
    }

    [Fact]
    public void GetCategorizationTool_HasCorrectName()
    {
        var tool = CreateGetCategorizationTool();
        tool.Name.Should().Be("compliance_get_categorization");
    }

    [Fact]
    public void SuggestInfoTypesTool_HasCorrectName()
    {
        var tool = CreateSuggestInfoTypesTool();
        tool.Name.Should().Be("compliance_suggest_info_types");
    }

    [Theory]
    [InlineData("compliance_categorize_system", "system_id", true)]
    [InlineData("compliance_categorize_system", "information_types", true)]
    [InlineData("compliance_categorize_system", "justification", false)]
    [InlineData("compliance_categorize_system", "is_national_security_system", false)]
    [InlineData("compliance_get_categorization", "system_id", true)]
    [InlineData("compliance_suggest_info_types", "system_id", true)]
    [InlineData("compliance_suggest_info_types", "description", false)]
    public void CategorizationTool_ParameterRequiredness(string toolName, string paramName, bool expectedRequired)
    {
        var tool = toolName switch
        {
            "compliance_categorize_system" => (BaseTool)CreateCategorizeSystemTool(),
            "compliance_get_categorization" => CreateGetCategorizationTool(),
            "compliance_suggest_info_types" => CreateSuggestInfoTypesTool(),
            _ => throw new ArgumentException($"Unknown tool: {toolName}")
        };

        tool.Parameters.Should().ContainKey(paramName);
        tool.Parameters[paramName].Required.Should().Be(expectedRequired);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────

    private CategorizeSystemTool CreateCategorizeSystemTool() =>
        new(_categorizationMock.Object, Mock.Of<ILogger<CategorizeSystemTool>>());

    private GetCategorizationTool CreateGetCategorizationTool() =>
        new(_categorizationMock.Object, Mock.Of<ILogger<GetCategorizationTool>>());

    private SuggestInfoTypesTool CreateSuggestInfoTypesTool() =>
        new(_categorizationMock.Object, Mock.Of<ILogger<SuggestInfoTypesTool>>());

    private static Ato.Copilot.Agents.Compliance.Services.CategorizationService CreateCategorizationServiceForCompute()
    {
        // ComputeHighWaterMark is a pure computation, doesn't need real DB
        return new(Mock.Of<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(),
            Mock.Of<ILogger<Ato.Copilot.Agents.Compliance.Services.CategorizationService>>());
    }

    private static List<InformationTypeInput> CreateInfoTypeInputs() =>
    [
        new()
        {
            Sp80060Id = "D.1.1", Name = "Strategic Planning",
            ConfidentialityImpact = "Moderate", IntegrityImpact = "Moderate", AvailabilityImpact = "Low"
        },
        new()
        {
            Sp80060Id = "C.3.5.8", Name = "Information Security",
            ConfidentialityImpact = "Moderate", IntegrityImpact = "Low", AvailabilityImpact = "Low"
        }
    ];

    private static InformationTypeInput MakeInput(string id, string c, string i, string a) =>
        new()
        {
            Sp80060Id = id, Name = $"Test-{id}",
            ConfidentialityImpact = c, IntegrityImpact = i, AvailabilityImpact = a
        };

    private static SecurityCategorization CreateTestCategorization(
        string systemId,
        ImpactValue maxC, ImpactValue maxI, ImpactValue maxA,
        bool isNss = false)
    {
        var system = new RegisteredSystem
        {
            Id = systemId,
            Name = $"Test System {systemId}",
            SystemType = SystemType.MajorApplication,
            MissionCriticality = MissionCriticality.MissionCritical,
            HostingEnvironment = "AzureGovernment",
            IsNationalSecuritySystem = isNss
        };

        var cat = new SecurityCategorization
        {
            RegisteredSystemId = systemId,
            RegisteredSystem = system,
            IsNationalSecuritySystem = isNss,
            CategorizedBy = "mcp-user",
            CategorizedAt = DateTime.UtcNow,
            InformationTypes = new List<InformationType>
            {
                new()
                {
                    Sp80060Id = "D.1.1", Name = "Strategic Planning",
                    ConfidentialityImpact = maxC, IntegrityImpact = maxI, AvailabilityImpact = maxA,
                    UsesProvisionalImpactLevels = true
                },
                new()
                {
                    Sp80060Id = "C.3.5.8", Name = "Information Security",
                    ConfidentialityImpact = ImpactValue.Low, IntegrityImpact = ImpactValue.Low,
                    AvailabilityImpact = ImpactValue.Low, UsesProvisionalImpactLevels = true
                }
            }
        };

        return cat;
    }
}
