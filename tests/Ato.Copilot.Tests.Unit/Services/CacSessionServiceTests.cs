using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Models.Auth;

namespace Ato.Copilot.Tests.Unit.Services;

public class CacSessionServiceTests
{
    private readonly CacSessionService _service;
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;

    public CacSessionServiceTests()
    {
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"CacSessionTests_{Guid.NewGuid()}")
            .Options;
        _dbFactory = new TestDbContextFactory(options);

        _service = new CacSessionService(
            _dbFactory,
            Options.Create(new CacAuthOptions()),
            Options.Create(new PimServiceOptions()),
            Mock.Of<ILogger<CacSessionService>>());
    }

    [Fact]
    public async Task CreateSessionAsync_ShouldCreateActiveSession()
    {
        var session = await _service.CreateSessionAsync(
            "user-1", "Jane Smith", "jane@agency.mil",
            CacSessionService.ComputeTokenHash("test-token"),
            ClientType.VSCode, "10.0.0.1");

        session.Should().NotBeNull();
        session.UserId.Should().Be("user-1");
        session.DisplayName.Should().Be("Jane Smith");
        session.Email.Should().Be("jane@agency.mil");
        session.Status.Should().Be(SessionStatus.Active);
        session.ClientType.Should().Be(ClientType.VSCode);
        session.IpAddress.Should().Be("10.0.0.1");
    }

    [Fact]
    public async Task CreateSessionAsync_ShouldHashTokenAsSHA256()
    {
        var hash = CacSessionService.ComputeTokenHash("test-token");

        hash.Should().HaveLength(64, because: "SHA-256 produces 64 hex characters");
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task CreateSessionAsync_ShouldSetDefault8hTimeout()
    {
        var before = DateTimeOffset.UtcNow;

        var session = await _service.CreateSessionAsync(
            "user-2", "Test User", "test@agency.mil",
            CacSessionService.ComputeTokenHash("token-2"),
            ClientType.Web, "10.0.0.2");

        var expectedExpiry = before.AddHours(8);
        session.ExpiresAt.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateSessionAsync_ShouldTerminateExistingActiveSessions()
    {
        // Create first session
        var first = await _service.CreateSessionAsync(
            "user-3", "Test User", "test@agency.mil",
            CacSessionService.ComputeTokenHash("token-a"),
            ClientType.VSCode, "10.0.0.1");

        // Create second session (should terminate first)
        var second = await _service.CreateSessionAsync(
            "user-3", "Test User", "test@agency.mil",
            CacSessionService.ComputeTokenHash("token-b"),
            ClientType.Web, "10.0.0.2");

        // Verify first session is terminated
        await using var db = _dbFactory.CreateDbContext();
        var firstFromDb = await db.CacSessions.FindAsync(first.Id);
        firstFromDb!.Status.Should().Be(SessionStatus.Terminated);

        // Second session should be active
        second.Status.Should().Be(SessionStatus.Active);
    }

    [Fact]
    public async Task GetActiveSessionAsync_ShouldReturnActiveSession()
    {
        await _service.CreateSessionAsync(
            "user-4", "Test User", "test@agency.mil",
            CacSessionService.ComputeTokenHash("token-4"),
            ClientType.CLI, "10.0.0.4");

        var session = await _service.GetActiveSessionAsync("user-4");

        session.Should().NotBeNull();
        session!.UserId.Should().Be("user-4");
        session.Status.Should().Be(SessionStatus.Active);
    }

    [Fact]
    public async Task GetActiveSessionAsync_ShouldReturnNullWhenNoSession()
    {
        var session = await _service.GetActiveSessionAsync("nonexistent-user");

        session.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveSessionAsync_ShouldDetectExpiredSession()
    {
        // Create a session, then manually expire it
        var created = await _service.CreateSessionAsync(
            "user-5", "Test User", "test@agency.mil",
            CacSessionService.ComputeTokenHash("token-5"),
            ClientType.VSCode, "10.0.0.5");

        // Manually set ExpiresAt to the past
        await using (var db = _dbFactory.CreateDbContext())
        {
            var session = await db.CacSessions.FindAsync(created.Id);
            session!.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        // GetActiveSession should detect expired and return null
        var result = await _service.GetActiveSessionAsync("user-5");
        result.Should().BeNull();

        // Verify the session was marked as Expired (lazy cleanup)
        await using (var db = _dbFactory.CreateDbContext())
        {
            var session = await db.CacSessions.FindAsync(created.Id);
            session!.Status.Should().Be(SessionStatus.Expired);
        }
    }

    [Fact]
    public async Task TerminateSessionAsync_ShouldSetStatusTerminated()
    {
        var session = await _service.CreateSessionAsync(
            "user-6", "Test User", "test@agency.mil",
            CacSessionService.ComputeTokenHash("token-6"),
            ClientType.Teams, "10.0.0.6");

        await _service.TerminateSessionAsync(session.Id);

        await using var db = _dbFactory.CreateDbContext();
        var terminated = await db.CacSessions.FindAsync(session.Id);
        terminated!.Status.Should().Be(SessionStatus.Terminated);
    }

    [Fact]
    public async Task IsSessionActiveAsync_ShouldReturnTrueForActiveSession()
    {
        await _service.CreateSessionAsync(
            "user-7", "Test User", "test@agency.mil",
            CacSessionService.ComputeTokenHash("token-7"),
            ClientType.VSCode, "10.0.0.7");

        var isActive = await _service.IsSessionActiveAsync("user-7");
        isActive.Should().BeTrue();
    }

    [Fact]
    public async Task IsSessionActiveAsync_ShouldReturnFalseForNoSession()
    {
        var isActive = await _service.IsSessionActiveAsync("no-session-user");
        isActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetSessionsByUserAsync_ShouldReturnAllSessions()
    {
        // Create two sessions (second terminates first)
        await _service.CreateSessionAsync(
            "user-8", "Test User", "test@agency.mil",
            CacSessionService.ComputeTokenHash("token-8a"),
            ClientType.VSCode, "10.0.0.8");

        await _service.CreateSessionAsync(
            "user-8", "Test User", "test@agency.mil",
            CacSessionService.ComputeTokenHash("token-8b"),
            ClientType.Web, "10.0.0.8");

        var sessions = await _service.GetSessionsByUserAsync("user-8");
        sessions.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateTimeoutAsync_ShouldUpdateExpiresAt()
    {
        var session = await _service.CreateSessionAsync(
            "user-9", "Test User", "test@agency.mil",
            CacSessionService.ComputeTokenHash("token-9"),
            ClientType.VSCode, "10.0.0.9");

        var updated = await _service.UpdateTimeoutAsync(session.Id, 4);

        updated.ExpiresAt.Should().BeCloseTo(
            session.SessionStart.AddHours(4), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateTimeoutAsync_ShouldRejectBelowMinimum()
    {
        var session = await _service.CreateSessionAsync(
            "user-10", "Test User", "test@agency.mil",
            CacSessionService.ComputeTokenHash("token-10"),
            ClientType.VSCode, "10.0.0.10");

        var act = () => _service.UpdateTimeoutAsync(session.Id, 0);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task UpdateTimeoutAsync_ShouldRejectAboveMaximum()
    {
        var session = await _service.CreateSessionAsync(
            "user-11", "Test User", "test@agency.mil",
            CacSessionService.ComputeTokenHash("token-11"),
            ClientType.VSCode, "10.0.0.11");

        var act = () => _service.UpdateTimeoutAsync(session.Id, 25);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task UpdateTimeoutAsync_ShouldReturnPreviousAndNewExpiration()
    {
        var session = await _service.CreateSessionAsync(
            "user-prev-new", "Test User", "test@agency.mil",
            CacSessionService.ComputeTokenHash("token-prev-new"),
            ClientType.VSCode, "10.0.0.12");

        var previousExpiry = session.ExpiresAt;
        var updated = await _service.UpdateTimeoutAsync(session.Id, 12);

        // Previous should have been 8 hours from SessionStart
        previousExpiry.Should().BeCloseTo(session.SessionStart.AddHours(8), TimeSpan.FromSeconds(5));
        // New should be 12 hours from SessionStart
        updated.ExpiresAt.Should().BeCloseTo(session.SessionStart.AddHours(12), TimeSpan.FromSeconds(5));
        // They should be different
        updated.ExpiresAt.Should().NotBe(previousExpiry);
    }

    [Fact]
    public async Task UpdateTimeoutAsync_InactiveSession_ShouldThrow()
    {
        var session = await _service.CreateSessionAsync(
            "user-inactive-timeout", "Test User", "test@agency.mil",
            CacSessionService.ComputeTokenHash("token-inactive"),
            ClientType.VSCode, "10.0.0.13");

        // Terminate the session first
        await _service.TerminateSessionAsync(session.Id);

        var act = () => _service.UpdateTimeoutAsync(session.Id, 4);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void ComputeTokenHash_ShouldBeConsistent()
    {
        var hash1 = CacSessionService.ComputeTokenHash("consistent-token");
        var hash2 = CacSessionService.ComputeTokenHash("consistent-token");

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeTokenHash_DifferentTokens_ShouldProduceDifferentHashes()
    {
        var hash1 = CacSessionService.ComputeTokenHash("token-a");
        var hash2 = CacSessionService.ComputeTokenHash("token-b");

        hash1.Should().NotBe(hash2);
    }

    // ─── Expiration Warning Tests (T089) ─────────────────────────────────────

    [Fact]
    public async Task GetExpirationWarnings_SessionNearExpiry_ShouldReturnWarning()
    {
        // Create a session that expires in 10 minutes (within 15-min warning threshold)
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"ExpiryWarn_{Guid.NewGuid()}")
            .Options;
        var dbFactory = new TestDbContextFactory(options);

        await using (var db = dbFactory.CreateDbContext())
        {
            db.CacSessions.Add(new CacSession
            {
                UserId = "user-expiry-1",
                DisplayName = "Jane Smith",
                Email = "jane@test.gov",
                TokenHash = CacSessionService.ComputeTokenHash("expiry-token"),
                SessionStart = DateTimeOffset.UtcNow.AddHours(-7).AddMinutes(-50),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                Status = SessionStatus.Active
            });
            await db.SaveChangesAsync();
        }

        var svc = new CacSessionService(
            dbFactory,
            Options.Create(new CacAuthOptions()),
            Options.Create(new PimServiceOptions { SessionExpirationWarningMinutes = 15 }),
            Mock.Of<ILogger<CacSessionService>>());

        var warnings = await svc.GetExpirationWarningsAsync("user-expiry-1");

        warnings.Should().ContainSingle(w => w.Contains("CAC session expires"));
    }

    [Fact]
    public async Task GetExpirationWarnings_SessionNotNearExpiry_ShouldReturnEmpty()
    {
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"ExpiryNoWarn_{Guid.NewGuid()}")
            .Options;
        var dbFactory = new TestDbContextFactory(options);

        await using (var db = dbFactory.CreateDbContext())
        {
            db.CacSessions.Add(new CacSession
            {
                UserId = "user-expiry-2",
                DisplayName = "Jane Smith",
                Email = "jane@test.gov",
                TokenHash = CacSessionService.ComputeTokenHash("no-expiry-token"),
                SessionStart = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(4),
                Status = SessionStatus.Active
            });
            await db.SaveChangesAsync();
        }

        var svc = new CacSessionService(
            dbFactory,
            Options.Create(new CacAuthOptions()),
            Options.Create(new PimServiceOptions { SessionExpirationWarningMinutes = 15 }),
            Mock.Of<ILogger<CacSessionService>>());

        var warnings = await svc.GetExpirationWarningsAsync("user-expiry-2");

        warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task GetExpirationWarnings_PimRoleNearExpiry_ShouldReturnWarning()
    {
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"PimExpiryWarn_{Guid.NewGuid()}")
            .Options;
        var dbFactory = new TestDbContextFactory(options);

        await using (var db = dbFactory.CreateDbContext())
        {
            // Session not near expiry
            db.CacSessions.Add(new CacSession
            {
                UserId = "user-pim-expiry",
                DisplayName = "Jane Smith",
                Email = "jane@test.gov",
                TokenHash = CacSessionService.ComputeTokenHash("pim-expiry-token"),
                SessionStart = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(4),
                Status = SessionStatus.Active
            });
            // PIM role expiring in 5 minutes
            db.JitRequests.Add(new JitRequestEntity
            {
                UserId = "user-pim-expiry",
                UserDisplayName = "Jane Smith",
                RoleName = "Contributor",
                Scope = "/subscriptions/default",
                RequestType = JitRequestType.PimRoleActivation,
                Status = JitRequestStatus.Active,
                Justification = "Test PIM near expiry",
                ActivatedAt = DateTimeOffset.UtcNow.AddHours(-4),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                RequestedAt = DateTimeOffset.UtcNow.AddHours(-4)
            });
            await db.SaveChangesAsync();
        }

        var svc = new CacSessionService(
            dbFactory,
            Options.Create(new CacAuthOptions()),
            Options.Create(new PimServiceOptions { SessionExpirationWarningMinutes = 15 }),
            Mock.Of<ILogger<CacSessionService>>());

        var warnings = await svc.GetExpirationWarningsAsync("user-pim-expiry");

        warnings.Should().ContainSingle(w => w.Contains("PIM role") && w.Contains("Contributor"));
    }

    private class TestDbContextFactory : IDbContextFactory<AtoCopilotContext>
    {
        private readonly DbContextOptions<AtoCopilotContext> _options;
        public TestDbContextFactory(DbContextOptions<AtoCopilotContext> options) => _options = options;
        public AtoCopilotContext CreateDbContext() => new(_options);
    }
}
