using System.Text.Json;
using Moq;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Tools;

/// <summary>
/// Unit tests for Feature 025 — Inventory Tools.
/// T027: Verifies parameter parsing, service delegation, response envelope format,
///       and error propagation for all 9 MCP tools.
/// </summary>
public class InventoryToolTests
{
    private readonly Mock<IInventoryService> _svcMock = new();

    private static readonly InventoryItem SampleHwItem = new()
    {
        Id = "hw-1",
        RegisteredSystemId = "sys-1",
        ItemName = "web-server-01",
        Type = InventoryItemType.Hardware,
        HardwareFunction = HardwareFunction.Server,
        Manufacturer = "Dell",
        IpAddress = "10.0.0.1",
        Status = InventoryItemStatus.Active,
        CreatedBy = "tester"
    };

    private static readonly InventoryItem SampleSwItem = new()
    {
        Id = "sw-1",
        RegisteredSystemId = "sys-1",
        ItemName = "Apache HTTP Server",
        Type = InventoryItemType.Software,
        Vendor = "Apache",
        Version = "2.4.57",
        Status = InventoryItemStatus.Active,
        CreatedBy = "tester"
    };

    // =========================================================================
    // inventory_add_item
    // =========================================================================

    [Fact]
    public async Task AddItem_ReturnsSuccess()
    {
        _svcMock
            .Setup(s => s.AddItemAsync(It.IsAny<string>(), It.IsAny<InventoryItemInput>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleHwItem);

        var tool = new InventoryAddItemTool(_svcMock.Object, Mock.Of<ILogger<InventoryAddItemTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["item_name"] = "web-server-01",
            ["type"] = "hardware",
            ["function"] = "Server",
            ["manufacturer"] = "Dell",
            ["ip_address"] = "10.0.0.1"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("id").GetString().Should().Be("hw-1");
    }

    [Fact]
    public async Task AddItem_MissingSystemId_ReturnsError()
    {
        var tool = new InventoryAddItemTool(_svcMock.Object, Mock.Of<ILogger<InventoryAddItemTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["item_name"] = "test"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task AddItem_ServiceError_ReturnsErrorCode()
    {
        _svcMock
            .Setup(s => s.AddItemAsync(It.IsAny<string>(), It.IsAny<InventoryItemInput>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DUPLICATE_IP: IP already in use"));

        var tool = new InventoryAddItemTool(_svcMock.Object, Mock.Of<ILogger<InventoryAddItemTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["item_name"] = "s1",
            ["type"] = "hardware",
            ["function"] = "Server",
            ["manufacturer"] = "Dell",
            ["ip_address"] = "10.0.0.1"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("DUPLICATE_IP");
    }

    // =========================================================================
    // inventory_update_item
    // =========================================================================

    [Fact]
    public async Task UpdateItem_ReturnsSuccess()
    {
        _svcMock
            .Setup(s => s.UpdateItemAsync(It.IsAny<string>(), It.IsAny<InventoryItemInput>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleHwItem);

        var tool = new InventoryUpdateItemTool(_svcMock.Object, Mock.Of<ILogger<InventoryUpdateItemTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["item_id"] = "hw-1",
            ["location"] = "Rack A"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task UpdateItem_MissingItemId_ReturnsError()
    {
        var tool = new InventoryUpdateItemTool(_svcMock.Object, Mock.Of<ILogger<InventoryUpdateItemTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["location"] = "Rack A"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
    }

    // =========================================================================
    // inventory_decommission_item
    // =========================================================================

    [Fact]
    public async Task DecommissionItem_ReturnsSuccess()
    {
        _svcMock
            .Setup(s => s.DecommissionItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InventoryItem
            {
                Id = "hw-1", RegisteredSystemId = "sys-1", ItemName = "web-server-01",
                Type = InventoryItemType.Hardware, Status = InventoryItemStatus.Decommissioned, CreatedBy = "tester"
            });

        var tool = new InventoryDecommissionItemTool(_svcMock.Object, Mock.Of<ILogger<InventoryDecommissionItemTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["item_id"] = "hw-1",
            ["rationale"] = "End of life"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    // =========================================================================
    // inventory_list
    // =========================================================================

    [Fact]
    public async Task ListItems_ReturnsSuccess()
    {
        _svcMock
            .Setup(s => s.ListItemsAsync(It.IsAny<string>(), It.IsAny<InventoryListOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InventoryItem> { SampleHwItem, SampleSwItem });

        var tool = new InventoryListTool(_svcMock.Object, Mock.Of<ILogger<InventoryListTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("count").GetInt32().Should().Be(2);
    }

    // =========================================================================
    // inventory_get
    // =========================================================================

    [Fact]
    public async Task GetItem_ReturnsSuccess()
    {
        _svcMock
            .Setup(s => s.GetItemAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleHwItem);

        var tool = new InventoryGetTool(_svcMock.Object, Mock.Of<ILogger<InventoryGetTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["item_id"] = "hw-1"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task GetItem_NotFound_ReturnsError()
    {
        _svcMock
            .Setup(s => s.GetItemAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryItem?)null);

        var tool = new InventoryGetTool(_svcMock.Object, Mock.Of<ILogger<InventoryGetTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["item_id"] = "nonexistent"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("ITEM_NOT_FOUND");
    }

    // =========================================================================
    // inventory_export
    // =========================================================================

    [Fact]
    public async Task ExportItem_ReturnsBase64()
    {
        _svcMock
            .Setup(s => s.ExportToExcelAsync(It.IsAny<string>(), It.IsAny<InventoryExportOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0x50, 0x4B, 0x03, 0x04 });

        var tool = new InventoryExportTool(_svcMock.Object, Mock.Of<ILogger<InventoryExportTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("file_base64").GetString().Should().NotBeEmpty();
    }

    // =========================================================================
    // inventory_import
    // =========================================================================

    [Fact]
    public async Task ImportItem_ReturnsSuccess()
    {
        _svcMock
            .Setup(s => s.ImportFromExcelAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InventoryImportResult { HardwareCreated = 2, SoftwareCreated = 3, RowsSkipped = 0 });

        var tool = new InventoryImportTool(_svcMock.Object, Mock.Of<ILogger<InventoryImportTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_base64"] = Convert.ToBase64String(new byte[] { 1, 2, 3 })
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("hardware_created").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task ImportItem_InvalidBase64_ReturnsError()
    {
        var tool = new InventoryImportTool(_svcMock.Object, Mock.Of<ILogger<InventoryImportTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_base64"] = "not-valid-base64!!!"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_BASE64");
    }

    // =========================================================================
    // inventory_completeness
    // =========================================================================

    [Fact]
    public async Task Completeness_ReturnsSuccess()
    {
        _svcMock
            .Setup(s => s.CheckCompletenessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InventoryCompleteness
            {
                SystemId = "sys-1",
                TotalItems = 5,
                HardwareCount = 3,
                SoftwareCount = 2,
                CompletenessScore = 80.0,
                IsComplete = false,
                HardwareWithoutSoftware = new List<string> { "hw-1" }
            });

        var tool = new InventoryCompletenessTool(_svcMock.Object, Mock.Of<ILogger<InventoryCompletenessTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("completeness_score").GetDouble().Should().Be(80.0);
    }

    // =========================================================================
    // inventory_auto_seed
    // =========================================================================

    [Fact]
    public async Task AutoSeed_ReturnsSuccess()
    {
        _svcMock
            .Setup(s => s.AutoSeedFromBoundaryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InventoryItem> { SampleHwItem });

        var tool = new InventoryAutoSeedTool(_svcMock.Object, Mock.Of<ILogger<InventoryAutoSeedTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("created_count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task AutoSeed_MissingSystemId_ReturnsError()
    {
        var tool = new InventoryAutoSeedTool(_svcMock.Object, Mock.Of<ILogger<InventoryAutoSeedTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?> { });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
    }
}
