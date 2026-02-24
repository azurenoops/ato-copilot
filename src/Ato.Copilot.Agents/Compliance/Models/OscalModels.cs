using System.Text.Json.Serialization;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Models;

/// <summary>
/// Root wrapper for the OSCAL JSON catalog document.
/// Maps to: <c>{ "catalog": { ... } }</c>
/// All nested OSCAL types live in <see cref="Ato.Copilot.Core.Models.Compliance"/>.
/// </summary>
public sealed record NistCatalogRoot
{
    /// <summary>The single catalog object in the OSCAL document.</summary>
    [JsonPropertyName("catalog")]
    public NistCatalog Catalog { get; init; } = new();
}
