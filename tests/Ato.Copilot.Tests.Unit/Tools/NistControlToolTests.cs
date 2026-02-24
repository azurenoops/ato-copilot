using System.Text.Json;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Tools;

/// <summary>
/// Unit tests for <see cref="NistControlSearchTool"/> and <see cref="NistControlExplainerTool"/>.
/// Validates JSON envelope responses (success/error), empty results, and catalog unavailable scenarios.
/// </summary>
public class NistControlToolTests
{
    // ─── NistControlSearchTool ───────────────────────────────────────────────

    [Fact]
    public async Task SearchTool_ReturnsMatchingControls()
    {
        var mockService = new Mock<INistControlsService>();
        mockService.Setup(s => s.SearchControlsAsync(
                "encryption", null, null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NistControl>
            {
                new() { Id = "sc-13", Family = "SC", Title = "Cryptographic Protection", Description = "Use FIPS-validated cryptography." },
                new() { Id = "sc-28", Family = "SC", Title = "Protection of Information at Rest", Description = "Protect data at rest using encryption." }
            });

        var tool = new NistControlSearchTool(mockService.Object, Mock.Of<ILogger<NistControlSearchTool>>());
        var args = new Dictionary<string, object?> { ["query"] = "encryption" };

        var result = await tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("data").GetProperty("total_matches").GetInt32().Should().Be(2);
        var controls = root.GetProperty("data").GetProperty("controls");
        controls.GetArrayLength().Should().Be(2);
        controls[0].GetProperty("id").GetString().Should().Be("SC-13");
        controls[1].GetProperty("id").GetString().Should().Be("SC-28");
        root.GetProperty("metadata").GetProperty("tool").GetString().Should().Be("search_nist_controls");
    }

    [Fact]
    public async Task SearchTool_NoResults_ReturnsFriendlyMessage()
    {
        var mockService = new Mock<INistControlsService>();
        mockService.Setup(s => s.SearchControlsAsync(
                "xyznonexistent", null, null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NistControl>());

        var tool = new NistControlSearchTool(mockService.Object, Mock.Of<ILogger<NistControlSearchTool>>());
        var args = new Dictionary<string, object?> { ["query"] = "xyznonexistent" };

        var result = await tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("data").GetProperty("total_matches").GetInt32().Should().Be(0);
        root.GetProperty("data").GetProperty("controls").GetArrayLength().Should().Be(0);
        root.GetProperty("data").GetProperty("message").GetString()
            .Should().Contain("No controls found");
    }

    [Fact]
    public async Task SearchTool_CatalogUnavailable_ReturnsErrorEnvelope()
    {
        var mockService = new Mock<INistControlsService>();
        mockService.Setup(s => s.SearchControlsAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Catalog not loaded"));

        var tool = new NistControlSearchTool(mockService.Object, Mock.Of<ILogger<NistControlSearchTool>>());
        var args = new Dictionary<string, object?> { ["query"] = "access" };

        var result = await tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("error");
        root.GetProperty("errorCode").GetString().Should().Be("CATALOG_UNAVAILABLE");
        root.GetProperty("suggestion").GetString().Should().Contain("retry");
    }

    [Fact]
    public async Task SearchTool_FamilyFilter_PassedToService()
    {
        var mockService = new Mock<INistControlsService>();
        mockService.Setup(s => s.SearchControlsAsync(
                "audit", "AU", null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NistControl>
            {
                new() { Id = "au-2", Family = "AU", Title = "Event Logging", Description = "The organization defines events to be logged." }
            });

        var tool = new NistControlSearchTool(mockService.Object, Mock.Of<ILogger<NistControlSearchTool>>());
        var args = new Dictionary<string, object?>
        {
            ["query"] = "audit",
            ["family"] = "AU"
        };

        var result = await tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("data").GetProperty("family_filter").GetString().Should().Be("AU");
        root.GetProperty("data").GetProperty("total_matches").GetInt32().Should().Be(1);
        mockService.Verify(s => s.SearchControlsAsync("audit", "AU", null, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchTool_MaxResultsClamped_To25()
    {
        var mockService = new Mock<INistControlsService>();
        mockService.Setup(s => s.SearchControlsAsync(
                It.IsAny<string>(), null, null, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NistControl>());

        var tool = new NistControlSearchTool(mockService.Object, Mock.Of<ILogger<NistControlSearchTool>>());
        var args = new Dictionary<string, object?>
        {
            ["query"] = "test",
            ["max_results"] = 100
        };

        await tool.ExecuteCoreAsync(args);
        mockService.Verify(s => s.SearchControlsAsync("test", null, null, 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchTool_LongDescription_Truncated()
    {
        var longDescription = new string('A', 300);
        var mockService = new Mock<INistControlsService>();
        mockService.Setup(s => s.SearchControlsAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NistControl>
            {
                new() { Id = "ac-1", Family = "AC", Title = "Policy and Procedures", Description = longDescription }
            });

        var tool = new NistControlSearchTool(mockService.Object, Mock.Of<ILogger<NistControlSearchTool>>());
        var args = new Dictionary<string, object?> { ["query"] = "policy" };

        var result = await tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);

        var excerpt = doc.RootElement.GetProperty("data").GetProperty("controls")[0]
            .GetProperty("excerpt").GetString()!;
        excerpt.Length.Should().BeLessThanOrEqualTo(203); // 200 + "..."
        excerpt.Should().EndWith("...");
    }

    // ─── NistControlExplainerTool ────────────────────────────────────────────

    [Fact]
    public async Task ExplainerTool_ValidControl_ReturnsDetailedExplanation()
    {
        var mockService = new Mock<INistControlsService>();
        mockService.Setup(s => s.GetControlEnhancementAsync("SC-13", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ControlEnhancement(
                "SC-13", "Cryptographic Protection",
                "Implement FIPS-validated cryptography.",
                "Refer to NIST SP 800-57 for key management.",
                new List<string> { "SC-13.a", "SC-13.b" },
                DateTime.UtcNow));
        mockService.Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("NIST SP 800-53 Rev 5");

        var tool = new NistControlExplainerTool(mockService.Object, Mock.Of<ILogger<NistControlExplainerTool>>());
        var args = new Dictionary<string, object?> { ["control_id"] = "SC-13" };

        var result = await tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("success");
        var data = root.GetProperty("data");
        data.GetProperty("control_id").GetString().Should().Be("SC-13");
        data.GetProperty("title").GetString().Should().Be("Cryptographic Protection");
        data.GetProperty("statement").GetString().Should().Contain("FIPS");
        data.GetProperty("guidance").GetString().Should().Contain("SP 800-57");
        data.GetProperty("objectives").GetArrayLength().Should().Be(2);
        data.GetProperty("catalog_version").GetString().Should().Be("NIST SP 800-53 Rev 5");
        root.GetProperty("metadata").GetProperty("tool").GetString().Should().Be("explain_nist_control");
    }

    [Fact]
    public async Task ExplainerTool_ControlNotFound_ReturnsError()
    {
        var mockService = new Mock<INistControlsService>();
        mockService.Setup(s => s.GetControlEnhancementAsync("ZZ-99", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ControlEnhancement?)null);
        mockService.Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("NIST SP 800-53 Rev 5");

        var tool = new NistControlExplainerTool(mockService.Object, Mock.Of<ILogger<NistControlExplainerTool>>());
        var args = new Dictionary<string, object?> { ["control_id"] = "ZZ-99" };

        var result = await tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("error");
        root.GetProperty("errorCode").GetString().Should().Be("CONTROL_NOT_FOUND");
        root.GetProperty("message").GetString().Should().Contain("ZZ-99");
        root.GetProperty("suggestion").GetString().Should().Contain("search_nist_controls");
    }

    [Fact]
    public async Task ExplainerTool_NullControlId_ReturnsInvalidInput()
    {
        var mockService = new Mock<INistControlsService>();
        var tool = new NistControlExplainerTool(mockService.Object, Mock.Of<ILogger<NistControlExplainerTool>>());
        var args = new Dictionary<string, object?> { ["control_id"] = null };

        var result = await tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("error");
        root.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task ExplainerTool_CatalogUnavailable_ReturnsErrorEnvelope()
    {
        var mockService = new Mock<INistControlsService>();
        mockService.Setup(s => s.GetControlEnhancementAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Catalog not loaded"));

        var tool = new NistControlExplainerTool(mockService.Object, Mock.Of<ILogger<NistControlExplainerTool>>());
        var args = new Dictionary<string, object?> { ["control_id"] = "AC-2" };

        var result = await tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("error");
        root.GetProperty("errorCode").GetString().Should().Be("CATALOG_UNAVAILABLE");
    }

    [Fact]
    public async Task ExplainerTool_ArgumentException_ReturnsInvalidInput()
    {
        var mockService = new Mock<INistControlsService>();
        mockService.Setup(s => s.GetControlEnhancementAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid control ID format"));

        var tool = new NistControlExplainerTool(mockService.Object, Mock.Of<ILogger<NistControlExplainerTool>>());
        var args = new Dictionary<string, object?> { ["control_id"] = "invalid!" };

        var result = await tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("error");
        root.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
        root.GetProperty("message").GetString().Should().Contain("Invalid control ID format");
    }

    // ─── Tool Metadata ──────────────────────────────────────────────────────

    [Fact]
    public void SearchTool_HasCorrectMetadata()
    {
        var tool = new NistControlSearchTool(Mock.Of<INistControlsService>(), Mock.Of<ILogger<NistControlSearchTool>>());
        tool.Name.Should().Be("search_nist_controls");
        tool.Description.Should().Contain("NIST SP 800-53");
        tool.Parameters.Should().ContainKey("query");
        tool.Parameters["query"].Required.Should().BeTrue();
        tool.Parameters.Should().ContainKey("family");
        tool.Parameters["family"].Required.Should().BeFalse();
        tool.Parameters.Should().ContainKey("max_results");
        tool.Parameters["max_results"].Required.Should().BeFalse();
    }

    [Fact]
    public void ExplainerTool_HasCorrectMetadata()
    {
        var tool = new NistControlExplainerTool(Mock.Of<INistControlsService>(), Mock.Of<ILogger<NistControlExplainerTool>>());
        tool.Name.Should().Be("explain_nist_control");
        tool.Description.Should().Contain("NIST SP 800-53");
        tool.Parameters.Should().ContainKey("control_id");
        tool.Parameters["control_id"].Required.Should().BeTrue();
    }
}
