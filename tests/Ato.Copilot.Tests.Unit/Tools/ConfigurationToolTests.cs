using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Ato.Copilot.Agents.Configuration.Tools;
using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.State.Abstractions;

namespace Ato.Copilot.Tests.Unit.Tools;

public class ConfigurationToolTests
{
    private readonly ConfigurationTool _tool;
    private readonly Mock<IAgentStateManager> _stateMock;
    private readonly Dictionary<string, object?> _stateStore;

    public ConfigurationToolTests()
    {
        _stateStore = new Dictionary<string, object?>();
        _stateMock = new Mock<IAgentStateManager>();

        // Simulate in-memory state with (agentId, key) compound key
        _stateMock.Setup(s => s.GetStateAsync<string>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string agentId, string key, CancellationToken _) =>
                _stateStore.TryGetValue($"{agentId}:{key}", out var val) ? val?.ToString() : null);

        _stateMock.Setup(s => s.GetStateAsync<ConfigurationSettings>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string agentId, string key, CancellationToken _) =>
                _stateStore.TryGetValue($"{agentId}:{key}", out var val) ? val as ConfigurationSettings : null);

        _stateMock.Setup(s => s.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, ConfigurationSettings, CancellationToken>((agentId, key, value, _) => _stateStore[$"{agentId}:{key}"] = value)
            .Returns(Task.CompletedTask);

        _stateMock.Setup(s => s.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((agentId, key, value, _) => _stateStore[$"{agentId}:{key}"] = value)
            .Returns(Task.CompletedTask);

        _tool = new ConfigurationTool(
            _stateMock.Object,
            Mock.Of<ILogger<ConfigurationTool>>());
    }

    private static string GetStatus(string json)
    {
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("status").GetString()!;
    }

    [Fact]
    public void Tool_Should_Have_Correct_Name()
    {
        _tool.Name.Should().Be("configuration_manage");
    }

    [Fact]
    public void Tool_Should_Have_Description()
    {
        _tool.Description.Should().NotBeNullOrEmpty();
        _tool.Description.Should().Contain("settings");
    }

    [Fact]
    public void Tool_Should_Have_Parameters()
    {
        _tool.Parameters.Should().NotBeEmpty();
        _tool.Parameters.Should().ContainKey("action");
    }

    // ── get_configuration ──────────────────────────────────────────

    [Fact]
    public async Task GetConfiguration_WithNoSettings_ShouldReturnSuccessStatus()
    {
        var args = new Dictionary<string, object?> { ["action"] = "get_configuration" };
        var result = await _tool.ExecuteAsync(args);

        result.Should().NotBeNullOrEmpty();
        GetStatus(result).Should().Be("success");
    }

    // ── set_subscription ───────────────────────────────────────────

    [Fact]
    public async Task SetSubscription_WithValidGuid_ShouldSucceed()
    {
        var subId = "00000000-0000-0000-0000-000000000001";
        var args = new Dictionary<string, object?>
        {
            ["action"] = "set_subscription",
            ["subscriptionId"] = subId
        };

        var result = await _tool.ExecuteAsync(args);

        result.Should().NotBeNullOrEmpty();
        GetStatus(result).Should().Be("success");
        result.Should().Contain(subId);
    }

    [Fact]
    public async Task SetSubscription_WithInvalidGuid_ShouldFail()
    {
        var args = new Dictionary<string, object?>
        {
            ["action"] = "set_subscription",
            ["subscriptionId"] = "not-a-guid"
        };

        var result = await _tool.ExecuteAsync(args);

        GetStatus(result).Should().Be("error");
    }

    [Fact]
    public async Task SetSubscription_WithMissing_ShouldFail()
    {
        var args = new Dictionary<string, object?>
        {
            ["action"] = "set_subscription"
        };

        var result = await _tool.ExecuteAsync(args);

        GetStatus(result).Should().Be("error");
    }

    // ── set_framework ──────────────────────────────────────────────

    [Fact]
    public async Task SetFramework_WithValidFramework_ShouldSucceed()
    {
        var args = new Dictionary<string, object?>
        {
            ["action"] = "set_framework",
            ["framework"] = "FedRAMPHigh"
        };

        var result = await _tool.ExecuteAsync(args);

        GetStatus(result).Should().Be("success");
    }

    [Fact]
    public async Task SetFramework_CaseInsensitive_ShouldNormalize()
    {
        var args = new Dictionary<string, object?>
        {
            ["action"] = "set_framework",
            ["framework"] = "fedramphigh"
        };

        var result = await _tool.ExecuteAsync(args);

        GetStatus(result).Should().Be("success");
    }

    [Fact]
    public async Task SetFramework_WithInvalid_ShouldFail()
    {
        var args = new Dictionary<string, object?>
        {
            ["action"] = "set_framework",
            ["framework"] = "InvalidFramework"
        };

        var result = await _tool.ExecuteAsync(args);

        GetStatus(result).Should().Be("error");
    }

    [Fact]
    public async Task SetFramework_WithMissing_ShouldFail()
    {
        var args = new Dictionary<string, object?>
        {
            ["action"] = "set_framework"
        };

        var result = await _tool.ExecuteAsync(args);

        GetStatus(result).Should().Be("error");
    }

    // ── set_baseline ───────────────────────────────────────────────

    [Fact]
    public async Task SetBaseline_WithValid_ShouldSucceed()
    {
        var args = new Dictionary<string, object?>
        {
            ["action"] = "set_baseline",
            ["baseline"] = "High"
        };

        var result = await _tool.ExecuteAsync(args);

        GetStatus(result).Should().Be("success");
    }

    [Fact]
    public async Task SetBaseline_CaseInsensitive_ShouldNormalize()
    {
        var args = new Dictionary<string, object?>
        {
            ["action"] = "set_baseline",
            ["baseline"] = "moderate"
        };

        var result = await _tool.ExecuteAsync(args);

        GetStatus(result).Should().Be("success");
    }

    [Fact]
    public async Task SetBaseline_WithInvalid_ShouldFail()
    {
        var args = new Dictionary<string, object?>
        {
            ["action"] = "set_baseline",
            ["baseline"] = "Ultra"
        };

        var result = await _tool.ExecuteAsync(args);

        GetStatus(result).Should().Be("error");
    }

    // ── set_preference ─────────────────────────────────────────────

    [Fact]
    public async Task SetPreference_DryRunDefault_ShouldSucceed()
    {
        var args = new Dictionary<string, object?>
        {
            ["action"] = "set_preference",
            ["preferenceName"] = "dryRunDefault",
            ["preferenceValue"] = "true"
        };

        var result = await _tool.ExecuteAsync(args);

        GetStatus(result).Should().Be("success");
    }

    [Fact]
    public async Task SetPreference_CloudEnvironment_ShouldSucceed()
    {
        var args = new Dictionary<string, object?>
        {
            ["action"] = "set_preference",
            ["preferenceName"] = "cloudEnvironment",
            ["preferenceValue"] = "AzureGovernment"
        };

        var result = await _tool.ExecuteAsync(args);

        GetStatus(result).Should().Be("success");
    }

    [Fact]
    public async Task SetPreference_InvalidName_ShouldFail()
    {
        var args = new Dictionary<string, object?>
        {
            ["action"] = "set_preference",
            ["preferenceName"] = "nonExistentPref",
            ["preferenceValue"] = "value"
        };

        var result = await _tool.ExecuteAsync(args);

        GetStatus(result).Should().Be("error");
    }

    [Fact]
    public async Task SetPreference_InvalidValue_ShouldFail()
    {
        var args = new Dictionary<string, object?>
        {
            ["action"] = "set_preference",
            ["preferenceName"] = "cloudEnvironment",
            ["preferenceValue"] = "InvalidCloud"
        };

        var result = await _tool.ExecuteAsync(args);

        GetStatus(result).Should().Be("error");
    }

    [Fact]
    public async Task SetPreference_MissingName_ShouldFail()
    {
        var args = new Dictionary<string, object?>
        {
            ["action"] = "set_preference",
            ["preferenceValue"] = "true"
        };

        var result = await _tool.ExecuteAsync(args);

        GetStatus(result).Should().Be("error");
    }

    // ── invalid action ─────────────────────────────────────────────

    [Fact]
    public async Task InvalidAction_ShouldFail()
    {
        var args = new Dictionary<string, object?>
        {
            ["action"] = "destroy_everything"
        };

        var result = await _tool.ExecuteAsync(args);

        GetStatus(result).Should().Be("error");
    }

    [Fact]
    public async Task MissingAction_ShouldFail()
    {
        var args = new Dictionary<string, object?>();

        var result = await _tool.ExecuteAsync(args);

        GetStatus(result).Should().Be("error");
    }

    // ── state persistence ──────────────────────────────────────────

    [Fact]
    public async Task SetSubscription_ShouldPersistToState()
    {
        var subId = "11111111-1111-1111-1111-111111111111";
        var args = new Dictionary<string, object?>
        {
            ["action"] = "set_subscription",
            ["subscriptionId"] = subId
        };

        await _tool.ExecuteAsync(args);

        _stateMock.Verify(
            s => s.SetStateAsync(
                It.IsAny<string>(),
                It.Is<string>(k => k.Contains("config")),
                It.IsAny<ConfigurationSettings>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SetFramework_ThenGet_ShouldReturnUpdatedValue()
    {
        // Set framework
        await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["action"] = "set_framework",
            ["framework"] = "NIST80053"
        });

        // Get configuration
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["action"] = "get_configuration"
        });

        result.Should().Contain("NIST80053");
    }

    // ── thread safety ──────────────────────────────────────────────

    [Fact]
    public async Task ConcurrentCalls_ShouldNotThrow()
    {
        var tasks = Enumerable.Range(0, 10).Select(i =>
            _tool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["action"] = "get_configuration"
            })).ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().AllSatisfy(r => r.Should().NotBeNullOrEmpty());
    }
}
