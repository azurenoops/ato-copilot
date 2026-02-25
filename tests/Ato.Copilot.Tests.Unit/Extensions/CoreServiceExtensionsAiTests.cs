using Xunit;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ato.Copilot.Core.Extensions;

namespace Ato.Copilot.Tests.Unit.Extensions;

/// <summary>
/// Unit tests for IChatClient factory registration in CoreServiceExtensions.
/// Verifies conditional registration based on Gateway:AzureOpenAI configuration.
/// </summary>
public class CoreServiceExtensionsAiTests
{
    [Fact]
    public void AddAtoCopilotCore_WhenEndpointConfigured_RegistersIChatClient()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gateway:AzureOpenAI:Endpoint"] = "https://test.openai.azure.us/",
                ["Gateway:AzureOpenAI:ApiKey"] = "test-api-key-12345",
                ["Gateway:AzureOpenAI:ChatDeploymentName"] = "gpt-4o"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAtoCopilotCore(config);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IChatClient));
        descriptor.Should().NotBeNull("IChatClient should be registered when Endpoint is configured");
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddAtoCopilotCore_WhenEndpointEmpty_DoesNotRegisterIChatClient()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gateway:AzureOpenAI:Endpoint"] = "",
                ["Gateway:AzureOpenAI:ApiKey"] = "test-api-key"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAtoCopilotCore(config);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IChatClient));
        descriptor.Should().BeNull("IChatClient should not be registered when Endpoint is empty");
    }

    [Fact]
    public void AddAtoCopilotCore_WhenAzureOpenAISectionMissing_DoesNotRegisterIChatClient()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gateway:Azure:TenantId"] = "some-tenant"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAtoCopilotCore(config);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IChatClient));
        descriptor.Should().BeNull("IChatClient should not be registered when AzureOpenAI section is missing");
    }

    [Fact]
    public void AddAtoCopilotCore_WhenUnconfigured_NoStartupErrors()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        var act = () => services.AddAtoCopilotCore(config);
        act.Should().NotThrow("AddAtoCopilotCore should not throw when Azure OpenAI is unconfigured");
    }

    [Fact]
    public void AddAtoCopilotCore_WithManagedIdentity_RegistersIChatClient()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gateway:AzureOpenAI:Endpoint"] = "https://test.openai.azure.us/",
                ["Gateway:AzureOpenAI:UseManagedIdentity"] = "true",
                ["Gateway:AzureOpenAI:ChatDeploymentName"] = "gpt-4o"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAtoCopilotCore(config);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IChatClient));
        descriptor.Should().NotBeNull("IChatClient should be registered with managed identity");
    }

    [Fact]
    public void AddAtoCopilotCore_WithGovEndpoint_RegistersIChatClient()
    {
        // FR-002/SC-008: Azure Government .us endpoint validation
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gateway:AzureOpenAI:Endpoint"] = "https://my-service.openai.azure.us/",
                ["Gateway:AzureOpenAI:ApiKey"] = "gov-api-key",
                ["Gateway:AzureOpenAI:ChatDeploymentName"] = "gpt-4o",
                ["Gateway:Azure:CloudEnvironment"] = "AzureGovernment"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAtoCopilotCore(config);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IChatClient));
        descriptor.Should().NotBeNull("IChatClient should work with Azure Government .us endpoints");
    }
}
