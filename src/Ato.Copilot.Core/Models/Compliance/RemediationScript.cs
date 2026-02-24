namespace Ato.Copilot.Core.Models.Compliance;

/// <summary>
/// AI-generated or curated remediation script.
/// </summary>
public class RemediationScript
{
    /// <summary>Script content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>AzureCli, PowerShell, Bicep, or Terraform.</summary>
    public ScriptType ScriptType { get; set; }

    /// <summary>What the script does.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Required parameters and descriptions.</summary>
    public Dictionary<string, string> Parameters { get; set; } = new();

    /// <summary>Estimated execution time.</summary>
    public TimeSpan EstimatedDuration { get; set; }

    /// <summary>Whether script passed safety validation.</summary>
    public bool IsSanitized { get; set; }
}
