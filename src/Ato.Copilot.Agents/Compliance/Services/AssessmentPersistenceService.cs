using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Database persistence service for assessments and findings.
/// Uses <see cref="IDbContextFactory{TContext}"/> for thread-safe context creation
/// with upsert semantics and 24-hour cache for latest assessment.
/// </summary>
public class AssessmentPersistenceService : IAssessmentPersistenceService
{
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AssessmentPersistenceService> _logger;

    private static readonly TimeSpan LatestAssessmentCacheTtl = TimeSpan.FromHours(24);

    /// <summary>
    /// Initializes a new instance of the <see cref="AssessmentPersistenceService"/> class.
    /// </summary>
    /// <param name="dbFactory">EF Core context factory.</param>
    /// <param name="cache">Memory cache for latest assessment.</param>
    /// <param name="logger">Logger instance.</param>
    public AssessmentPersistenceService(
        IDbContextFactory<AtoCopilotContext> dbFactory,
        IMemoryCache cache,
        ILogger<AssessmentPersistenceService> logger)
    {
        _dbFactory = dbFactory;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SaveAssessmentAsync(
        ComplianceAssessment assessment,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var existing = await context.Assessments
                .FirstOrDefaultAsync(a => a.Id == assessment.Id, cancellationToken);

            if (existing is not null)
            {
                // Upsert: update existing assessment
                context.Entry(existing).CurrentValues.SetValues(assessment);

                // Remove old findings and add new ones
                var existingFindings = await context.Findings
                    .Where(f => f.AssessmentId == assessment.Id)
                    .ToListAsync(cancellationToken);
                context.Findings.RemoveRange(existingFindings);
                context.Findings.AddRange(assessment.Findings);

                _logger.LogDebug("Updated existing assessment {Id}", assessment.Id);
            }
            else
            {
                // Insert new assessment
                context.Assessments.Add(assessment);
                _logger.LogDebug("Inserted new assessment {Id}", assessment.Id);
            }

            await context.SaveChangesAsync(cancellationToken);

            // Invalidate latest assessment cache for this subscription
            var cacheKey = $"latest-assessment:{assessment.SubscriptionId}";
            _cache.Set(cacheKey, assessment, LatestAssessmentCacheTtl);

            _logger.LogInformation("Saved assessment {Id} for Sub={Sub} (Score: {Score}%)",
                assessment.Id, assessment.SubscriptionId, assessment.ComplianceScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save assessment {Id}", assessment.Id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ComplianceAssessment?> GetAssessmentAsync(
        string assessmentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
            return await context.Assessments
                .Include(a => a.Findings)
                .FirstOrDefaultAsync(a => a.Id == assessmentId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get assessment {Id}", assessmentId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ComplianceAssessment?> GetLatestAssessmentAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"latest-assessment:{subscriptionId}";

        if (_cache.TryGetValue<ComplianceAssessment>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var latest = await context.Assessments
                .Include(a => a.Findings)
                .Where(a => a.SubscriptionId == subscriptionId)
                .OrderByDescending(a => a.AssessedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (latest is not null)
            {
                _cache.Set(cacheKey, latest, LatestAssessmentCacheTtl);
            }

            return latest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest assessment for Sub={Sub}", subscriptionId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<List<ComplianceAssessment>> GetAssessmentHistoryAsync(
        string subscriptionId,
        int days,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var cutoff = DateTime.UtcNow.AddDays(-days);

            return await context.Assessments
                .Include(a => a.Findings)
                .Where(a => a.SubscriptionId == subscriptionId && a.AssessedAt >= cutoff)
                .OrderByDescending(a => a.AssessedAt)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get assessment history for Sub={Sub}", subscriptionId);
            return new List<ComplianceAssessment>();
        }
    }

    /// <inheritdoc />
    public async Task<ComplianceFinding?> GetFindingAsync(
        string findingId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
            return await context.Findings
                .FirstOrDefaultAsync(f => f.Id == findingId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get finding {Id}", findingId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdateFindingStatusAsync(
        string findingId,
        FindingStatus status,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var finding = await context.Findings
                .FirstOrDefaultAsync(f => f.Id == findingId, cancellationToken);

            if (finding is null)
            {
                _logger.LogWarning("Finding {Id} not found for status update", findingId);
                return false;
            }

            finding.Status = status;

            if (status == FindingStatus.Remediated)
            {
                finding.RemediationTrackingStatus = RemediationTrackingStatus.Completed;
                finding.RemediatedAt = DateTime.UtcNow;
            }
            else if (status == FindingStatus.InProgress)
            {
                finding.RemediationTrackingStatus = RemediationTrackingStatus.InProgress;
            }

            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Updated finding {Id} status to {Status}", findingId, status);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update finding {Id} status", findingId);
            return false;
        }
    }
}
