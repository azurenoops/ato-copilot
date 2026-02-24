using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services.Engines.Remediation;

/// <summary>
/// Validates remediation scripts against safe command patterns.
/// Rejects scripts containing destructive commands such as resource deletion,
/// subscription-wide changes, and resource group removal.
/// </summary>
public partial class ScriptSanitizationService : IScriptSanitizationService
{
    private readonly ILogger<ScriptSanitizationService> _logger;

    // Destructive command patterns that should be rejected
    private static readonly (string Pattern, string Description)[] DestructivePatterns =
    {
        // Azure CLI destructive
        (@"\baz\s+group\s+delete\b", "Azure CLI resource group deletion (az group delete)"),
        (@"\baz\s+resource\s+delete\b", "Azure CLI resource deletion (az resource delete)"),
        (@"\baz\s+account\s+clear\b", "Azure CLI account clear"),
        (@"\baz\s+ad\s+app\s+delete\b", "Azure AD application deletion"),
        (@"\baz\s+keyvault\s+purge\b", "Azure Key Vault purge"),
        (@"\baz\s+storage\s+account\s+delete\b", "Azure storage account deletion"),
        (@"\baz\s+vm\s+delete\b", "Azure VM deletion"),
        (@"\baz\s+sql\s+server\s+delete\b", "Azure SQL server deletion"),

        // PowerShell destructive
        (@"\bRemove-AzResourceGroup\b", "PowerShell resource group removal"),
        (@"\bRemove-AzResource\b", "PowerShell resource removal"),
        (@"\bRemove-AzStorageAccount\b", "PowerShell storage account removal"),
        (@"\bRemove-AzVM\b", "PowerShell VM removal"),
        (@"\bRemove-AzKeyVault\b", "PowerShell Key Vault removal"),
        (@"\bRemove-AzSqlServer\b", "PowerShell SQL server removal"),
        (@"\bRemove-AzSubscription\b", "PowerShell subscription removal"),

        // Bicep/Terraform destructive
        (@"\bterraform\s+destroy\b", "Terraform destroy"),
        (@"\btf\s+destroy\b", "Terraform destroy (alias)"),

        // General dangerous patterns
        (@"\b(?:rm|del|rmdir)\s+-[rf]", "Recursive file/directory deletion"),
        (@"\bFormat-Volume\b", "Volume formatting"),
        (@"\bClear-Content\b.*\*", "Wildcard content clearing"),
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptSanitizationService"/> class.
    /// </summary>
    public ScriptSanitizationService(ILogger<ScriptSanitizationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsSafe(string scriptContent)
    {
        if (string.IsNullOrWhiteSpace(scriptContent))
        {
            _logger.LogDebug("Empty script content — treated as safe");
            return true;
        }

        var violations = GetViolations(scriptContent);
        var isSafe = violations.Count == 0;

        if (isSafe)
        {
            _logger.LogDebug("Script passed sanitization check");
        }
        else
        {
            _logger.LogWarning(
                "Script REJECTED — {Count} violation(s): {Violations}",
                violations.Count, string.Join("; ", violations));
        }

        return isSafe;
    }

    /// <inheritdoc />
    public List<string> GetViolations(string scriptContent)
    {
        if (string.IsNullOrWhiteSpace(scriptContent))
            return new List<string>();

        var violations = new List<string>();

        foreach (var (pattern, description) in DestructivePatterns)
        {
            if (Regex.IsMatch(scriptContent, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline))
            {
                violations.Add(description);
                _logger.LogDebug("Detected destructive pattern: {Description}", description);
            }
        }

        return violations;
    }
}
