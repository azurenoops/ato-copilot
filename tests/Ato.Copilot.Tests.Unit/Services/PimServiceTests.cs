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
/// Unit tests for PimService — T086.
/// Tests role activation, deactivation, ticket/justification validation,
/// high-privilege approval routing, and history queries.
/// </summary>
public class PimServiceTests
{
    private readonly IPimService _service;
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;
    private readonly PimServiceOptions _options;

    public PimServiceTests()
    {
        _options = new PimServiceOptions
        {
            DefaultActivationDurationHours = 8,
            MaxActivationDurationHours = 24,
            RequireTicketNumber = false,
            MinJustificationLength = 20,
            MaxJustificationLength = 500,
            HighPrivilegeRoles = new List<string>
            {
                "Owner", "User Access Administrator", "Security Administrator",
                "Global Administrator", "Privileged Role Administrator"
            },
            ApprovedTicketSystems = new Dictionary<string, string>
            {
                ["ServiceNow"] = @"^SNOW-[A-Z]+-\d+$",
                ["Jira"] = @"^[A-Z]{2,10}-\d+$",
                ["Remedy"] = @"^HD-\d+$",
                ["AzureDevOps"] = @"^AB#\d+$"
            }
        };

        var dbOptions = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"PimServiceTests_{Guid.NewGuid()}")
            .Options;

        _dbFactory = new InMemoryDbContextFactory(dbOptions);

