// ═══════════════════════════════════════════════════════════════════════════
// Feature 019 — Prisma Cloud Scan Import: CSV Parser Tests
// TDD: Tests written FIRST (red), implementation in PrismaCsvParser makes green.
// ═══════════════════════════════════════════════════════════════════════════

using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ato.Copilot.Agents.Compliance.Services.ScanImport;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Parsers;

/// <summary>
/// Unit tests for <see cref="PrismaCsvParser"/> — Feature 019 Prisma CSV parsing.
/// Covers valid CSV, multi-row grouping, NIST extraction, edge cases, error handling.
/// </summary>
public class PrismaCsvParserTests
{
    /// <summary>Helper to create CSV bytes from a string.</summary>
    private static byte[] CsvBytes(string csv) => System.Text.Encoding.UTF8.GetBytes(csv);

    /// <summary>Helper to load test data file from TestData directory.</summary>
    private static byte[] LoadTestFile(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        return File.ReadAllBytes(path);
    }

    private static readonly string ValidHeader =
        "Alert ID,Status,Policy Name,Policy Type,Severity,Cloud Type,Account Name,Account ID,Region,Resource Name,Resource ID,Resource Type,Alert Time,Resolution Reason,Resolution Time,Compliance Standard,Compliance Requirement,Compliance Section";

    // ─── Valid CSV Parsing ────────────────────────────────────────────────────

    [Fact]
    public void Parse_ValidCsv_Returns_CorrectAlertCount()
    {
        var csv = ValidHeader + "\n" +
            "P-100,open,Policy A,config,high,azure,Prod,sub-1,eastus,res1,/subs/sub-1/rg/res1,Microsoft.Storage/storageAccounts,2026-01-01T00:00:00Z,,,,," + "\n" +
            "P-101,resolved,Policy B,config,medium,azure,Prod,sub-1,eastus,res2,/subs/sub-1/rg/res2,Microsoft.Sql/servers,2026-01-02T00:00:00Z,Resolved,2026-01-10T00:00:00Z,,,";

        var parser = CreateParser();
        var result = parser.Parse(CsvBytes(csv), "test.csv");

        result.TotalAlerts.Should().Be(2);
        result.Alerts.Should().HaveCount(2);
        result.SourceType.Should().Be(ScanImportType.PrismaCsv);
    }

    [Fact]
    public void Parse_SampleTestFixture_Returns_CorrectCounts()
    {
        // sample-prisma-export.csv has 11 data rows → 9 unique Alert IDs
        // (P-12345 appears 3 times for multi-row grouping)
        var bytes = LoadTestFile("sample-prisma-export.csv");
        var parser = CreateParser();
        var result = parser.Parse(bytes, "sample-prisma-export.csv");

        result.TotalAlerts.Should().Be(9);
        result.TotalRows.Should().Be(11);
    }

