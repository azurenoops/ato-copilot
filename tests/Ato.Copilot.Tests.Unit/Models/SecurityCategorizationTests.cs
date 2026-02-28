using Xunit;
using FluentAssertions;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Models;

/// <summary>
/// Tests for SecurityCategorization computed properties (high-water mark
/// C/I/A impacts, DoD IL derivation, FIPS 199 notation) and InformationType
/// entity behaviour.
/// </summary>
public class SecurityCategorizationTests
{
    // ─── Defaults ────────────────────────────────────────────────────────

    [Fact]
    public void SecurityCategorization_Defaults_AreCorrect()
    {
        var cat = new SecurityCategorization();
        cat.Id.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(cat.Id, out _).Should().BeTrue();
        cat.IsNationalSecuritySystem.Should().BeFalse();
        cat.InformationTypes.Should().BeEmpty();
        cat.CategorizedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        cat.ModifiedAt.Should().BeNull();
    }

    // ─── Computed Properties (No Information Types) ──────────────────────

    [Fact]
    public void ConfidentialityImpact_NoInfoTypes_ReturnsLow()
    {
        var cat = new SecurityCategorization();
        cat.ConfidentialityImpact.Should().Be(ImpactValue.Low);
    }

    [Fact]
    public void IntegrityImpact_NoInfoTypes_ReturnsLow()
    {
        var cat = new SecurityCategorization();
        cat.IntegrityImpact.Should().Be(ImpactValue.Low);
    }

    [Fact]
    public void AvailabilityImpact_NoInfoTypes_ReturnsLow()
    {
        var cat = new SecurityCategorization();
        cat.AvailabilityImpact.Should().Be(ImpactValue.Low);
    }

    [Fact]
    public void OverallCategorization_NoInfoTypes_ReturnsLow()
    {
        var cat = new SecurityCategorization();
        cat.OverallCategorization.Should().Be(ImpactValue.Low);
    }

    // ─── High-Water Mark (single info type) ──────────────────────────────

    [Theory]
    [InlineData(ImpactValue.Low, ImpactValue.Low, ImpactValue.Low, ImpactValue.Low)]
    [InlineData(ImpactValue.Moderate, ImpactValue.Low, ImpactValue.Low, ImpactValue.Moderate)]
    [InlineData(ImpactValue.Low, ImpactValue.High, ImpactValue.Low, ImpactValue.High)]
    [InlineData(ImpactValue.Low, ImpactValue.Low, ImpactValue.Moderate, ImpactValue.Moderate)]
    [InlineData(ImpactValue.High, ImpactValue.High, ImpactValue.High, ImpactValue.High)]
    public void OverallCategorization_SingleInfoType_HighWaterMark(
        ImpactValue c, ImpactValue i, ImpactValue a, ImpactValue expected)
    {
        var cat = CreateCategorization(new[] { (c, i, a) });
        cat.OverallCategorization.Should().Be(expected);
    }

    // ─── High-Water Mark (multiple info types) ───────────────────────────

    [Fact]
    public void OverallCategorization_MultipleInfoTypes_TakesMaxOfEachDimension()
    {
        var cat = CreateCategorization(new[]
        {
            (ImpactValue.Low, ImpactValue.Moderate, ImpactValue.Low),
            (ImpactValue.Moderate, ImpactValue.Low, ImpactValue.High),
            (ImpactValue.Low, ImpactValue.Low, ImpactValue.Low)
        });

        cat.ConfidentialityImpact.Should().Be(ImpactValue.Moderate);
        cat.IntegrityImpact.Should().Be(ImpactValue.Moderate);
        cat.AvailabilityImpact.Should().Be(ImpactValue.High);
        cat.OverallCategorization.Should().Be(ImpactValue.High);
    }

    [Fact]
    public void OverallCategorization_AllLow_ReturnsLow()
    {
        var cat = CreateCategorization(new[]
        {
            (ImpactValue.Low, ImpactValue.Low, ImpactValue.Low),
            (ImpactValue.Low, ImpactValue.Low, ImpactValue.Low)
        });

        cat.OverallCategorization.Should().Be(ImpactValue.Low);
    }

