using System.ComponentModel.DataAnnotations;

namespace Ato.Copilot.Core.Models;

/// <summary>
/// Configuration for a named HTTP client resilience pipeline including
/// retry, circuit breaker, and timeout policies.
/// </summary>
public class ResiliencePipelineConfig
{
    /// <summary>Named HTTP client this pipeline applies to.</summary>
    [Required]
    public string Name { get; set; } = "default";

    /// <summary>Maximum retry attempts for transient failures.</summary>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Base delay in seconds for exponential backoff.</summary>
    [Range(0.1, 60)]
    public double BaseDelaySeconds { get; set; } = 2.0;

    /// <summary>Whether to add jitter to retry delays.</summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>Number of failures to trigger circuit breaker open state.</summary>
    [Range(1, 100)]
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>Window in seconds for counting failures toward the circuit breaker threshold.</summary>
    [Range(1, 300)]
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 30;

    /// <summary>Duration in seconds the circuit stays open before allowing a probe request.</summary>
    [Range(1, 300)]
    public int CircuitBreakerBreakDurationSeconds { get; set; } = 30;

    /// <summary>Per-request timeout in seconds, enforced via CancellationToken.</summary>
    [Range(1, 300)]
    public int RequestTimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Root configuration object for the Resilience appsettings section.
/// </summary>
public class ResilienceOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Resilience";

    /// <summary>Named resilience pipelines for HTTP clients.</summary>
    public List<ResiliencePipelineConfig> Pipelines { get; set; } = [];
}
