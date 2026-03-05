// ═══════════════════════════════════════════════════════════════════════════
// Feature 019 — Prisma Cloud Scan Import: API JSON Parser Tests
// TDD: Tests written FIRST (red), implementation in PrismaApiJsonParser makes green.
// ═══════════════════════════════════════════════════════════════════════════

using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ato.Copilot.Agents.Compliance.Services.ScanImport;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Parsers;

/// <summary>
/// Unit tests for <see cref="PrismaApiJsonParser"/> — Feature 019 Prisma API JSON parsing.
/// Covers valid JSON, enriched fields (remediation, history, labels), edge cases, error handling.
/// </summary>
public class PrismaApiJsonParserTests
{
    private static byte[] JsonBytes(string json) => System.Text.Encoding.UTF8.GetBytes(json);

    private static byte[] LoadTestFile(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        return File.ReadAllBytes(path);
    }

    private static PrismaApiJsonParser CreateParser() =>
        new(NullLogger<PrismaApiJsonParser>.Instance);

    // ─── Valid JSON Parsing ──────────────────────────────────────────────────

    [Fact]
    public void Parse_SampleApiFixture_Returns_CorrectAlertCount()
    {
        var bytes = LoadTestFile("sample-prisma-api.json");
        var parser = CreateParser();
        var result = parser.Parse(bytes, "sample-prisma-api.json");

        result.TotalAlerts.Should().Be(5);
        result.Alerts.Should().HaveCount(5);
        result.SourceType.Should().Be(ScanImportType.PrismaApi);
    }

    [Fact]
    public void Parse_SampleApiFixture_ExtractsAllAccountIds()
    {
        var bytes = LoadTestFile("sample-prisma-api.json");
        var parser = CreateParser();
        var result = parser.Parse(bytes, "sample-prisma-api.json");

        result.AccountIds.Should().ContainSingle()
            .Which.Should().Be("a1b2c3d4-5678-90ab-cdef-1234567890ab");
    }

    [Fact]
    public void Parse_SingleAlert_ExtractsAllCoreFields()
    {
        var json = """
        [
          {
            "id": "P-100",
            "status": "open",
            "alertTime": 1740652800000,
            "policy": {
              "policyId": "pol-001",
              "name": "Test Policy",
              "policyType": "config",
              "severity": "high",
              "remediable": false,
              "complianceMetadata": []
            },
            "resource": {
              "id": "/subs/abc/rg/res1",
              "name": "res1",
              "resourceType": "Microsoft.Storage/storageAccounts",
              "region": "eastus",
              "cloudType": "azure",
              "accountId": "sub-abc",
              "accountName": "MyAccount"
            }
          }
        ]
        """;

        var parser = CreateParser();
        var result = parser.Parse(JsonBytes(json), "test.json");

        var alert = result.Alerts.Should().ContainSingle().Subject;
        alert.AlertId.Should().Be("P-100");
        alert.Status.Should().Be("open");
        alert.PolicyName.Should().Be("Test Policy");
        alert.PolicyType.Should().Be("config");
        alert.Severity.Should().Be("high");
        alert.CloudType.Should().Be("azure");
        alert.AccountName.Should().Be("MyAccount");
        alert.AccountId.Should().Be("sub-abc");
        alert.Region.Should().Be("eastus");
        alert.ResourceName.Should().Be("res1");
        alert.ResourceId.Should().Be("/subs/abc/rg/res1");
        alert.ResourceType.Should().Be("Microsoft.Storage/storageAccounts");
    }

    [Fact]
    public void Parse_SingleObject_NotArray_ParsesSuccessfully()
    {
        var json = """
        {
          "id": "P-999",
          "status": "resolved",
          "alertTime": 1740652800000,
          "policy": {
            "name": "Single Object Test",
            "policyType": "config",
            "severity": "medium",
            "remediable": false,
            "complianceMetadata": []
          },
          "resource": {
            "cloudType": "azure",
            "accountId": "sub-single"
          }
        }
        """;

        var parser = CreateParser();
        var result = parser.Parse(JsonBytes(json), "single.json");

        result.Alerts.Should().ContainSingle();
        result.Alerts[0].AlertId.Should().Be("P-999");
        result.Alerts[0].Status.Should().Be("resolved");
    }

