namespace Ato.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Provides curated NIST 800-53 remediation steps by control family,
/// regex-based step parsing from guidance text, and skill level mapping.
/// </summary>
public interface INistRemediationStepsService
{
    /// <summary>
    /// Gets curated remediation steps for a specific NIST control.
    /// </summary>
    /// <param name="controlFamily">NIST control family (e.g., AC, AU, CM, SC)</param>
    /// <param name="controlId">Optional specific control ID (e.g., AC-2)</param>
    /// <returns>Ordered list of remediation step descriptions</returns>
    List<string> GetRemediationSteps(string controlFamily, string controlId);

    /// <summary>
    /// Parses remediation steps from free-text guidance using regex patterns
    /// for numbered steps, bulleted lists, and action verb extraction.
    /// </summary>
    /// <param name="guidanceText">Raw remediation guidance text</param>
    /// <returns>Extracted step descriptions</returns>
    List<string> ParseStepsFromGuidance(string guidanceText);

    /// <summary>
    /// Gets the recommended skill level for remediating findings in a control family.
    /// </summary>
    /// <param name="controlFamily">NIST control family (e.g., AC, SC, CP)</param>
    /// <returns>Skill level string (Beginner, Intermediate, Advanced)</returns>
    string GetSkillLevel(string controlFamily);
}
