using Serilog.Core;
using Serilog.Events;

namespace Ato.Copilot.Core.Observability;

/// <summary>
/// Serilog destructuring policy that scrubs sensitive data from structured log properties.
/// Prevents credential leakage in log output by replacing matched property values with "[REDACTED]".
/// Matches: Bearer tokens, ClientSecret, ConnectionString, AccessToken, RefreshToken,
/// Password, ApiKey, and Authorization headers (per FR-037).
/// </summary>
public class SensitiveDataDestructuringPolicy : IDestructuringPolicy
{
    /// <summary>Property names containing sensitive data that must be redacted.</summary>
    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ClientSecret",
        "ConnectionString",
        "AccessToken",
        "RefreshToken",
        "Password",
        "ApiKey",
        "Authorization",
        "BearerToken",
        "Token",
        "Secret",
        "Credential"
    };

    /// <summary>Redaction placeholder for sensitive values.</summary>
    private const string RedactedValue = "[REDACTED]";

    /// <inheritdoc />
    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue? result)
    {
        result = null;

        if (value is not Dictionary<string, object?> dict)
            return false;

        var properties = new List<LogEventProperty>();
        foreach (var kvp in dict)
        {
            if (IsSensitiveProperty(kvp.Key))
            {
                properties.Add(new LogEventProperty(kvp.Key, new ScalarValue(RedactedValue)));
            }
            else
            {
                properties.Add(new LogEventProperty(kvp.Key,
                    propertyValueFactory.CreatePropertyValue(kvp.Value, destructureObjects: true)));
            }
        }

        result = new StructureValue(properties);
        return true;
    }

    /// <summary>
    /// Determines whether a property name refers to sensitive data that must be redacted.
    /// Checks both exact matches and partial patterns (e.g., properties ending in "Secret" or "Token").
    /// </summary>
    /// <param name="propertyName">The property name to check.</param>
    /// <returns>True if the property contains sensitive data.</returns>
    public static bool IsSensitiveProperty(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return false;

        // Exact match
        if (SensitivePropertyNames.Contains(propertyName))
            return true;

        // Partial match: property name ends with or contains a sensitive term
        foreach (var sensitive in SensitivePropertyNames)
        {
            if (propertyName.EndsWith(sensitive, StringComparison.OrdinalIgnoreCase) ||
                propertyName.StartsWith(sensitive, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
