using System.ComponentModel.DataAnnotations;

namespace Ato.Copilot.Agents.Compliance.Configuration;

/// <summary>
/// Configuration options for the Compliance Agent
/// </summary>
public class ComplianceAgentOptions
{
    public const string SectionName = "AgentConfiguration:ComplianceAgent";

    public bool Enabled { get; set; } = true;
    public double Temperature { get; set; } = 0.2;
    public int MaxTokens { get; set; } = 4000;
    public bool EnableAutomatedRemediation { get; set; } = true;
    public string DefaultFramework { get; set; } = "NIST80053";
    public string DefaultBaseline { get; set; } = "FedRAMPHigh";

    public DefenderForCloudOptions DefenderForCloud { get; set; } = new();
    public CodeScanningOptions CodeScanning { get; set; } = new();
    public EvidenceOptions Evidence { get; set; } = new();
    public NistControlsOptions NistControls { get; set; } = new();
    public AssessmentPurgeOptions AssessmentPurge { get; set; } = new();
}

public class DefenderForCloudOptions
{
    public bool Enabled { get; set; }
    public bool IncludeSecureScore { get; set; } = true;
    public bool MapToNistControls { get; set; } = true;
    public int CacheDurationMinutes { get; set; } = 60;
    public bool EnableDeduplication { get; set; } = true;
}

public class CodeScanningOptions
{
    public bool EnableSecretsDetection { get; set; } = true;
    public bool EnableDependencyScanning { get; set; } = true;
    public bool EnableStigChecks { get; set; } = true;
    public List<string> SecretPatterns { get; set; } = new() { "API_KEY", "PASSWORD", "SECRET", "TOKEN" };
}

public class EvidenceOptions
{
    public string StorageAccount { get; set; } = string.Empty;
    public string Container { get; set; } = "evidence";
    public int RetentionDays { get; set; } = 2555;
    public bool EnableVersioning { get; set; } = true;
    public bool EnableImmutability { get; set; } = true;
}

/// <summary>
/// Configuration options for the NIST Controls service.
/// Bound from <c>Agents:Compliance:NistControls</c> config section via <c>IOptions&lt;NistControlsOptions&gt;</c>.
/// </summary>
public class NistControlsOptions
{
    /// <summary>OSCAL catalog remote URL.</summary>
    [Required]
    public string BaseUrl { get; set; } = "https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json/NIST_SP-800-53_rev5_catalog.json";

    /// <summary>HTTP request timeout in seconds.</summary>
    [Range(10, 300)]
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Cache TTL in hours.</summary>
    [Range(1, 168)]
    public int CacheDurationHours { get; set; } = 24;

    /// <summary>Polly retry limit.</summary>
    [Range(1, 5)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Polly base delay in seconds for exponential backoff.</summary>
    [Range(1, 60)]
    public int RetryDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Controls whether the embedded OSCAL resource is used as fallback when the remote fetch fails.
    /// When false, the service returns null on remote failure (useful for testing remote-only scenarios).
    /// The embedded resource is always compiled into the assembly regardless of this setting.
    /// </summary>
    public bool EnableOfflineFallback { get; set; } = true;

    /// <summary>Initial warmup delay in seconds after application startup.</summary>
    [Range(5, 60)]
    public int WarmupDelaySeconds { get; set; } = 10;
}

public class AssessmentPurgeOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalHours { get; set; } = 24;
    public int InitialDelayMinutes { get; set; } = 5;
    public int RetentionDays { get; set; } = 90;
}
