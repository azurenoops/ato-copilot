using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ato.Copilot.Core.Configuration;

namespace Ato.Copilot.Tests.Unit.Configuration;

/// <summary>
/// Unit tests for AzureAiOptions — verifies default values,
/// configuration binding, computed properties, and AiProvider enum.
/// </summary>
public class AzureAiOptionsTests
{
    [Fact]
    public void DefaultValues_Enabled_IsFalse()
    {
        var options = new AzureAiOptions();
        options.Enabled.Should().BeFalse();
    }

    [Fact]
    public void DefaultValues_MaxToolIterations_Is10()
    {
        var options = new AzureAiOptions();
        options.MaxToolIterations.Should().Be(10);
    }

    [Fact]
    public void DefaultValues_Temperature_Is03()
    {
        var options = new AzureAiOptions();
        options.Temperature.Should().Be(0.3);
    }

    [Fact]
    public void DefaultValues_HaveExpectedDefaults()
    {
        var options = new AzureAiOptions();

        options.Endpoint.Should().BeEmpty();
        options.DeploymentName.Should().Be("gpt-4o");
        options.UseManagedIdentity.Should().BeTrue();
        options.CloudEnvironment.Should().Be("AzurePublicCloud");
        options.MaxCompletionTokens.Should().Be(4096);
        options.ConversationWindowSize.Should().Be(20);
        options.RunTimeoutSeconds.Should().Be(60);
        options.Provider.Should().Be(AiProvider.OpenAi);
    }

    [Fact]
    public void Binding_FromConfigSection_BindsAllProperties()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAi:Enabled"] = "true",
                ["AzureAi:Provider"] = "Foundry",
                ["AzureAi:Endpoint"] = "https://test.openai.azure.us/",
                ["AzureAi:DeploymentName"] = "gpt-4o-custom",
                ["AzureAi:ApiKey"] = "test-key-123",
                ["AzureAi:UseManagedIdentity"] = "false",
                ["AzureAi:CloudEnvironment"] = "AzureGovernment",
                ["AzureAi:MaxToolIterations"] = "15",
                ["AzureAi:Temperature"] = "0.7",
                ["AzureAi:FoundryProjectEndpoint"] = "https://foundry.azure.us/proj",
                ["AzureAi:RunTimeoutSeconds"] = "120"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<AzureAiOptions>(config.GetSection("AzureAi"));
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<AzureAiOptions>>().Value;

        options.Enabled.Should().BeTrue();
        options.Provider.Should().Be(AiProvider.Foundry);
        options.Endpoint.Should().Be("https://test.openai.azure.us/");
        options.DeploymentName.Should().Be("gpt-4o-custom");
        options.ApiKey.Should().Be("test-key-123");
        options.UseManagedIdentity.Should().BeFalse();
        options.CloudEnvironment.Should().Be("AzureGovernment");
        options.MaxToolIterations.Should().Be(15);
        options.Temperature.Should().Be(0.7);
        options.FoundryProjectEndpoint.Should().Be("https://foundry.azure.us/proj");
        options.RunTimeoutSeconds.Should().Be(120);
    }

    [Fact]
    public void Binding_MissingSection_UsesDefaults()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.Configure<AzureAiOptions>(config.GetSection("AzureAi"));
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<AzureAiOptions>>().Value;

        options.Enabled.Should().BeFalse();
        options.MaxToolIterations.Should().Be(10);
        options.Temperature.Should().Be(0.3);
        options.Endpoint.Should().BeEmpty();
    }

    [Fact]
    public void Binding_PartialConfig_MergesWithDefaults()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAi:Enabled"] = "true",
                ["AzureAi:Endpoint"] = "https://my-gov.openai.azure.us/"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<AzureAiOptions>(config.GetSection("AzureAi"));
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<AzureAiOptions>>().Value;

        options.Enabled.Should().BeTrue();
        options.Endpoint.Should().Be("https://my-gov.openai.azure.us/");
        // Defaults preserved
        options.MaxToolIterations.Should().Be(10);
        options.Temperature.Should().Be(0.3);
        options.DeploymentName.Should().Be("gpt-4o");
    }

    [Fact]
    public void IsConfigured_WhenEnabledAndEndpointSet_ReturnsTrue()
    {
        var options = new AzureAiOptions { Enabled = true, Endpoint = "https://test.openai.azure.us/" };
        options.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public void IsConfigured_WhenDisabled_ReturnsFalse()
    {
        var options = new AzureAiOptions { Enabled = false, Endpoint = "https://test.openai.azure.us/" };
        options.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void IsFoundry_WhenProviderFoundryAndEndpointSet_ReturnsTrue()
    {
        var options = new AzureAiOptions
        {
            Provider = AiProvider.Foundry,
            FoundryProjectEndpoint = "https://foundry.azure.us/proj"
        };
        options.IsFoundry.Should().BeTrue();
    }

    [Fact]
    public void IsFoundry_WhenProviderOpenAi_ReturnsFalse()
    {
        var options = new AzureAiOptions
        {
            Provider = AiProvider.OpenAi,
            FoundryProjectEndpoint = "https://foundry.azure.us/proj"
        };
        options.IsFoundry.Should().BeFalse();
    }
}
