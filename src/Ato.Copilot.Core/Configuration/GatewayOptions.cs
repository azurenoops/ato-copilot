namespace Ato.Copilot.Core.Configuration;

/// <summary>
/// Azure AD authentication configuration
/// </summary>
public class AzureAdOptions
{
    public const string SectionName = "AzureAd";

    public string Instance { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Authority => $"{Instance}{TenantId}/v2.0";
    public bool RequireMfa { get; set; }
    public bool RequireCac { get; set; }
    public List<string> ValidIssuers { get; set; } = new();
    public bool EnableUserTokenPassthrough { get; set; }
}

/// <summary>
/// Gateway connection configuration for Azure, OpenAI, and GitHub
/// </summary>
public class GatewayOptions
{
    public const string SectionName = "Gateway";

    public AzureGatewayOptions Azure { get; set; } = new();
    public AzureOpenAIGatewayOptions AzureOpenAI { get; set; } = new();
    public GitHubGatewayOptions GitHub { get; set; } = new();
    public int ConnectionTimeoutSeconds { get; set; } = 60;
    public int RequestTimeoutSeconds { get; set; } = 300;
}

public class AzureGatewayOptions
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public bool UseManagedIdentity { get; set; }
    public string CloudEnvironment { get; set; } = "AzureGovernment";
    public bool Enabled { get; set; } = true;
    public bool EnableUserTokenPassthrough { get; set; }
}

public class AzureOpenAIGatewayOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = "gpt-4o";
    public bool UseManagedIdentity { get; set; }
    public string ChatDeploymentName { get; set; } = "gpt-4o";
    public string EmbeddingDeploymentName { get; set; } = "text-embedding-ada-002";
}

public class GitHubGatewayOptions
{
    public string AccessToken { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://api.github.com";
    public string DefaultOwner { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}
