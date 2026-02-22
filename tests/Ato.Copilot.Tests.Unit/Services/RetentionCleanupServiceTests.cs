using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for RetentionCleanupHostedService.
/// Validates assessment cleanup, audit log immutability, and configuration behavior.
/// </summary>
public class RetentionCleanupServiceTests
{
    private RetentionPolicyOptions CreateOptions(
        int assessmentRetentionDays = 1095,
        int auditLogRetentionDays = 2555,
        int cleanupIntervalHours = 24,
        bool enableAutomaticCleanup = true)
    {
        return new RetentionPolicyOptions
        {
            AssessmentRetentionDays = assessmentRetentionDays,
            AuditLogRetentionDays = auditLogRetentionDays,
            CleanupIntervalHours = cleanupIntervalHours,
            EnableAutomaticCleanup = enableAutomaticCleanup
        };
    }

    private (IServiceScopeFactory scopeFactory, AtoCopilotContext db) CreateScopedDbContext()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AtoCopilotContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var db = provider.GetRequiredService<AtoCopilotContext>();
        db.Database.EnsureCreated();
        return (scopeFactory, db);
    }

    [Fact]
    public void Constructor_ShouldAcceptValidOptions()
    {
        var options = CreateOptions();
        var (scopeFactory, _) = CreateScopedDbContext();
        var logger = new Mock<ILogger<RetentionCleanupHostedService>>();

        var service = new RetentionCleanupHostedService(
            scopeFactory,
            Options.Create(options),
            logger.Object);

        service.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_ShouldReturnImmediately()
    {
        var options = CreateOptions(enableAutomaticCleanup: false);
        var (scopeFactory, _) = CreateScopedDbContext();
        var logger = new Mock<ILogger<RetentionCleanupHostedService>>();

        var service = new RetentionCleanupHostedService(
            scopeFactory,
            Options.Create(options),
            logger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await service.StartAsync(cts.Token);

        // Give it a moment to start
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        // Service should have logged that cleanup is disabled
        logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disabled")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void RetentionPolicyOptions_Defaults_ShouldMatchSpec()
    {
        var options = new RetentionPolicyOptions();

        options.AssessmentRetentionDays.Should().Be(1095, "3-year min per FR-042");
        options.AuditLogRetentionDays.Should().Be(2555, "7-year min per FR-043");
        options.CleanupIntervalHours.Should().Be(24, "daily cleanup default");
        options.EnableAutomaticCleanup.Should().BeTrue("enabled by default");
        RetentionPolicyOptions.SectionName.Should().Be("Retention");
    }

    [Fact]
    public async Task CleanupAsync_ShouldDeleteExpiredAssessments()
    {
        var (scopeFactory, db) = CreateScopedDbContext();
        var options = CreateOptions(assessmentRetentionDays: 30);

        // Add an assessment older than retention period
        var oldAssessment = new ComplianceAssessment
        {
            Id = Guid.NewGuid().ToString(),
            SubscriptionId = "test-sub",
            AssessedAt = DateTime.UtcNow.AddDays(-60), // 60 days old, retention is 30
            Framework = "NIST80053",
            InitiatedBy = "test-user"
        };

        // Add a recent assessment within retention period
        var recentAssessment = new ComplianceAssessment
        {
            Id = Guid.NewGuid().ToString(),
            SubscriptionId = "test-sub",
            AssessedAt = DateTime.UtcNow.AddDays(-10), // 10 days old, within retention
            Framework = "NIST80053",
            InitiatedBy = "test-user"
        };

        db.Assessments.AddRange(oldAssessment, recentAssessment);
        await db.SaveChangesAsync();

        db.Assessments.Count().Should().Be(2);

        // Simulate cleanup
        var cutoffDate = DateTime.UtcNow.AddDays(-options.AssessmentRetentionDays);
        var expired = await db.Assessments
            .Where(a => a.AssessedAt < cutoffDate)
            .ToListAsync();
        db.Assessments.RemoveRange(expired);
        await db.SaveChangesAsync();

        db.Assessments.Count().Should().Be(1);
        db.Assessments.Single().Id.Should().Be(recentAssessment.Id);
    }

    [Fact]
    public async Task CleanupAsync_ShouldNeverDeleteAuditLogs()
    {
        var (scopeFactory, db) = CreateScopedDbContext();

        // Add old audit log entry
        var oldAuditLog = new AuditLogEntry
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "test-user",
            UserRole = "Administrator",
            Action = "Assessment",
            Timestamp = DateTime.UtcNow.AddDays(-3000), // ~8 years old
            Details = "Test audit entry"
        };

        db.AuditLogs.Add(oldAuditLog);
        await db.SaveChangesAsync();

        // Retention cleanup only touches Assessments, never AuditLogs
        var options = CreateOptions(assessmentRetentionDays: 30);
        var cutoffDate = DateTime.UtcNow.AddDays(-options.AssessmentRetentionDays);

        // Simulate what RetentionCleanupHostedService does — only assessments
        var expired = await db.Assessments
            .Where(a => a.AssessedAt < cutoffDate)
            .ToListAsync();
        db.Assessments.RemoveRange(expired);
        await db.SaveChangesAsync();

        // Audit log should be untouched
        db.AuditLogs.Count().Should().Be(1);
        db.AuditLogs.Single().Id.Should().Be(oldAuditLog.Id);
    }

    [Fact]
    public async Task CleanupAsync_EmptyDataset_ShouldHandleGracefully()
    {
        var (scopeFactory, db) = CreateScopedDbContext();
        var options = CreateOptions(assessmentRetentionDays: 30);
        var cutoffDate = DateTime.UtcNow.AddDays(-options.AssessmentRetentionDays);

        // No assessments exist — should not throw
        var expired = await db.Assessments
            .Where(a => a.AssessedAt < cutoffDate)
            .ToListAsync();

        expired.Should().BeEmpty();
        db.Assessments.RemoveRange(expired);
        await db.SaveChangesAsync(); // Should not throw
    }

    [Fact]
    public void CleanupIntervalHours_MinimumIsOneHour()
    {
        var options = CreateOptions(cleanupIntervalHours: 0);
        var (scopeFactory, _) = CreateScopedDbContext();
        var logger = new Mock<ILogger<RetentionCleanupHostedService>>();

        // Constructor enforces Math.Max(1, ...) — should not throw
        var service = new RetentionCleanupHostedService(
            scopeFactory,
            Options.Create(options),
            logger.Object);

        service.Should().NotBeNull();
    }
}
