using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Compliance.Services.ScanImport;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Parsers;

/// <summary>
/// Unit tests for <see cref="CklParser"/> — Feature 017 CKL XML parsing.
/// Covers valid files, malformed XML, severity overrides, edge cases.
/// </summary>
public class CklParserTests
{
    private readonly CklParser _parser;

    public CklParserTests()
    {
        _parser = new CklParser(Mock.Of<ILogger<CklParser>>());
    }

    /// <summary>Helper to load test data file from TestData directory.</summary>
    private static byte[] LoadTestFile(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        return File.ReadAllBytes(path);
    }

    /// <summary>Helper to create CKL XML bytes from a string.</summary>
    private static byte[] CklBytes(string xml)
    {
        return System.Text.Encoding.UTF8.GetBytes(xml);
    }

    // ─── Valid CKL Parsing ───────────────────────────────────────────────────

    [Fact]
    public void Parse_ValidCkl_Returns_CorrectEntryCount()
    {
        var content = LoadTestFile("sample-valid.ckl");
        var result = _parser.Parse(content, "sample-valid.ckl");

        result.Entries.Should().HaveCount(5);
    }

    [Fact]
    public void Parse_ValidCkl_Returns_CorrectStatuses()
    {
        var content = LoadTestFile("sample-valid.ckl");
        var result = _parser.Parse(content, "sample-valid.ckl");

        result.Entries.Count(e => e.Status == "Open").Should().Be(2);
        result.Entries.Count(e => e.Status == "NotAFinding").Should().Be(2);
        result.Entries.Count(e => e.Status == "Not_Applicable").Should().Be(1);
    }

    [Fact]
    public void Parse_ValidCkl_Returns_CorrectSeverities()
    {
        var content = LoadTestFile("sample-valid.ckl");
        var result = _parser.Parse(content, "sample-valid.ckl");

        var v254239 = result.Entries.First(e => e.VulnId == "V-254239");
        v254239.Severity.Should().Be("high");

        var v254240 = result.Entries.First(e => e.VulnId == "V-254240");
        v254240.Severity.Should().Be("medium");

        var v254242 = result.Entries.First(e => e.VulnId == "V-254242");
        v254242.Severity.Should().Be("low");
    }

    // ─── ASSET Section ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_ValidCkl_Returns_CorrectAssetInfo()
    {
        var content = LoadTestFile("sample-valid.ckl");
        var result = _parser.Parse(content, "sample-valid.ckl");

        result.Asset.HostName.Should().Be("web-server-01");
        result.Asset.HostIp.Should().Be("10.0.1.100");
        result.Asset.HostFqdn.Should().Be("web-server-01.example.mil");
        result.Asset.HostMac.Should().Be("00:0A:95:9D:68:16");
        result.Asset.AssetType.Should().Be("Computing");
        result.Asset.TargetKey.Should().Be("4089");
    }

    // ─── STIG_INFO Section ───────────────────────────────────────────────────

    [Fact]
    public void Parse_ValidCkl_Returns_CorrectStigInfo()
    {
        var content = LoadTestFile("sample-valid.ckl");
        var result = _parser.Parse(content, "sample-valid.ckl");

        result.StigInfo.StigId.Should().Be("Windows_Server_2022_STIG");
        result.StigInfo.Version.Should().Be("3");
        result.StigInfo.ReleaseInfo.Should().Contain("Release: 1");
        result.StigInfo.Title.Should().Contain("Windows Server 2022");
    }

    // ─── CCI References ─────────────────────────────────────────────────────

    [Fact]
    public void Parse_ValidCkl_Returns_MultipleCciRefs()
    {
        var content = LoadTestFile("sample-valid.ckl");
        var result = _parser.Parse(content, "sample-valid.ckl");

        var v254239 = result.Entries.First(e => e.VulnId == "V-254239");
        v254239.CciRefs.Should().HaveCount(2);
        v254239.CciRefs.Should().Contain("CCI-000018");
        v254239.CciRefs.Should().Contain("CCI-000172");
    }

    [Fact]
    public void Parse_ValidCkl_Returns_SingleCciRef()
    {
        var content = LoadTestFile("sample-valid.ckl");
        var result = _parser.Parse(content, "sample-valid.ckl");

        var v254240 = result.Entries.First(e => e.VulnId == "V-254240");
        v254240.CciRefs.Should().HaveCount(1);
        v254240.CciRefs.Should().Contain("CCI-000067");
    }

    // ─── VULN Detail Fields ──────────────────────────────────────────────────

    [Fact]
    public void Parse_ValidCkl_Returns_RuleIdAndVersion()
    {
        var content = LoadTestFile("sample-valid.ckl");
        var result = _parser.Parse(content, "sample-valid.ckl");

        var v254239 = result.Entries.First(e => e.VulnId == "V-254239");
        v254239.RuleId.Should().Be("SV-254239r849090_rule");
        v254239.StigVersion.Should().Be("WN22-AU-000010");
    }