    // ─── NIST Control Extraction ────────────────────────────────────────────

    [Fact]
    public void Parse_ExtractsNistControls_FiltersNonNist()
    {
        var bytes = LoadTestFile("sample-prisma-api.json");
        var parser = CreateParser();
        var result = parser.Parse(bytes, "sample-prisma-api.json");

        // P-20001 has SC-28 and SC-12 (NIST) plus CIS 3.2 (non-NIST)
        var alert1 = result.Alerts.First(a => a.AlertId == "P-20001");
        alert1.NistControlIds.Should().HaveCount(2);
        alert1.NistControlIds.Should().Contain("SC-28");
        alert1.NistControlIds.Should().Contain("SC-12");

        // P-20002 has AU-2
        var alert2 = result.Alerts.First(a => a.AlertId == "P-20002");
        alert2.NistControlIds.Should().ContainSingle().Which.Should().Be("AU-2");

        // P-20004 has no compliance metadata - informational
        var alert4 = result.Alerts.First(a => a.AlertId == "P-20004");
        alert4.NistControlIds.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ExtractsNistWithParentheses()
    {
        var bytes = LoadTestFile("sample-prisma-api.json");
        var parser = CreateParser();
        var result = parser.Parse(bytes, "sample-prisma-api.json");

        // P-20005 has IA-5(1)
        var alert5 = result.Alerts.First(a => a.AlertId == "P-20005");
        alert5.NistControlIds.Should().ContainSingle().Which.Should().Be("IA-5(1)");
    }

    // ─── Enriched Fields (API JSON only) ─────────────────────────────────────

    [Fact]
    public void Parse_ExtractsDescription()
    {
        var bytes = LoadTestFile("sample-prisma-api.json");
        var parser = CreateParser();
        var result = parser.Parse(bytes, "sample-prisma-api.json");

        var alert1 = result.Alerts.First(a => a.AlertId == "P-20001");
        alert1.Description.Should().Contain("customer-managed keys");
    }

    [Fact]
    public void Parse_ExtractsRecommendation()
    {
        var bytes = LoadTestFile("sample-prisma-api.json");
        var parser = CreateParser();
        var result = parser.Parse(bytes, "sample-prisma-api.json");

        var alert1 = result.Alerts.First(a => a.AlertId == "P-20001");
        alert1.Recommendation.Should().Contain("Navigate to the Azure Portal");
    }

    [Fact]
    public void Parse_ExtractsRemediationScript()
    {
        var bytes = LoadTestFile("sample-prisma-api.json");
        var parser = CreateParser();
        var result = parser.Parse(bytes, "sample-prisma-api.json");

        // P-20001 has remediation.cliScriptTemplate
        var alert1 = result.Alerts.First(a => a.AlertId == "P-20001");
        alert1.RemediationScript.Should().Contain("az storage account update");

        // P-20002 has remediation: null
        var alert2 = result.Alerts.First(a => a.AlertId == "P-20002");
        alert2.RemediationScript.Should().BeNull();
    }

    [Fact]
    public void Parse_ExtractsRemediableFlag()
    {
        var bytes = LoadTestFile("sample-prisma-api.json");
        var parser = CreateParser();
        var result = parser.Parse(bytes, "sample-prisma-api.json");

        result.Alerts.First(a => a.AlertId == "P-20001").Remediable.Should().BeTrue();
        result.Alerts.First(a => a.AlertId == "P-20002").Remediable.Should().BeFalse();
        result.Alerts.First(a => a.AlertId == "P-20003").Remediable.Should().BeTrue();
    }

    [Fact]
    public void Parse_ExtractsPolicyLabels()
    {
        var bytes = LoadTestFile("sample-prisma-api.json");
        var parser = CreateParser();
        var result = parser.Parse(bytes, "sample-prisma-api.json");

        var alert1 = result.Alerts.First(a => a.AlertId == "P-20001");
        alert1.PolicyLabels.Should().NotBeNull();
        alert1.PolicyLabels.Should().Contain("CSPM");
        alert1.PolicyLabels.Should().Contain("Azure");
        alert1.PolicyLabels.Should().Contain("Storage");
        alert1.PolicyLabels.Should().Contain("Encryption");

        // P-20004 has empty labels
        var alert4 = result.Alerts.First(a => a.AlertId == "P-20004");
        alert4.PolicyLabels.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ExtractsAlertHistory()
    {
        var bytes = LoadTestFile("sample-prisma-api.json");
        var parser = CreateParser();
        var result = parser.Parse(bytes, "sample-prisma-api.json");

        // P-20001 has 2 history entries
        var alert1 = result.Alerts.First(a => a.AlertId == "P-20001");
        alert1.AlertHistory.Should().HaveCount(2);
        alert1.AlertHistory![0].ModifiedBy.Should().Be("System");
        alert1.AlertHistory[0].Status.Should().Be("open");
        alert1.AlertHistory[0].Reason.Should().Be("NEW_ALERT");

        // Verify epoch conversion: 1740652800000 = 2025-02-27T12:00:00Z
        alert1.AlertHistory[0].ModifiedOn.Should().Be(
            DateTimeOffset.FromUnixTimeMilliseconds(1740652800000).UtcDateTime);

        // P-20002 has history with non-System modifiedBy
        var alert2 = result.Alerts.First(a => a.AlertId == "P-20002");
        alert2.AlertHistory.Should().HaveCount(2);
        alert2.AlertHistory![1].ModifiedBy.Should().Be("admin@contoso.com");
        alert2.AlertHistory[1].Status.Should().Be("resolved");
    }

    [Fact]
    public void Parse_AlertTime_ConvertsFromEpochMs()
    {
        var bytes = LoadTestFile("sample-prisma-api.json");
        var parser = CreateParser();
        var result = parser.Parse(bytes, "sample-prisma-api.json");

        // 1740652800000 = 2025-02-27T12:00:00Z
        var expected = DateTimeOffset.FromUnixTimeMilliseconds(1740652800000).UtcDateTime;
        result.Alerts.First(a => a.AlertId == "P-20001").AlertTime.Should().Be(expected);
    }

    // ─── Edge Cases ─────────────────────────────────────────────────────────

    [Fact]
    public void Parse_AlertWithEmptyResourceFields_ParsesSuccessfully()
    {
        var bytes = LoadTestFile("sample-prisma-api.json");
        var parser = CreateParser();
        var result = parser.Parse(bytes, "sample-prisma-api.json");

        // P-20005 has empty resource fields
        var alert5 = result.Alerts.First(a => a.AlertId == "P-20005");
        alert5.ResourceId.Should().BeEmpty();
        alert5.ResourceName.Should().BeEmpty();
        alert5.ResourceType.Should().BeEmpty();
    }

    [Fact]
    public void Parse_AlertWithNoHistory_HasNullAlertHistory()
    {
        var json = """
        [{
          "id": "P-NO-HIST",
          "status": "open",
          "alertTime": 1740652800000,
          "policy": {
            "name": "No History Policy",
            "policyType": "config",
            "severity": "low",
            "remediable": false,
            "complianceMetadata": []
          },
          "resource": { "cloudType": "azure", "accountId": "sub-1" }
        }]
        """;

        var parser = CreateParser();
        var result = parser.Parse(JsonBytes(json), "test.json");

        result.Alerts[0].AlertHistory.Should().BeNull();
    }

    [Fact]
    public void Parse_AlertWithNoNistCompliance_HasEmptyNistControlIds()
    {
        var json = """
        [{
          "id": "P-NO-NIST",
          "status": "open",
          "alertTime": 1740652800000,
          "policy": {
            "name": "CIS Only Policy",
            "policyType": "config",
            "severity": "medium",
            "remediable": false,
            "complianceMetadata": [
              {
                "standardName": "CIS v2.0.0 (Azure)",
                "requirementId": "1.1"
              }
            ]
          },
          "resource": { "cloudType": "azure", "accountId": "sub-1" }
        }]
        """;

        var parser = CreateParser();
        var result = parser.Parse(JsonBytes(json), "test.json");

        result.Alerts[0].NistControlIds.Should().BeEmpty();
    }

    [Fact]
    public void Parse_AlertWithMissingOptionalFields_UsesDefaults()
    {
        var json = """
        [{
          "id": "P-MINIMAL",
          "status": "open",
          "alertTime": 0,
          "policy": {
            "name": "Minimal Policy",
            "remediable": false,
            "complianceMetadata": []
          },
          "resource": { "cloudType": "azure", "accountId": "sub-1" }
        }]
        """;

        var parser = CreateParser();
        var result = parser.Parse(JsonBytes(json), "test.json");

        var alert = result.Alerts[0];
        alert.PolicyType.Should().BeEmpty();
        alert.Severity.Should().Be("medium"); // default
        alert.Description.Should().BeNull();
        alert.Recommendation.Should().BeNull();
        alert.RemediationScript.Should().BeNull();
        alert.PolicyLabels.Should().BeNull();
        alert.Remediable.Should().BeFalse();
    }

    // ─── Error Handling ─────────────────────────────────────────────────────

    [Fact]
    public void Parse_EmptyInput_Throws()
    {
        var parser = CreateParser();
        var act = () => parser.Parse(Array.Empty<byte>(), "empty.json");

        act.Should().Throw<PrismaParseException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public void Parse_InvalidJson_Throws()
    {
        var parser = CreateParser();
        var act = () => parser.Parse(JsonBytes("{not valid json}"), "bad.json");

        act.Should().Throw<PrismaParseException>()
            .WithMessage("*parse error*");
    }

    [Fact]
    public void Parse_EmptyArray_Throws()
    {
        var parser = CreateParser();
        var act = () => parser.Parse(JsonBytes("[]"), "empty-array.json");

        act.Should().Throw<PrismaParseException>()
            .WithMessage("*no alert*");
    }

    [Fact]
    public void Parse_AlertMissingId_Throws()
    {
        var json = """
        [{
          "status": "open",
          "policy": { "name": "Test" },
          "resource": { "cloudType": "azure" }
        }]
        """;

        var parser = CreateParser();
        var act = () => parser.Parse(JsonBytes(json), "missing-id.json");

        act.Should().Throw<PrismaParseException>()
            .WithMessage("*missing*id*");
    }

    [Fact]
    public void Parse_AlertMissingStatus_Throws()
    {
        var json = """
        [{
          "id": "P-100",
          "policy": { "name": "Test" },
          "resource": { "cloudType": "azure" }
        }]
        """;

        var parser = CreateParser();
        var act = () => parser.Parse(JsonBytes(json), "missing-status.json");

        act.Should().Throw<PrismaParseException>()
            .WithMessage("*missing*status*");
    }

    [Fact]
    public void Parse_AlertMissingPolicyName_Throws()
    {
        var json = """
        [{
          "id": "P-100",
          "status": "open",
          "policy": { "policyType": "config" },
          "resource": { "cloudType": "azure" }
        }]
        """;

        var parser = CreateParser();
        var act = () => parser.Parse(JsonBytes(json), "missing-policy-name.json");

        act.Should().Throw<PrismaParseException>()
            .WithMessage("*missing*policy.name*");
    }

    [Fact]
    public void Parse_PlainText_NotJson_Throws()
    {
        var parser = CreateParser();
        var act = () => parser.Parse(JsonBytes("this is plain text"), "text.json");

        act.Should().Throw<PrismaParseException>()
            .WithMessage("*Invalid JSON format*");
    }

    // ─── Status mapping coverage ────────────────────────────────────────────

    [Theory]
    [InlineData("open", "open")]
    [InlineData("resolved", "resolved")]
    [InlineData("dismissed", "dismissed")]
    public void Parse_PreservesAlertStatus(string inputStatus, string expectedStatus)
    {
        var json = $$"""
        [{
          "id": "P-STATUS",
          "status": "{{inputStatus}}",
          "alertTime": 1740652800000,
          "policy": {
            "name": "Status Test",
            "policyType": "config",
            "severity": "medium",
            "remediable": false,
            "complianceMetadata": []
          },
          "resource": { "cloudType": "azure", "accountId": "sub-1" }
        }]
        """;

        var parser = CreateParser();
        var result = parser.Parse(JsonBytes(json), "status.json");

        result.Alerts[0].Status.Should().Be(expectedStatus);
    }

    [Fact]
    public void Parse_TotalRowsEqualsAlertCount()
    {
        var bytes = LoadTestFile("sample-prisma-api.json");
        var parser = CreateParser();
        var result = parser.Parse(bytes, "sample-prisma-api.json");

        // For API JSON, TotalRows == TotalAlerts (no multi-row grouping like CSV)
        result.TotalRows.Should().Be(result.TotalAlerts);
    }
}
