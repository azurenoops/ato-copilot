using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ato.Copilot.Core.Configuration;

namespace Ato.Copilot.Tests.Unit.Configuration;

/// <summary>
/// Unit tests for AzureOpenAIGatewayOptions — verifies default values,
/// configuration binding, and new Feature 011 properties.
/// </summary>
public class AzureOpenAIGatewayOptionsTests
{
    [Fact]
    public void DefaultValues_AgentAIEnabled_IsFalse()
    {
        var options = new AzureOpenAIGatewayOptions();
        options.AgentAIEnabled.Should().BeFalse();
    }

    [Fact]
    public void DefaultValues_MaxToolCallRounds_Is5()
    {
        var options = new AzureOpenAIGatewayOptions();
        options.MaxToolCallRounds.Should().Be(5);
    }

    [Fact]
    public void DefaultValues_Temperature_Is03()
    {
        var options = new AzureOpenAIGatewayOptions();
        options.Temperature.Should().Be(0.3);
    }

    [Fact]
    public void DefaultValues_ExistingProperties_HaveExpectedDefaults()
    {
        var options = new AzureOpenAIGatewayOptions();

        options.ApiKey.Should().BeEmpty();
        options.Endpoint.Should().BeEmpty();
        options.DeploymentName.Should().Be("gpt-4o");
        options.UseManagedIdentity.Should().BeFalse();
        options.ChatDeploymentName.Should().Be("gpt-4o");
        options.EmbeddingDeploymentName.Should().Be("text-embedding-ada-002");
    }

    [Fact]
    public void Binding_FromConfigSection_BindsAllProperties()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gateway:AzureOpenAI:Endpoint"] = "https://test.openai.azure.us/",
                ["Gateway:AzureOpenAI:ApiKey"] = "test-key-123",
                ["Gateway:AzureOpenAI:ChatDeploymentName"] = "gpt-4o-custom",
                ["Gateway:AzureOpenAI:EmbeddingDeploymentName"] = "text-embedding-3-large",
                ["Gateway:AzureOpenAI:UseManagedIdentity"] = "true",
                ["Gateway:AzureOpenAI:AgentAIEnabled"] = "true",
                ["Gateway:AzureOpenAI:MaxToolCallRounds"] = "10",
                ["Gateway:AzureOpenAI:Temperature"] = "0.7"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<AzureOpenAIGatewayOptions>(config.GetSection("Gateway:AzureOpenAI"));
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<AzureOpenAIGatewayOptions>>().Value;

        options.Endpoint.Should().Be("https://test.openai.azure.us/");
        options.ApiKey.Should().Be("test-key-123");
        options.ChatDeploymentName.Should().Be("gpt-4o-custom");
        options.EmbeddingDeploymentName.Should().Be("text-embedding-3-large");
        options.UseManagedIdentity.Should().BeTrue();
        options.AgentAIEnabled.Should().BeTrue();
        options.MaxToolCallRounds.Should().Be(10);
        options.Temperature.Should().Be(0.7);
    }

    [Fact]
    public void Binding_MissingSection_UsesDefaults()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.Configure<AzureOpenAIGatewayOptions>(config.GetSection("Gateway:AzureOpenAI"));
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<AzureOpenAIGatewayOptions>>().Value;

        options.AgentAIEnabled.Should().BeFalse();
        options.MaxToolCallRounds.Should().Be(5);
        options.Temperature.Should().Be(0.3);
        options.Endpoint.Should().BeEmpty();
    }

    [Fact]
    public void Binding_PartialConfig_MergesWithDefaults()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gateway:AzureOpenAI:AgentAIEnabled"] = "true",
                ["Gateway:AzureOpenAI:Endpoint"] = "https://my-gov.openai.azure.us/"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<AzureOpenAIGatewayOptions>(config.GetSection("Gateway:AzureOpenAI"));
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<AzureOpenAIGatewayOptions>>().Value;

        options.AgentAIEnabled.Should().BeTrue();
        options.Endpoint.Should().Be("https://my-gov.openai.azure.us/");
        // Defaults preserved
        options.MaxToolCallRounds.Should().Be(5);
        options.Temperature.Should().Be(0.3);
        options.ChatDeploymentName.Should().Be("gpt-4o");
    }

    [Fact]
    public void GatewayOptions_ContainsAzureOpenAIProperty()
    {
        var gateway = new GatewayOptions();
        gateway.AzureOpenAI.Should().NotBeNull();
        gateway.AzureOpenAI.Should().BeOfType<AzureOpenAIGatewayOptions>();
    }
}
