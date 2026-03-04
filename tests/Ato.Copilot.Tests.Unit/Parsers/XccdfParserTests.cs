// ═══════════════════════════════════════════════════════════════════════════
// Feature 017 — SCAP/STIG Import: XCCDF Parser Tests (T041)
// Covers valid parsing, namespace handling, rule ID extraction,
// score parsing, target info, malformed XML, edge cases.
// ═══════════════════════════════════════════════════════════════════════════

using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Compliance.Services.ScanImport;
using Ato.Copilot.Core.Models.Compliance;
using System.Text;

namespace Ato.Copilot.Tests.Unit.Parsers;

/// <summary>
/// Unit tests for <see cref="XccdfParser"/> — Feature 017 XCCDF XML parsing.
/// </summary>
public class XccdfParserTests
{
    private readonly XccdfParser _parser;

    public XccdfParserTests()
    {
        _parser = new XccdfParser(Mock.Of<ILogger<XccdfParser>>());
    }

    /// <summary>Helper to load test data file from TestData directory.</summary>
    private static byte[] LoadTestFile(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        return File.ReadAllBytes(path);
    }

    /// <summary>Helper to create XCCDF XML bytes from a string.</summary>
    private static byte[] XccdfBytes(string xml) => Encoding.UTF8.GetBytes(xml);

    // ═══════════════════════════════════════════════════════════════════════
    // Valid XCCDF Parsing (sample-valid.xccdf)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_ValidXccdf_Returns_CorrectResultCount()
    {
        var content = LoadTestFile("sample-valid.xccdf");
        var result = _parser.Parse(content, "sample-valid.xccdf");

        result.Results.Should().HaveCount(5);
    }

    [Fact]
    public void Parse_ValidXccdf_Returns_CorrectResultBreakdown()
    {
        var content = LoadTestFile("sample-valid.xccdf");
        var result = _parser.Parse(content, "sample-valid.xccdf");

        result.Results.Count(r => r.Result == "fail").Should().Be(2);
        result.Results.Count(r => r.Result == "pass").Should().Be(2);
        result.Results.Count(r => r.Result == "notapplicable").Should().Be(1);
    }

    [Fact]
    public void Parse_ValidXccdf_Returns_CorrectTarget()
    {
        var content = LoadTestFile("sample-valid.xccdf");
        var result = _parser.Parse(content, "sample-valid.xccdf");

        result.Target.Should().Be("web-server-01");
        result.TargetAddress.Should().Be("10.0.1.100");
    }

    [Fact]
    public void Parse_ValidXccdf_Returns_CorrectScore()
    {
        var content = LoadTestFile("sample-valid.xccdf");
        var result = _parser.Parse(content, "sample-valid.xccdf");

        result.Score.Should().Be(72.5m);
        result.MaxScore.Should().Be(100m);
    }

    [Fact]
    public void Parse_ValidXccdf_Returns_CorrectBenchmarkHref()
    {
        var content = LoadTestFile("sample-valid.xccdf");
        var result = _parser.Parse(content, "sample-valid.xccdf");

        result.BenchmarkHref.Should().Be("xccdf_mil.disa.stig_benchmark_Windows_Server_2022_STIG");
    }

    [Fact]
    public void Parse_ValidXccdf_Returns_CorrectTitle()
    {
        var content = LoadTestFile("sample-valid.xccdf");
        var result = _parser.Parse(content, "sample-valid.xccdf");

        result.Title.Should().Be("SCAP SCC Scan Results - Windows Server 2022");
    }

    [Fact]
    public void Parse_ValidXccdf_Returns_TargetFacts()
    {
        var content = LoadTestFile("sample-valid.xccdf");
        var result = _parser.Parse(content, "sample-valid.xccdf");

        result.TargetFacts.Should().ContainKey("urn:scap:fact:asset:identifier:host_name");
        result.TargetFacts["urn:scap:fact:asset:identifier:host_name"].Should().Be("web-server-01");
        result.TargetFacts.Should().ContainKey("urn:scap:fact:asset:identifier:os_name");
        result.TargetFacts["urn:scap:fact:asset:identifier:os_name"].Should().Be("Microsoft Windows Server 2022");
    }

