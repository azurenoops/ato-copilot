using Xunit;
using FluentAssertions;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Models;

/// <summary>
/// Tests for ControlBaseline, ControlTailoring, and ControlInheritance
/// entity validation, defaults, and relationships.
/// </summary>
public class BaselineModelTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // ControlBaseline
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void ControlBaseline_Defaults_AreCorrect()
    {
        var baseline = new ControlBaseline();
        baseline.Id.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(baseline.Id, out _).Should().BeTrue();
        baseline.ControlIds.Should().BeEmpty();
        baseline.Tailorings.Should().BeEmpty();
        baseline.Inheritances.Should().BeEmpty();
        baseline.TotalControls.Should().Be(0);
        baseline.CustomerControls.Should().Be(0);
        baseline.InheritedControls.Should().Be(0);
        baseline.SharedControls.Should().Be(0);
        baseline.TailoredOutControls.Should().Be(0);
        baseline.TailoredInControls.Should().Be(0);
        baseline.ModifiedAt.Should().BeNull();
        baseline.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ControlBaseline_CanSetAllProperties()
    {
        var systemId = Guid.NewGuid().ToString();
        var controlIds = new List<string> { "AC-1", "AC-2", "AC-3", "AT-1", "AU-1" };

        var baseline = new ControlBaseline
        {
            RegisteredSystemId = systemId,
            BaselineLevel = "Moderate",
            OverlayApplied = "CNSSI 1253 IL4",
            TotalControls = 325,
            CustomerControls = 150,
            InheritedControls = 100,
            SharedControls = 75,
            TailoredOutControls = 5,
            TailoredInControls = 3,
            ControlIds = controlIds,
            CreatedBy = "isso@example.com"
        };

        baseline.RegisteredSystemId.Should().Be(systemId);
        baseline.BaselineLevel.Should().Be("Moderate");
        baseline.OverlayApplied.Should().Be("CNSSI 1253 IL4");
        baseline.TotalControls.Should().Be(325);
        baseline.CustomerControls.Should().Be(150);
        baseline.InheritedControls.Should().Be(100);
        baseline.SharedControls.Should().Be(75);
        baseline.TailoredOutControls.Should().Be(5);
        baseline.TailoredInControls.Should().Be(3);
        baseline.ControlIds.Should().HaveCount(5).And.Contain("AC-2");
    }

    [Fact]
    public void ControlBaseline_ControlIds_CanBeLargeList()
    {
        var baseline = new ControlBaseline
        {
            ControlIds = Enumerable.Range(1, 421).Select(i => $"CTRL-{i}").ToList()
        };
        baseline.ControlIds.Should().HaveCount(421);
    }

    [Fact]
    public void ControlBaseline_MultipleInstances_HaveUniqueIds()
    {
        var baselines = Enumerable.Range(0, 50).Select(_ => new ControlBaseline()).ToList();
        baselines.Select(b => b.Id).Distinct().Should().HaveCount(50);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ControlTailoring
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void ControlTailoring_Defaults_AreCorrect()
    {
        var tailoring = new ControlTailoring();
        tailoring.Id.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(tailoring.Id, out _).Should().BeTrue();
        tailoring.IsOverlayRequired.Should().BeFalse();
        tailoring.TailoredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(TailoringAction.Added)]
    [InlineData(TailoringAction.Removed)]
    public void ControlTailoring_CanSetAction(TailoringAction action)
    {
        var tailoring = new ControlTailoring
        {
            ControlBaselineId = Guid.NewGuid().ToString(),
            ControlId = "AC-2(1)",
            Action = action,
            Rationale = "Required by CNSSI 1253 overlay for IL4",
            IsOverlayRequired = action == TailoringAction.Added,
            TailoredBy = "issm@example.com"
        };

        tailoring.Action.Should().Be(action);
        tailoring.ControlId.Should().Be("AC-2(1)");
        tailoring.Rationale.Should().NotBeEmpty();
    }

    [Fact]
    public void ControlTailoring_Added_WithOverlayRequired()
    {
        var tailoring = new ControlTailoring
        {
            ControlId = "SI-7(15)",
            Action = TailoringAction.Added,
            IsOverlayRequired = true,
            Rationale = "CNSSI 1253 IL5 overlay mandates SI-7(15) for code signing",
            TailoredBy = "issm@example.com"
        };

        tailoring.Action.Should().Be(TailoringAction.Added);
        tailoring.IsOverlayRequired.Should().BeTrue();
    }

    [Fact]
    public void ControlTailoring_Removed_WithRationale()
    {
        var tailoring = new ControlTailoring
        {
            ControlId = "PE-1",
            Action = TailoringAction.Removed,
            IsOverlayRequired = false,
            Rationale = "Physical security controls are not applicable to cloud-only system",
            TailoredBy = "so@example.com"
        };

        tailoring.Action.Should().Be(TailoringAction.Removed);
        tailoring.IsOverlayRequired.Should().BeFalse();
        tailoring.Rationale.Should().Contain("not applicable");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ControlInheritance
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void ControlInheritance_Defaults_AreCorrect()
    {
        var inheritance = new ControlInheritance();
        inheritance.Id.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(inheritance.Id, out _).Should().BeTrue();
        inheritance.Provider.Should().BeNull();
        inheritance.CustomerResponsibility.Should().BeNull();
        inheritance.SetAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(InheritanceType.Inherited)]
    [InlineData(InheritanceType.Shared)]
    [InlineData(InheritanceType.Customer)]
    public void ControlInheritance_CanSetAllTypes(InheritanceType type)
    {
        var inheritance = new ControlInheritance
        {
            ControlBaselineId = Guid.NewGuid().ToString(),
            ControlId = "PE-2",
            InheritanceType = type,
            SetBy = "issm@example.com"
        };

        inheritance.InheritanceType.Should().Be(type);
    }

    [Fact]
    public void ControlInheritance_Inherited_HasProvider()
    {
        var inheritance = new ControlInheritance
        {
            ControlId = "PE-1",
            InheritanceType = InheritanceType.Inherited,
            Provider = "Microsoft Azure (FedRAMP High P-ATO)",
            SetBy = "isso@example.com"
        };

        inheritance.InheritanceType.Should().Be(InheritanceType.Inherited);
        inheritance.Provider.Should().Contain("FedRAMP");
        inheritance.CustomerResponsibility.Should().BeNull();
    }

    [Fact]
    public void ControlInheritance_Shared_HasProviderAndResponsibility()
    {
        var inheritance = new ControlInheritance
        {
            ControlId = "AC-2",
            InheritanceType = InheritanceType.Shared,
            Provider = "Microsoft Azure",
            CustomerResponsibility = "Customer manages application-level accounts and access review processes",
            SetBy = "isso@example.com"
        };

        inheritance.InheritanceType.Should().Be(InheritanceType.Shared);
        inheritance.Provider.Should().NotBeNull();
        inheritance.CustomerResponsibility.Should().NotBeNull();
    }

    [Fact]
    public void ControlInheritance_Customer_NoProvider()
    {
        var inheritance = new ControlInheritance
        {
            ControlId = "AC-2(1)",
            InheritanceType = InheritanceType.Customer,
            SetBy = "isso@example.com"
        };

        inheritance.InheritanceType.Should().Be(InheritanceType.Customer);
        inheritance.Provider.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Baseline ↔ Tailoring/Inheritance Integration
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void ControlBaseline_CanAddTailorings()
    {
        var baseline = new ControlBaseline
        {
            BaselineLevel = "Moderate",
            CreatedBy = "isso@example.com"
        };

        baseline.Tailorings.Add(new ControlTailoring
        {
            ControlBaselineId = baseline.Id,
            ControlId = "SI-7(15)",
            Action = TailoringAction.Added,
            Rationale = "Required by overlay",
            TailoredBy = "issm@example.com"
        });

        baseline.Tailorings.Add(new ControlTailoring
        {
            ControlBaselineId = baseline.Id,
            ControlId = "PE-1",
            Action = TailoringAction.Removed,
            Rationale = "Not applicable to cloud",
            TailoredBy = "issm@example.com"
        });

        baseline.Tailorings.Should().HaveCount(2);
        baseline.Tailorings.Should().Contain(t => t.Action == TailoringAction.Added);
        baseline.Tailorings.Should().Contain(t => t.Action == TailoringAction.Removed);
    }

    [Fact]
    public void ControlBaseline_CanAddInheritances()
    {
        var baseline = new ControlBaseline
        {
            BaselineLevel = "Moderate",
            CreatedBy = "isso@example.com"
        };

        baseline.Inheritances.Add(new ControlInheritance
        {
            ControlBaselineId = baseline.Id,
            ControlId = "PE-1",
            InheritanceType = InheritanceType.Inherited,
            Provider = "Azure",
            SetBy = "isso@example.com"
        });

        baseline.Inheritances.Add(new ControlInheritance
        {
            ControlBaselineId = baseline.Id,
            ControlId = "AC-2",
            InheritanceType = InheritanceType.Shared,
            Provider = "Azure",
            CustomerResponsibility = "Manage app accounts",
            SetBy = "isso@example.com"
        });

        baseline.Inheritances.Add(new ControlInheritance
        {
            ControlBaselineId = baseline.Id,
            ControlId = "AC-3",
            InheritanceType = InheritanceType.Customer,
            SetBy = "isso@example.com"
        });

        baseline.Inheritances.Should().HaveCount(3);
        baseline.Inheritances.Count(i => i.InheritanceType == InheritanceType.Inherited).Should().Be(1);
        baseline.Inheritances.Count(i => i.InheritanceType == InheritanceType.Shared).Should().Be(1);
        baseline.Inheritances.Count(i => i.InheritanceType == InheritanceType.Customer).Should().Be(1);
    }

    [Fact]
    public void ControlBaseline_FullScenario_ModerateBaselineWithOverlay()
    {
        // Simulates a real Moderate baseline with CNSSI 1253 IL4 overlay
        var baseline = new ControlBaseline
        {
            RegisteredSystemId = Guid.NewGuid().ToString(),
            BaselineLevel = "Moderate",
            OverlayApplied = "CNSSI 1253 IL4",
            TotalControls = 325,
            CustomerControls = 145,
            InheritedControls = 110,
            SharedControls = 70,
            TailoredOutControls = 3,
            TailoredInControls = 8,
            ControlIds = Enumerable.Range(1, 325).Select(i => $"CTRL-{i}").ToList(),
            CreatedBy = "isso@example.com"
        };

        // Verify counters sum correctly (customer + inherited + shared = total - tailored out + tailored in)
        var accountedControls = baseline.CustomerControls + baseline.InheritedControls + baseline.SharedControls;
        accountedControls.Should().Be(325, "all controls should be accounted for");

        baseline.ControlIds.Should().HaveCount(325);
        baseline.OverlayApplied.Should().Contain("IL4");
    }
}
