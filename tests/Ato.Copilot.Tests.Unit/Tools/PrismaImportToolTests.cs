// ═══════════════════════════════════════════════════════════════════════════
// Feature 019 — Prisma Cloud Scan Import: MCP Tool Unit Tests
// TDD: Tests written FIRST (red), implementation makes them green.
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

// ─────────────────────────────────────────────────────────────────────────────
// ImportPrismaCsvTool Tests
// ─────────────────────────────────────────────────────────────────────────────

public class ImportPrismaCsvToolTests : IDisposable
{
    private readonly Mock<IScanImportService> _importServiceMock = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly ImportPrismaCsvTool _tool;

    public ImportPrismaCsvToolTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AtoCopilotContext>(o =>
            o.UseInMemoryDatabase($"ImportPrismaCsvToolTests-{Guid.NewGuid()}"));
        _serviceProvider = services.BuildServiceProvider();

        _tool = new ImportPrismaCsvTool(
            _importServiceMock.Object,
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ImportPrismaCsvTool>.Instance);
    }

    public void Dispose() => _serviceProvider.Dispose();

    [Fact]
    public void Name_ReturnsCorrectToolName()
    {
        _tool.Name.Should().Be("compliance_import_prisma_csv");
    }

    [Fact]
    public void Parameters_ContainAllRequiredAndOptionalKeys()
    {
        _tool.Parameters.Should().ContainKey("file_content");
        _tool.Parameters.Should().ContainKey("file_name");
        _tool.Parameters.Should().ContainKey("system_id");
        _tool.Parameters.Should().ContainKey("conflict_resolution");
        _tool.Parameters.Should().ContainKey("dry_run");
        _tool.Parameters.Should().ContainKey("assessment_id");

        // system_id is optional for Prisma (auto-resolution from subscription)
        _tool.Parameters["system_id"].Required.Should().BeFalse();
        _tool.Parameters["file_content"].Required.Should().BeTrue();
        _tool.Parameters["file_name"].Required.Should().BeTrue();
        _tool.Parameters["conflict_resolution"].Required.Should().BeFalse();
    }

    [Fact]
    public async Task ImportPrismaCsv_MissingFileContent_ReturnsError()
    {
        var args = new Dictionary<string, object?>
        {
            ["file_content"] = "",
            ["file_name"] = "test.csv"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task ImportPrismaCsv_MissingFileName_ReturnsError()
    {
        var args = new Dictionary<string, object?>
        {
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("csv data")),
            ["file_name"] = ""
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task ImportPrismaCsv_InvalidBase64_ReturnsError()
    {
        var args = new Dictionary<string, object?>
        {
            ["file_content"] = "not-valid-base64!!!",
            ["file_name"] = "test.csv"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_BASE64");
    }

    [Fact]
    public async Task ImportPrismaCsv_FileTooLarge_ReturnsError()
    {
        var largeContent = new byte[26 * 1024 * 1024]; // 26 MB
        var args = new Dictionary<string, object?>
        {
            ["file_content"] = Convert.ToBase64String(largeContent),
            ["file_name"] = "huge.csv"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("FILE_TOO_LARGE");
    }

    // ─── Helper to create standard PrismaImportResult ────────────────────────

    private static PrismaImportResult MakeSuccessResult(
        string systemId = "sys-1", string systemName = "Test System",
        string importRecordId = "imp-prisma-1", int findingsCreated = 5,
        int findingsUpdated = 0, int skippedCount = 0, int unmappedPolicies = 0,
        int effectivenessCreated = 3, int nistControlsAffected = 3,
        bool evidenceCreated = true, bool isDryRun = false,
        int totalProcessed = 5, int totalSkipped = 0)
    {
        var sysResult = new PrismaSystemImportResult(
            ImportRecordId: importRecordId,
            SystemId: systemId,
            SystemName: systemName,
            Status: ScanImportStatus.Completed,
            TotalAlerts: totalProcessed,
            OpenCount: findingsCreated,
            ResolvedCount: 0,
            DismissedCount: 0,
            SnoozedCount: 0,
            FindingsCreated: findingsCreated,
            FindingsUpdated: findingsUpdated,
            SkippedCount: skippedCount,
            UnmappedPolicies: unmappedPolicies,
            EffectivenessRecordsCreated: effectivenessCreated,
            EffectivenessRecordsUpdated: 0,
            NistControlsAffected: nistControlsAffected,
            EvidenceCreated: evidenceCreated,
            FileHash: "sha256:abc123",
            IsDryRun: isDryRun,
            Warnings: new List<string>());

        return new PrismaImportResult(
            Imports: new List<PrismaSystemImportResult> { sysResult },
            UnresolvedSubscriptions: new List<UnresolvedSubscriptionInfo>(),
            SkippedNonAzure: null,
            TotalProcessed: totalProcessed,
            TotalSkipped: totalSkipped,
            DurationMs: 150);
    }

    private static PrismaImportResult MakeErrorResult(string errorMessage)
    {
        return new PrismaImportResult(
            Imports: new List<PrismaSystemImportResult>(),
            UnresolvedSubscriptions: new List<UnresolvedSubscriptionInfo>(),
            SkippedNonAzure: null,
            TotalProcessed: 0,
            TotalSkipped: 0,
            DurationMs: 0,
            ErrorMessage: errorMessage);
    }

    [Fact]
    public async Task ImportPrismaCsv_ValidInput_CallsServiceAndReturnsSuccess()
    {
        var csvContent = "Alert ID,Status,Policy Name\nP-100,open,Test";

        _importServiceMock
            .Setup(s => s.ImportPrismaCsvAsync(
                null, null, It.IsAny<byte[]>(), "export.csv",
                ImportConflictResolution.Skip, false, "mcp-user",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSuccessResult());

        var args = new Dictionary<string, object?>
        {
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(csvContent)),
            ["file_name"] = "export.csv"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.TryGetProperty("data", out var data).Should().BeTrue();
        data.TryGetProperty("total_processed", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ImportPrismaCsv_WithExplicitSystemId_PassesToService()
    {
        var csvContent = "Alert ID,Status,Policy Name\nP-100,open,Test";

        _importServiceMock
            .Setup(s => s.ImportPrismaCsvAsync(
                "sys-explicit", null, It.IsAny<byte[]>(), "export.csv",
                ImportConflictResolution.Skip, false, "mcp-user",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSuccessResult(systemId: "sys-explicit", systemName: "Explicit System",
                importRecordId: "imp-prisma-2", findingsCreated: 1, totalProcessed: 1));

        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-explicit",
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(csvContent)),
            ["file_name"] = "export.csv"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        _importServiceMock.Verify(
            s => s.ImportPrismaCsvAsync(
                "sys-explicit", null, It.IsAny<byte[]>(), "export.csv",
                It.IsAny<ImportConflictResolution>(), false, "mcp-user",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ImportPrismaCsv_DryRunTrue_PassesDryRunToService()
    {
        var csvContent = "Alert ID,Status,Policy Name\nP-100,open,Test";

        _importServiceMock
            .Setup(s => s.ImportPrismaCsvAsync(
                null, null, It.IsAny<byte[]>(), "test.csv",
                ImportConflictResolution.Skip, true, "mcp-user",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSuccessResult(isDryRun: true, findingsCreated: 1, totalProcessed: 1));

        var args = new Dictionary<string, object?>
        {
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(csvContent)),
            ["file_name"] = "test.csv",
            ["dry_run"] = true
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task ImportPrismaCsv_ServiceError_ReturnsErrorResponse()
    {
        _importServiceMock
            .Setup(s => s.ImportPrismaCsvAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<byte[]>(),
                It.IsAny<string>(), It.IsAny<ImportConflictResolution>(),
                It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeErrorResult("Invalid CSV: missing required headers"));

        var args = new Dictionary<string, object?>
        {
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("bad csv data")),
            ["file_name"] = "bad.csv"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
    }

    [Fact]
    public async Task ImportPrismaCsv_ConflictResolutionOverwrite_PassesToService()
    {
        var csvContent = "Alert ID,Status,Policy Name\nP-100,open,Test";

        _importServiceMock
            .Setup(s => s.ImportPrismaCsvAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<byte[]>(),
                It.IsAny<string>(), ImportConflictResolution.Overwrite,
                It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSuccessResult(findingsCreated: 0, findingsUpdated: 3, totalProcessed: 3));

        var args = new Dictionary<string, object?>
        {
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(csvContent)),
            ["file_name"] = "test.csv",
            ["conflict_resolution"] = "Overwrite"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ListPrismaPoliciesTool Tests (US3 — T038)
// ─────────────────────────────────────────────────────────────────────────────

public class ListPrismaPoliciesToolTests
{
    private readonly Mock<IScanImportService> _importServiceMock = new();
    private readonly ListPrismaPoliciesTool _tool;

    public ListPrismaPoliciesToolTests()
    {
        _tool = new ListPrismaPoliciesTool(
            _importServiceMock.Object,
            NullLogger<ListPrismaPoliciesTool>.Instance);
    }

    [Fact]
    public void Name_ReturnsCorrectToolName()
    {
        _tool.Name.Should().Be("compliance_list_prisma_policies");
    }

    [Fact]
    public async Task MissingSystemId_ReturnsError()
    {
        var args = new Dictionary<string, object?>();
        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task ValidSystemId_ReturnsPolicies()
    {
        var policies = new List<PrismaPolicyEntry>
        {
            new("Storage Encryption", "config", "high",
                new List<string> { "SC-28" }, 3, 1, 0,
                new List<string> { "Microsoft.Storage/storageAccounts" },
                "imp-1", DateTime.UtcNow)
        };

        _importServiceMock
            .Setup(s => s.ListPrismaPoliciesAsync("sys-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrismaPolicyListResult("sys-1", 1, policies));

        var args = new Dictionary<string, object?> { ["system_id"] = "sys-1" };
        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("total_policies").GetInt32().Should().Be(1);
        data.GetProperty("policies").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task SystemNotFound_ReturnsNotFoundError()
    {
        _importServiceMock
            .Setup(s => s.ListPrismaPoliciesAsync("bad-sys", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("System 'bad-sys' not found."));

        var args = new Dictionary<string, object?> { ["system_id"] = "bad-sys" };
        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task NoPolicies_ReturnsEmptyArray()
    {
        _importServiceMock
            .Setup(s => s.ListPrismaPoliciesAsync("sys-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrismaPolicyListResult("sys-1", 0, new List<PrismaPolicyEntry>()));

        var args = new Dictionary<string, object?> { ["system_id"] = "sys-1" };
        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("total_policies").GetInt32().Should().Be(0);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// PrismaTrendTool Tests (US3 — T038)
// ─────────────────────────────────────────────────────────────────────────────

public class PrismaTrendToolTests
{
    private readonly Mock<IScanImportService> _importServiceMock = new();
    private readonly PrismaTrendTool _tool;

    public PrismaTrendToolTests()
    {
        _tool = new PrismaTrendTool(
            _importServiceMock.Object,
            NullLogger<PrismaTrendTool>.Instance);
    }

    [Fact]
    public void Name_ReturnsCorrectToolName()
    {
        _tool.Name.Should().Be("compliance_prisma_trend");
    }

    [Fact]
    public async Task MissingSystemId_ReturnsError()
    {
        var args = new Dictionary<string, object?>();
        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task ValidTrend_ReturnsComparisonData()
    {
        var trendResult = new PrismaTrendResult(
            SystemId: "sys-1",
            Imports: new List<PrismaTrendImport>
            {
                new("imp-1", DateTime.UtcNow.AddDays(-30), "feb.csv", 55, 40, 10, 5),
                new("imp-2", DateTime.UtcNow, "mar.csv", 47, 32, 12, 3)
            },
            NewFindings: 8,
            ResolvedFindings: 16,
            PersistentFindings: 31,
            RemediationRate: 34.04m,
            ResourceTypeBreakdown: null,
            NistControlBreakdown: null);

        _importServiceMock
            .Setup(s => s.GetPrismaTrendAsync("sys-1", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trendResult);

        var args = new Dictionary<string, object?> { ["system_id"] = "sys-1" };
        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("new_findings").GetInt32().Should().Be(8);
        data.GetProperty("resolved_findings").GetInt32().Should().Be(16);
        data.GetProperty("persistent_findings").GetInt32().Should().Be(31);
        data.GetProperty("remediation_rate").GetDecimal().Should().Be(34.04m);
        data.GetProperty("imports").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task NoPrismaImports_ReturnsNotFoundError()
    {
        _importServiceMock
            .Setup(s => s.GetPrismaTrendAsync("sys-1", null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No Prisma imports found for system 'sys-1'."));

        var args = new Dictionary<string, object?> { ["system_id"] = "sys-1" };
        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task WithGroupBy_PassesToService()
    {
        var trendResult = new PrismaTrendResult(
            SystemId: "sys-1",
            Imports: new List<PrismaTrendImport>
            {
                new("imp-1", DateTime.UtcNow, "test.csv", 10, 5, 3, 2)
            },
            NewFindings: 10,
            ResolvedFindings: 0,
            PersistentFindings: 0,
            RemediationRate: 0m,
            ResourceTypeBreakdown: new Dictionary<string, int> { ["Microsoft.Storage/storageAccounts"] = 5 },
            NistControlBreakdown: null);

        _importServiceMock
            .Setup(s => s.GetPrismaTrendAsync("sys-1", null, "resource_type", It.IsAny<CancellationToken>()))
            .ReturnsAsync(trendResult);

        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["group_by"] = "resource_type"
        };
        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("resource_type_breakdown").Should().NotBeNull();
    }
}


public class ImportPrismaApiToolTests : IDisposable
{
    private readonly Mock<IScanImportService> _importServiceMock = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly ImportPrismaApiTool _tool;

    public ImportPrismaApiToolTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AtoCopilotContext>(o =>
            o.UseInMemoryDatabase($"ImportPrismaApiToolTests-{Guid.NewGuid()}"));
        _serviceProvider = services.BuildServiceProvider();

        _tool = new ImportPrismaApiTool(
            _importServiceMock.Object,
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ImportPrismaApiTool>.Instance);
    }

    public void Dispose() => _serviceProvider.Dispose();

    [Fact]
    public void Name_ReturnsCorrectToolName()
    {
        _tool.Name.Should().Be("compliance_import_prisma_api");
    }

    [Fact]
    public void Parameters_ContainsAllExpectedKeys()
    {
        _tool.Parameters.Keys.Should().Contain("file_content");
        _tool.Parameters.Keys.Should().Contain("file_name");
        _tool.Parameters.Keys.Should().Contain("system_id");
        _tool.Parameters.Keys.Should().Contain("conflict_resolution");
        _tool.Parameters.Keys.Should().Contain("dry_run");
        _tool.Parameters.Keys.Should().Contain("assessment_id");
    }

    [Fact]
    public async Task MissingFileContent_ReturnsInvalidInputError()
    {
        var args = new Dictionary<string, object?>
        {
            ["file_name"] = "test.json"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task MissingFileName_ReturnsInvalidInputError()
    {
        var args = new Dictionary<string, object?>
        {
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("[{}]"))
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task InvalidBase64_ReturnsBase64Error()
    {
        var args = new Dictionary<string, object?>
        {
            ["file_content"] = "!!!not-base64!!!",
            ["file_name"] = "test.json"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_BASE64");
    }

    [Fact]
    public async Task FileTooLarge_ReturnsFileTooLargeError()
    {
        var largeContent = new byte[26 * 1024 * 1024]; // 26 MB
        var args = new Dictionary<string, object?>
        {
            ["file_content"] = Convert.ToBase64String(largeContent),
            ["file_name"] = "huge.json"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("FILE_TOO_LARGE");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static PrismaImportResult MakeApiSuccessResult(
        string systemId = "sys-1", string systemName = "Test System",
        string importRecordId = "imp-api-1", int findingsCreated = 5,
        int findingsUpdated = 0, int totalProcessed = 5,
        bool isDryRun = false, int remediableCount = 2,
        int cliScriptsExtracted = 1, int alertsWithHistory = 5,
        List<string>? policyLabels = null)
    {
        var sysResult = new PrismaSystemImportResult(
            ImportRecordId: importRecordId,
            SystemId: systemId,
            SystemName: systemName,
            Status: ScanImportStatus.Completed,
            TotalAlerts: totalProcessed,
            OpenCount: findingsCreated,
            ResolvedCount: 0,
            DismissedCount: 0,
            SnoozedCount: 0,
            FindingsCreated: findingsCreated,
            FindingsUpdated: findingsUpdated,
            SkippedCount: 0,
            UnmappedPolicies: 0,
            EffectivenessRecordsCreated: 3,
            EffectivenessRecordsUpdated: 0,
            NistControlsAffected: 3,
            EvidenceCreated: true,
            FileHash: "sha256:def456",
            IsDryRun: isDryRun,
            Warnings: new List<string>(),
            RemediableCount: remediableCount,
            CliScriptsExtracted: cliScriptsExtracted,
            PolicyLabelsFound: policyLabels ?? new List<string> { "CSPM", "Azure", "Storage" },
            AlertsWithHistory: alertsWithHistory);

        return new PrismaImportResult(
            Imports: new List<PrismaSystemImportResult> { sysResult },
            UnresolvedSubscriptions: new List<UnresolvedSubscriptionInfo>(),
            SkippedNonAzure: null,
            TotalProcessed: totalProcessed,
            TotalSkipped: 0,
            DurationMs: 200);
    }

    private static PrismaImportResult MakeApiErrorResult(string errorMessage)
    {
        return new PrismaImportResult(
            Imports: new List<PrismaSystemImportResult>(),
            UnresolvedSubscriptions: new List<UnresolvedSubscriptionInfo>(),
            SkippedNonAzure: null,
            TotalProcessed: 0,
            TotalSkipped: 0,
            DurationMs: 0,
            ErrorMessage: errorMessage);
    }

    [Fact]
    public async Task ValidInput_CallsServiceAndReturnsSuccess()
    {
        var jsonContent = "[{\"id\":\"P-100\",\"status\":\"open\",\"policy\":{\"name\":\"Test\"}}]";

        _importServiceMock
            .Setup(s => s.ImportPrismaApiAsync(
                null, null, It.IsAny<byte[]>(), "alerts.json",
                ImportConflictResolution.Skip, false, "mcp-user",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeApiSuccessResult());

        var args = new Dictionary<string, object?>
        {
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonContent)),
            ["file_name"] = "alerts.json"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.TryGetProperty("data", out var data).Should().BeTrue();
        data.TryGetProperty("total_processed", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ValidInput_ReturnsEnhancedFields()
    {
        var jsonContent = "[{\"id\":\"P-100\",\"status\":\"open\",\"policy\":{\"name\":\"Test\"}}]";

        _importServiceMock
            .Setup(s => s.ImportPrismaApiAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<byte[]>(),
                It.IsAny<string>(), It.IsAny<ImportConflictResolution>(),
                It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeApiSuccessResult(
                remediableCount: 3, cliScriptsExtracted: 2,
                alertsWithHistory: 4,
                policyLabels: new List<string> { "CSPM", "Azure" }));

        var args = new Dictionary<string, object?>
        {
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonContent)),
            ["file_name"] = "alerts.json"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        var imports = json.RootElement.GetProperty("data").GetProperty("imports");
        var enhanced = imports[0].GetProperty("enhanced");
        enhanced.GetProperty("remediable_count").GetInt32().Should().Be(3);
        enhanced.GetProperty("cli_scripts_extracted").GetInt32().Should().Be(2);
        enhanced.GetProperty("alerts_with_history").GetInt32().Should().Be(4);

        var labels = enhanced.GetProperty("policy_labels_found");
        labels.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task WithExplicitSystemId_PassesToService()
    {
        var jsonContent = "[{\"id\":\"P-100\",\"status\":\"open\",\"policy\":{\"name\":\"Test\"}}]";

        _importServiceMock
            .Setup(s => s.ImportPrismaApiAsync(
                "sys-explicit", null, It.IsAny<byte[]>(), "alerts.json",
                ImportConflictResolution.Skip, false, "mcp-user",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeApiSuccessResult(systemId: "sys-explicit"));

        var args = new Dictionary<string, object?>
        {
            ["system_id"] = "sys-explicit",
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonContent)),
            ["file_name"] = "alerts.json"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");

        _importServiceMock.Verify(
            s => s.ImportPrismaApiAsync(
                "sys-explicit", null, It.IsAny<byte[]>(), "alerts.json",
                It.IsAny<ImportConflictResolution>(), false, "mcp-user",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DryRunTrue_PassesDryRunToService()
    {
        var jsonContent = "[{\"id\":\"P-100\",\"status\":\"open\",\"policy\":{\"name\":\"Test\"}}]";

        _importServiceMock
            .Setup(s => s.ImportPrismaApiAsync(
                null, null, It.IsAny<byte[]>(), "alerts.json",
                ImportConflictResolution.Skip, true, "mcp-user",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeApiSuccessResult(isDryRun: true));

        var args = new Dictionary<string, object?>
        {
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonContent)),
            ["file_name"] = "alerts.json",
            ["dry_run"] = true
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task ServiceError_ReturnsErrorResponse()
    {
        _importServiceMock
            .Setup(s => s.ImportPrismaApiAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<byte[]>(),
                It.IsAny<string>(), It.IsAny<ImportConflictResolution>(),
                It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeApiErrorResult("JSON parse error: invalid structure"));

        var args = new Dictionary<string, object?>
        {
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("bad")),
            ["file_name"] = "bad.json"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("error");
    }

    [Fact]
    public async Task ConflictResolutionOverwrite_PassesToService()
    {
        var jsonContent = "[{\"id\":\"P-100\",\"status\":\"open\",\"policy\":{\"name\":\"Test\"}}]";

        _importServiceMock
            .Setup(s => s.ImportPrismaApiAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<byte[]>(),
                It.IsAny<string>(), ImportConflictResolution.Overwrite,
                It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeApiSuccessResult(findingsCreated: 0, findingsUpdated: 3, totalProcessed: 3));

        var args = new Dictionary<string, object?>
        {
            ["file_content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonContent)),
            ["file_name"] = "alerts.json",
            ["conflict_resolution"] = "Overwrite"
        };

        var result = await _tool.ExecuteAsync(args);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("status").GetString().Should().Be("success");
    }
}
