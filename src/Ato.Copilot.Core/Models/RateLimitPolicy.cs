using System.ComponentModel.DataAnnotations;

namespace Ato.Copilot.Core.Models;

/// <summary>
/// Configuration for a named endpoint rate limit policy using sliding window algorithm.
/// </summary>
public class RateLimitPolicy
{
    /// <summary>Named policy identifier (e.g., "chat", "stream", "jsonrpc").</summary>
    [Required]
    public string PolicyName { get; set; } = "";

    /// <summary>Route pattern this policy applies to (e.g., "/mcp/chat").</summary>
    [Required]
    public string Endpoint { get; set; } = "";

    /// <summary>Maximum requests permitted per sliding window.</summary>
    [Range(1, 10000)]
    public int PermitLimit { get; set; } = 30;

    /// <summary>Sliding window duration in seconds.</summary>
    [Range(1, 3600)]
    public int WindowSeconds { get; set; } = 60;

    /// <summary>Number of segments in the sliding window.</summary>
    [Range(1, 10)]
    public int SegmentsPerWindow { get; set; } = 2;
}

/// <summary>
/// Root configuration object for the RateLimiting appsettings section.
/// </summary>
public class RateLimitingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "RateLimiting";

    /// <summary>Per-endpoint rate limit policies. Defaults defined in appsettings.json.</summary>
    public List<RateLimitPolicy> Policies { get; set; } = [];

    /// <summary>Endpoints exempt from rate limiting.</summary>
    public List<string> ExemptEndpoints { get; set; } = ["/health", "/mcp/tools"];
}
