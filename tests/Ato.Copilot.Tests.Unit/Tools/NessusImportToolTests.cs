// ═══════════════════════════════════════════════════════════════════════════
// Feature 026 — ACAS/Nessus Scan Import: MCP Tool Unit Tests
// Tests for ImportNessusTool parameter validation and error handling.
// ═══════════════════════════════════════════════════════════════════════════

using System.Text;
using System.Text.Json;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ato.Copilot.Tests.Unit.Tools;

public class NessusImportToolTests : IDisposable
{
    private readonly Mock<IScanImportService> _importServiceMock = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly ImportNessusTool _tool;

    public NessusImportToolTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AtoCopilotContext>(o =>
            o.UseInMemoryDatabase($"NessusToolTests-{Guid.NewGuid()}"));
        _serviceProvider = services.BuildServiceProvider();

        _tool = new ImportNessusTool(
            _importServiceMock.Object,
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ImportNessusTool>.Instance);
    }

    public void Dispose() => _serviceProvider.Dispose();

    [Fact]
    public void Name_ReturnsCorrectToolName()
    {
        _tool.Name.Should().Be("compliance_import_nessus");
    }

    [Fact]
    public void Parameters_ContainAllRequiredAndOptionalKeys()
    {
        _tool.Parameters.Should().ContainKey("system_id");
        _tool.Parameters.Should().ContainKey("file_content");
        _tool.Parameters.Should().ContainKey("file_name");
        _tool.Parameters.Should().ContainKey("conflict_resolution");
        _tool.Parameters.Should().ContainKey("dry_run");
        _tool.Parameters.Should().ContainKey("assessment_id");

        _tool.Parameters["system_id"].Required.Should().BeTrue();
        _tool.Parameters["file_content"].Required.Should().BeTrue();
        _tool.Parameters["file_name"].Required.Should().BeTrue();
        _tool.Parameters["conflict_resolution"].Required.Should().BeFalse();
    }

    [Fact]
    public async Task ImportNessus_MissingSystemId_ReturnsError()
    {
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "",
            ["file_content"] = Convert.ToBase64String("dummy"u8.ToArray()),
            ["file_name"] = "test.nessus"
        };

        var result = await _tool.ExecuteCoreAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task ImportNessus_MissingFileContent_ReturnsError()
    {
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_content"] = "",
            ["file_name"] = "test.nessus"
        };

        var result = await _tool.ExecuteCoreAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task ImportNessus_InvalidFileExtension_ReturnsError()
    {
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_content"] = Convert.ToBase64String("dummy"u8.ToArray()),
            ["file_name"] = "scan_results.xml"
        };

        var result = await _tool.ExecuteCoreAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_FILE_TYPE");
    }

    [Fact]
    public async Task ImportNessus_FileTooLarge_ReturnsError()
    {
        // Create a file larger than 5 MB
        var largeContent = new byte[6 * 1024 * 1024];
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_content"] = Convert.ToBase64String(largeContent),
            ["file_name"] = "large_scan.nessus"
        };

        var result = await _tool.ExecuteCoreAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("FILE_TOO_LARGE");
    }

    [Fact]
    public async Task ImportNessus_InvalidBase64_ReturnsError()
    {
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_content"] = "not-valid-base64!!!",
            ["file_name"] = "scan.nessus"
        };

        var result = await _tool.ExecuteCoreAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_BASE64");
    }

    [Fact]
    public async Task ImportNessus_ValidInvocation_ReturnsSuccess()
    {
        var nessusResult = new NessusImportResult(
            ImportRecordId: "import-001",
            Status: ScanImportStatus.Completed,
            ReportName: "Test Scan",
            TotalPluginResults: 5,
            InformationalCount: 1,
            CriticalCount: 1,
            HighCount: 1,
            MediumCount: 1,
            LowCount: 1,
            HostCount: 1,
            FindingsCreated: 4,
            FindingsUpdated: 0,
            SkippedCount: 0,
            PoamWeaknessesCreated: 0,
            EffectivenessRecordsCreated: 0,
            EffectivenessRecordsUpdated: 0,
            NistControlsAffected: 0,
            CredentialedScan: true,
            IsDryRun: false,
            Warnings: new List<string>(),
            ErrorMessage: null);

        _importServiceMock.Setup(s => s.ImportNessusAsync(
                It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<byte[]>(), It.IsAny<string>(),
                It.IsAny<ImportConflictResolution>(), It.IsAny<bool>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(nessusResult);

        var nessusXml = "<?xml version=\"1.0\"?><NessusClientData_v2></NessusClientData_v2>";
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(nessusXml)),
            ["file_name"] = "scan.nessus"
        };

        var result = await _tool.ExecuteCoreAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("import_record_id").GetString().Should().Be("import-001");
        json.RootElement.GetProperty("data").GetProperty("severity_breakdown").GetProperty("critical").GetInt32().Should().Be(1);
    }
}