    [Fact]
    public void Parse_ValidCkl_Returns_FindingDetails()
    {
        var content = LoadTestFile("sample-valid.ckl");
        var result = _parser.Parse(content, "sample-valid.ckl");

        var v254239 = result.Entries.First(e => e.VulnId == "V-254239");
        v254239.FindingDetails.Should().Contain("Audit policy not configured");
    }

    [Fact]
    public void Parse_ValidCkl_Returns_Comments()
    {
        var content = LoadTestFile("sample-valid.ckl");
        var result = _parser.Parse(content, "sample-valid.ckl");

        var v254239 = result.Entries.First(e => e.VulnId == "V-254239");
        v254239.Comments.Should().Contain("Sprint 42");
    }

    [Fact]
    public void Parse_ValidCkl_Returns_GroupTitle()
    {
        var content = LoadTestFile("sample-valid.ckl");
        var result = _parser.Parse(content, "sample-valid.ckl");

        var v254239 = result.Entries.First(e => e.VulnId == "V-254239");
        v254239.GroupTitle.Should().Be("SRG-OS-000003-GPOS-00004");
    }

    // ─── Severity Override ───────────────────────────────────────────────────

    [Fact]
    public void Parse_SeverityOverrideCkl_Returns_OverrideValues()
    {
        var content = LoadTestFile("sample-severity-override.ckl");
        var result = _parser.Parse(content, "sample-severity-override.ckl");

        result.Entries.Should().HaveCount(1);
        var entry = result.Entries[0];

        entry.VulnId.Should().Be("V-254250");
        entry.Severity.Should().Be("medium"); // Original severity
        entry.SeverityOverride.Should().Be("high");
        entry.SeverityJustification.Should().Contain("public-facing network segment");
    }

    // ─── Malformed XML ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_MalformedXml_Throws_CklParseException()
    {
        var content = LoadTestFile("sample-malformed.ckl");

        var act = () => _parser.Parse(content, "sample-malformed.ckl");

        act.Should().Throw<CklParseException>()
            .Which.FileName.Should().Be("sample-malformed.ckl");
    }

    [Fact]
    public void Parse_MalformedXml_Exception_Contains_DescriptiveMessage()
    {
        var content = LoadTestFile("sample-malformed.ckl");

        var act = () => _parser.Parse(content, "sample-malformed.ckl");

        act.Should().Throw<CklParseException>()
            .WithMessage("*Malformed XML*");
    }

    // ─── Empty VULN List ─────────────────────────────────────────────────────

    [Fact]
    public void Parse_EmptyVulnList_Returns_EmptyEntries()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<CHECKLIST>
  <ASSET><HOST_NAME>empty-server</HOST_NAME></ASSET>
  <STIGS>
    <iSTIG>
      <STIG_INFO>
        <SI_DATA><SID_NAME>stigid</SID_NAME><SID_DATA>Empty_STIG</SID_DATA></SI_DATA>
      </STIG_INFO>
    </iSTIG>
  </STIGS>
</CHECKLIST>";

        var result = _parser.Parse(CklBytes(xml), "empty.ckl");

        result.Entries.Should().BeEmpty();
        result.StigInfo.StigId.Should().Be("Empty_STIG");
        result.Asset.HostName.Should().Be("empty-server");
    }

    // ─── Not_Reviewed Status ─────────────────────────────────────────────────

    [Fact]
    public void Parse_NotReviewedStatus_Returns_CorrectMapping()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<CHECKLIST>
  <ASSET><HOST_NAME>test</HOST_NAME></ASSET>
  <STIGS>
    <iSTIG>
      <STIG_INFO>
        <SI_DATA><SID_NAME>stigid</SID_NAME><SID_DATA>Test_STIG</SID_DATA></SI_DATA>
      </STIG_INFO>
      <VULN>
        <STIG_DATA><VULN_ATTRIBUTE>Vuln_Num</VULN_ATTRIBUTE><ATTRIBUTE_DATA>V-100001</ATTRIBUTE_DATA></STIG_DATA>
        <STIG_DATA><VULN_ATTRIBUTE>Severity</VULN_ATTRIBUTE><ATTRIBUTE_DATA>medium</ATTRIBUTE_DATA></STIG_DATA>
        <STATUS>Not_Reviewed</STATUS>
        <FINDING_DETAILS></FINDING_DETAILS>
        <COMMENTS></COMMENTS>
        <SEVERITY_OVERRIDE></SEVERITY_OVERRIDE>
        <SEVERITY_JUSTIFICATION></SEVERITY_JUSTIFICATION>
      </VULN>
    </iSTIG>
  </STIGS>