    [Fact]
    public void Parse_ValidXccdf_Returns_CorrectTimestamps()
    {
        var content = LoadTestFile("sample-valid.xccdf");
        var result = _parser.Parse(content, "sample-valid.xccdf");

        result.StartTime.Should().NotBeNull();
        result.EndTime.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Rule-Result Detail Parsing
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_ValidXccdf_ExtractsRuleIdsCorrectly()
    {
        var content = LoadTestFile("sample-valid.xccdf");
        var result = _parser.Parse(content, "sample-valid.xccdf");

        var firstResult = result.Results[0];
        firstResult.RuleIdRef.Should().Be("xccdf_mil.disa.stig_rule_SV-254239r849090_rule");
        firstResult.ExtractedRuleId.Should().Be("SV-254239r849090_rule");
    }

    [Fact]
    public void Parse_ValidXccdf_CapsSeverityToLowerCase()
    {
        var content = LoadTestFile("sample-valid.xccdf");
        var result = _parser.Parse(content, "sample-valid.xccdf");

        // Severities in test data: high, medium, medium, low, medium
        result.Results[0].Severity.Should().Be("high");
        result.Results[1].Severity.Should().Be("medium");
        result.Results[3].Severity.Should().Be("low");
    }

    [Fact]
    public void Parse_ValidXccdf_ParsesWeight()
    {
        var content = LoadTestFile("sample-valid.xccdf");
        var result = _parser.Parse(content, "sample-valid.xccdf");

        result.Results.Should().OnlyContain(r => r.Weight == 10.0m);
    }

    [Fact]
    public void Parse_ValidXccdf_ParsesMessage()
    {
        var content = LoadTestFile("sample-valid.xccdf");
        var result = _parser.Parse(content, "sample-valid.xccdf");

        result.Results[0].Message.Should().Be("Registry value not configured as expected.");
        result.Results[1].Message.Should().Be("Audit policy not properly configured.");
        // Pass results have no message
        result.Results[2].Message.Should().BeNull();
    }

    [Fact]
    public void Parse_ValidXccdf_ParsesCheckRef()
    {
        var content = LoadTestFile("sample-valid.xccdf");
        var result = _parser.Parse(content, "sample-valid.xccdf");

        // Only rule 1 has a check element
        result.Results[0].CheckRef.Should().Be("oval:mil.disa.stig.windows_server_2022:def:254239");
        result.Results[1].CheckRef.Should().BeNull();
    }

    [Fact]
    public void Parse_ValidXccdf_ParsesPerRuleTimestamp()
    {
        var content = LoadTestFile("sample-valid.xccdf");
        var result = _parser.Parse(content, "sample-valid.xccdf");

        result.Results[0].Timestamp.Should().NotBeNull();
        result.Results[2].Timestamp.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Rule ID Extraction (ExtractRuleId)
    // ═══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("xccdf_mil.disa.stig_rule_SV-254239r849090_rule", "SV-254239r849090_rule")]
    [InlineData("SV-254239r849090_rule", "SV-254239r849090_rule")]
    [InlineData("custom_rule_id", "custom_rule_id")]
    [InlineData("", "")]
    public void ExtractRuleId_ReturnsExpected(string input, string expected)
    {
        var result = XccdfParser.ExtractRuleId(input);
        result.Should().Be(expected);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // XCCDF 1.1 Namespace Support
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_Xccdf11Namespace_ParsesSuccessfully()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<TestResult xmlns=""http://checklists.nist.gov/xccdf/1.1""
            id=""test_result_1"">
  <target>test-host</target>
  <rule-result idref=""SV-100001r111111_rule"" severity=""high"" weight=""10.0"">
    <result>fail</result>
  </rule-result>
  <score system=""urn:xccdf:scoring:default"" maximum=""100"">50.0</score>
</TestResult>";

        var result = _parser.Parse(XccdfBytes(xml), "test-1.1.xccdf");

        result.Target.Should().Be("test-host");
        result.Results.Should().HaveCount(1);
        result.Results[0].Result.Should().Be("fail");
        result.Score.Should().Be(50.0m);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Benchmark-Wrapped TestResult
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_BenchmarkWrappedTestResult_ParsesSuccessfully()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Benchmark xmlns=""http://checklists.nist.gov/xccdf/1.2""
           id=""xccdf_mil.disa.stig_benchmark_Test"">
  <TestResult id=""test_result_1"">
    <target>benchmark-host</target>
    <rule-result idref=""xccdf_mil.disa.stig_rule_SV-200001r222222_rule"" severity=""medium"" weight=""10.0"">
      <result>pass</result>
    </rule-result>
    <score system=""urn:xccdf:scoring:default"" maximum=""100"">100.0</score>
  </TestResult>
</Benchmark>";

        var result = _parser.Parse(XccdfBytes(xml), "benchmark.xccdf");

        result.Target.Should().Be("benchmark-host");
        result.Results.Should().HaveCount(1);
        result.Results[0].ExtractedRuleId.Should().Be("SV-200001r222222_rule");
        result.Results[0].Result.Should().Be("pass");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // No-Namespace Support
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_NoNamespace_ParsesSuccessfully()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<TestResult id=""test_result_1"">
  <target>no-ns-host</target>
  <rule-result idref=""SV-300001r333333_rule"" severity=""low"" weight=""5.0"">
    <result>notapplicable</result>
  </rule-result>
</TestResult>";

        var result = _parser.Parse(XccdfBytes(xml), "no-ns.xccdf");

        result.Target.Should().Be("no-ns-host");
        result.Results.Should().HaveCount(1);
        result.Results[0].Result.Should().Be("notapplicable");
        result.Results[0].Weight.Should().Be(5.0m);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Error / Edge Cases
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_EmptyContent_Throws_XccdfParseException()
    {
        var act = () => _parser.Parse(Array.Empty<byte>(), "empty.xccdf");

        act.Should().Throw<XccdfParseException>()
            .Which.FileName.Should().Be("empty.xccdf");
    }

    [Fact]
    public void Parse_NullContent_Throws_XccdfParseException()
    {
        var act = () => _parser.Parse(null!, "null.xccdf");

        act.Should().Throw<XccdfParseException>()
            .Which.FileName.Should().Be("null.xccdf");
    }

    [Fact]
    public void Parse_MalformedXml_Throws_XccdfParseException()
    {
        var content = LoadTestFile("sample-malformed.xccdf");

        var act = () => _parser.Parse(content, "sample-malformed.xccdf");

        act.Should().Throw<XccdfParseException>()
            .Which.Message.Should().Contain("Invalid XML");
    }

    [Fact]
    public void Parse_NoTestResult_Throws_XccdfParseException()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Benchmark xmlns=""http://checklists.nist.gov/xccdf/1.2""
           id=""xccdf_mil.disa.stig_benchmark_Test"">
  <status>accepted</status>
</Benchmark>";

        var act = () => _parser.Parse(XccdfBytes(xml), "no-testresult.xccdf");

        act.Should().Throw<XccdfParseException>()
            .Which.Message.Should().Contain("TestResult");
    }

    [Fact]
    public void Parse_EmptyRuleResults_Returns_EmptyList()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<TestResult xmlns=""http://checklists.nist.gov/xccdf/1.2""
            id=""test_result_1"">
  <target>empty-host</target>
</TestResult>";

        var result = _parser.Parse(XccdfBytes(xml), "empty-rules.xccdf");

        result.Results.Should().BeEmpty();
        result.Target.Should().Be("empty-host");
    }

    [Theory]
    [InlineData("error")]
    [InlineData("unknown")]
    [InlineData("notchecked")]
    public void Parse_ErrorUnknownNotchecked_LowercasedCorrectly(string resultValue)
    {
        var xml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<TestResult xmlns=""http://checklists.nist.gov/xccdf/1.2"" id=""tr_1"">
  <target>host</target>
  <rule-result idref=""SV-100001r111111_rule"" severity=""medium"" weight=""10.0"">
    <result>{resultValue}</result>
  </rule-result>
</TestResult>";

        var result = _parser.Parse(XccdfBytes(xml), "test.xccdf");

        result.Results[0].Result.Should().Be(resultValue.ToLowerInvariant());
    }

    [Fact]
    public void Parse_NoScore_Returns_NullScores()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<TestResult xmlns=""http://checklists.nist.gov/xccdf/1.2"" id=""tr_1"">
  <target>host</target>
  <rule-result idref=""SV-100001r111111_rule"" severity=""medium"" weight=""10.0"">
    <result>pass</result>
  </rule-result>
</TestResult>";

        var result = _parser.Parse(XccdfBytes(xml), "no-score.xccdf");

        result.Score.Should().BeNull();
        result.MaxScore.Should().BeNull();
    }

    [Fact]
    public void Parse_NoTargetFacts_Returns_EmptyDictionary()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<TestResult xmlns=""http://checklists.nist.gov/xccdf/1.2"" id=""tr_1"">
  <target>host</target>
  <rule-result idref=""SV-100001r111111_rule"" severity=""medium"" weight=""10.0"">
    <result>pass</result>
  </rule-result>
</TestResult>";

        var result = _parser.Parse(XccdfBytes(xml), "no-facts.xccdf");

        result.TargetFacts.Should().BeEmpty();
    }

    [Fact]
    public void Parse_UnrecognizedNamespace_Throws_XccdfParseException()
    {
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<SomeRoot xmlns=""http://example.com/unknown"">
  <child>text</child>
</SomeRoot>";

        var act = () => _parser.Parse(XccdfBytes(xml), "unknown-ns.xccdf");

        act.Should().Throw<XccdfParseException>()
            .Which.Message.Should().Contain("namespace");
    }
}