    [Fact]
    public void Parse_ValidCsv_ExtractsAllFields()
    {
        var csv = ValidHeader + "\n" +
            "P-200,open,Test Policy,config,high,azure,MyAccount,sub-abc,eastus,myres,/subs/sub-abc/rg/myres,Microsoft.Storage/storageAccounts,2026-03-01T10:00:00Z,,,NIST 800-53 Rev 5,SC-28,SC-28 Protection";

        var parser = CreateParser();
        var result = parser.Parse(CsvBytes(csv), "test.csv");

        var alert = result.Alerts.Should().ContainSingle().Subject;
        alert.AlertId.Should().Be("P-200");
        alert.Status.Should().Be("open");
        alert.PolicyName.Should().Be("Test Policy");
        alert.PolicyType.Should().Be("config");
        alert.Severity.Should().Be("high");
        alert.CloudType.Should().Be("azure");
        alert.AccountName.Should().Be("MyAccount");
        alert.AccountId.Should().Be("sub-abc");
        alert.Region.Should().Be("eastus");
        alert.ResourceName.Should().Be("myres");
        alert.ResourceId.Should().Be("/subs/sub-abc/rg/myres");
        alert.ResourceType.Should().Be("Microsoft.Storage/storageAccounts");
        alert.AlertTime.Should().Be(new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc));
        alert.NistControlIds.Should().ContainSingle().Which.Should().Be("SC-28");
    }

    // ─── Multi-Row Grouping by Alert ID ──────────────────────────────────────

    [Fact]
    public void Parse_MultiRowAlerts_GroupedByAlertId_MergesNistControls()
    {
        var csv = ValidHeader + "\n" +
            "P-300,open,Policy X,config,high,azure,Prod,sub-1,eastus,res1,/r1,Type1,2026-01-01T00:00:00Z,,,NIST 800-53 Rev 5,SC-28,SC-28 Protection\n" +
            "P-300,open,Policy X,config,high,azure,Prod,sub-1,eastus,res1,/r1,Type1,2026-01-01T00:00:00Z,,,NIST 800-53 Rev 5,SC-12,SC-12 Key Management\n" +
            "P-300,open,Policy X,config,high,azure,Prod,sub-1,eastus,res1,/r1,Type1,2026-01-01T00:00:00Z,,,CIS v2.0.0 (Azure),3.2,Storage Keys";

        var parser = CreateParser();
        var result = parser.Parse(CsvBytes(csv), "test.csv");

        // 3 rows → 1 alert after grouping
        result.TotalAlerts.Should().Be(1);
        result.TotalRows.Should().Be(3);

        var alert = result.Alerts.Single();
        // Only NIST controls extracted, CIS excluded
        alert.NistControlIds.Should().HaveCount(2);
        alert.NistControlIds.Should().Contain("SC-28");
        alert.NistControlIds.Should().Contain("SC-12");
    }

    [Fact]
    public void Parse_MultiRowAlerts_NonNistRows_SkippedForControlExtraction()
    {
        var csv = ValidHeader + "\n" +
            "P-400,open,Policy Y,config,medium,azure,Prod,sub-1,eastus,res1,/r1,Type1,2026-01-01T00:00:00Z,,,CIS v2.0.0 (Azure),3.2,Storage Keys\n" +
            "P-400,open,Policy Y,config,medium,azure,Prod,sub-1,eastus,res1,/r1,Type1,2026-01-01T00:00:00Z,,,SOC 2,CC6.1,CC6.1 Encryption";

        var parser = CreateParser();
        var result = parser.Parse(CsvBytes(csv), "test.csv");

        var alert = result.Alerts.Single();
        // All rows are non-NIST → empty NistControlIds (unmapped policy)
        alert.NistControlIds.Should().BeEmpty();
    }

    // ─── NIST Control Extraction ─────────────────────────────────────────────

    [Fact]
    public void Parse_NistControlWithEnhancement_ExtractsCorrectly()
    {
        var csv = ValidHeader + "\n" +
            "P-500,open,IAM Policy,config,high,azure,Prod,sub-1,eastus,res1,/r1,Type1,2026-01-01T00:00:00Z,,,NIST 800-53 Rev 5,IA-5(1),IA-5(1) Password Auth";

        var parser = CreateParser();
        var result = parser.Parse(CsvBytes(csv), "test.csv");

        result.Alerts.Single().NistControlIds.Should().ContainSingle().Which.Should().Be("IA-5(1)");
    }

    // ─── Quoted Fields with Embedded Commas ──────────────────────────────────

    [Fact]
    public void Parse_QuotedFieldsWithCommas_ParsesCorrectly()
    {
        var csv = ValidHeader + "\n" +
            "P-600,open,\"Azure Storage account should use customer-managed key for encryption, version 2.0\",config,high,azure,Prod,sub-1,eastus,res1,/r1,Type1,2026-01-01T00:00:00Z,,,,,";

        var parser = CreateParser();
        var result = parser.Parse(CsvBytes(csv), "test.csv");

        result.Alerts.Single().PolicyName
            .Should().Be("Azure Storage account should use customer-managed key for encryption, version 2.0");
    }

    [Fact]
    public void Parse_QuotedFieldsWithEscapedQuotes_ParsesCorrectly()
    {
        var csv = ValidHeader + "\n" +
            "P-601,open,\"Policy with \"\"quotes\"\" inside\",config,medium,azure,Prod,sub-1,eastus,res1,/r1,Type1,2026-01-01T00:00:00Z,,,,,";

        var parser = CreateParser();
        var result = parser.Parse(CsvBytes(csv), "test.csv");

        result.Alerts.Single().PolicyName.Should().Be("Policy with \"quotes\" inside");
    }

    // ─── Status Mapping ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("open")]
    [InlineData("resolved")]
    [InlineData("dismissed")]
    [InlineData("snoozed")]
    public void Parse_AllStatuses_PreservedOnAlert(string status)
    {
        var csv = ValidHeader + "\n" +
            $"P-700,{status},Policy Z,config,medium,azure,Prod,sub-1,eastus,res1,/r1,Type1,2026-01-01T00:00:00Z,,,,,";

        var parser = CreateParser();
        var result = parser.Parse(CsvBytes(csv), "test.csv");

        result.Alerts.Single().Status.Should().Be(status);
    }

    // ─── Resolution Fields ───────────────────────────────────────────────────

    [Fact]
    public void Parse_ResolvedAlert_CapturesResolutionFields()
    {
        var csv = ValidHeader + "\n" +
            "P-800,resolved,Policy R,config,medium,azure,Prod,sub-1,eastus,res1,/r1,Type1,2026-01-01T00:00:00Z,Resolved,2026-01-10T14:30:00Z,,,";

        var parser = CreateParser();
        var result = parser.Parse(CsvBytes(csv), "test.csv");

        var alert = result.Alerts.Single();
        alert.ResolutionReason.Should().Be("Resolved");
        alert.ResolutionTime.Should().Be(new DateTime(2026, 1, 10, 14, 30, 0, DateTimeKind.Utc));
    }

    // ─── Account IDs ─────────────────────────────────────────────────────────

    [Fact]
    public void Parse_MultipleSubscriptions_CollectsUniqueAccountIds()
    {
        var csv = ValidHeader + "\n" +
            "P-900,open,Policy A,config,high,azure,Prod,sub-aaa,eastus,r1,/r1,T1,2026-01-01T00:00:00Z,,,,,\n" +
            "P-901,open,Policy B,config,high,azure,Dev,sub-bbb,westus,r2,/r2,T2,2026-01-01T00:00:00Z,,,,,\n" +
            "P-902,open,Policy C,config,high,azure,Prod,sub-aaa,eastus,r3,/r3,T3,2026-01-01T00:00:00Z,,,,,";

        var parser = CreateParser();
        var result = parser.Parse(CsvBytes(csv), "test.csv");

        result.AccountIds.Should().HaveCount(2);
        result.AccountIds.Should().Contain("sub-aaa");
        result.AccountIds.Should().Contain("sub-bbb");
    }

    // ─── Empty Compliance Columns (Unmapped Policy) ──────────────────────────

    [Fact]
    public void Parse_EmptyComplianceColumns_CreatesAlertWithEmptyNistControlIds()
    {
        var csv = ValidHeader + "\n" +
            "P-1000,open,Custom Policy,anomaly,informational,azure,Prod,sub-1,eastus,res1,/r1,T1,2026-01-01T00:00:00Z,,,,,";

        var parser = CreateParser();
        var result = parser.Parse(CsvBytes(csv), "test.csv");

        result.Alerts.Single().NistControlIds.Should().BeEmpty();
    }

    // ─── UTF-8 BOM Handling ──────────────────────────────────────────────────

    [Fact]
    public void Parse_Utf8Bom_StripsAndParsesCorrectly()
    {
        var csv = ValidHeader + "\n" +
            "P-1100,open,Policy BOM,config,medium,azure,Prod,sub-1,eastus,res1,/r1,T1,2026-01-01T00:00:00Z,,,,,";

        // Prepend UTF-8 BOM
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var csvBytes = bom.Concat(CsvBytes(csv)).ToArray();

        var parser = CreateParser();
        var result = parser.Parse(csvBytes, "test.csv");

        result.TotalAlerts.Should().Be(1);
        result.Alerts.Single().AlertId.Should().Be("P-1100");
    }

    // ─── Error Handling ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_MissingRequiredHeaders_ThrowsPrismaParseException()
    {
        var csv = "Alert ID,Status,Some Column\nP-1,open,value";

        var parser = CreateParser();
        var act = () => parser.Parse(CsvBytes(csv), "test.csv");

        act.Should().Throw<PrismaParseException>()
            .Which.Message.Should().Contain("Policy Name");
    }

    [Fact]
    public void Parse_EmptyFile_ThrowsPrismaParseException()
    {
        var parser = CreateParser();
        var act = () => parser.Parse(CsvBytes(""), "test.csv");

        act.Should().Throw<PrismaParseException>();
    }

    [Fact]
    public void Parse_HeaderOnly_NoDataRows_ThrowsPrismaParseException()
    {
        var parser = CreateParser();
        var act = () => parser.Parse(CsvBytes(ValidHeader), "test.csv");

        act.Should().Throw<PrismaParseException>()
            .Which.Message.Should().Contain("no alert rows");
    }

    // ─── Empty Rows and Trailing Commas ──────────────────────────────────────

    [Fact]
    public void Parse_EmptyRowsBetweenData_SkipsBlankLines()
    {
        var csv = ValidHeader + "\n\n" +
            "P-1200,open,Policy A,config,high,azure,Prod,sub-1,eastus,r1,/r1,T1,2026-01-01T00:00:00Z,,,,,\n" +
            "\n" +
            "P-1201,open,Policy B,config,medium,azure,Prod,sub-1,eastus,r2,/r2,T2,2026-01-01T00:00:00Z,,,,,\n";

        var parser = CreateParser();
        var result = parser.Parse(CsvBytes(csv), "test.csv");

        result.TotalAlerts.Should().Be(2);
    }

    [Fact]
    public void Parse_TrailingCommas_HandledGracefully()
    {
        var csv = ValidHeader + ",\n" +
            "P-1300,open,Policy A,config,high,azure,Prod,sub-1,eastus,r1,/r1,T1,2026-01-01T00:00:00Z,,,,,,";

        var parser = CreateParser();
        var result = parser.Parse(CsvBytes(csv), "test.csv");

        result.TotalAlerts.Should().Be(1);
    }

    // ─── Mixed Cloud Types ───────────────────────────────────────────────────

    [Fact]
    public void Parse_MixedCloudTypes_AllAlertsParsed()
    {
        var csv = ValidHeader + "\n" +
            "P-1400,open,Azure Policy,config,high,azure,Prod,sub-1,eastus,r1,/r1,T1,2026-01-01T00:00:00Z,,,,,\n" +
            "P-1401,open,AWS Policy,config,high,aws,AWS-Staging,123456789012,us-east-1,bucket,arn:aws:s3:::bucket,AWS.S3.Bucket,2026-01-01T00:00:00Z,,,,,";

        var parser = CreateParser();
        var result = parser.Parse(CsvBytes(csv), "test.csv");

        result.TotalAlerts.Should().Be(2);
        result.Alerts.Select(a => a.CloudType).Should().Contain("azure").And.Contain("aws");
    }

    // ─── CSV-specific defaults ───────────────────────────────────────────────

    [Fact]
    public void Parse_CsvAlerts_HaveDefaultsForApiOnlyFields()
    {
        var csv = ValidHeader + "\n" +
            "P-1500,open,Policy A,config,high,azure,Prod,sub-1,eastus,r1,/r1,T1,2026-01-01T00:00:00Z,,,,,";

        var parser = CreateParser();
        var result = parser.Parse(CsvBytes(csv), "test.csv");

        var alert = result.Alerts.Single();
        alert.Description.Should().BeNull();
        alert.Recommendation.Should().BeNull();
        alert.RemediationScript.Should().BeNull();
        alert.PolicyLabels.Should().BeNull();
        alert.Remediable.Should().BeFalse();
        alert.AlertHistory.Should().BeNull();
    }

    // ─── CRLF vs LF ─────────────────────────────────────────────────────────

    [Fact]
    public void Parse_CrLfLineEndings_ParsesCorrectly()
    {
        var csv = ValidHeader + "\r\n" +
            "P-1600,open,Policy A,config,high,azure,Prod,sub-1,eastus,r1,/r1,T1,2026-01-01T00:00:00Z,,,,,\r\n" +
            "P-1601,open,Policy B,config,medium,azure,Prod,sub-1,eastus,r2,/r2,T2,2026-01-01T00:00:00Z,,,,,\r\n";

        var parser = CreateParser();
        var result = parser.Parse(CsvBytes(csv), "test.csv");

        result.TotalAlerts.Should().Be(2);
    }

    // ─── Duplicate NIST Controls in Grouped Alert ────────────────────────────

    [Fact]
    public void Parse_DuplicateNistControlsInGroupedAlert_Deduplicated()
    {
        var csv = ValidHeader + "\n" +
            "P-1700,open,Policy X,config,high,azure,Prod,sub-1,eastus,r1,/r1,T1,2026-01-01T00:00:00Z,,,NIST 800-53 Rev 5,SC-28,SC-28 Protection\n" +
            "P-1700,open,Policy X,config,high,azure,Prod,sub-1,eastus,r1,/r1,T1,2026-01-01T00:00:00Z,,,NIST 800-53 Rev 5,SC-28,SC-28 Protection"; // same control

        var parser = CreateParser();
        var result = parser.Parse(CsvBytes(csv), "test.csv");

        result.Alerts.Single().NistControlIds.Should().ContainSingle().Which.Should().Be("SC-28");
    }

    // ─── Helper ──────────────────────────────────────────────────────────────

    private static PrismaCsvParser CreateParser() =>
        new(Mock.Of<ILogger<PrismaCsvParser>>());
}
