using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Auth;
using Ato.Copilot.Core.Models.Auth;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for JitVmAccessService — T093.
/// Tests JIT VM access request, session listing, revocation, and error handling.
/// </summary>
public class JitVmAccessServiceTests
{
    private readonly IJitVmAccessService _service;
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;
    private readonly PimServiceOptions _options;

    public JitVmAccessServiceTests()
    {
        _options = new PimServiceOptions
        {
            MinJustificationLength = 20,
            RequireTicketNumber = false
        };

        var dbOptions = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"JitVmAccessTests_{Guid.NewGuid()}")
            .Options;
        _dbFactory = new TestDbContextFactory(dbOptions);

        _service = new JitVmAccessService(
            _dbFactory,
            Options.Create(_options),
            Mock.Of<ILogger<JitVmAccessService>>());
    }

    // ─── RequestAccessAsync ──────────────────────────────────────────────

    [Fact]
    public async Task RequestAccess_ValidRequest_ShouldCreateJitSession()
    {
        var result = await _service.RequestAccessAsync(
            "user-1", "vm-web01", "rg-prod", null,
            22, "ssh", "10.0.1.50", 3,
            "Need SSH access for deployment troubleshooting",
            null, Guid.NewGuid());

        result.Success.Should().BeTrue();
        result.VmName.Should().Be("vm-web01");
        result.ResourceGroup.Should().Be("rg-prod");
        result.Port.Should().Be(22);
        result.Protocol.Should().Be("SSH");
        result.SourceIp.Should().Be("10.0.1.50");
        result.DurationHours.Should().Be(3);
        result.JitRequestId.Should().NotBeNullOrEmpty();
        result.ConnectionCommand.Should().Contain("ssh");
        result.ConnectionCommand.Should().Contain("vm-web01");
        result.ExpiresAt.Should().BeAfter(result.ActivatedAt);
    }

    [Fact]
    public async Task RequestAccess_ShouldRecordJitRequestEntity()
    {
        var result = await _service.RequestAccessAsync(
            "user-1", "vm-web01", "rg-prod", "sub-123",
            22, "ssh", "10.0.1.50", 3,
            "Need SSH access for deployment troubleshooting",
            "SNOW-OPS-001", Guid.NewGuid());

        result.Success.Should().BeTrue();

        // Verify entity persisted in DB
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.JitRequests
            .FirstOrDefaultAsync(r => r.Id == Guid.Parse(result.JitRequestId));

        entity.Should().NotBeNull();
        entity!.RequestType.Should().Be(JitRequestType.JitVmAccess);
        entity.VmName.Should().Be("vm-web01");
        entity.ResourceGroup.Should().Be("rg-prod");
        entity.Port.Should().Be(22);
        entity.Protocol.Should().Be("SSH");
        entity.SourceIp.Should().Be("10.0.1.50");
        entity.Status.Should().Be(JitRequestStatus.Active);
        entity.TicketNumber.Should().Be("SNOW-OPS-001");
    }

    [Fact]
    public async Task RequestAccess_DefaultsPort22ForSSH()
    {
        var result = await _service.RequestAccessAsync(
            "user-1", "vm-web01", "rg-prod", null,
            0, "ssh", null, 3,
            "Need SSH access for deployment troubleshooting",
            null, Guid.NewGuid());

        result.Success.Should().BeTrue();
        result.Port.Should().Be(22);
    }

    [Fact]
    public async Task RequestAccess_DefaultsPort3389ForRDP()
    {
        var result = await _service.RequestAccessAsync(
            "user-1", "vm-win01", "rg-prod", null,
            0, "rdp", null, 3,
            "Need RDP access for Windows troubleshooting",
            null, Guid.NewGuid());

        result.Success.Should().BeTrue();
        result.Port.Should().Be(3389);
        result.Protocol.Should().Be("RDP");
        result.ConnectionCommand.Should().Contain("mstsc");
    }

    [Fact]
    public async Task RequestAccess_MissingVmName_ShouldReturnError()
    {
        var result = await _service.RequestAccessAsync(
            "user-1", "", "rg-prod", null,
            22, "ssh", null, 3,
            "Need SSH access for troubleshooting purposes",
            null, Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("MISSING_PARAMETERS");
    }

    [Fact]
    public async Task RequestAccess_MissingResourceGroup_ShouldReturnError()
    {
        var result = await _service.RequestAccessAsync(
            "user-1", "vm-web01", "", null,
            22, "ssh", null, 3,
            "Need SSH access for troubleshooting purposes",
            null, Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("MISSING_PARAMETERS");
    }

    [Fact]
    public async Task RequestAccess_JustificationTooShort_ShouldReturnError()
    {
        var result = await _service.RequestAccessAsync(
            "user-1", "vm-web01", "rg-prod", null,
            22, "ssh", null, 3,
            "short",
            null, Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("JUSTIFICATION_TOO_SHORT");
    }

    [Fact]
    public async Task RequestAccess_InvalidDuration_ShouldReturnError()
    {
        var result = await _service.RequestAccessAsync(
            "user-1", "vm-web01", "rg-prod", null,
            22, "ssh", null, 25,
            "Need SSH access for deployment troubleshooting",
            null, Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_DURATION");
    }

    [Fact]
    public async Task RequestAccess_AutoDetectsSourceIp_WhenNotProvided()
    {
        var result = await _service.RequestAccessAsync(
            "user-1", "vm-web01", "rg-prod", null,
            22, "ssh", null, 3,
            "Need SSH access for deployment troubleshooting",
            null, Guid.NewGuid());

        result.Success.Should().BeTrue();
        result.SourceIp.Should().NotBeNullOrEmpty();
    }

    // ─── ListActiveSessionsAsync ─────────────────────────────────────────

    [Fact]
    public async Task ListActiveSessions_ShouldReturnUserSessions()
    {
        // Create a session first
        await _service.RequestAccessAsync(
            "user-1", "vm-web01", "rg-prod", null,
            22, "ssh", "10.0.1.50", 3,
            "Need SSH access for deployment troubleshooting",
            null, Guid.NewGuid());

        var sessions = await _service.ListActiveSessionsAsync("user-1");

        sessions.Should().HaveCount(1);
        sessions[0].VmName.Should().Be("vm-web01");
        sessions[0].Port.Should().Be(22);
        sessions[0].RemainingMinutes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ListActiveSessions_FiltersbyUser()
    {
        await _service.RequestAccessAsync(
            "user-1", "vm-web01", "rg-prod", null,
            22, "ssh", "10.0.1.50", 3,
            "Need SSH access for deployment troubleshooting",
            null, Guid.NewGuid());

        await _service.RequestAccessAsync(
            "user-2", "vm-web02", "rg-prod", null,
            22, "ssh", "10.0.2.50", 3,
            "Need SSH access for production maintenance",
            null, Guid.NewGuid());

        var sessions = await _service.ListActiveSessionsAsync("user-1");
        sessions.Should().HaveCount(1);
        sessions[0].VmName.Should().Be("vm-web01");
    }

    [Fact]
    public async Task ListActiveSessions_EmptyList_WhenNoSessions()
    {
        var sessions = await _service.ListActiveSessionsAsync("user-no-sessions");
        sessions.Should().BeEmpty();
    }

    // ─── RevokeAccessAsync ───────────────────────────────────────────────

    [Fact]
    public async Task RevokeAccess_ShouldDeactivateSession()
    {
        await _service.RequestAccessAsync(
            "user-1", "vm-web01", "rg-prod", null,
            22, "ssh", "10.0.1.50", 3,
            "Need SSH access for deployment troubleshooting",
            null, Guid.NewGuid());

        var result = await _service.RevokeAccessAsync("user-1", "vm-web01", "rg-prod");

        result.Revoked.Should().BeTrue();
        result.VmName.Should().Be("vm-web01");
        result.ResourceGroup.Should().Be("rg-prod");
        result.Message.Should().Contain("revoked");

        // Verify session no longer active
        var remainingSessions = await _service.ListActiveSessionsAsync("user-1");
        remainingSessions.Should().BeEmpty();
    }

    [Fact]
    public async Task RevokeAccess_UpdatesEntityStatus()
    {
        var createResult = await _service.RequestAccessAsync(
            "user-1", "vm-web01", "rg-prod", null,
            22, "ssh", "10.0.1.50", 3,
            "Need SSH access for deployment troubleshooting",
            null, Guid.NewGuid());

        await _service.RevokeAccessAsync("user-1", "vm-web01", "rg-prod");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.JitRequests
            .FirstOrDefaultAsync(r => r.Id == Guid.Parse(createResult.JitRequestId));

        entity.Should().NotBeNull();
        entity!.Status.Should().Be(JitRequestStatus.Deactivated);
        entity.DeactivatedAt.Should().NotBeNull();
        entity.ActualDuration.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokeAccess_SessionNotFound_ShouldReturnError()
    {
        var result = await _service.RevokeAccessAsync("user-1", "vm-nonexistent", "rg-prod");

        result.Revoked.Should().BeFalse();
        result.ErrorCode.Should().Be("SESSION_NOT_FOUND");
    }

    [Fact]
    public async Task RevokeAccess_AlreadyRevoked_ShouldReturnError()
    {
        await _service.RequestAccessAsync(
            "user-1", "vm-web01", "rg-prod", null,
            22, "ssh", "10.0.1.50", 3,
            "Need SSH access for deployment troubleshooting",
            null, Guid.NewGuid());

        // Revoke once
        var first = await _service.RevokeAccessAsync("user-1", "vm-web01", "rg-prod");
        first.Revoked.Should().BeTrue();

        // Revoke again
        var second = await _service.RevokeAccessAsync("user-1", "vm-web01", "rg-prod");
        second.Revoked.Should().BeFalse();
        second.ErrorCode.Should().Be("SESSION_NOT_FOUND");
    }

    [Fact]
    public async Task RequestAccess_TicketRequired_MissingTicket_ShouldReturnError()
    {
        // Create service with RequireTicketNumber=true
        var options = new PimServiceOptions
        {
            MinJustificationLength = 20,
            RequireTicketNumber = true
        };
        var service = new JitVmAccessService(
            _dbFactory,
            Options.Create(options),
            Mock.Of<ILogger<JitVmAccessService>>());

        var result = await service.RequestAccessAsync(
            "user-1", "vm-web01", "rg-prod", null,
            22, "ssh", null, 3,
            "Need SSH access for deployment troubleshooting",
            null, Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("TICKET_REQUIRED");
    }

    private class TestDbContextFactory : IDbContextFactory<AtoCopilotContext>
    {
        private readonly DbContextOptions<AtoCopilotContext> _options;
        public TestDbContextFactory(DbContextOptions<AtoCopilotContext> options) => _options = options;
        public AtoCopilotContext CreateDbContext() => new(_options);
    }
}
