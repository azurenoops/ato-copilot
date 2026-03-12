using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Compliance.Services.ScanImport;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Parsers;

/// <summary>
/// Unit tests for <see cref="NessusParser"/> — Feature 026 .nessus XML parsing.
/// Covers UT-001, UT-002, UT-003, UT-008, UT-018, UT-019, UT-020.
/// </summary>
public class NessusParserTests
{
    private readonly NessusParser _parser;

    public NessusParserTests()
    {
        _parser = new NessusParser(Mock.Of<ILogger<NessusParser>>());
    }

    private static byte[] LoadTestFile(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        return File.ReadAllBytes(path);
    }

    private static byte[] NessusBytes(string xml) => System.Text.Encoding.UTF8.GetBytes(xml);

    // ─── UT-001: Parse valid .nessus file ────────────────────────────────────

    [Fact]
    public void Parse_ValidNessusFile_ExtractsAllHostsAndPlugins()
    {
        // Arrange
        var content = LoadTestFile("sample-single-host.nessus");

        // Act
        var result = _parser.Parse(content);

        // Assert — 1 host with 5 total plugins (4 non-info + 1 info excluded)
        result.Hosts.Should().HaveCount(1);
        result.ReportName.Should().Be("Single Host Scan");
        result.TotalPluginResults.Should().Be(5);
        result.InformationalCount.Should().Be(1);
        // Non-informational plugins only in host results
        result.Hosts[0].PluginResults.Should().HaveCount(4);
    }

    // ─── UT-002: Extract plugin fields ───────────────────────────────────────

    [Fact]
    public void Parse_ExtractsPluginFields_CorrectlyFromReportItem()
    {
        // Arrange
        var content = LoadTestFile("sample-single-host.nessus");

        // Act
        var result = _parser.Parse(content);
        var criticalPlugin = result.Hosts[0].PluginResults.First(p => p.PluginId == 97833);

        // Assert — verify all key fields from the Critical plugin (severity 4)
        criticalPlugin.PluginName.Should().Contain("MS17-010");
        criticalPlugin.PluginFamily.Should().Be("Windows : Microsoft Bulletins");
        criticalPlugin.Severity.Should().Be(4);
        criticalPlugin.RiskFactor.Should().Be("Critical");
        criticalPlugin.Port.Should().Be(445);
        criticalPlugin.Protocol.Should().Be("tcp");
        criticalPlugin.ServiceName.Should().Be("cifs");
        criticalPlugin.Synopsis.Should().NotBeNullOrEmpty();
        criticalPlugin.Description.Should().NotBeNullOrEmpty();
        criticalPlugin.Solution.Should().NotBeNullOrEmpty();
        criticalPlugin.PluginOutput.Should().NotBeNullOrEmpty();
        criticalPlugin.Cves.Should().Contain("CVE-2017-0143");
        criticalPlugin.Cves.Should().Contain("CVE-2017-0144");
        criticalPlugin.CvssV3BaseScore.Should().Be(8.1);
        criticalPlugin.CvssV3Vector.Should().Contain("CVSS:3.0");
        criticalPlugin.CvssV2BaseScore.Should().Be(9.3);
        criticalPlugin.VprScore.Should().Be(9.8);
        criticalPlugin.ExploitAvailable.Should().BeTrue();
        criticalPlugin.StigSeverity.Should().Be("I");
        criticalPlugin.Xrefs.Should().Contain("STIG-ID:WN19-00-000010");
        criticalPlugin.Xrefs.Should().Contain("IAVA:2017-A-0065");
    }

    // ─── UT-003: Extract host properties ─────────────────────────────────────

    [Fact]
    public void Parse_ExtractsHostProperties_FromReportHostTags()
    {
        // Arrange
        var content = LoadTestFile("sample-single-host.nessus");

        // Act
        var result = _parser.Parse(content);
        var host = result.Hosts[0];

        // Assert
        host.Hostname.Should().Be("server01.domain.mil");
        host.HostIp.Should().Be("10.0.1.50");
        host.OperatingSystem.Should().Contain("Windows Server 2019");
        host.MacAddress.Should().Be("00:0C:29:3E:5A:1B");
        host.CredentialedScan.Should().BeTrue();
        host.ScanStart.Should().NotBeNull();
        host.ScanEnd.Should().NotBeNull();
        host.ScanEnd.Should().BeAfter(host.ScanStart!.Value);
    }