    [Fact]
    public void OverallCategorization_MixedModerateHigh_ReturnsHigh()
    {
        var cat = CreateCategorization(new[]
        {
            (ImpactValue.Moderate, ImpactValue.Moderate, ImpactValue.Moderate),
            (ImpactValue.Low, ImpactValue.Low, ImpactValue.High)
        });

        cat.OverallCategorization.Should().Be(ImpactValue.High);
    }

    // ─── DoD Impact Level Derivation ─────────────────────────────────────

    [Theory]
    [InlineData(ImpactValue.Low, false, null, "IL2")]
    [InlineData(ImpactValue.Moderate, false, null, "IL4")]
    [InlineData(ImpactValue.High, false, null, "IL5")]
    [InlineData(ImpactValue.Low, true, null, "IL2")]
    [InlineData(ImpactValue.Moderate, true, null, "IL4")]
    [InlineData(ImpactValue.High, true, null, "IL5")]
    [InlineData(ImpactValue.High, true, "Secret", "IL6")]
    [InlineData(ImpactValue.Moderate, true, "TopSecret", "IL6")]
    public void DoDImpactLevel_DeriveCorrectly(
        ImpactValue overall, bool isNss, string? classified, string expectedIl)
    {
        var system = new RegisteredSystem
        {
            Name = "Test",
            ClassifiedDesignation = classified
        };

        var cat = CreateCategorization(new[] { (overall, overall, overall) });
        cat.IsNationalSecuritySystem = isNss;
        cat.RegisteredSystem = system;

        cat.DoDImpactLevel.Should().Be(expectedIl);
    }

    // ─── NIST Baseline Derivation ────────────────────────────────────────

    [Theory]
    [InlineData(ImpactValue.Low, "Low")]
    [InlineData(ImpactValue.Moderate, "Moderate")]
    [InlineData(ImpactValue.High, "High")]
    public void NistBaseline_DeriveCorrectly(ImpactValue overall, string expected)
    {
        var cat = CreateCategorization(new[] { (overall, overall, overall) });
        cat.RegisteredSystem = new RegisteredSystem { Name = "Test" };

        cat.NistBaseline.Should().Be(expected);
    }

    // ─── FIPS 199 Formal Notation ────────────────────────────────────────

    [Fact]
    public void FormalNotation_AllModerate_ReturnsCorrectFormat()
    {
        var cat = CreateCategorization(new[]
        {
            (ImpactValue.Moderate, ImpactValue.Moderate, ImpactValue.Moderate)
        });
        cat.RegisteredSystem = new RegisteredSystem { Name = "ACME Portal" };

        cat.FormalNotation.Should().Be(
            "SC ACME Portal = {(confidentiality, MODERATE), (integrity, MODERATE), (availability, MODERATE)}");
    }

    [Fact]
    public void FormalNotation_MixedImpacts_ReturnsCorrectFormat()
    {
        var cat = CreateCategorization(new[]
        {
            (ImpactValue.High, ImpactValue.Low, ImpactValue.Moderate)
        });
        cat.RegisteredSystem = new RegisteredSystem { Name = "SysA" };

        cat.FormalNotation.Should().Be(
            "SC SysA = {(confidentiality, HIGH), (integrity, LOW), (availability, MODERATE)}");
    }

    [Fact]
    public void FormalNotation_NoSystem_UsesDefaultName()
    {
        var cat = CreateCategorization(new[]
        {
            (ImpactValue.Low, ImpactValue.Low, ImpactValue.Low)
        });
        // RegisteredSystem is null — computed property uses "System" as default

        cat.FormalNotation.Should().Contain("SC System =");
    }

    // ─── InformationType Entity ──────────────────────────────────────────

    [Fact]
    public void InformationType_Defaults_AreCorrect()
    {
        var it = new InformationType();
        it.Id.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(it.Id, out _).Should().BeTrue();
        it.UsesProvisionalImpactLevels.Should().BeTrue();
        it.AdjustmentJustification.Should().BeNull();
    }

