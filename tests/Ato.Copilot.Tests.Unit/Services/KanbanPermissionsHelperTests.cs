using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Constants;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for KanbanPermissionsHelper:
/// Verifies the RBAC matrix for all 4 roles × 11 permissions.
/// </summary>
public class KanbanPermissionsHelperTests
{
    // ── Administrator (all 11 perms) ──────────────────────────────────────

    [Theory]
    [InlineData(KanbanPermissions.CanCreateBoard)]
    [InlineData(KanbanPermissions.CanCreateTask)]
    [InlineData(KanbanPermissions.CanAssignAny)]
    [InlineData(KanbanPermissions.CanSelfAssign)]
    [InlineData(KanbanPermissions.CanMoveOwn)]
    [InlineData(KanbanPermissions.CanMoveAny)]
    [InlineData(KanbanPermissions.CanComment)]
    [InlineData(KanbanPermissions.CanExport)]
    [InlineData(KanbanPermissions.CanDeleteAnyComment)]
    [InlineData(KanbanPermissions.CanCloseWithoutValidation)]
    [InlineData(KanbanPermissions.CanArchive)]
    public void Administrator_HasAllPermissions(string permission)
    {
        KanbanPermissionsHelper.CanPerformAction(ComplianceRoles.Administrator, permission)
            .Should().BeTrue();
    }

    // ── SecurityLead (7 perms) ────────────────────────────────────────────

    [Theory]
    [InlineData(KanbanPermissions.CanCreateBoard, true)]
    [InlineData(KanbanPermissions.CanCreateTask, true)]
    [InlineData(KanbanPermissions.CanAssignAny, true)]
    [InlineData(KanbanPermissions.CanMoveOwn, true)]
    [InlineData(KanbanPermissions.CanMoveAny, true)]
    [InlineData(KanbanPermissions.CanComment, true)]
    [InlineData(KanbanPermissions.CanExport, true)]
    [InlineData(KanbanPermissions.CanSelfAssign, false)]
    [InlineData(KanbanPermissions.CanDeleteAnyComment, false)]
    [InlineData(KanbanPermissions.CanCloseWithoutValidation, false)]
    [InlineData(KanbanPermissions.CanArchive, false)]
    public void SecurityLead_HasExpectedPermissions(string permission, bool expected)
    {
        KanbanPermissionsHelper.CanPerformAction(ComplianceRoles.SecurityLead, permission)
            .Should().Be(expected);
    }

    // ── Analyst (3 perms) ─────────────────────────────────────────────────

    [Theory]
    [InlineData(KanbanPermissions.CanSelfAssign, true)]
    [InlineData(KanbanPermissions.CanMoveOwn, true)]
    [InlineData(KanbanPermissions.CanComment, true)]
    [InlineData(KanbanPermissions.CanCreateBoard, false)]
    [InlineData(KanbanPermissions.CanCreateTask, false)]
    [InlineData(KanbanPermissions.CanAssignAny, false)]
    [InlineData(KanbanPermissions.CanMoveAny, false)]
    [InlineData(KanbanPermissions.CanExport, false)]
    [InlineData(KanbanPermissions.CanDeleteAnyComment, false)]
    [InlineData(KanbanPermissions.CanCloseWithoutValidation, false)]
    [InlineData(KanbanPermissions.CanArchive, false)]
    public void Analyst_HasExpectedPermissions(string permission, bool expected)
    {
        KanbanPermissionsHelper.CanPerformAction(ComplianceRoles.Analyst, permission)
            .Should().Be(expected);
    }

    // ── Auditor (1 perm) ──────────────────────────────────────────────────

    [Theory]
    [InlineData(KanbanPermissions.CanExport, true)]
    [InlineData(KanbanPermissions.CanCreateBoard, false)]
    [InlineData(KanbanPermissions.CanCreateTask, false)]
    [InlineData(KanbanPermissions.CanAssignAny, false)]
    [InlineData(KanbanPermissions.CanSelfAssign, false)]
    [InlineData(KanbanPermissions.CanMoveOwn, false)]
    [InlineData(KanbanPermissions.CanMoveAny, false)]
    [InlineData(KanbanPermissions.CanComment, false)]
    [InlineData(KanbanPermissions.CanDeleteAnyComment, false)]
    [InlineData(KanbanPermissions.CanCloseWithoutValidation, false)]
    [InlineData(KanbanPermissions.CanArchive, false)]
    public void Auditor_HasExpectedPermissions(string permission, bool expected)
    {
        KanbanPermissionsHelper.CanPerformAction(ComplianceRoles.Auditor, permission)
            .Should().Be(expected);
    }

    // ── Unknown role (no perms) ───────────────────────────────────────────

    [Fact]
    public void UnknownRole_HasNoPermissions()
    {
        KanbanPermissionsHelper.CanPerformAction("guest", KanbanPermissions.CanCreateBoard)
            .Should().BeFalse();
        KanbanPermissionsHelper.CanPerformAction("guest", KanbanPermissions.CanExport)
            .Should().BeFalse();
    }

    // ── GetPermissionsForRole ─────────────────────────────────────────────

    [Fact]
    public void GetPermissions_Administrator_Returns11()
    {
        var perms = KanbanPermissionsHelper.GetPermissionsForRole(ComplianceRoles.Administrator);

        perms.Should().HaveCount(11);
    }

    [Fact]
    public void GetPermissions_Auditor_Returns1()
    {
        var perms = KanbanPermissionsHelper.GetPermissionsForRole(ComplianceRoles.Auditor);

        perms.Should().HaveCount(1);
        perms.Should().Contain(KanbanPermissions.CanExport);
    }

    [Fact]
    public void GetPermissions_UnknownRole_ReturnsEmpty()
    {
        var perms = KanbanPermissionsHelper.GetPermissionsForRole("guest");

        perms.Should().BeEmpty();
    }
}
