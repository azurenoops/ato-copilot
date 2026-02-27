using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Ato.Copilot.Agents.Compliance.Tools;

namespace Ato.Copilot.Tests.Unit.Agents;

/// <summary>
/// Tests for IacComplianceScanTool (T027, FR-029f, R-009).
/// </summary>
public class IacComplianceScanToolTests
{
    private readonly IacComplianceScanTool _tool;

    public IacComplianceScanToolTests()
    {
        _tool = new IacComplianceScanTool(Mock.Of<ILogger<IacComplianceScanTool>>());
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        _tool.Name.Should().Be("iac_compliance_scan");
    }

    [Fact]
    public void Parameters_ContainsExpectedKeys()
    {
        var keys = _tool.Parameters.Keys;
        keys.Should().Contain("filePath");
        keys.Should().Contain("fileContent");
        keys.Should().Contain("fileType");
        keys.Should().Contain("framework");
    }

    [Fact]
    public async Task ExecuteCoreAsync_BicepFileWithHttpUrl_ReturnsFindings()
    {
        var content = @"
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'mystorage'
  location: 'eastus'
  properties: {
    supportsHttpsTrafficOnly: false
    primaryEndpoints: {
      blob: 'http://mystorage.blob.core.windows.net'
    }
  }
}";

        var args = new Dictionary<string, object?>
        {
            ["filePath"] = "main.bicep",
            ["fileContent"] = content,
            ["fileType"] = "bicep"
        };

        var result = await _tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("fileType").GetString().Should().Be("bicep");
        root.GetProperty("totalFindings").GetInt32().Should().BeGreaterThan(0);

        var findings = root.GetProperty("findings");
        findings.GetArrayLength().Should().BeGreaterThan(0);

        // Should detect the HTTP URL (SC-8 violation)
        var findingArray = findings.EnumerateArray().ToList();
        findingArray.Should().Contain(f =>
            f.GetProperty("controlId").GetString() == "SC-8");
    }

    [Fact]
    public async Task ExecuteCoreAsync_TerraformFileWithHardcodedSecret_ReturnsFindings()
    {
        var content = @"
resource ""azurerm_key_vault_secret"" ""example"" {
  name         = ""database-password""
  value        = ""SuperSecret123!""
  key_vault_id = azurerm_key_vault.example.id
}

variable ""admin_password"" {
  default = ""P@ssw0rd123""
}";

        var args = new Dictionary<string, object?>
        {
            ["filePath"] = "main.tf",
            ["fileContent"] = content,
            ["fileType"] = "terraform"
        };

        var result = await _tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("totalFindings").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteCoreAsync_NonIacFileType_ReturnsNoFindings()
    {
        var args = new Dictionary<string, object?>
        {
            ["filePath"] = "readme.md",
            ["fileContent"] = "# My Project\nThis is a readme.",
            ["fileType"] = "markdown"
        };

        var result = await _tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("findings").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ExecuteCoreAsync_EmptyContent_ReturnsError()
    {
        var args = new Dictionary<string, object?>
        {
            ["filePath"] = "main.bicep",
            ["fileContent"] = "",
            ["fileType"] = "bicep"
        };

        var result = await _tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetString().Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteCoreAsync_CancellationToken_IsHonored()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var args = new Dictionary<string, object?>
        {
            ["filePath"] = "main.bicep",
            ["fileContent"] = "resource foo 'Microsoft.Storage/storageAccounts@2023-01-01' = {}",
            ["fileType"] = "bicep"
        };

        var act = () => _tool.ExecuteCoreAsync(args, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteCoreAsync_CleanBicepFile_ReturnsZeroFindings()
    {
        var content = @"
@secure()
param adminPassword string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'mystorage'
  location: 'eastus'
  properties: {
    supportsHttpsTrafficOnly: true
  }
}";

        var args = new Dictionary<string, object?>
        {
            ["filePath"] = "main.bicep",
            ["fileContent"] = content,
            ["fileType"] = "bicep"
        };

        var result = await _tool.ExecuteCoreAsync(args);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("totalFindings").GetInt32().Should().Be(0);
    }
}
