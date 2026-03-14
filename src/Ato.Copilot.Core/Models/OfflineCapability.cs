using System.ComponentModel.DataAnnotations;

namespace Ato.Copilot.Core.Models;

/// <summary>
/// Descriptor for an operation's offline availability.
/// Used by OfflineModeService to build capability lists for IL6 air-gapped environments.
/// Not persisted to database — registered as a static collection.
/// </summary>
public class OfflineCapability
{
    /// <summary>Human-readable capability name.</summary>
    [Required]
    public string CapabilityName { get; set; } = "";

    /// <summary>Whether this operation requires network connectivity.</summary>
    public bool RequiresNetwork { get; set; }

    /// <summary>Description of what the user can do offline instead.</summary>
    [Required]
    public string FallbackDescription { get; set; } = "";

    /// <summary>Last time this capability's data was synced from online sources. Null if never synced.</summary>
    public DateTimeOffset? LastSyncedAt { get; set; }
}
