using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Compliance.Scanners;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Scanners;

/// <summary>
/// Tests for <see cref="ScannerRegistry"/>: dispatch to specialized scanner,
/// fallback to default, all 20 NIST families.
/// </summary>
public class ScannerRegistryTests
{
    private readonly Mock<IComplianceScanner> _defaultScanner;
    private readonly ILogger<ScannerRegistry> _logger;

    public ScannerRegistryTests()
    {
        _defaultScanner = new Mock<IComplianceScanner>();
        _defaultScanner.Setup(s => s.FamilyCode).Returns("DEFAULT");

        _logger = Mock.Of<ILogger<ScannerRegistry>>();
    }

    private ScannerRegistry CreateRegistry(params Mock<IComplianceScanner>[] specialized)
    {
        var scanners = new List<IComplianceScanner> { _defaultScanner.Object };
        scanners.AddRange(specialized.Select(m => m.Object));
        return new ScannerRegistry(scanners, _logger);
    }

    // ─── Constructor ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsIfNoDefaultScanner()
    {
        var nonDefault = new Mock<IComplianceScanner>();
        nonDefault.Setup(s => s.FamilyCode).Returns("AC");

        var scanners = new[] { nonDefault.Object };

        var act = () => new ScannerRegistry(scanners, _logger);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*DefaultComplianceScanner*");
    }

    [Fact]
    public void Constructor_SucceedsWithOnlyDefault()
    {
        var scanners = new[] { _defaultScanner.Object };
        var registry = new ScannerRegistry(scanners, _logger);
        registry.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_SucceedsWithSpecializedScanners()
    {
        var acScanner = new Mock<IComplianceScanner>();
        acScanner.Setup(s => s.FamilyCode).Returns("AC");

        var iaScanner = new Mock<IComplianceScanner>();
        iaScanner.Setup(s => s.FamilyCode).Returns("IA");

        var registry = CreateRegistry(acScanner, iaScanner);
        registry.Should().NotBeNull();
    }

    // ─── GetScanner Dispatch ────────────────────────────────────────────

    [Fact]
    public void GetScanner_ReturnsSpecializedScanner_WhenRegistered()
    {
        var acScanner = new Mock<IComplianceScanner>();
        acScanner.Setup(s => s.FamilyCode).Returns("AC");

        var registry = CreateRegistry(acScanner);

        var result = registry.GetScanner("AC");

        result.Should().BeSameAs(acScanner.Object);
    }

    [Fact]
    public void GetScanner_ReturnsDefault_WhenNoSpecializedRegistered()
    {
        var registry = CreateRegistry();

        var result = registry.GetScanner("AC");

        result.Should().BeSameAs(_defaultScanner.Object);
    }

    [Fact]
    public void GetScanner_IsCaseInsensitive()
    {
        var scScanner = new Mock<IComplianceScanner>();
        scScanner.Setup(s => s.FamilyCode).Returns("SC");

        var registry = CreateRegistry(scScanner);

        registry.GetScanner("sc").Should().BeSameAs(scScanner.Object);
        registry.GetScanner("SC").Should().BeSameAs(scScanner.Object);
        registry.GetScanner("Sc").Should().BeSameAs(scScanner.Object);
    }

    [Fact]
    public void GetScanner_MultipleSpecialized_DispatchesCorrectly()
    {
        var acScanner = new Mock<IComplianceScanner>();
        acScanner.Setup(s => s.FamilyCode).Returns("AC");

        var iaScanner = new Mock<IComplianceScanner>();
        iaScanner.Setup(s => s.FamilyCode).Returns("IA");

        var scScanner = new Mock<IComplianceScanner>();
        scScanner.Setup(s => s.FamilyCode).Returns("SC");

        var registry = CreateRegistry(acScanner, iaScanner, scScanner);

        registry.GetScanner("AC").Should().BeSameAs(acScanner.Object);
        registry.GetScanner("IA").Should().BeSameAs(iaScanner.Object);
        registry.GetScanner("SC").Should().BeSameAs(scScanner.Object);
        registry.GetScanner("AU").Should().BeSameAs(_defaultScanner.Object);
    }

    // ─── All 20 NIST Families Fallback ──────────────────────────────────

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
    public void GetScanner_All20Families_ReturnDefault_WhenNoSpecialized(string familyCode)
    {
        var registry = CreateRegistry();

        var result = registry.GetScanner(familyCode);

        result.Should().BeSameAs(_defaultScanner.Object);
    }

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
    public void GetScanner_All20Families_ReturnSpecialized_WhenAllRegistered(string familyCode)
    {
        var scanners = ControlFamilies.AllFamilies.Select(code =>
        {
            var mock = new Mock<IComplianceScanner>();
            mock.Setup(s => s.FamilyCode).Returns(code);
            return mock;
        }).ToArray();

        var registry = CreateRegistry(scanners);

        var result = registry.GetScanner(familyCode);
        result.FamilyCode.Should().Be(familyCode);
    }

    // ─── Edge Cases ─────────────────────────────────────────────────────

    [Fact]
    public void GetScanner_UnknownFamilyCode_ReturnsDefault()
    {
        var registry = CreateRegistry();

        registry.GetScanner("XX").Should().BeSameAs(_defaultScanner.Object);
        registry.GetScanner("").Should().BeSameAs(_defaultScanner.Object);
        registry.GetScanner("UNKNOWN").Should().BeSameAs(_defaultScanner.Object);
    }

    [Fact]
    public void Constructor_EmptyList_Throws()
    {
        var act = () => new ScannerRegistry(Enumerable.Empty<IComplianceScanner>(), _logger);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetScanner_ReturnsSameInstanceOnRepeatedCalls()
    {
        var acScanner = new Mock<IComplianceScanner>();
        acScanner.Setup(s => s.FamilyCode).Returns("AC");

        var registry = CreateRegistry(acScanner);

        var first = registry.GetScanner("AC");
        var second = registry.GetScanner("AC");

        first.Should().BeSameAs(second);
    }
}
