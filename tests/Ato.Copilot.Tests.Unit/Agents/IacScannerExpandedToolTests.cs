using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Ato.Copilot.Agents.Compliance.Tools;

namespace Ato.Copilot.Tests.Unit.Agents;

/// <summary>
/// Expanded tests for IacComplianceScanTool (T086):
/// verifies 50+ rules, suggested-fix format, CAT severity mapping,
/// and rule coverage across all 8 control families.
/// </summary>
public class IacScannerExpandedToolTests
{
    private readonly IacComplianceScanTool _tool;

    public IacScannerExpandedToolTests()
    {
        _tool = new IacComplianceScanTool(Mock.Of<ILogger<IacComplianceScanTool>>());
    }

    // ────────────────────────────────────────────────────────────────
    // Rule Count
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public void RuleCount_IsAtLeast50()
    {
        IacComplianceScanTool.RuleCount.Should().BeGreaterOrEqualTo(50);
    }

    // ────────────────────────────────────────────────────────────────
    // Severity Mapping
    // ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Critical", "CAT I")]
    [InlineData("High", "CAT I")]
    [InlineData("Medium", "CAT II")]
    [InlineData("Low", "CAT III")]
    public async Task Findings_HaveCorrectCatSeverity(string severity, string expectedCat)
    {
        // Use a trigger that fires a known-severity rule
        var (content, ruleId) = severity switch
        {
            "Critical" => ("  sourceAddressPrefix: '*'", "AC-4-01"),            // Critical
            "High"     => ("  publicNetworkAccess: 'Enabled'", "AC-3-01"),       // High
            "Medium"   => ("  publicIPAllocationMethod: 'Static'", "AC-17-01"),  // Medium
            "Low"      => ("  detailedErrorLoggingEnabled: false", "AU-12-02"),   // Low
            _ => throw new ArgumentException()
        };

        var args = new Dictionary<string, object?>
        {
            ["filePath"] = "test.bicep",
            ["fileContent"] = content,
            ["fileType"] = "bicep"
        };

        var result = await _tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);
        var findings = doc.RootElement.GetProperty("findings").EnumerateArray().ToList();

