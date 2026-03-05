using Xunit;
using FluentAssertions;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Models;

/// <summary>
/// Unit tests for Feature 018 — SAP entities, enum, and DTOs.
/// Validates default values, construction, enum members, and record immutability.
/// T007: Entity construction and default value tests.
/// </summary>
public class SapModelTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // SapStatus Enum
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SapStatus_Should_Have_Draft_And_Finalized()
    {
        Enum.GetValues<SapStatus>().Should().HaveCount(2);
        Enum.IsDefined(SapStatus.Draft).Should().BeTrue();
        Enum.IsDefined(SapStatus.Finalized).Should().BeTrue();
    }

    [Fact]
    public void SapStatus_Draft_Should_Be_Zero()
    {
        ((int)SapStatus.Draft).Should().Be(0);
    }

    [Fact]
    public void SapStatus_Finalized_Should_Be_One()
    {
        ((int)SapStatus.Finalized).Should().Be(1);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SecurityAssessmentPlan Entity
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SecurityAssessmentPlan_Should_Have_Generated_Id()
    {
        var sap = new SecurityAssessmentPlan();
        sap.Id.Should().NotBeNullOrEmpty();
        Guid.TryParse(sap.Id, out _).Should().BeTrue();
    }

    [Fact]
    public void SecurityAssessmentPlan_MultipleInstances_HaveUniqueIds()
    {
        var plans = Enumerable.Range(0, 50).Select(_ => new SecurityAssessmentPlan()).ToList();
        plans.Select(p => p.Id).Distinct().Should().HaveCount(50);
    }

    [Fact]
    public void SecurityAssessmentPlan_Should_Have_Default_Values()
    {
        var sap = new SecurityAssessmentPlan();

        sap.RegisteredSystemId.Should().Be(string.Empty);
        sap.AssessmentId.Should().BeNull();
        sap.Status.Should().Be(SapStatus.Draft);
        sap.Title.Should().Be(string.Empty);
        sap.BaselineLevel.Should().Be(string.Empty);
        sap.ScopeNotes.Should().BeNull();
        sap.RulesOfEngagement.Should().BeNull();
        sap.ScheduleStart.Should().BeNull();
        sap.ScheduleEnd.Should().BeNull();
        sap.Content.Should().Be(string.Empty);
        sap.ContentHash.Should().BeNull();
        sap.TotalControls.Should().Be(0);
        sap.CustomerControls.Should().Be(0);
        sap.InheritedControls.Should().Be(0);
        sap.SharedControls.Should().Be(0);
        sap.StigBenchmarkCount.Should().Be(0);
        sap.GeneratedBy.Should().Be(string.Empty);
        sap.FinalizedBy.Should().BeNull();
        sap.FinalizedAt.Should().BeNull();
        sap.Format.Should().Be("markdown");
    }

    [Fact]
    public void SecurityAssessmentPlan_GeneratedAt_IsRecent()
    {
        var before = DateTime.UtcNow;
        var sap = new SecurityAssessmentPlan();
        var after = DateTime.UtcNow;

        sap.GeneratedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void SecurityAssessmentPlan_NavigationCollections_AreEmpty()
    {
        var sap = new SecurityAssessmentPlan();
        sap.ControlEntries.Should().NotBeNull().And.BeEmpty();
        sap.TeamMembers.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void SecurityAssessmentPlan_CanSetAllProperties()
    {
        var systemId = Guid.NewGuid().ToString();
        var assessmentId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        var start = now.AddDays(7);
        var end = now.AddDays(30);

        var sap = new SecurityAssessmentPlan
        {
            RegisteredSystemId = systemId,
            AssessmentId = assessmentId,
            Status = SapStatus.Finalized,
            Title = "SAP — Test System — FY26 Q2",
            BaselineLevel = "Moderate",
            ScopeNotes = "Limited to CUI boundary",
            RulesOfEngagement = "Business hours only",
            ScheduleStart = start,
            ScheduleEnd = end,
            Content = "# SAP Content",
            ContentHash = new string('a', 64),
            TotalControls = 325,
            CustomerControls = 150,
            InheritedControls = 100,
            SharedControls = 75,
            StigBenchmarkCount = 12,
            GeneratedBy = "sca@example.com",
            FinalizedBy = "lead@example.com",
            FinalizedAt = now,
            Format = "docx"
        };

        sap.RegisteredSystemId.Should().Be(systemId);
        sap.AssessmentId.Should().Be(assessmentId);
        sap.Status.Should().Be(SapStatus.Finalized);
        sap.Title.Should().Be("SAP — Test System — FY26 Q2");
        sap.BaselineLevel.Should().Be("Moderate");
        sap.ScopeNotes.Should().Be("Limited to CUI boundary");
        sap.RulesOfEngagement.Should().Be("Business hours only");
        sap.ScheduleStart.Should().Be(start);
        sap.ScheduleEnd.Should().Be(end);
        sap.Content.Should().Be("# SAP Content");
        sap.ContentHash.Should().HaveLength(64);
        sap.TotalControls.Should().Be(325);
        sap.CustomerControls.Should().Be(150);
        sap.InheritedControls.Should().Be(100);
        sap.SharedControls.Should().Be(75);
        sap.StigBenchmarkCount.Should().Be(12);
        sap.GeneratedBy.Should().Be("sca@example.com");
        sap.FinalizedBy.Should().Be("lead@example.com");
        sap.FinalizedAt.Should().Be(now);
        sap.Format.Should().Be("docx");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SapControlEntry Entity
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SapControlEntry_Should_Have_Generated_Id()
    {
        var entry = new SapControlEntry();
        entry.Id.Should().NotBeNullOrEmpty();
        Guid.TryParse(entry.Id, out _).Should().BeTrue();
    }

    [Fact]
    public void SapControlEntry_MultipleInstances_HaveUniqueIds()
    {
        var entries = Enumerable.Range(0, 100).Select(_ => new SapControlEntry()).ToList();
        entries.Select(e => e.Id).Distinct().Should().HaveCount(100);
    }

    [Fact]
    public void SapControlEntry_Should_Have_Default_Values()
    {
        var entry = new SapControlEntry();

        entry.SecurityAssessmentPlanId.Should().Be(string.Empty);
        entry.ControlId.Should().Be(string.Empty);
        entry.ControlTitle.Should().Be(string.Empty);
        entry.ControlFamily.Should().Be(string.Empty);
        entry.InheritanceType.Should().Be(InheritanceType.Customer);
        entry.Provider.Should().BeNull();
        entry.EvidenceExpected.Should().Be(0);
        entry.EvidenceCollected.Should().Be(0);
        entry.IsMethodOverridden.Should().BeFalse();
        entry.OverrideRationale.Should().BeNull();
    }

    [Fact]
    public void SapControlEntry_AssessmentMethods_DefaultsToAllThree()
    {
        var entry = new SapControlEntry();
        entry.AssessmentMethods.Should().HaveCount(3);
        entry.AssessmentMethods.Should().ContainInOrder("Examine", "Interview", "Test");
    }

    [Fact]
    public void SapControlEntry_ListProperties_AreInitializedEmpty()
    {
        var entry = new SapControlEntry();
        entry.AssessmentObjectives.Should().NotBeNull().And.BeEmpty();
        entry.EvidenceRequirements.Should().NotBeNull().And.BeEmpty();
        entry.StigBenchmarks.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void SapControlEntry_CanSetAllProperties()
    {
        var planId = Guid.NewGuid().ToString();
        var entry = new SapControlEntry
        {
            SecurityAssessmentPlanId = planId,
            ControlId = "AC-2",
            ControlTitle = "Account Management",
            ControlFamily = "Access Control",
            InheritanceType = InheritanceType.Shared,
            Provider = "AWS GovCloud",
            AssessmentMethods = new List<string> { "Examine", "Test" },
            AssessmentObjectives = new List<string> { "AC-2a", "AC-2b", "AC-2c" },
            EvidenceRequirements = new List<string> { "Account policy document", "System administrator interview" },
            StigBenchmarks = new List<string> { "Windows_Server_2022_STIG", "RHEL_9_STIG" },
            EvidenceExpected = 3,
            EvidenceCollected = 1,
            IsMethodOverridden = true,
            OverrideRationale = "Test method not applicable for inherited controls"
        };

        entry.SecurityAssessmentPlanId.Should().Be(planId);
        entry.ControlId.Should().Be("AC-2");
        entry.ControlTitle.Should().Be("Account Management");
        entry.ControlFamily.Should().Be("Access Control");
        entry.InheritanceType.Should().Be(InheritanceType.Shared);
        entry.Provider.Should().Be("AWS GovCloud");
        entry.AssessmentMethods.Should().HaveCount(2).And.Contain("Examine").And.Contain("Test");
        entry.AssessmentObjectives.Should().HaveCount(3);
        entry.EvidenceRequirements.Should().HaveCount(2);
        entry.StigBenchmarks.Should().HaveCount(2);
        entry.EvidenceExpected.Should().Be(3);
        entry.EvidenceCollected.Should().Be(1);
        entry.IsMethodOverridden.Should().BeTrue();
        entry.OverrideRationale.Should().Be("Test method not applicable for inherited controls");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SapTeamMember Entity
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SapTeamMember_Should_Have_Generated_Id()
    {
        var member = new SapTeamMember();
        member.Id.Should().NotBeNullOrEmpty();
        Guid.TryParse(member.Id, out _).Should().BeTrue();
    }

    [Fact]
    public void SapTeamMember_Should_Have_Default_Values()
    {
        var member = new SapTeamMember();

        member.SecurityAssessmentPlanId.Should().Be(string.Empty);
        member.Name.Should().Be(string.Empty);
        member.Organization.Should().Be(string.Empty);
        member.Role.Should().Be(string.Empty);
        member.ContactInfo.Should().BeNull();
    }

    [Fact]
    public void SapTeamMember_CanSetAllProperties()
    {
        var planId = Guid.NewGuid().ToString();
        var member = new SapTeamMember
        {
            SecurityAssessmentPlanId = planId,
            Name = "Jane Doe",
            Organization = "ACME Security",
            Role = "Lead Assessor",
            ContactInfo = "jane.doe@acme.com"
        };

        member.SecurityAssessmentPlanId.Should().Be(planId);
        member.Name.Should().Be("Jane Doe");
        member.Organization.Should().Be("ACME Security");
        member.Role.Should().Be("Lead Assessor");
        member.ContactInfo.Should().Be("jane.doe@acme.com");
    }

    [Fact]
    public void SapTeamMember_MultipleInstances_HaveUniqueIds()
    {
        var members = Enumerable.Range(0, 25).Select(_ => new SapTeamMember()).ToList();
        members.Select(m => m.Id).Distinct().Should().HaveCount(25);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SapMethodOverrideInput Record
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SapMethodOverrideInput_ConstructsWithRequiredParams()
    {
        var input = new SapMethodOverrideInput("AC-2", new List<string> { "Examine" });

        input.ControlId.Should().Be("AC-2");
        input.Methods.Should().ContainSingle().Which.Should().Be("Examine");
        input.Rationale.Should().BeNull();
    }

    [Fact]
    public void SapMethodOverrideInput_ConstructsWithAllParams()
    {
        var input = new SapMethodOverrideInput(
            "AC-2",
            new List<string> { "Examine", "Interview" },
            "Test not applicable");

        input.ControlId.Should().Be("AC-2");
        input.Methods.Should().HaveCount(2);
        input.Rationale.Should().Be("Test not applicable");
    }

    [Fact]
    public void SapMethodOverrideInput_IsRecord_WithDeconstructSupport()
    {
        var input = new SapMethodOverrideInput("AC-2", new List<string> { "Examine" }, "Reason");

        // Records support deconstruction
        var (controlId, methods, rationale) = input;
        controlId.Should().Be("AC-2");
        methods.Should().ContainSingle().Which.Should().Be("Examine");
        rationale.Should().Be("Reason");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SapTeamMemberInput Record
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SapTeamMemberInput_ConstructsWithRequiredParams()
    {
        var input = new SapTeamMemberInput("Jane Doe", "ACME", "Lead Assessor");

        input.Name.Should().Be("Jane Doe");
        input.Organization.Should().Be("ACME");
        input.Role.Should().Be("Lead Assessor");
        input.ContactInfo.Should().BeNull();
    }

    [Fact]
    public void SapTeamMemberInput_ConstructsWithAllParams()
    {
        var input = new SapTeamMemberInput("Jane Doe", "ACME", "Lead Assessor", "jane@acme.com");

        input.ContactInfo.Should().Be("jane@acme.com");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SapGenerationInput Record
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SapGenerationInput_ConstructsWithSystemIdOnly()
    {
        var input = new SapGenerationInput("sys-001");

        input.SystemId.Should().Be("sys-001");
        input.AssessmentId.Should().BeNull();
        input.ScheduleStart.Should().BeNull();
        input.ScheduleEnd.Should().BeNull();
        input.ScopeNotes.Should().BeNull();
        input.RulesOfEngagement.Should().BeNull();
        input.TeamMembers.Should().BeNull();
        input.MethodOverrides.Should().BeNull();
        input.Format.Should().Be("markdown");
    }

    [Fact]
    public void SapGenerationInput_ConstructsWithAllParams()
    {
        var now = DateTime.UtcNow;
        var teamMembers = new List<SapTeamMemberInput>
        {
            new("Jane Doe", "ACME", "Lead Assessor")
        };
        var overrides = new List<SapMethodOverrideInput>
        {
            new("AC-2", new List<string> { "Examine" })
        };

        var input = new SapGenerationInput(
            SystemId: "sys-001",
            AssessmentId: "assess-001",
            ScheduleStart: now,
            ScheduleEnd: now.AddDays(30),
            ScopeNotes: "CUI boundary only",
            RulesOfEngagement: "Business hours",
            TeamMembers: teamMembers,
            MethodOverrides: overrides,
            Format: "docx");

        input.AssessmentId.Should().Be("assess-001");
        input.ScheduleStart.Should().Be(now);
        input.ScheduleEnd.Should().Be(now.AddDays(30));
        input.ScopeNotes.Should().Be("CUI boundary only");
        input.RulesOfEngagement.Should().Be("Business hours");
        input.TeamMembers.Should().HaveCount(1);
        input.MethodOverrides.Should().HaveCount(1);
        input.Format.Should().Be("docx");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SapUpdateInput Record
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SapUpdateInput_ConstructsWithSapIdOnly()
    {
        var input = new SapUpdateInput("sap-001");

        input.SapId.Should().Be("sap-001");
        input.ScheduleStart.Should().BeNull();
        input.ScheduleEnd.Should().BeNull();
        input.ScopeNotes.Should().BeNull();
        input.RulesOfEngagement.Should().BeNull();
        input.TeamMembers.Should().BeNull();
        input.MethodOverrides.Should().BeNull();
    }

    [Fact]
    public void SapUpdateInput_ConstructsWithAllParams()
    {
        var now = DateTime.UtcNow;
        var input = new SapUpdateInput(
            SapId: "sap-001",
            ScheduleStart: now,
            ScheduleEnd: now.AddDays(14),
            ScopeNotes: "Updated scope",
            RulesOfEngagement: "After hours OK",
            TeamMembers: new List<SapTeamMemberInput>(),
            MethodOverrides: new List<SapMethodOverrideInput>());

        input.SapId.Should().Be("sap-001");
        input.ScheduleStart.Should().Be(now);
        input.ScheduleEnd.Should().Be(now.AddDays(14));
        input.ScopeNotes.Should().Be("Updated scope");
        input.RulesOfEngagement.Should().Be("After hours OK");
        input.TeamMembers.Should().BeEmpty();
        input.MethodOverrides.Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SapDocument DTO
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SapDocument_Should_Have_Default_Values()
    {
        var doc = new SapDocument();

        doc.SapId.Should().Be(string.Empty);
        doc.SystemId.Should().Be(string.Empty);
        doc.AssessmentId.Should().BeNull();
        doc.Title.Should().Be(string.Empty);
        doc.Status.Should().Be("Draft");
        doc.Format.Should().Be("markdown");
        doc.BaselineLevel.Should().Be(string.Empty);
        doc.Content.Should().Be(string.Empty);
        doc.ContentHash.Should().BeNull();
        doc.TotalControls.Should().Be(0);
        doc.CustomerControls.Should().Be(0);
        doc.InheritedControls.Should().Be(0);
        doc.SharedControls.Should().Be(0);
        doc.StigBenchmarkCount.Should().Be(0);
        doc.ControlsWithObjectives.Should().Be(0);
        doc.EvidenceGaps.Should().Be(0);
        doc.FinalizedAt.Should().BeNull();
        doc.Warnings.Should().NotBeNull().And.BeEmpty();
        doc.FamilySummaries.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void SapDocument_GeneratedAt_IsRecent()
    {
        var before = DateTime.UtcNow;
        var doc = new SapDocument();
        var after = DateTime.UtcNow;

        doc.GeneratedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void SapDocument_CanSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var doc = new SapDocument
        {
            SapId = "sap-001",
            SystemId = "sys-001",
            AssessmentId = "assess-001",
            Title = "SAP — Test",
            Status = "Finalized",
            Format = "docx",
            BaselineLevel = "Moderate",
            Content = "# SAP",
            ContentHash = new string('b', 64),
            TotalControls = 325,
            CustomerControls = 150,
            InheritedControls = 100,
            SharedControls = 75,
            StigBenchmarkCount = 12,
            ControlsWithObjectives = 300,
            EvidenceGaps = 5,
            GeneratedAt = now,
            FinalizedAt = now.AddDays(1),
            Warnings = new List<string> { "Warning 1" },
            FamilySummaries = new List<SapFamilySummary>
            {
                new() { Family = "Access Control (AC)", ControlCount = 25 }
            }
        };

        doc.SapId.Should().Be("sap-001");
        doc.Status.Should().Be("Finalized");
        doc.ContentHash.Should().HaveLength(64);
        doc.TotalControls.Should().Be(325);
        doc.ControlsWithObjectives.Should().Be(300);
        doc.EvidenceGaps.Should().Be(5);
        doc.Warnings.Should().ContainSingle();
        doc.FamilySummaries.Should().ContainSingle();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SapFamilySummary DTO
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SapFamilySummary_Should_Have_Default_Values()
    {
        var summary = new SapFamilySummary();

        summary.Family.Should().Be(string.Empty);
        summary.ControlCount.Should().Be(0);
        summary.CustomerCount.Should().Be(0);
        summary.InheritedCount.Should().Be(0);
        summary.Methods.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void SapFamilySummary_CanSetAllProperties()
    {
        var summary = new SapFamilySummary
        {
            Family = "Access Control (AC)",
            ControlCount = 25,
            CustomerCount = 15,
            InheritedCount = 10,
            Methods = new List<string> { "Examine", "Interview", "Test" }
        };

        summary.Family.Should().Be("Access Control (AC)");
        summary.ControlCount.Should().Be(25);
        summary.CustomerCount.Should().Be(15);
        summary.InheritedCount.Should().Be(10);
        summary.Methods.Should().HaveCount(3);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SapValidationResult DTO
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SapValidationResult_Should_Have_Default_Values()
    {
        var result = new SapValidationResult();

        result.IsComplete.Should().BeFalse();
        result.Warnings.Should().NotBeNull().And.BeEmpty();
        result.ControlsCovered.Should().Be(0);
        result.ControlsMissingObjectives.Should().Be(0);
        result.ControlsMissingMethods.Should().Be(0);
        result.HasTeam.Should().BeFalse();
        result.HasSchedule.Should().BeFalse();
    }

    [Fact]
    public void SapValidationResult_CanSetAllProperties()
    {
        var result = new SapValidationResult
        {
            IsComplete = true,
            Warnings = new List<string> { "No schedule set" },
            ControlsCovered = 325,
            ControlsMissingObjectives = 5,
            ControlsMissingMethods = 0,
            HasTeam = true,
            HasSchedule = false
        };

        result.IsComplete.Should().BeTrue();
        result.Warnings.Should().ContainSingle();
        result.ControlsCovered.Should().Be(325);
        result.ControlsMissingObjectives.Should().Be(5);
        result.ControlsMissingMethods.Should().Be(0);
        result.HasTeam.Should().BeTrue();
        result.HasSchedule.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Entity Relationships (Navigation Property Types)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SecurityAssessmentPlan_ControlEntries_CanBePopulated()
    {
        var sap = new SecurityAssessmentPlan();
        sap.ControlEntries.Add(new SapControlEntry { ControlId = "AC-1" });
        sap.ControlEntries.Add(new SapControlEntry { ControlId = "AC-2" });

        sap.ControlEntries.Should().HaveCount(2);
    }

    [Fact]
    public void SecurityAssessmentPlan_TeamMembers_CanBePopulated()
    {
        var sap = new SecurityAssessmentPlan();
        sap.TeamMembers.Add(new SapTeamMember { Name = "Jane Doe", Role = "Lead Assessor" });
        sap.TeamMembers.Add(new SapTeamMember { Name = "John Smith", Role = "Assessor" });

        sap.TeamMembers.Should().HaveCount(2);
    }

    [Fact]
    public void SapControlEntry_Navigation_BackToParent()
    {
        var sap = new SecurityAssessmentPlan { Id = "sap-123" };
        var entry = new SapControlEntry
        {
            SecurityAssessmentPlanId = sap.Id,
            SecurityAssessmentPlan = sap
        };

        entry.SecurityAssessmentPlan.Should().BeSameAs(sap);
        entry.SecurityAssessmentPlanId.Should().Be("sap-123");
    }

    [Fact]
    public void SapTeamMember_Navigation_BackToParent()
    {
        var sap = new SecurityAssessmentPlan { Id = "sap-456" };
        var member = new SapTeamMember
        {
            SecurityAssessmentPlanId = sap.Id,
            SecurityAssessmentPlan = sap
        };

        member.SecurityAssessmentPlan.Should().BeSameAs(sap);
        member.SecurityAssessmentPlanId.Should().Be("sap-456");
    }
}
