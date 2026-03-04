// ═══════════════════════════════════════════════════════════════════════════
// Feature 017 — SCAP/STIG Import: MCP Tool Unit Tests
// Tests for ImportCklTool, ImportXccdfTool, ExportCklTool,
// ListImportsTool, and GetImportSummaryTool.
// ═══════════════════════════════════════════════════════════════════════════

using System.Text;
using System.Text.Json;
using Ato.Copilot.Agents.Compliance.Services.ScanImport;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ato.Copilot.Tests.Unit.Tools;

// ─────────────────────────────────────────────────────────────────────────────
// ImportCklTool Tests
// ─────────────────────────────────────────────────────────────────────────────

public class ImportCklToolTests : IDisposable
{
    private readonly Mock<IScanImportService> _importServiceMock = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly ImportCklTool _tool;

    public ImportCklToolTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AtoCopilotContext>(o =>
            o.UseInMemoryDatabase($"ImportCklToolTests-{Guid.NewGuid()}"));
        _serviceProvider = services.BuildServiceProvider();

        _tool = new ImportCklTool(
            _importServiceMock.Object,
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ImportCklTool>.Instance);
    }

    public void Dispose() => _serviceProvider.Dispose();

    [Fact]
    public void Name_ReturnsCorrectToolName()
    {
        _tool.Name.Should().Be("compliance_import_ckl");
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
    public async Task ImportCkl_MissingSystemId_ReturnsError()
    {
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "",
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("<xml/>")),
            ["file_name"] = "test.ckl"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task ImportCkl_MissingFileContent_ReturnsError()
    {
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_content"] = "",
            ["file_name"] = "test.ckl"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task ImportCkl_MissingFileName_ReturnsError()
    {
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("<xml/>")),
            ["file_name"] = ""
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task ImportCkl_InvalidBase64_ReturnsError()
    {
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_content"] = "not-valid-base64!!!",
            ["file_name"] = "test.ckl"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_BASE64");
    }

    [Fact]
    public async Task ImportCkl_FileTooLarge_ReturnsError()
    {
        var largeContent = new byte[6 * 1024 * 1024]; // 6 MB
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_content"] = Convert.ToBase64String(largeContent),
            ["file_name"] = "huge.ckl"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("FILE_TOO_LARGE");
    }

    [Fact]
    public async Task ImportCkl_ValidInput_CallsServiceAndReturnsSuccess()
    {
        var cklContent = "<CHECKLIST><STIGS></STIGS></CHECKLIST>";
        var importResult = new ImportResult(
            ImportRecordId: "imp-1",
            Status: ScanImportStatus.Completed,
            BenchmarkId: "Windows_Server_2022_STIG",
            BenchmarkTitle: "Windows Server 2022 STIG",
            TotalEntries: 10,
            OpenCount: 3,
            PassCount: 5,
            NotApplicableCount: 1,
            NotReviewedCount: 1,
            ErrorCount: 0,
            SkippedCount: 0,
            UnmatchedCount: 0,
            FindingsCreated: 8,
            FindingsUpdated: 0,
            EffectivenessRecordsCreated: 6,
            EffectivenessRecordsUpdated: 0,
            NistControlsAffected: 6,
            UnmatchedRules: new List<UnmatchedRuleInfo>(),
            Warnings: new List<string>(),
            ErrorMessage: null);

        _importServiceMock
            .Setup(s => s.ImportCklAsync(
                "sys-1", null, It.IsAny<byte[]>(), "test.ckl",
                ImportConflictResolution.Skip, false, "mcp-user",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(importResult);

        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(cklContent)),
            ["file_name"] = "test.ckl"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("import_record_id").GetString().Should().Be("imp-1");
        data.GetProperty("import_status").GetString().Should().Be("Completed");
        data.GetProperty("benchmark").GetString().Should().Be("Windows_Server_2022_STIG");
        data.GetProperty("total_entries").GetInt32().Should().Be(10);

        var summary = data.GetProperty("summary");
        summary.GetProperty("open").GetInt32().Should().Be(3);
        summary.GetProperty("pass").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task ImportCkl_DryRun_PassesDryRunParameter()
    {
        var importResult = new ImportResult(
            ImportRecordId: "dry-1", Status: ScanImportStatus.Completed,
            BenchmarkId: "test", BenchmarkTitle: "Test", TotalEntries: 1,
            OpenCount: 0, PassCount: 1, NotApplicableCount: 0,
            NotReviewedCount: 0, ErrorCount: 0, SkippedCount: 0,
            UnmatchedCount: 0, FindingsCreated: 0, FindingsUpdated: 0,
            EffectivenessRecordsCreated: 0, EffectivenessRecordsUpdated: 0,
            NistControlsAffected: 0, UnmatchedRules: new(), Warnings: new(),
            ErrorMessage: null);

        _importServiceMock
            .Setup(s => s.ImportCklAsync(
                "sys-1", null, It.IsAny<byte[]>(), "test.ckl",
                ImportConflictResolution.Skip, true, "mcp-user",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(importResult);

        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("<xml/>")),
            ["file_name"] = "test.ckl",
            ["dry_run"] = true
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("dry_run").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ImportCkl_ConflictResolution_ParsesOverwrite()
    {
        var importResult = new ImportResult(
            ImportRecordId: "ow-1", Status: ScanImportStatus.Completed,
            BenchmarkId: "test", BenchmarkTitle: "Test", TotalEntries: 1,
            OpenCount: 0, PassCount: 1, NotApplicableCount: 0,
            NotReviewedCount: 0, ErrorCount: 0, SkippedCount: 0,
            UnmatchedCount: 0, FindingsCreated: 0, FindingsUpdated: 1,
            EffectivenessRecordsCreated: 0, EffectivenessRecordsUpdated: 1,
            NistControlsAffected: 1, UnmatchedRules: new(), Warnings: new(),
            ErrorMessage: null);

        _importServiceMock
            .Setup(s => s.ImportCklAsync(
                "sys-1", null, It.IsAny<byte[]>(), "test.ckl",
                ImportConflictResolution.Overwrite, false, "mcp-user",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(importResult);

        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("<xml/>")),
            ["file_name"] = "test.ckl",
            ["conflict_resolution"] = "Overwrite"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task ImportCkl_ServiceThrows_ReturnsImportFailedError()
    {
        _importServiceMock
            .Setup(s => s.ImportCklAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<byte[]>(),
                It.IsAny<string>(), It.IsAny<ImportConflictResolution>(),
                It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("System not found"));

        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-invalid",
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("<xml/>")),
            ["file_name"] = "test.ckl"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("IMPORT_FAILED");
        json.RootElement.GetProperty("message").GetString().Should().Contain("System not found");
    }

    [Fact]
    public async Task ImportCkl_WithAssessmentId_PassesToService()
    {
        var importResult = new ImportResult(
            ImportRecordId: "aid-1", Status: ScanImportStatus.Completed,
            BenchmarkId: "test", BenchmarkTitle: "Test", TotalEntries: 1,
            OpenCount: 0, PassCount: 1, NotApplicableCount: 0,
            NotReviewedCount: 0, ErrorCount: 0, SkippedCount: 0,
            UnmatchedCount: 0, FindingsCreated: 1, FindingsUpdated: 0,
            EffectivenessRecordsCreated: 1, EffectivenessRecordsUpdated: 0,
            NistControlsAffected: 1, UnmatchedRules: new(), Warnings: new(),
            ErrorMessage: null);

        _importServiceMock
            .Setup(s => s.ImportCklAsync(
                "sys-1", "assess-42", It.IsAny<byte[]>(), "test.ckl",
                ImportConflictResolution.Skip, false, "mcp-user",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(importResult);

        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("<xml/>")),
            ["file_name"] = "test.ckl",
            ["assessment_id"] = "assess-42"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        _importServiceMock.Verify(s => s.ImportCklAsync(
            "sys-1", "assess-42", It.IsAny<byte[]>(), "test.ckl",
            ImportConflictResolution.Skip, false, "mcp-user",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportCkl_FailedResult_ReturnsErrorStatus()
    {
        var importResult = new ImportResult(
            ImportRecordId: "fail-1", Status: ScanImportStatus.Failed,
            BenchmarkId: null, BenchmarkTitle: null, TotalEntries: 0,
            OpenCount: 0, PassCount: 0, NotApplicableCount: 0,
            NotReviewedCount: 0, ErrorCount: 1, SkippedCount: 0,
            UnmatchedCount: 0, FindingsCreated: 0, FindingsUpdated: 0,
            EffectivenessRecordsCreated: 0, EffectivenessRecordsUpdated: 0,
            NistControlsAffected: 0, UnmatchedRules: new(), Warnings: new(),
            ErrorMessage: "Invalid CKL structure");

        _importServiceMock
            .Setup(s => s.ImportCklAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<byte[]>(),
                It.IsAny<string>(), It.IsAny<ImportConflictResolution>(),
                It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(importResult);

        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("<xml/>")),
            ["file_name"] = "bad.ckl"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("data").GetProperty("error_message").GetString().Should().Be("Invalid CKL structure");
    }

    [Fact]
    public async Task ImportCkl_UnmatchedRules_AppearsInOutput()
    {
        var importResult = new ImportResult(
            ImportRecordId: "um-1", Status: ScanImportStatus.CompletedWithWarnings,
            BenchmarkId: "test", BenchmarkTitle: "Test", TotalEntries: 3,
            OpenCount: 1, PassCount: 1, NotApplicableCount: 0,
            NotReviewedCount: 0, ErrorCount: 0, SkippedCount: 0,
            UnmatchedCount: 1, FindingsCreated: 2, FindingsUpdated: 0,
            EffectivenessRecordsCreated: 2, EffectivenessRecordsUpdated: 0,
            NistControlsAffected: 2,
            UnmatchedRules: new List<UnmatchedRuleInfo>
            {
                new("V-999999", "SV-999999r1_rule", "Unknown Check", "high")
            },
            Warnings: new List<string> { "1 unmatched rule(s)" },
            ErrorMessage: null);

        _importServiceMock
            .Setup(s => s.ImportCklAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<byte[]>(),
                It.IsAny<string>(), It.IsAny<ImportConflictResolution>(),
                It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(importResult);

        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("<xml/>")),
            ["file_name"] = "test.ckl"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        var data = json.RootElement.GetProperty("data");
        data.GetProperty("unmatched_rules").GetArrayLength().Should().Be(1);
        data.GetProperty("warnings").GetArrayLength().Should().BeGreaterThan(0);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ExportCklTool Tests
// ─────────────────────────────────────────────────────────────────────────────

public class ExportCklToolTests
{
    private readonly Mock<IScanImportService> _importServiceMock = new();
    private readonly ExportCklTool _tool;

    public ExportCklToolTests()
    {
        _tool = new ExportCklTool(
            _importServiceMock.Object,
            NullLogger<ExportCklTool>.Instance);
    }

    [Fact]
    public void Name_ReturnsCorrectToolName()
    {
        _tool.Name.Should().Be("compliance_export_ckl");
    }

    [Fact]
    public async Task ExportCkl_MissingSystemId_ReturnsError()
    {
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "",
            ["benchmark_id"] = "Windows_Server_2022_STIG"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task ExportCkl_MissingBenchmarkId_ReturnsError()
    {
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["benchmark_id"] = ""
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task ExportCkl_ValidInput_ReturnsBase64Content()
    {
        var expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("<CHECKLIST/>"));

        _importServiceMock
            .Setup(s => s.ExportCklAsync("sys-1", "Windows_STIG", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedBase64);

        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["benchmark_id"] = "Windows_STIG"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("file_content").GetString().Should().Be(expectedBase64);
        data.GetProperty("file_name").GetString().Should().Contain("Windows_STIG");
    }

    [Fact]
    public async Task ExportCkl_ServiceThrows_ReturnsExportFailedError()
    {
        _importServiceMock
            .Setup(s => s.ExportCklAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No data"));

        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["benchmark_id"] = "Windows_STIG"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("EXPORT_FAILED");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ListImportsTool Tests
// ─────────────────────────────────────────────────────────────────────────────

public class ListImportsToolTests
{
    private readonly Mock<IScanImportService> _importServiceMock = new();
    private readonly ListImportsTool _tool;

    public ListImportsToolTests()
    {
        _tool = new ListImportsTool(
            _importServiceMock.Object,
            NullLogger<ListImportsTool>.Instance);
    }

    [Fact]
    public void Name_ReturnsCorrectToolName()
    {
        _tool.Name.Should().Be("compliance_list_imports");
    }

    [Fact]
    public async Task ListImports_MissingSystemId_ReturnsError()
    {
        var args = new Dictionary<string, object?> { ["system_id"] = "" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task ListImports_ValidInput_ReturnsPagedResults()
    {
        var records = new List<ScanImportRecord>
        {
            new()
            {
                Id = "imp-1",
                FileName = "test.ckl",
                ImportType = ScanImportType.Ckl,
                BenchmarkId = "Windows_STIG",
                BenchmarkTitle = "Windows Server 2022 STIG",
                ImportStatus = ScanImportStatus.Completed,
                ImportedBy = "user1",
                ImportedAt = DateTime.UtcNow.AddHours(-1),
                TotalEntries = 10,
                OpenCount = 3,
                PassCount = 5,
                FindingsCreated = 8,
                FindingsUpdated = 0,
                RegisteredSystemId = "sys-1"
            }
        };

        _importServiceMock
            .Setup(s => s.ListImportsAsync(
                "sys-1", 1, 20, null, null, false, null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((records, 1));

        var args = new Dictionary<string, object?> { ["system_id"] = "sys-1" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("total_count").GetInt32().Should().Be(1);
        data.GetProperty("imports").GetArrayLength().Should().Be(1);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// GetImportSummaryTool Tests
// ─────────────────────────────────────────────────────────────────────────────

public class GetImportSummaryToolTests
{
    private readonly Mock<IScanImportService> _importServiceMock = new();
    private readonly GetImportSummaryTool _tool;

    public GetImportSummaryToolTests()
    {
        _tool = new GetImportSummaryTool(
            _importServiceMock.Object,
            NullLogger<GetImportSummaryTool>.Instance);
    }

    [Fact]
    public void Name_ReturnsCorrectToolName()
    {
        _tool.Name.Should().Be("compliance_get_import_summary");
    }

    [Fact]
    public async Task GetSummary_MissingImportId_ReturnsError()
    {
        var args = new Dictionary<string, object?> { ["import_id"] = "" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task GetSummary_NotFound_ReturnsNotFoundError()
    {
        _importServiceMock
            .Setup(s => s.GetImportSummaryAsync("imp-999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ValueTuple<ScanImportRecord, List<ScanImportFinding>>?)null);

        var args = new Dictionary<string, object?> { ["import_id"] = "imp-999" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task GetSummary_Found_ReturnsDetailedSummary()
    {
        var record = new ScanImportRecord
        {
            Id = "imp-1",
            FileName = "windows.ckl",
            FileHash = "abc123",
            ImportType = ScanImportType.Ckl,
            ImportStatus = ScanImportStatus.Completed,
            BenchmarkId = "Windows_STIG",
            BenchmarkTitle = "Windows Server 2022 STIG",
            TargetHostName = "WEBSERVER01",
            ImportedBy = "admin",
            ImportedAt = DateTime.UtcNow,
            TotalEntries = 5,
            OpenCount = 2,
            PassCount = 3,
            FindingsCreated = 5,
            NistControlsAffected = 4,
            RegisteredSystemId = "sys-1"
        };

        var findings = new List<ScanImportFinding>
        {
            new()
            {
                Id = "f1",
                ScanImportRecordId = "imp-1",
                VulnId = "V-254239",
                RuleId = "SV-254239r1_rule",
                RawStatus = "Open",
                MappedSeverity = CatSeverity.CatI,
                ImportAction = ImportFindingAction.Created,
                ResolvedStigControlId = "stig-1",
                ResolvedNistControlIds = new List<string> { "AC-2" }
            }
        };

        _importServiceMock
            .Setup(s => s.GetImportSummaryAsync("imp-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((record, findings));

        var args = new Dictionary<string, object?> { ["import_id"] = "imp-1" };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("id").GetString().Should().Be("imp-1");
        data.GetProperty("target_host").GetString().Should().Be("WEBSERVER01");
        data.GetProperty("findings").GetArrayLength().Should().Be(1);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ImportXccdfTool Tests
// ─────────────────────────────────────────────────────────────────────────────

public class ImportXccdfToolTests
{
    private readonly Mock<IScanImportService> _importServiceMock = new();
    private readonly ImportXccdfTool _tool;

    public ImportXccdfToolTests()
    {
        _tool = new ImportXccdfTool(
            _importServiceMock.Object,
            NullLogger<ImportXccdfTool>.Instance);
    }

    [Fact]
    public void Name_ReturnsCorrectToolName()
    {
        _tool.Name.Should().Be("compliance_import_xccdf");
    }

    [Fact]
    public async Task ImportXccdf_MissingSystemId_ReturnsError()
    {
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "",
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("<xml/>")),
            ["file_name"] = "results.xml"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task ImportXccdf_InvalidBase64_ReturnsError()
    {
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_content"] = "not-base64!!!",
            ["file_name"] = "results.xml"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_BASE64");
    }

    [Fact]
    public async Task ImportXccdf_FileTooLarge_ReturnsError()
    {
        var largeContent = new byte[6 * 1024 * 1024];
        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_content"] = Convert.ToBase64String(largeContent),
            ["file_name"] = "results.xml"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("FILE_TOO_LARGE");
    }
}
