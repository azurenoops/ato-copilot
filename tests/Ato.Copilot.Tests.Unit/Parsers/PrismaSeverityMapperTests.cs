// ═══════════════════════════════════════════════════════════════════════════
// Feature 019 — Prisma Cloud Scan Import: Severity Mapper Tests
// TDD: Tests written FIRST (red), then implementation makes them green.
// ═══════════════════════════════════════════════════════════════════════════

using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services.ScanImport;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Parsers;

/// <summary>
/// Unit tests for <see cref="PrismaSeverityMapper"/> — maps Prisma Cloud severity strings
/// to DoD CAT severity and ComplianceFinding severity values.
/// </summary>
public class PrismaSeverityMapperTests
{
    // ─── MapToCatSeverity ────────────────────────────────────────────────────

    [Theory]
    [InlineData("critical", CatSeverity.CatI)]
    [InlineData("high", CatSeverity.CatI)]
    [InlineData("medium", CatSeverity.CatII)]
    [InlineData("low", CatSeverity.CatIII)]
    public void MapToCatSeverity_StandardValues_ReturnsCorrectCat(string severity, CatSeverity expected)
    {
        PrismaSeverityMapper.MapToCatSeverity(severity).Should().Be(expected);
    }

    [Fact]
    public void MapToCatSeverity_Informational_ReturnsNull()
    {
        // Informational has no CAT equivalent per data-model.md
        PrismaSeverityMapper.MapToCatSeverity("informational").Should().BeNull();
    }

    [Fact]
    public void MapToCatSeverity_Unknown_ReturnsDefaultCatII()
    {
        // Unknown/unrecognized defaults to CatII (medium equivalent)
        PrismaSeverityMapper.MapToCatSeverity("unknown").Should().Be(CatSeverity.CatII);
    }

    [Fact]
    public void MapToCatSeverity_Null_ReturnsDefaultCatII()
    {
        PrismaSeverityMapper.MapToCatSeverity(null).Should().Be(CatSeverity.CatII);
    }

    [Theory]
    [InlineData("CRITICAL", CatSeverity.CatI)]
    [InlineData("High", CatSeverity.CatI)]
    [InlineData("MEDIUM", CatSeverity.CatII)]
    [InlineData("Low", CatSeverity.CatIII)]
    [InlineData("INFORMATIONAL", null)]
    public void MapToCatSeverity_CaseInsensitive(string severity, CatSeverity? expected)
    {
        PrismaSeverityMapper.MapToCatSeverity(severity).Should().Be(expected);
    }

    // ─── MapToFindingSeverity ────────────────────────────────────────────────

    [Theory]
    [InlineData("critical", FindingSeverity.Critical)]
    [InlineData("high", FindingSeverity.High)]
    [InlineData("medium", FindingSeverity.Medium)]
    [InlineData("low", FindingSeverity.Low)]
    [InlineData("informational", FindingSeverity.Informational)]
    public void MapToFindingSeverity_StandardValues_ReturnsCorrect(string severity, FindingSeverity expected)
    {
        PrismaSeverityMapper.MapToFindingSeverity(severity).Should().Be(expected);
    }

    [Fact]
    public void MapToFindingSeverity_Unknown_ReturnsDefaultMedium()
    {
        PrismaSeverityMapper.MapToFindingSeverity("unknown").Should().Be(FindingSeverity.Medium);
    }

    [Fact]
    public void MapToFindingSeverity_Null_ReturnsDefaultMedium()
    {
        PrismaSeverityMapper.MapToFindingSeverity(null).Should().Be(FindingSeverity.Medium);
    }

    [Theory]
    [InlineData("CRITICAL", FindingSeverity.Critical)]
    [InlineData("High", FindingSeverity.High)]
    [InlineData("MEDIUM", FindingSeverity.Medium)]
    [InlineData("Low", FindingSeverity.Low)]
    [InlineData("Informational", FindingSeverity.Informational)]
    public void MapToFindingSeverity_CaseInsensitive(string severity, FindingSeverity expected)
    {
        PrismaSeverityMapper.MapToFindingSeverity(severity).Should().Be(expected);
    }

    // ─── Edge Cases ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("  medium  ")]
    public void MapToFindingSeverity_WhitespacePadded_HandleGracefully(string severity)
    {
        // Empty/whitespace → default Medium; padded values → trimmed to match
        var result = PrismaSeverityMapper.MapToFindingSeverity(severity);
        result.Should().Be(FindingSeverity.Medium);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void MapToCatSeverity_EmptyOrWhitespace_ReturnsDefaultCatII(string severity)
    {
        PrismaSeverityMapper.MapToCatSeverity(severity).Should().Be(CatSeverity.CatII);
    }

    [Fact]
    public void MapToCatSeverity_WhitespacePaddedMedium_ReturnsCatII()
    {
        PrismaSeverityMapper.MapToCatSeverity("  medium  ").Should().Be(CatSeverity.CatII);
    }

    [Fact]
    public void MapToFindingSeverity_WhitespacePaddedHigh_ReturnsHigh()
    {
        PrismaSeverityMapper.MapToFindingSeverity("  high  ").Should().Be(FindingSeverity.High);
    }

    [Fact]
    public void MapToCatSeverity_CriticalAndHighBothMapToCatI()
    {
        // Verify DoD convention: both critical and high → CAT I
        PrismaSeverityMapper.MapToCatSeverity("critical").Should().Be(CatSeverity.CatI);
        PrismaSeverityMapper.MapToCatSeverity("high").Should().Be(CatSeverity.CatI);
    }

    [Fact]
    public void MapToFindingSeverity_InformationalDistinctFromLow()
    {
        // Informational should map to Informational, not Low
        PrismaSeverityMapper.MapToFindingSeverity("informational")
            .Should().Be(FindingSeverity.Informational)
            .And.NotBe(FindingSeverity.Low);
    }
}