</CHECKLIST>";

        var result = _parser.Parse(CklBytes(xml), "not-reviewed.ckl");

        result.Entries.Should().HaveCount(1);
        result.Entries[0].Status.Should().Be("Not_Reviewed");
        result.Entries[0].VulnId.Should().Be("V-100001");
    }

    // ─── Missing Optional Fields ─────────────────────────────────────────────

    [Fact]
    public void Parse_MissingOptionalFields_Returns_NullValues()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<CHECKLIST>
  <ASSET><HOST_NAME>minimal</HOST_NAME></ASSET>
  <STIGS>
    <iSTIG>
      <STIG_INFO>
        <SI_DATA><SID_NAME>stigid</SID_NAME><SID_DATA>Minimal_STIG</SID_DATA></SI_DATA>
      </STIG_INFO>
      <VULN>
        <STIG_DATA><VULN_ATTRIBUTE>Vuln_Num</VULN_ATTRIBUTE><ATTRIBUTE_DATA>V-100002</ATTRIBUTE_DATA></STIG_DATA>
        <STIG_DATA><VULN_ATTRIBUTE>Severity</VULN_ATTRIBUTE><ATTRIBUTE_DATA>low</ATTRIBUTE_DATA></STIG_DATA>
        <STATUS>Open</STATUS>
        <FINDING_DETAILS></FINDING_DETAILS>
        <COMMENTS></COMMENTS>
        <SEVERITY_OVERRIDE></SEVERITY_OVERRIDE>
        <SEVERITY_JUSTIFICATION></SEVERITY_JUSTIFICATION>
      </VULN>
    </iSTIG>
  </STIGS>
</CHECKLIST>";

        var result = _parser.Parse(CklBytes(xml), "minimal.ckl");

        var entry = result.Entries[0];
        entry.RuleId.Should().BeNull();
        entry.StigVersion.Should().BeNull();
        entry.RuleTitle.Should().BeNull();
        entry.FindingDetails.Should().BeNull();
        entry.Comments.Should().BeNull();
        entry.SeverityOverride.Should().BeNull();
        entry.SeverityJustification.Should().BeNull();
        entry.GroupTitle.Should().BeNull();
        entry.CciRefs.Should().BeEmpty();
    }

    // ─── Missing CHECKLIST Root ──────────────────────────────────────────────

    [Fact]
    public void Parse_MissingChecklistRoot_Throws_CklParseException()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<NOTACHECKLIST>
  <STIGS><iSTIG><STIG_INFO></STIG_INFO></iSTIG></STIGS>
</NOTACHECKLIST>";

        var act = () => _parser.Parse(CklBytes(xml), "bad-root.ckl");

        act.Should().Throw<CklParseException>()
            .WithMessage("*Missing root*CHECKLIST*");
    }

    // ─── Missing STIGS/iSTIG ─────────────────────────────────────────────────

    [Fact]
    public void Parse_MissingISTIG_Throws_CklParseException()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<CHECKLIST>
  <ASSET><HOST_NAME>test</HOST_NAME></ASSET>
</CHECKLIST>";

        var act = () => _parser.Parse(CklBytes(xml), "no-stigs.ckl");

        act.Should().Throw<CklParseException>()
            .WithMessage("*Missing*iSTIG*");
    }

    // ─── Missing ASSET ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_MissingAsset_Returns_NullAssetFields()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<CHECKLIST>
  <STIGS>
    <iSTIG>
      <STIG_INFO>
        <SI_DATA><SID_NAME>stigid</SID_NAME><SID_DATA>NoAsset_STIG</SID_DATA></SI_DATA>
      </STIG_INFO>
    </iSTIG>
  </STIGS>
</CHECKLIST>";

        var result = _parser.Parse(CklBytes(xml), "no-asset.ckl");

        result.Asset.HostName.Should().BeNull();
        result.Asset.HostIp.Should().BeNull();
    }

    // ─── Empty File ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_EmptyBytes_Throws_CklParseException()
    {
        var act = () => _parser.Parse(Array.Empty<byte>(), "empty.ckl");

        act.Should().Throw<CklParseException>();
    }

    // ─── RuleTitle Parsing ───────────────────────────────────────────────────

    [Fact]
    public void Parse_ValidCkl_Returns_RuleTitle()
    {
        var content = LoadTestFile("sample-valid.ckl");
        var result = _parser.Parse(content, "sample-valid.ckl");

        var v254239 = result.Entries.First(e => e.VulnId == "V-254239");
        v254239.RuleTitle.Should().Contain("audit Account Management");
    }

    // ─── Empty Comments/FindingDetails Become Null ───────────────────────────

    [Fact]
    public void Parse_ValidCkl_EmptyComments_BecomesNull()
    {
        var content = LoadTestFile("sample-valid.ckl");
        var result = _parser.Parse(content, "sample-valid.ckl");

        // V-254240 has empty COMMENTS
        var v254240 = result.Entries.First(e => e.VulnId == "V-254240");
        v254240.Comments.Should().BeNull();
    }
}