    // ─── UT-008: Informational plugins excluded from findings ────────────────

    [Fact]
    public void Parse_InformationalPlugins_ExcludedFromHostResults()
    {
        // Arrange
        var content = LoadTestFile("sample-single-host.nessus");

        // Act
        var result = _parser.Parse(content);

        // Assert — severity 0 plugins are counted but not in host plugin results
        result.InformationalCount.Should().Be(1);
        result.Hosts[0].PluginResults.Should().OnlyContain(p => p.Severity > 0);
    }

    // ─── UT-018: Malformed XML rejection ─────────────────────────────────────

    [Fact]
    public void Parse_MalformedXml_ThrowsNessusParseException()
    {
        // Arrange
        var content = LoadTestFile("sample-malformed.nessus");

        // Act
        var act = () => _parser.Parse(content);

        // Assert
        act.Should().Throw<NessusParseException>()
            .WithMessage("*malformed XML*");
    }

    // ─── UT-019: Empty ReportHost handling ───────────────────────────────────

    [Fact]
    public void Parse_NoReportHosts_ReturnsEmptyHostList()
    {
        // Arrange — valid XML structure but no ReportHost elements
        var xml = """
            <?xml version="1.0" ?>
            <NessusClientData_v2>
              <Report name="Empty Scan">
              </Report>
            </NessusClientData_v2>
            """;
        var content = NessusBytes(xml);

        // Act
        var result = _parser.Parse(content);

        // Assert
        result.Hosts.Should().BeEmpty();
        result.TotalPluginResults.Should().Be(0);
        result.InformationalCount.Should().Be(0);
        result.ReportName.Should().Be("Empty Scan");
    }

    // ─── UT-020: Multi-host extraction ───────────────────────────────────────

    [Fact]
    public void Parse_MultiHostFile_ExtractsCorrectHostData()
    {
        // Arrange
        var content = LoadTestFile("sample-multi-host.nessus");

        // Act
        var result = _parser.Parse(content);

        // Assert — 3 hosts with correct separation
        result.Hosts.Should().HaveCount(3);

        var winHost = result.Hosts.First(h => h.Hostname == "win-server.domain.mil");
        winHost.HostIp.Should().Be("10.0.1.10");
        winHost.OperatingSystem.Should().Contain("Windows Server 2022");
        winHost.PluginResults.Should().HaveCount(2); // severity 4 + severity 1

        var linuxHost = result.Hosts.First(h => h.Hostname == "linux-web.domain.mil");
        linuxHost.HostIp.Should().Be("10.0.1.20");
        linuxHost.PluginResults.Should().HaveCount(2); // severity 3 + severity 2 (info excluded)

        var networkHost = result.Hosts.First(h => h.HostIp == "10.0.1.1");
        networkHost.CredentialedScan.Should().BeFalse();
        networkHost.PluginResults.Should().HaveCount(1); // severity 3 only (info excluded)
    }

    // ─── Additional edge cases ───────────────────────────────────────────────

    [Fact]
    public void Parse_MissingRootElement_ThrowsNessusParseException()
    {
        // Arrange
        var xml = """<?xml version="1.0" ?><WrongRoot></WrongRoot>""";
        var content = NessusBytes(xml);

        // Act
        var act = () => _parser.Parse(content);

        // Assert
        act.Should().Throw<NessusParseException>()
            .WithMessage("*missing*NessusClientData_v2*");
    }

    [Fact]
    public void Parse_PluginWithNoCves_ReturnEmptyCveList()
    {
        // Arrange — plugin with no CVE elements
        var xml = """
            <?xml version="1.0" ?>
            <NessusClientData_v2>
              <Report name="No CVE Test">
                <ReportHost name="host1">
                  <HostProperties>
                    <tag name="host-ip">10.0.0.1</tag>
                  </HostProperties>
                  <ReportItem pluginID="10287" pluginName="Traceroute" pluginFamily="General" severity="1" port="0" protocol="tcp" svc_name="general">
                    <risk_factor>Low</risk_factor>
                  </ReportItem>
                </ReportHost>
              </Report>
            </NessusClientData_v2>
            """;

        // Act
        var result = _parser.Parse(NessusBytes(xml));

        // Assert
        result.Hosts[0].PluginResults[0].Cves.Should().BeEmpty();
        result.Hosts[0].PluginResults[0].ExploitAvailable.Should().BeFalse();
    }
}
