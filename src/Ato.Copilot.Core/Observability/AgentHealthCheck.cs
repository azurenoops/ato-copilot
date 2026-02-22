using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Auth;

namespace Ato.Copilot.Core.Observability;

/// <summary>
/// Health check for the Compliance Agent subsystem (per FR-045).
/// Verifies that the agent's core services are resolvable from DI and
/// the database is accessible. Returns Healthy, Degraded, or Unhealthy
/// with a description string for the health endpoint JSON response.
/// </summary>
public class AgentHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AgentHealthCheck"/>.
    /// </summary>
    /// <param name="serviceProvider">The application service provider.</param>
    /// <param name="logger">Logger instance.</param>
    public AgentHealthCheck(IServiceProvider serviceProvider, ILogger<AgentHealthCheck> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<string>();

        try
        {
            // Check that core compliance services are resolvable
            using var scope = _serviceProvider.CreateScope();
            var provider = scope.ServiceProvider;

            if (provider.GetService<ICacSessionService>() is null)
                issues.Add("ICacSessionService not registered");

            if (provider.GetService<IPimService>() is null)
                issues.Add("IPimService not registered");

            // Check database accessibility
            try
            {
                var db = provider.GetService<AtoCopilotContext>();
                if (db is not null)
                {
                    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
                    if (!canConnect)
                        issues.Add("Database connection failed");
                }
                else
                {
                    issues.Add("AtoCopilotContext not registered");
                }
            }
            catch (Exception ex)
            {
                issues.Add($"Database check failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check execution failed");
            return HealthCheckResult.Unhealthy(
                "Health check execution failed",
                ex,
                new Dictionary<string, object> { ["error"] = ex.Message });
        }

        if (issues.Count == 0)
        {
            return HealthCheckResult.Healthy("All compliance agent services operational");
        }

        // Core services missing → Unhealthy; DB issues → Degraded
        var hasCoreServiceMissing = issues.Any(i =>
            i.Contains("not registered", StringComparison.OrdinalIgnoreCase));
        var description = string.Join("; ", issues);

        if (hasCoreServiceMissing)
        {
            _logger.LogWarning("Health check: Unhealthy — {Issues}", description);
            return HealthCheckResult.Unhealthy(
                description,
                data: new Dictionary<string, object> { ["issues"] = issues });
        }

        _logger.LogWarning("Health check: Degraded — {Issues}", description);
        return HealthCheckResult.Degraded(
            description,
            data: new Dictionary<string, object> { ["issues"] = issues });
    }
}
