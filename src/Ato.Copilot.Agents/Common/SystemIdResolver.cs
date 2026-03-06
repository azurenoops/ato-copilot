using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Agents.Common;

/// <summary>
/// Resolves a system identifier (GUID, name, or acronym) to a RegisteredSystem GUID.
/// Registered as a singleton; creates scoped DbContext for each resolution.
/// </summary>
public class SystemIdResolver : ISystemIdResolver
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SystemIdResolver> _logger;

    public SystemIdResolver(
        IServiceScopeFactory scopeFactory,
        ILogger<SystemIdResolver> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> ResolveAsync(string systemIdOrName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(systemIdOrName))
            throw new InvalidOperationException("The 'system_id' parameter is required.");

        var trimmed = systemIdOrName.Trim();

        // Fast path: if it's already a valid GUID, return it as-is
        if (Guid.TryParse(trimmed, out _))
            return trimmed;

        // Slow path: look up by name or acronym (case-insensitive)
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        var normalised = trimmed.ToUpperInvariant();

        var matches = await context.RegisteredSystems
            .Where(s => s.Name.ToUpper() == normalised ||
                        (s.Acronym != null && s.Acronym.ToUpper() == normalised))
            .Select(s => new { s.Id, s.Name, s.Acronym })
            .ToListAsync(cancellationToken);

        if (matches.Count == 0)
        {
            _logger.LogWarning(
                "SystemIdResolver: no system found matching '{Input}'", trimmed);
            throw new InvalidOperationException(
                $"No registered system found matching '{trimmed}'. " +
                "Provide the system GUID, exact system name, or acronym.");
        }

        if (matches.Count > 1)
        {
            var names = string.Join(", ", matches.Select(m => $"{m.Name} ({m.Id})"));
            _logger.LogWarning(
                "SystemIdResolver: ambiguous input '{Input}' matched {Count} systems: {Names}",
                trimmed, matches.Count, names);
            throw new InvalidOperationException(
                $"'{trimmed}' matches {matches.Count} systems: {names}. " +
                "Please provide the system GUID or a more specific name.");
        }

        var resolved = matches[0];
        _logger.LogInformation(
            "SystemIdResolver: resolved '{Input}' → {SystemId} ({SystemName})",
            trimmed, resolved.Id, resolved.Name);

        return resolved.Id;
    }
}