    [Fact]
    public void InformationType_CanSetAllProperties()
    {
        var it = new InformationType
        {
            SecurityCategorizationId = Guid.NewGuid().ToString(),
            Sp80060Id = "D.1.1",
            Name = "Budget Formulation",
            Category = "Government Resource Management",
            ConfidentialityImpact = ImpactValue.Moderate,
            IntegrityImpact = ImpactValue.Moderate,
            AvailabilityImpact = ImpactValue.Low,
            UsesProvisionalImpactLevels = false,
            AdjustmentJustification = "System processes limited budget data"
        };

        it.Sp80060Id.Should().Be("D.1.1");
        it.Name.Should().Be("Budget Formulation");
        it.Category.Should().Be("Government Resource Management");
        it.ConfidentialityImpact.Should().Be(ImpactValue.Moderate);
        it.IntegrityImpact.Should().Be(ImpactValue.Moderate);
        it.AvailabilityImpact.Should().Be(ImpactValue.Low);
        it.UsesProvisionalImpactLevels.Should().BeFalse();
        it.AdjustmentJustification.Should().NotBeNull();
    }

    [Fact]
    public void InformationType_MultipleInstances_HaveUniqueIds()
    {
        var types = Enumerable.Range(0, 50).Select(_ => new InformationType()).ToList();
        types.Select(t => t.Id).Distinct().Should().HaveCount(50);
    }

    // ─── SecurityCategorization with InformationTypes Integration ────────

    [Fact]
    public void SecurityCategorization_WithManyInfoTypes_ComputesCorrectHighWaterMark()
    {
        // Simulate a real categorization with 5 info types of varying impacts
        var cat = CreateCategorization(new[]
        {
            (ImpactValue.Low, ImpactValue.Low, ImpactValue.Moderate),         // D.1.1
            (ImpactValue.Moderate, ImpactValue.Low, ImpactValue.Low),         // D.2.1
            (ImpactValue.Low, ImpactValue.Moderate, ImpactValue.Low),         // D.3.1
            (ImpactValue.Low, ImpactValue.Low, ImpactValue.Low),              // D.4.1
            (ImpactValue.High, ImpactValue.Moderate, ImpactValue.Moderate),   // D.5.1
        });

        cat.ConfidentialityImpact.Should().Be(ImpactValue.High);
        cat.IntegrityImpact.Should().Be(ImpactValue.Moderate);
        cat.AvailabilityImpact.Should().Be(ImpactValue.Moderate);
        cat.OverallCategorization.Should().Be(ImpactValue.High);
    }

    [Fact]
    public void SecurityCategorization_CanAddInfoType_AfterCreation()
    {
        var cat = new SecurityCategorization();
        cat.OverallCategorization.Should().Be(ImpactValue.Low);

        cat.InformationTypes.Add(new InformationType
        {
            ConfidentialityImpact = ImpactValue.High,
            IntegrityImpact = ImpactValue.Low,
            AvailabilityImpact = ImpactValue.Low
        });

        cat.ConfidentialityImpact.Should().Be(ImpactValue.High);
        cat.OverallCategorization.Should().Be(ImpactValue.High);
    }

    // ─── Helper ──────────────────────────────────────────────────────────

    private static SecurityCategorization CreateCategorization(
        (ImpactValue c, ImpactValue i, ImpactValue a)[] impacts)
    {
        var cat = new SecurityCategorization
        {
            CategorizedBy = "test@example.com"
        };

        var counter = 1;
        foreach (var (c, i, a) in impacts)
        {
            cat.InformationTypes.Add(new InformationType
            {
                SecurityCategorizationId = cat.Id,
                Sp80060Id = $"D.{counter}.1",
                Name = $"Info Type {counter}",
                ConfidentialityImpact = c,
                IntegrityImpact = i,
                AvailabilityImpact = a
            });
            counter++;
        }

        return cat;
    }
}
