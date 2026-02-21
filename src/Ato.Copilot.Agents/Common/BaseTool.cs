using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Agents.Common;

/// <summary>
/// Base class for all tools in the ATO Copilot.
/// All tools MUST extend this class (Constitution Principle II).
/// </summary>
public abstract class BaseTool
{
    protected readonly ILogger Logger;

    protected BaseTool(ILogger logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// Unique name of the tool
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Human-readable description of what this tool does
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Parameter definitions for this tool
    /// </summary>
    public abstract IReadOnlyDictionary<string, ToolParameter> Parameters { get; }

    /// <summary>
    /// Execute the tool with the given arguments
    /// </summary>
    public abstract Task<string> ExecuteAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a typed argument value from the arguments dictionary
    /// </summary>
    protected T? GetArg<T>(Dictionary<string, object?> args, string key)
    {
        if (!args.TryGetValue(key, out var value) || value == null)
            return default;

        if (value is T typedValue)
            return typedValue;

        if (value is System.Text.Json.JsonElement jsonElement)
        {
            try { return System.Text.Json.JsonSerializer.Deserialize<T>(jsonElement.GetRawText()); }
            catch { return default; }
        }

        try { return (T)Convert.ChangeType(value, typeof(T)); }
        catch { return default; }
    }
}

/// <summary>
/// Defines a parameter for a tool
/// </summary>
public class ToolParameter
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public bool Required { get; set; }
}
