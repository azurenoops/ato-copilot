using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Models.Auth;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for SessionCleanupHostedService — T100.
/// Tests session expiration, JIT request expiration, and cleanup scheduling.
/// </summary>
public class SessionCleanupHostedServiceTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AtoCopilotContext _db;

    public SessionCleanupHostedServiceTests()
    {
        var dbName = $"SessionCleanup_{Guid.NewGuid()}";

        var services = new ServiceCollection();
        services.AddDbContext<AtoCopilotContext>(o =>
            o.UseInMemoryDatabase(dbName));
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
        _db = _serviceProvider.GetRequiredService<AtoCopilotContext>();
    }

    private SessionCleanupHostedService CreateService(int intervalMinutes = 5)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agents:Auth:SessionCleanup:IntervalMinutes"] = intervalMinutes.ToString()
            })
            .Build();

        return new SessionCleanupHostedService(
            _serviceProvider,
            Mock.Of<ILogger<SessionCleanupHostedService>>(),
            config);
    }

    [Fact]
    public async Task ExpiresStaleActiveSessions()
    {
        // Arrange — session that expired 1 hour ago
        var id = Guid.NewGuid();
        _db.CacSessions.Add(new CacSession
        {
            Id = id,
            UserId = "user1",
            DisplayName = "User 1",
            Email = "user1@test.com",
            TokenHash = "hash1",
            Status = SessionStatus.Active,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1),
            SessionStart = DateTimeOffset.UtcNow.AddHours(-9)
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var service = CreateService();

        // Act — invoke cleanup directly via reflection (BackgroundService.ExecuteAsync is protected)
        var method = typeof(SessionCleanupHostedService)
            .GetMethod("CleanupAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, [CancellationToken.None])!;

        // Assert — reload to bypass change tracker
        var session = await _db.CacSessions.AsNoTracking().FirstAsync(s => s.Id == id);
        session.Status.Should().Be(SessionStatus.Expired);
    }

    [Fact]
    public async Task DoesNotExpireActiveFutureSessions()
    {
        _db.CacSessions.Add(new CacSession
        {
            Id = Guid.NewGuid(),
            UserId = "user2",
            DisplayName = "User 2",
            Email = "user2@test.com",
            TokenHash = "hash2",
            Status = SessionStatus.Active,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(4),
            SessionStart = DateTimeOffset.UtcNow.AddHours(-4)
        });
        await _db.SaveChangesAsync();

        var service = CreateService();
        var method = typeof(SessionCleanupHostedService)
            .GetMethod("CleanupAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, [CancellationToken.None])!;

        var session = await _db.CacSessions.FirstAsync();
        session.Status.Should().Be(SessionStatus.Active);
    }

    [Fact]
    public async Task ExpiresStaleJitRequests()
    {
        var id = Guid.NewGuid();
        _db.JitRequests.Add(new JitRequestEntity
        {
            Id = id,
            UserId = "user3",
            UserDisplayName = "User 3",
            RoleName = "Contributor",
            Scope = "/subscriptions/default",
            Justification = "Test justification for cleanup",
            RequestType = JitRequestType.PimRoleActivation,
            Status = JitRequestStatus.Active,
            ActivatedAt = DateTimeOffset.UtcNow.AddHours(-10),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-2),
            DurationHours = 8,
            RequestedAt = DateTimeOffset.UtcNow.AddHours(-10)
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var service = CreateService();
        var method = typeof(SessionCleanupHostedService)
            .GetMethod("CleanupAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, [CancellationToken.None])!;

        var request = await _db.JitRequests.AsNoTracking().FirstAsync(r => r.Id == id);
        request.Status.Should().Be(JitRequestStatus.Expired);
    }

    [Fact]
    public async Task DoesNotExpireActiveJitRequestsNotYetExpired()
    {
        _db.JitRequests.Add(new JitRequestEntity
        {
            Id = Guid.NewGuid(),
            UserId = "user4",
            UserDisplayName = "User 4",
            RoleName = "Reader",
            Scope = "/subscriptions/default",
            Justification = "Test justification for non-expired",
            RequestType = JitRequestType.PimRoleActivation,
            Status = JitRequestStatus.Active,
            ActivatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(6),
            DurationHours = 8,
            RequestedAt = DateTimeOffset.UtcNow.AddHours(-2)
        });
        await _db.SaveChangesAsync();

        var service = CreateService();
        var method = typeof(SessionCleanupHostedService)
            .GetMethod("CleanupAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, [CancellationToken.None])!;

        var request = await _db.JitRequests.FirstAsync();
        request.Status.Should().Be(JitRequestStatus.Active);
    }

    [Fact]
    public async Task IgnoresAlreadyExpiredSessions()
    {
        _db.CacSessions.Add(new CacSession
        {
            Id = Guid.NewGuid(),
            UserId = "user5",
            DisplayName = "User 5",
            Email = "user5@test.com",
            TokenHash = "hash5",
            Status = SessionStatus.Expired, // already expired
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-2),
            SessionStart = DateTimeOffset.UtcNow.AddHours(-10)
        });
        await _db.SaveChangesAsync();

        var service = CreateService();
        var method = typeof(SessionCleanupHostedService)
            .GetMethod("CleanupAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, [CancellationToken.None])!;

        // Should still be Expired, not changed
        var session = await _db.CacSessions.FirstAsync();
        session.Status.Should().Be(SessionStatus.Expired);
    }

    [Fact]
    public async Task HandlesMultipleStaleEntitiesInSingleBatch()
    {
        // Add 3 stale sessions and 2 stale JIT requests
        for (int i = 0; i < 3; i++)
        {
            _db.CacSessions.Add(new CacSession
            {
                Id = Guid.NewGuid(),
                UserId = $"batch-user-{i}",
                DisplayName = $"Batch User {i}",
                Email = $"batch{i}@test.com",
                TokenHash = $"hash-batch-{i}",
                Status = SessionStatus.Active,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-i - 1),
                SessionStart = DateTimeOffset.UtcNow.AddHours(-9)
            });
        }
        for (int i = 0; i < 2; i++)
        {
            _db.JitRequests.Add(new JitRequestEntity
            {
                Id = Guid.NewGuid(),
                UserId = $"batch-jit-{i}",
                UserDisplayName = $"Batch JIT {i}",
                RoleName = "Contributor",
                Scope = "/subscriptions/default",
                Justification = "Batch test justification text here",
                RequestType = JitRequestType.PimRoleActivation,
                Status = JitRequestStatus.Active,
                ActivatedAt = DateTimeOffset.UtcNow.AddHours(-10),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-i - 1),
                DurationHours = 8,
                RequestedAt = DateTimeOffset.UtcNow.AddHours(-10)
            });
        }
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var service = CreateService();
        var method = typeof(SessionCleanupHostedService)
            .GetMethod("CleanupAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, [CancellationToken.None])!;

        var sessions = await _db.CacSessions.AsNoTracking().ToListAsync();
        sessions.Should().AllSatisfy(s => s.Status.Should().Be(SessionStatus.Expired));

        var requests = await _db.JitRequests.AsNoTracking().ToListAsync();
        requests.Should().AllSatisfy(r => r.Status.Should().Be(JitRequestStatus.Expired));
    }
}
