using Ato.Copilot.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Ato.Copilot.Tests.Unit.Services;

public class OfflineModeServiceTests
{
    [Fact]
    public void IsOffline_ReadsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Server:OfflineMode"] = "true" })
            .Build();

        var service = new OfflineModeService(config, Mock.Of<ILogger<OfflineModeService>>());

        service.IsOffline.Should().BeTrue();
    }

    [Fact]
    public void IsOffline_DefaultsFalseWhenNotConfigured()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var service = new OfflineModeService(config, Mock.Of<ILogger<OfflineModeService>>());

        service.IsOffline.Should().BeFalse();
    }

    [Fact]
    public void GetAvailableCapabilities_ReturnsDeterministicOperations()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Server:OfflineMode"] = "true" })
            .Build();

        var service = new OfflineModeService(config, Mock.Of<ILogger<OfflineModeService>>());
        var available = service.GetAvailableCapabilities();

        available.Should().NotBeEmpty();
        available.Should().OnlyContain(c => !c.RequiresNetwork);
        available.Select(c => c.CapabilityName).Should().Contain("NIST Control Lookups");
        available.Select(c => c.CapabilityName).Should().Contain("Cached Assessments");
        available.Select(c => c.CapabilityName).Should().Contain("Document Generation");
    }

    [Fact]
    public void GetUnavailableCapabilities_ReturnsAiDependentOps()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Server:OfflineMode"] = "true" })
            .Build();

        var service = new OfflineModeService(config, Mock.Of<ILogger<OfflineModeService>>());
        var unavailable = service.GetUnavailableCapabilities();

        unavailable.Should().NotBeEmpty();
        unavailable.Should().OnlyContain(c => c.RequiresNetwork);
        unavailable.Select(c => c.CapabilityName).Should().Contain("AI Chat");
        unavailable.Select(c => c.CapabilityName).Should().Contain("ARM Resource Scan");
        unavailable.Select(c => c.CapabilityName).Should().Contain("Live Assessment");
    }

    [Fact]
    public void OfflineMode_ForcesOfflineFallbackEnabled()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Server:OfflineMode"] = "true" })
            .Build();

        _ = new OfflineModeService(config, Mock.Of<ILogger<OfflineModeService>>());

        config["NistControls:EnableOfflineFallback"].Should().Be("true");
    }

    [Fact]
    public void OnlineMode_DoesNotForceOfflineFallback()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Server:OfflineMode"] = "false" })
            .Build();

        _ = new OfflineModeService(config, Mock.Of<ILogger<OfflineModeService>>());

        config["NistControls:EnableOfflineFallback"].Should().BeNull();
    }
}