        _service = new PimService(
            _dbFactory,
            Options.Create(_options),
            Mock.Of<ILogger<PimService>>());
    }

    // ─── ListEligibleRolesAsync ──────────────────────────────────────────────

    [Fact]
    public async Task ListEligibleRoles_ShouldReturnRoles()
    {
        var roles = await _service.ListEligibleRolesAsync("user-1");
        roles.Should().NotBeEmpty();
        roles.Should().Contain(r => r.RoleName == "Contributor");
        roles.Should().Contain(r => r.RoleName == "Reader");
        roles.Should().Contain(r => r.RoleName == "Owner");
    }

    [Fact]
    public async Task ListEligibleRoles_OwnerShouldRequireApproval()
    {
        var roles = await _service.ListEligibleRolesAsync("user-1");
        var owner = roles.First(r => r.RoleName == "Owner");
        owner.RequiresApproval.Should().BeTrue("Owner is a high-privilege role");
    }

    [Fact]
    public async Task ListEligibleRoles_ContributorShouldNotRequireApproval()
    {
        var roles = await _service.ListEligibleRolesAsync("user-1");
        var contrib = roles.First(r => r.RoleName == "Contributor");
        contrib.RequiresApproval.Should().BeFalse();
    }

    // ─── ActivateRoleAsync — Successful activation ───────────────────────────

    [Fact]
    public async Task ActivateRole_StandardRole_ShouldActivateImmediately()
    {
        var result = await _service.ActivateRoleAsync(
            "user-1", "Contributor", "default",
            "Remediating AC-2.1 finding per assessment", null, null,
            Guid.NewGuid());

        result.Activated.Should().BeTrue();
        result.PendingApproval.Should().BeFalse();
        result.RoleName.Should().Be("Contributor");
        result.ExpiresAt.Should().NotBeNull();
        result.DurationHours.Should().Be(8);
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task ActivateRole_ShouldCreateJitRequestInDatabase()
    {
        await _service.ActivateRoleAsync(
            "user-1", "Contributor", "default",
            "Remediating AC-2.1 finding per assessment", "SNOW-INC-4521", null,
            Guid.NewGuid());

        await using var db = await _dbFactory.CreateDbContextAsync();
        var requests = await db.JitRequests.Where(r => r.UserId == "user-1").ToListAsync();
        requests.Should().HaveCount(1);
        requests[0].RoleName.Should().Be("Contributor");
        requests[0].TicketNumber.Should().Be("SNOW-INC-4521");
        requests[0].TicketSystem.Should().Be("ServiceNow");
        requests[0].Status.Should().Be(JitRequestStatus.Active);
    }

    [Fact]
    public async Task ActivateRole_HighPrivilege_ShouldRouteToPendingApproval()
    {
        var result = await _service.ActivateRoleAsync(
            "user-1", "Owner", "default",
            "Emergency production access for incident response", null, null,
            Guid.NewGuid());

        result.Activated.Should().BeFalse();
        result.PendingApproval.Should().BeTrue();
        result.ApproversNotified.Should().Contain("Security Lead");
        result.ApproversNotified.Should().Contain("Compliance Officer");
    }

    // ─── ActivateRoleAsync — Error cases ─────────────────────────────────────

    [Fact]
    public async Task ActivateRole_NotEligible_ShouldReturnNotEligibleError()
    {
        var result = await _service.ActivateRoleAsync(
            "user-1", "NonExistentRole", "default",
            "Need access for compliance work tasks", null, null,
            Guid.NewGuid());

        result.ErrorCode.Should().Be("NOT_ELIGIBLE");
        result.Message.Should().Contain("not eligible");
    }

    [Fact]
    public async Task ActivateRole_AlreadyActive_ShouldReturnAlreadyActiveError()
    {
        // First activation
        await _service.ActivateRoleAsync(
            "user-1", "Contributor", "default",
            "First activation for compliance work", null, null,
            Guid.NewGuid());

        // Second activation of same role
        var result = await _service.ActivateRoleAsync(
            "user-1", "Contributor", "default",
            "Duplicate activation for compliance work", null, null,
            Guid.NewGuid());

        result.ErrorCode.Should().Be("ROLE_ALREADY_ACTIVE");
    }

    // ─── Justification validation (T031) ─────────────────────────────────────

    [Fact]
    public async Task ActivateRole_JustificationTooShort_ShouldReturnError()
    {
        var result = await _service.ActivateRoleAsync(
            "user-1", "Contributor", "default",
            "Too short", null, null,
            Guid.NewGuid());

        result.ErrorCode.Should().Be("JUSTIFICATION_TOO_SHORT");
        result.Message.Should().Contain("20 characters");
    }

    [Fact]
    public async Task ActivateRole_JustificationMinLength_ShouldPass()
    {
        var justification = new string('A', 20); // exactly 20 chars
        var result = await _service.ActivateRoleAsync(
            "user-1", "Contributor", "default",
            justification, null, null,
            Guid.NewGuid());

        result.ErrorCode.Should().BeNull();
    }

    // ─── Ticket validation (T030) ────────────────────────────────────────────

    [Fact]
    public async Task ActivateRole_RequireTicketTrue_NoTicket_ShouldReturnTicketRequired()
    {
        _options.RequireTicketNumber = true;
        // Re-create service with updated options
        var service = new PimService(_dbFactory, Options.Create(_options), Mock.Of<ILogger<PimService>>());

        var result = await service.ActivateRoleAsync(
            "user-1", "Contributor", "default",
            "Remediating AC-2.1 finding per assessment", null, null,
            Guid.NewGuid());

        result.ErrorCode.Should().Be("TICKET_REQUIRED");
    }

    [Theory]
    [InlineData("SNOW-INC-4521")]
    [InlineData("PROJ-1234")]
    [InlineData("HD-1234")]
    [InlineData("AB#1234")]
    public async Task ActivateRole_ValidTicketFormat_ShouldPass(string ticketNumber)
    {
        var result = await _service.ActivateRoleAsync(
            "user-1", "Reader", "default",
            "Remediating AC-2.1 finding per assessment", ticketNumber, null,
            Guid.NewGuid());

        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task ActivateRole_InvalidTicketFormat_ShouldReturnError()
    {
        var result = await _service.ActivateRoleAsync(
            "user-1", "Contributor", "default",
            "Remediating AC-2.1 finding per assessment", "INVALID-TICKET-123!!", null,
            Guid.NewGuid());

        result.ErrorCode.Should().Be("INVALID_TICKET");
        result.Message.Should().Contain("does not match");
    }

    [Fact]
    public async Task ActivateRole_RequireTicketFalse_NoTicket_ShouldPass()
    {
        _options.RequireTicketNumber = false;
        var service = new PimService(_dbFactory, Options.Create(_options), Mock.Of<ILogger<PimService>>());

        var result = await service.ActivateRoleAsync(
            "user-1", "Reader", "default",
            "Remediating AC-2.1 finding per assessment", null, null,
            Guid.NewGuid());

        result.ErrorCode.Should().BeNull();
    }

    // ─── Duration validation ─────────────────────────────────────────────────

    [Fact]
    public async Task ActivateRole_DurationExceedsPolicy_ShouldReturnError()
    {
        var result = await _service.ActivateRoleAsync(
            "user-1", "Contributor", "default",
            "Remediating AC-2.1 finding per assessment", null, 48,
            Guid.NewGuid());

        result.ErrorCode.Should().Be("DURATION_EXCEEDS_POLICY");
        result.Message.Should().Contain("24");
    }

    // ─── DeactivateRoleAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task DeactivateRole_ActiveRole_ShouldSucceed()
    {
        await _service.ActivateRoleAsync(
            "user-1", "Contributor", "default",
            "Remediating AC-2.1 finding per assessment", null, null,
            Guid.NewGuid());

        var result = await _service.DeactivateRoleAsync("user-1", "Contributor", "default");

        result.Deactivated.Should().BeTrue();
        result.RoleName.Should().Be("Contributor");
        result.ActualDuration.Should().NotBeEmpty();
        result.Message.Should().Contain("Least-privilege posture restored");
    }

    [Fact]
    public async Task DeactivateRole_NotActive_ShouldReturnError()
    {
        var result = await _service.DeactivateRoleAsync("user-1", "Contributor", "Production");

        result.ErrorCode.Should().Be("ROLE_NOT_ACTIVE");
    }

    [Fact]
    public async Task DeactivateRole_ShouldSetStatusToDeactivated()
    {
        await _service.ActivateRoleAsync(
            "user-1", "Contributor", "default",
            "Remediating AC-2.1 finding per assessment", null, null,
            Guid.NewGuid());

        await _service.DeactivateRoleAsync("user-1", "Contributor", "default");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var request = await db.JitRequests.FirstAsync(r => r.UserId == "user-1");
        request.Status.Should().Be(JitRequestStatus.Deactivated);
        request.ActualDuration.Should().NotBeNull();
    }

    // ─── IsHighPrivilegeRole ─────────────────────────────────────────────────

    [Theory]
    [InlineData("Owner", true)]
    [InlineData("User Access Administrator", true)]
    [InlineData("Security Administrator", true)]
    [InlineData("Contributor", false)]
    [InlineData("Reader", false)]
    [InlineData("Unknown Role", false)]
    public void IsHighPrivilegeRole_ShouldClassifyCorrectly(string roleName, bool expected)
    {
        _service.IsHighPrivilegeRole(roleName).Should().Be(expected);
    }

    // ─── ListActiveRolesAsync ────────────────────────────────────────────────

    [Fact]
    public async Task ListActiveRoles_ShouldReturnActiveRoles()
    {
        await _service.ActivateRoleAsync(
            "user-1", "Contributor", "default",
            "Remediating AC-2.1 finding per assessment", null, null,
            Guid.NewGuid());

        var active = await _service.ListActiveRolesAsync("user-1");

        active.Should().HaveCount(1);
        active[0].RoleName.Should().Be("Contributor");
        active[0].RemainingMinutes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ListActiveRoles_ShouldReturnEmptyWhenNone()
    {
        var active = await _service.ListActiveRolesAsync("user-no-roles");
        active.Should().BeEmpty();
    }

    // ─── GetHistoryAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetHistory_ShouldReturnPastActivations()
    {
        await _service.ActivateRoleAsync(
            "user-1", "Contributor", "default",
            "Remediating AC-2.1 finding per assessment", null, null,
            Guid.NewGuid());
        await _service.DeactivateRoleAsync("user-1", "Contributor", "default");

        var history = await _service.GetHistoryAsync("user-1", 7);

        history.TotalCount.Should().Be(1);
        history.History[0].Status.Should().Be("Deactivated");
        history.NistControlMapping.Should().Contain("AC-2");
        history.NistControlMapping.Should().Contain("AC-6");
    }

    // ─── SubmitApprovalAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task SubmitApproval_ShouldCreatePendingRequest()
    {
        var request = await _service.SubmitApprovalAsync(
            "user-1", "Jane Smith", "Owner", "/subscriptions/sub-1",
            "Emergency production access", null, 4,
            Guid.NewGuid());

        request.Status.Should().Be(JitRequestStatus.PendingApproval);
        request.RoleName.Should().Be("Owner");
    }

    // ─── ApproveRequestAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ApproveRequest_ShouldActivateRole()
    {
        var request = await _service.SubmitApprovalAsync(
            "user-1", "Jane Smith", "Owner", "/subscriptions/sub-1",
            "Emergency production access", null, 4,
            Guid.NewGuid());

        var result = await _service.ApproveRequestAsync(
            request.Id, "approver-1", "Security Lead", "Approve for incident response");

        result.Approved.Should().BeTrue();
        result.RequesterName.Should().Be("Jane Smith");
        result.RoleName.Should().Be("Owner");
    }

    [Fact]
    public async Task ApproveRequest_AlreadyDecided_ShouldReturnError()
    {
        var request = await _service.SubmitApprovalAsync(
            "user-1", "Jane Smith", "Owner", "/subscriptions/sub-1",
            "Emergency production access", null, 4,
            Guid.NewGuid());

        await _service.ApproveRequestAsync(request.Id, "approver-1", "Security Lead");

        var result = await _service.ApproveRequestAsync(request.Id, "approver-2", "Another Approver");
        result.ErrorCode.Should().Be("REQUEST_ALREADY_DECIDED");
    }

    [Fact]
    public async Task ApproveRequest_NonExistent_ShouldReturnNotFound()
    {
        var result = await _service.ApproveRequestAsync(
            Guid.NewGuid(), "approver-1", "Security Lead", "Looks good");

        result.ErrorCode.Should().Be("REQUEST_NOT_FOUND");
        result.Approved.Should().BeFalse();
    }

    // ─── DenyRequestAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task DenyRequest_ShouldDenyRole()
    {
        var request = await _service.SubmitApprovalAsync(
            "user-1", "Jane Smith", "Owner", "/subscriptions/sub-1",
            "Emergency production access", null, 4,
            Guid.NewGuid());

        var result = await _service.DenyRequestAsync(
            request.Id, "approver-1", "Security Lead",
            "Insufficient justification");

        result.Denied.Should().BeTrue();
        result.Reason.Should().Contain("Insufficient justification");
    }

    [Fact]
    public async Task DenyRequest_AlreadyDecided_ShouldReturnError()
    {
        var request = await _service.SubmitApprovalAsync(
            "user-1", "Jane Smith", "Owner", "/subscriptions/sub-1",
            "Emergency production access", null, 4,
            Guid.NewGuid());

        await _service.DenyRequestAsync(request.Id, "approver-1", "Security Lead", "No");

        var result = await _service.DenyRequestAsync(request.Id, "approver-2", "Reviewer", "Duplicate");
        result.ErrorCode.Should().Be("REQUEST_ALREADY_DECIDED");
    }

    [Fact]
    public async Task DenyRequest_NonExistent_ShouldReturnNotFound()
    {
        var result = await _service.DenyRequestAsync(
            Guid.NewGuid(), "approver-1", "Security Lead", "Not needed");

        result.ErrorCode.Should().Be("REQUEST_NOT_FOUND");
        result.Denied.Should().BeFalse();
    }

    // ─── ExtendRoleAsync (T088) ──────────────────────────────────────────────

    [Fact]
    public async Task ExtendRole_ValidExtension_ShouldExtend()
    {
        // Activate first
        await _service.ActivateRoleAsync(
            "user-ext-1", "Contributor", "default",
            "Extending role test justification text", null, 4,
            Guid.NewGuid());

        var result = await _service.ExtendRoleAsync(
            "user-ext-1", "Contributor", "default", 2);

        result.Extended.Should().BeTrue();
        result.RoleName.Should().Be("Contributor");
        result.NewExpiresAt.Should().BeAfter(result.PreviousExpiresAt);
    }

    [Fact]
    public async Task ExtendRole_ExceedsMaxDuration_ShouldReturnError()
    {
        await _service.ActivateRoleAsync(
            "user-ext-2", "Contributor", "default",
            "Extending role test justification text", null, 8,
            Guid.NewGuid());

        var result = await _service.ExtendRoleAsync(
            "user-ext-2", "Contributor", "default", 20);

        result.ErrorCode.Should().Be("DURATION_EXCEEDS_POLICY");
    }

    [Fact]
    public async Task ExtendRole_NotActive_ShouldReturnError()
    {
        var result = await _service.ExtendRoleAsync(
            "user-ext-3", "Contributor", "default", 2);

        result.ErrorCode.Should().Be("ROLE_NOT_ACTIVE");
    }

    // ─── PimListActiveTool & PimExtendRoleTool (T088) ────────────────────────

    [Fact]
    public async Task ListActiveRoles_WithActive_ShouldReturnRoles()
    {
        await _service.ActivateRoleAsync(
            "user-active-1", "Contributor", "default",
            "Need contributor for compliance work", null, 4,
            Guid.NewGuid());

        var active = await _service.ListActiveRolesAsync("user-active-1");

        active.Should().HaveCount(1);
        active[0].RoleName.Should().Be("Contributor");
        active[0].RemainingMinutes.Should().BeGreaterThan(0);
    }

    // ─── GetHistoryAsync (T097) ────────────────────────────────────────────────

    [Fact]
    public async Task GetHistory_DefaultDays_ShouldReturnRecentEntries()
    {
        // Seed a PIM activation within the last 7 days
        await _service.ActivateRoleAsync(
            "user-hist-1", "Contributor", "default",
            "Compliance investigation task", null, 4, Guid.NewGuid());

        var result = await _service.GetHistoryAsync("user-hist-1");

        result.History.Should().HaveCountGreaterOrEqualTo(1);
        result.TotalCount.Should().BeGreaterOrEqualTo(1);
        result.History.First().RoleName.Should().Be("Contributor");
        result.History.First().UserId.Should().Be("user-hist-1");
        result.NistControlMapping.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetHistory_FilterByRoleName_ShouldReturnOnlyMatchingRoles()
    {
        await _service.ActivateRoleAsync(
            "user-hist-2", "Contributor", "default",
            "Need contributor for audit", null, 4, Guid.NewGuid());
        await _service.ActivateRoleAsync(
            "user-hist-2", "Reader", "default",
            "Need reader for compliance work", null, 4, Guid.NewGuid());

        var result = await _service.GetHistoryAsync("user-hist-2", roleName: "Contributor");

        result.History.Should().AllSatisfy(h => h.RoleName.Should().Be("Contributor"));
    }

    [Fact]
    public async Task GetHistory_FilterByScope_ShouldReturnMatchingScope()
    {
        // Seed directly into DB to control scope precisely
        await using var db = _dbFactory.CreateDbContext();
        db.JitRequests.Add(new JitRequestEntity
        {
            RequestType = JitRequestType.PimRoleActivation,
            UserId = "user-hist-3",
            UserDisplayName = "Scope User",
            RoleName = "Contributor",
            Scope = "/subscriptions/sub-123",
            ScopeDisplayName = "Sub 123",
            Justification = "Need access to subscription",
            Status = JitRequestStatus.Active,
            RequestedAt = DateTimeOffset.UtcNow,
            DurationHours = 4
        });
        db.JitRequests.Add(new JitRequestEntity
        {
            RequestType = JitRequestType.PimRoleActivation,
            UserId = "user-hist-3",
            UserDisplayName = "Scope User",
            RoleName = "Reader",
            Scope = "/subscriptions/sub-999",
            ScopeDisplayName = "Sub 999",
            Justification = "Need reader on different sub",
            Status = JitRequestStatus.Active,
            RequestedAt = DateTimeOffset.UtcNow,
            DurationHours = 4
        });
        await db.SaveChangesAsync();

        var result = await _service.GetHistoryAsync("user-hist-3", scope: "sub-123");

        result.History.Should().HaveCountGreaterOrEqualTo(1);
        result.History.Should().AllSatisfy(h => h.Scope.Should().Contain("123"));
    }

    [Fact]
    public async Task GetHistory_NonAuditor_CannotSeeOtherUsersHistory()
    {
        await _service.ActivateRoleAsync(
            "user-hist-4a", "Contributor", "default",
            "This is user A compliance task", null, 4, Guid.NewGuid());
        await _service.ActivateRoleAsync(
            "user-hist-4b", "Reader", "default",
            "This is user B compliance task", null, 4, Guid.NewGuid());

        // Non-auditor querying with filterUserId should still see only own history
        var result = await _service.GetHistoryAsync(
            "user-hist-4a", filterUserId: "user-hist-4b", isAuditor: false);

        result.History.Should().AllSatisfy(h => h.UserId.Should().Be("user-hist-4a"));
    }

    [Fact]
    public async Task GetHistory_Auditor_CanSeeOtherUsersHistory()
    {
        await _service.ActivateRoleAsync(
            "user-hist-5target", "Contributor", "default",
            "Target user compliance action", null, 4, Guid.NewGuid());

        var result = await _service.GetHistoryAsync(
            "user-hist-5auditor", filterUserId: "user-hist-5target", isAuditor: true);

        result.History.Should().AllSatisfy(h => h.UserId.Should().Be("user-hist-5target"));
    }

    [Fact]
    public async Task GetHistory_ExpiredEntries_ShouldBeExcluded()
    {
        // Seed an old entry directly into the database
        await using var db = _dbFactory.CreateDbContext();
        db.JitRequests.Add(new JitRequestEntity
        {
            RequestType = JitRequestType.PimRoleActivation,
            UserId = "user-hist-6",
            UserDisplayName = "Old User",
            RoleName = "Contributor",
            Scope = "default",
            Justification = "Historical compliance record",
            Status = JitRequestStatus.Deactivated,
            RequestedAt = DateTimeOffset.UtcNow.AddDays(-60),
            DurationHours = 4
        });
        await db.SaveChangesAsync();

        // Default 7 days should not include the 60-day-old entry
        var result = await _service.GetHistoryAsync("user-hist-6");

        result.History.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHistory_CustomDays_ShouldIncludeOlderEntries()
    {
        await using var db = _dbFactory.CreateDbContext();
        db.JitRequests.Add(new JitRequestEntity
        {
            RequestType = JitRequestType.PimRoleActivation,
            UserId = "user-hist-7",
            UserDisplayName = "Custom Days User",
            RoleName = "Reader",
            Scope = "default",
            Justification = "Need reader for compliance review",
            Status = JitRequestStatus.Deactivated,
            RequestedAt = DateTimeOffset.UtcNow.AddDays(-25),
            DurationHours = 4
        });
        await db.SaveChangesAsync();

        var result = await _service.GetHistoryAsync("user-hist-7", days: 30);

        result.History.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task GetHistory_NistControlMapping_ShouldBePopulated()
    {
        await _service.ActivateRoleAsync(
            "user-hist-8", "Contributor", "default",
            "Task for NIST mapping check", null, 4, Guid.NewGuid());

        var result = await _service.GetHistoryAsync("user-hist-8");

        result.NistControlMapping.Should().Contain("AC-2");
        result.NistControlMapping.Should().Contain("AC-6");
        result.NistControlMapping.Should().Contain("AU-2");
        result.NistControlMapping.Should().Contain("AU-3");
    }

    [Fact]
    public async Task GetHistory_OrderedByRequestedAtDescending()
    {
        await _service.ActivateRoleAsync(
            "user-hist-9", "Reader", "default",
            "First activation for ordering", null, 2, Guid.NewGuid());
        await Task.Delay(50); // Ensure different timestamps
        await _service.ActivateRoleAsync(
            "user-hist-9", "Contributor", "default",
            "Second activation for ordering", null, 4, Guid.NewGuid());

        var result = await _service.GetHistoryAsync("user-hist-9");

        result.History.Should().HaveCountGreaterOrEqualTo(2);
        result.History.First().RequestedAt.Should().BeOnOrAfter(result.History.Last().RequestedAt);
    }

    // ─── Helper: InMemoryDbContextFactory ─────────────────────────────────────

    private class InMemoryDbContextFactory : IDbContextFactory<AtoCopilotContext>
    {
        private readonly DbContextOptions<AtoCopilotContext> _options;
        public InMemoryDbContextFactory(DbContextOptions<AtoCopilotContext> options) => _options = options;
        public AtoCopilotContext CreateDbContext() => new(_options);
    }
}
