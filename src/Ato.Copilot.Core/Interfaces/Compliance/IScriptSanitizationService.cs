namespace Ato.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Validates remediation scripts against safe command patterns.
/// Rejects scripts containing destructive commands (resource deletion,
/// subscription-wide changes) before execution.
/// </summary>
public interface IScriptSanitizationService
{
    /// <summary>
    /// Checks whether a script is safe to execute.
    /// </summary>
    /// <param name="scriptContent">Script content to validate</param>
    /// <returns>True if the script passes all safety checks</returns>
    bool IsSafe(string scriptContent);

    /// <summary>
    /// Gets the list of specific safety violations found in a script.
    /// </summary>
    /// <param name="scriptContent">Script content to validate</param>
    /// <returns>List of violation descriptions, empty if script is safe</returns>
    List<string> GetViolations(string scriptContent);
}
