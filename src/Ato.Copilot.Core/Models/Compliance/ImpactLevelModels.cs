namespace Ato.Copilot.Core.Models.Compliance;

// ───────────────────────────── Impact Level Entities (Feature 010) ─────────────────────────────

/// <summary>
/// Represents a DoD Impact Level (IL2-IL6) or FedRAMP baseline with security requirements.
/// </summary>
/// <param name="Level">Level identifier (e.g., "IL5", "FedRAMP-High").</param>
/// <param name="Name">Display name.</param>
/// <param name="DataClassification">Data classification description.</param>
/// <param name="SecurityRequirements">Encryption, network, personnel, and physical security details.</param>
/// <param name="AzureImplementation">Azure-specific implementation guidance.</param>
/// <param name="AdditionalControls">Extra controls beyond baseline.</param>
public record ImpactLevel(
    string Level,
    string Name,
    string DataClassification,
    SecurityRequirements SecurityRequirements,
    AzureImpactGuidance AzureImplementation,
    List<string> AdditionalControls);

/// <summary>
/// Security requirement details for an impact level.
/// </summary>
/// <param name="Encryption">Encryption requirements (e.g., "FIPS 140-2 Level 1").</param>
/// <param name="Network">Network boundary requirements.</param>
/// <param name="Personnel">Personnel security/clearance requirements.</param>
/// <param name="PhysicalSecurity">Physical security requirements.</param>
public record SecurityRequirements(
    string Encryption,
    string Network,
    string Personnel,
    string PhysicalSecurity);

/// <summary>
/// Azure-specific implementation guidance for an impact level.
/// </summary>
/// <param name="Region">Required Azure region (e.g., "Azure Government", "Gov Secret").</param>
/// <param name="Network">Azure network configuration guidance.</param>
/// <param name="Identity">Azure identity/access guidance.</param>
/// <param name="Encryption">Azure encryption configuration guidance.</param>
/// <param name="Services">Recommended Azure services.</param>
public record AzureImpactGuidance(
    string Region,
    string Network,
    string Identity,
    string Encryption,
    List<string> Services);
