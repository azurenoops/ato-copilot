using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Compliance.EvidenceCollectors;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Tests.Unit.EvidenceCollectors;

/// <summary>
/// Tests for <see cref="EvidenceCollectorRegistry"/>: dispatch to specialized collector,
/// fallback to default, edge cases.
/// </summary>
public class EvidenceCollectorRegistryTests
{
    private readonly Mock<IEvidenceCollector> _defaultCollector;
    private readonly ILogger<EvidenceCollectorRegistry> _logger;

    public EvidenceCollectorRegistryTests()
    {
        _defaultCollector = new Mock<IEvidenceCollector>();
        _defaultCollector.Setup(c => c.FamilyCode).Returns("DEFAULT");

        _logger = Mock.Of<ILogger<EvidenceCollectorRegistry>>();
    }

    private EvidenceCollectorRegistry CreateRegistry(params Mock<IEvidenceCollector>[] specialized)
    {
        var collectors = new List<IEvidenceCollector> { _defaultCollector.Object };
        collectors.AddRange(specialized.Select(m => m.Object));
        return new EvidenceCollectorRegistry(collectors, _logger);
    }

    // ─── Constructor ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsIfNoDefaultCollector()
    {
        var nonDefault = new Mock<IEvidenceCollector>();
        nonDefault.Setup(c => c.FamilyCode).Returns("AC");

        var collectors = new[] { nonDefault.Object };

        var act = () => new EvidenceCollectorRegistry(collectors, _logger);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*DefaultEvidenceCollector*");
    }

    [Fact]
    public void Constructor_SucceedsWithOnlyDefault()
    {
        var registry = CreateRegistry();
        registry.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_EmptyList_Throws()
    {
        var act = () => new EvidenceCollectorRegistry(
            Enumerable.Empty<IEvidenceCollector>(), _logger);

        act.Should().Throw<InvalidOperationException>();
    }

    // ─── GetCollector Dispatch ──────────────────────────────────────────

    [Fact]
    public void GetCollector_ReturnsSpecializedCollector_WhenRegistered()
    {
        var acCollector = new Mock<IEvidenceCollector>();
        acCollector.Setup(c => c.FamilyCode).Returns("AC");

        var registry = CreateRegistry(acCollector);

        registry.GetCollector("AC").Should().BeSameAs(acCollector.Object);
    }

    [Fact]
    public void GetCollector_ReturnsDefault_WhenNoSpecializedRegistered()
    {
        var registry = CreateRegistry();

        registry.GetCollector("AC").Should().BeSameAs(_defaultCollector.Object);
    }

    [Fact]
    public void GetCollector_IsCaseInsensitive()
    {
        var iaCollector = new Mock<IEvidenceCollector>();
        iaCollector.Setup(c => c.FamilyCode).Returns("IA");

        var registry = CreateRegistry(iaCollector);

        registry.GetCollector("ia").Should().BeSameAs(iaCollector.Object);
        registry.GetCollector("IA").Should().BeSameAs(iaCollector.Object);
        registry.GetCollector("Ia").Should().BeSameAs(iaCollector.Object);
    }

    [Fact]
    public void GetCollector_MultipleSpecialized_DispatchesCorrectly()
    {
        var acCollector = new Mock<IEvidenceCollector>();
        acCollector.Setup(c => c.FamilyCode).Returns("AC");

        var iaCollector = new Mock<IEvidenceCollector>();
        iaCollector.Setup(c => c.FamilyCode).Returns("IA");

        var scCollector = new Mock<IEvidenceCollector>();
        scCollector.Setup(c => c.FamilyCode).Returns("SC");

        var registry = CreateRegistry(acCollector, iaCollector, scCollector);

        registry.GetCollector("AC").Should().BeSameAs(acCollector.Object);
        registry.GetCollector("IA").Should().BeSameAs(iaCollector.Object);
        registry.GetCollector("SC").Should().BeSameAs(scCollector.Object);
        registry.GetCollector("AU").Should().BeSameAs(_defaultCollector.Object);
    }

    // ─── All 20 Families ────────────────────────────────────────────────

    [Theory]
    [InlineData("AC")]
    [InlineData("AT")]
    [InlineData("AU")]
    [InlineData("CA")]
    [InlineData("CM")]
    [InlineData("CP")]
    [InlineData("IA")]
    [InlineData("IR")]
    [InlineData("MA")]
    [InlineData("MP")]
    [InlineData("PE")]
    [InlineData("PL")]
    [InlineData("PM")]
    [InlineData("PS")]
    [InlineData("PT")]
    [InlineData("RA")]
    [InlineData("SA")]
    [InlineData("SC")]
    [InlineData("SI")]
    [InlineData("SR")]
    public void GetCollector_All20Families_ReturnDefault_WhenNoSpecialized(string familyCode)
    {
        var registry = CreateRegistry();

        registry.GetCollector(familyCode).Should().BeSameAs(_defaultCollector.Object);
    }

    // ─── Edge Cases ─────────────────────────────────────────────────────

    [Fact]
    public void GetCollector_UnknownFamilyCode_ReturnsDefault()
    {
        var registry = CreateRegistry();

        registry.GetCollector("XX").Should().BeSameAs(_defaultCollector.Object);
        registry.GetCollector("").Should().BeSameAs(_defaultCollector.Object);
    }

    [Fact]
    public void GetCollector_ReturnsSameInstanceOnRepeatedCalls()
    {
        var acCollector = new Mock<IEvidenceCollector>();
        acCollector.Setup(c => c.FamilyCode).Returns("AC");

        var registry = CreateRegistry(acCollector);

        var first = registry.GetCollector("AC");
        var second = registry.GetCollector("AC");

        first.Should().BeSameAs(second);
    }
}
