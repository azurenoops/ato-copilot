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

public class NistControlsOptions
{
    public string BaseUrl { get; set; } = "https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json";
    public int TimeoutSeconds { get; set; } = 60;
    public int CacheDurationHours { get; set; } = 24;
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;
    public bool EnableOfflineFallback { get; set; } = true;
    public string OfflineFallbackPath { get; set; } = "Data/nist-800-53-fallback.json";
}

public class AssessmentPurgeOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalHours { get; set; } = 24;
    public int InitialDelayMinutes { get; set; } = 5;
    public int RetentionDays { get; set; } = 90;
}