        findings.Should().Contain(f =>
            f.GetProperty("ruleId").GetString() == ruleId
            && f.GetProperty("catSeverity").GetString() == expectedCat);
    }

    // ────────────────────────────────────────────────────────────────
    // Suggested Fix Format (unified diff)
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AutoRemediableFinding_HasUnifiedDiffFormat()
    {
        // Triggers SC-8-01: HTTP URL → HTTPS
        var content = "  endpoint: 'http://storage.blob.core.windows.net'";
        var args = new Dictionary<string, object?>
        {
            ["filePath"] = "main.bicep",
            ["fileContent"] = content,
            ["fileType"] = "bicep"
        };

        var result = await _tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);
        var findings = doc.RootElement.GetProperty("findings").EnumerateArray().ToList();

        var sc8 = findings.FirstOrDefault(f => f.GetProperty("controlId").GetString() == "SC-8");
        sc8.ValueKind.Should().NotBe(JsonValueKind.Undefined);

        sc8.GetProperty("autoRemediable").GetBoolean().Should().BeTrue();

        var fix = sc8.GetProperty("suggestedFix").GetString();
        fix.Should().NotBeNullOrEmpty();
        fix!.Should().StartWith("--- original");
        fix.Should().Contain("+++ fixed");
        fix.Should().Contain("@@ -1 +1 @@");
        fix.Should().Contain("-");
        fix.Should().Contain("+");
        fix.Should().Contain("https://");
    }

    [Fact]
    public async Task NonAutoRemediableFinding_HasNullSuggestedFix()
    {
        // Triggers AC-4-01: NSG wildcard source — not auto-remediable
        var content = "  sourceAddressPrefix: '*'";
        var args = new Dictionary<string, object?>
        {
            ["filePath"] = "main.bicep",
            ["fileContent"] = content,
            ["fileType"] = "bicep"
        };

        var result = await _tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);
        var findings = doc.RootElement.GetProperty("findings").EnumerateArray().ToList();

        var ac4 = findings.FirstOrDefault(f => f.GetProperty("controlId").GetString() == "AC-4");
        ac4.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        ac4.GetProperty("autoRemediable").GetBoolean().Should().BeFalse();
        ac4.GetProperty("suggestedFix").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ────────────────────────────────────────────────────────────────
    // AC: Access Control rules
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC3_PublicNetworkAccessEnabled_Detected()
    {
        var content = "  publicNetworkAccess: 'Enabled'";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "AC-3-01");
    }

    [Fact]
    public async Task AC3_AllowBlobPublicAccess_Detected()
    {
        var content = "  allowBlobPublicAccess: true";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "AC-3-02");
    }

    [Fact]
    public async Task AC4_WildcardNsgSource_Detected()
    {
        var content = "  sourceAddressPrefix: '*'";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "AC-4-01");
    }

    [Fact]
    public async Task AC4_SshPort22_Detected()
    {
        var content = "  destinationPortRange: '22'";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "AC-4-02");
    }

    [Fact]
    public async Task AC4_RdpPort3389_Detected()
    {
        var content = "  destinationPortRange: '3389'";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "AC-4-03");
    }

    [Fact]
    public async Task AC4_AllPortsWildcard_Detected()
    {
        var content = "  destinationPortRange: '*'";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "AC-4-04");
    }

    [Fact]
    public async Task AC6_OwnerRole_Detected()
    {
        var content = "  adminUserEnabled: true";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "AC-6-01");
    }

    [Fact]
    public async Task AC17_PublicIp_Detected()
    {
        var content = "resource pip 'Microsoft.Network/publicIPAddresses@2023-05-01' = {";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "AC-17-01");
    }

    // ────────────────────────────────────────────────────────────────
    // AU: Audit and Accountability rules
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AU6_LoggingDisabledFalse_Detected()
    {
        var content = "  logging: false";
        var findings = await ScanAndGetFindings(content, "bicep");
        // AU-6-01 matches ("logging"|"diagnostics") AND ("false"|"disabled")
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "AU-6-01");
    }

    [Fact]
    public async Task AU9_ImmutabilityDisabled_Detected()
    {
        var content = "  immutabilityPolicy: 'Disabled'";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "AU-9-01");
    }

    // ────────────────────────────────────────────────────────────────
    // CM: Configuration Management rules
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CM2_OldTlsVersion_Detected()
    {
        var content = "  minTlsVersion: 'TLS1_0'";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "CM-2-01");
    }

    [Fact]
    public async Task CM6_FtpEnabled_Detected()
    {
        var content = "  ftpsState: 'AllAllowed'";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "CM-6-01");
    }

    [Fact]
    public async Task CM6_RemoteDebugging_Detected()
    {
        var content = "  remoteDebuggingEnabled: true";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "CM-6-02");
    }

    // ────────────────────────────────────────────────────────────────
    // IA: Identification and Authentication rules
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task IA5_HardcodedPassword_Detected()
    {
        var content = "  password: 'SuperSecret123!'";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "IA-5-01");
    }

    [Fact]
    public async Task IA5_HardcodedApiKey_Detected()
    {
        var content = "  apiKey: 'abc123secret'";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "IA-5-01");
    }

    [Fact]
    public async Task IA5_ConnectionString_Detected()
    {
        var content = "  connectionString: 'Server=myserver;Password=mypass'";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "IA-5-02");
    }

    [Fact]
    public async Task IA5_AksLocalAccounts_Detected()
    {
        var content = "  disableLocalAccounts: false";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "IA-5-04");
    }

    // ────────────────────────────────────────────────────────────────
    // SC: System and Communications Protection rules
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SC8_HttpUrl_Detected()
    {
        var content = "  endpoint: 'http://example.com/api'";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "SC-8-01");
    }

    [Fact]
    public async Task SC8_HttpsTrafficOnlyFalse_Detected()
    {
        var content = "  supportsHttpsTrafficOnly: false";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "SC-8-02");
    }

    [Fact]
    public async Task SC28_EncryptionDisabled_Detected()
    {
        var content = "  encryptionEnabled: false";
        var findings = await ScanAndGetFindings(content, "bicep");
        // SC-28 rules detect encryption disabled
        findings.Should().Contain(f =>
            f.GetProperty("controlId").GetString() == "SC-28");
    }

    [Fact]
    public async Task SC7_FirewallStartIpAllZeros_Detected()
    {
        var content = "  startIpAddress: '0.0.0.0'";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "SC-7-01");
    }

    [Fact]
    public async Task SC12_SoftDeleteDisabled_Detected()
    {
        var content = "  enableSoftDelete: false";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "SC-12-01");
    }

    [Fact]
    public async Task SC13_KeySizeSmall_Detected()
    {
        var content = "  keySize: 1024";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "SC-13-01");
    }

    // ────────────────────────────────────────────────────────────────
    // SI: System and Information Integrity rules
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SI3_DefenderFree_Detected()
    {
        var content = "  pricingTier: 'Free'";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "SI-3-01");
    }

    [Fact]
    public async Task SI4_WafDetectionMode_Detected()
    {
        var content = "  firewallMode: 'Detection'";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "SI-4-01");
    }

    // ────────────────────────────────────────────────────────────────
    // CP: Contingency Planning rules
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CP9_LrsStorage_Detected()
    {
        var content = "  sku: 'Standard_LRS'";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "CP-9-01");
    }

    // ────────────────────────────────────────────────────────────────
    // MP: Media Protection rules
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MP5_InfraEncryptionDisabled_Detected()
    {
        var content = "  requireInfrastructureEncryption: false";
        var findings = await ScanAndGetFindings(content, "bicep");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "MP-5-01");
    }

    // ────────────────────────────────────────────────────────────────
    // Finding structure validation
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Finding_HasAllRequiredFields()
    {
        var content = "  publicNetworkAccess: 'Enabled'";
        var args = new Dictionary<string, object?>
        {
            ["filePath"] = "main.bicep",
            ["fileContent"] = content,
            ["fileType"] = "bicep"
        };

        var result = await _tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);
        var finding = doc.RootElement.GetProperty("findings").EnumerateArray().First();

        finding.TryGetProperty("findingId", out _).Should().BeTrue();
        finding.TryGetProperty("ruleId", out _).Should().BeTrue();
        finding.TryGetProperty("controlId", out _).Should().BeTrue();
        finding.TryGetProperty("controlFamily", out _).Should().BeTrue();
        finding.TryGetProperty("severity", out _).Should().BeTrue();
        finding.TryGetProperty("catSeverity", out _).Should().BeTrue();
        finding.TryGetProperty("title", out _).Should().BeTrue();
        finding.TryGetProperty("description", out _).Should().BeTrue();
        finding.TryGetProperty("lineNumber", out _).Should().BeTrue();
        finding.TryGetProperty("lineContent", out _).Should().BeTrue();
        finding.TryGetProperty("remediation", out _).Should().BeTrue();
        finding.TryGetProperty("autoRemediable", out _).Should().BeTrue();
        finding.TryGetProperty("suggestedFix", out _).Should().BeTrue();
        finding.TryGetProperty("framework", out _).Should().BeTrue();
    }

    [Fact]
    public async Task FindingId_ContainsRuleId()
    {
        var content = "  publicNetworkAccess: 'Enabled'";
        var findings = await ScanAndGetFindings(content, "bicep");
        var finding = findings.First(f => f.GetProperty("ruleId").GetString() == "AC-3-01");
        finding.GetProperty("findingId").GetString().Should().StartWith("IAC-AC-3-01-");
    }

    // ────────────────────────────────────────────────────────────────
    // Terraform support
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Terraform_PasswordVariable_Detected()
    {
        var content = "  password = \"P@ssw0rd123\"";
        var findings = await ScanAndGetFindings(content, "terraform");
        findings.Should().Contain(f =>
            f.GetProperty("controlId").GetString() == "IA-5");
    }

    [Fact]
    public async Task Terraform_NsgSourceAny_Detected()
    {
        var content = "  source_address_prefix  = \"*\"";
        var findings = await ScanAndGetFindings(content, "terraform");
        findings.Should().Contain(f => f.GetProperty("ruleId").GetString() == "AC-4-01");
    }

    // ────────────────────────────────────────────────────────────────
    // SuggestedFix generates correct replacement
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SuggestedFix_HttpToHttps_ContainsHttpsReplacement()
    {
        var content = "  endpoint: 'http://myblob.blob.core.windows.net'";
        var findings = await ScanAndGetFindings(content, "bicep");
        var sc8 = findings.First(f => f.GetProperty("ruleId").GetString() == "SC-8-01");
        var fix = sc8.GetProperty("suggestedFix").GetString()!;

        // The fix should replace http:// with https://
        fix.Should().Contain("-");
        fix.Should().Contain("+");
        var plusLine = fix.Split('\n').First(l => l.StartsWith("+") && !l.StartsWith("+++"));
        plusLine.Should().Contain("https://");
    }

    [Fact]
    public async Task SuggestedFix_PublicNetworkAccess_DisablesIt()
    {
        var content = "  publicNetworkAccess: 'Enabled'";
        var findings = await ScanAndGetFindings(content, "bicep");
        var ac3 = findings.First(f => f.GetProperty("ruleId").GetString() == "AC-3-01");
        var fix = ac3.GetProperty("suggestedFix").GetString()!;

        var plusLine = fix.Split('\n').First(l => l.StartsWith("+") && !l.StartsWith("+++"));
        plusLine.Should().Contain("Disabled");
    }

    // ────────────────────────────────────────────────────────────────
    // Clean file produces zero findings
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CleanFile_NoInsecurePatterns_ZeroFindings()
    {
        var content = @"
@secure()
param adminPassword string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'mystorage'
  location: 'eastus'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
}
";
        var args = new Dictionary<string, object?>
        {
            ["filePath"] = "clean.bicep",
            ["fileContent"] = content,
            ["fileType"] = "bicep"
        };

        var result = await _tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("totalFindings").GetInt32().Should().Be(0);
    }

    // ────────────────────────────────────────────────────────────────
    // Multiple findings per file
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MultipleViolations_AllDetected()
    {
        var content = @"
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'mystorage'
  properties: {
    supportsHttpsTrafficOnly: false
    publicNetworkAccess: 'Enabled'
    minTlsVersion: 'TLS1_0'
    allowBlobPublicAccess: true
  }
}
";
        var findings = await ScanAndGetFindings(content, "bicep");
        // Should detect at least 4 issues
        findings.Count.Should().BeGreaterOrEqualTo(4);

        findings.Should().Contain(f => f.GetProperty("controlId").GetString() == "SC-8"); // httpsTrafficOnly: false
        findings.Should().Contain(f => f.GetProperty("controlId").GetString() == "AC-3"); // publicNetworkAccess
        findings.Should().Contain(f => f.GetProperty("controlId").GetString() == "CM-2"); // minTlsVersion TLS1_0
    }

    // ────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────

    private async Task<List<JsonElement>> ScanAndGetFindings(string content, string fileType)
    {
        var args = new Dictionary<string, object?>
        {
            ["filePath"] = $"test.{(fileType == "terraform" ? "tf" : "bicep")}",
            ["fileContent"] = content,
            ["fileType"] = fileType
        };

        var result = await _tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);
        return doc.RootElement.GetProperty("findings").EnumerateArray().ToList();
    }
}
