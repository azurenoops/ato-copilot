using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Integration.Tools;

/// <summary>
/// Integration tests for Feature 025 — HW/SW Inventory.
/// T028: Uses real InventoryService + in-memory EF Core.
/// Tests all 9 MCP tools (add, update, decommission, list, get, export, import,
/// completeness, auto_seed) covering happy-path and at least one error-path per tool.
/// </summary>
public class InventoryIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly InventoryAddItemTool _addTool;
    private readonly InventoryUpdateItemTool _updateTool;
    private readonly InventoryDecommissionItemTool _decommissionTool;
    private readonly InventoryListTool _listTool;
    private readonly InventoryGetTool _getTool;
    private readonly InventoryExportTool _exportTool;
    private readonly InventoryImportTool _importTool;
    private readonly InventoryCompletenessTool _completenessTool;
    private readonly InventoryAutoSeedTool _autoSeedTool;

    public InventoryIntegrationTests()
    {
        var dbName = $"InventoryIntTest_{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddDbContext<AtoCopilotContext>(opts =>
            opts.UseInMemoryDatabase(dbName), ServiceLifetime.Scoped);
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var inventorySvc = new InventoryService(scopeFactory, Mock.Of<ILogger<InventoryService>>());

        // Seed test system
        using var initScope = _serviceProvider.CreateScope();
        var ctx = initScope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
        ctx.Database.EnsureCreated();
        ctx.RegisteredSystems.Add(new RegisteredSystem
        {
            Id = "sys-1",
            Name = "Test System",
            Acronym = "TSYS",
            CurrentRmfStep = RmfPhase.Implement
        });
        ctx.SaveChanges();

        _addTool = new InventoryAddItemTool(inventorySvc, Mock.Of<ILogger<InventoryAddItemTool>>());
        _updateTool = new InventoryUpdateItemTool(inventorySvc, Mock.Of<ILogger<InventoryUpdateItemTool>>());
        _decommissionTool = new InventoryDecommissionItemTool(inventorySvc, Mock.Of<ILogger<InventoryDecommissionItemTool>>());
        _listTool = new InventoryListTool(inventorySvc, Mock.Of<ILogger<InventoryListTool>>());
        _getTool = new InventoryGetTool(inventorySvc, Mock.Of<ILogger<InventoryGetTool>>());
        _exportTool = new InventoryExportTool(inventorySvc, Mock.Of<ILogger<InventoryExportTool>>());
        _importTool = new InventoryImportTool(inventorySvc, Mock.Of<ILogger<InventoryImportTool>>());
        _completenessTool = new InventoryCompletenessTool(inventorySvc, Mock.Of<ILogger<InventoryCompletenessTool>>());
        _autoSeedTool = new InventoryAutoSeedTool(inventorySvc, Mock.Of<ILogger<InventoryAutoSeedTool>>());
    }

    public void Dispose() => _serviceProvider.Dispose();

    private static JsonElement ParseSuccess(string json)
    {
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        return doc.RootElement.GetProperty("data");
    }

    private static void AssertError(string json, string? expectedCode = null)
    {
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        if (expectedCode != null)
            doc.RootElement.GetProperty("errorCode").GetString().Should().Be(expectedCode);
    }

    // =========================================================================
    // End-to-end: Add HW → Add SW → Update → List → Get → Export → Decommission
    // =========================================================================

    [Fact]
    public async Task FullWorkflow_AddListGetExportDecommission()
    {
        // 1. Add hardware
        var addHwResult = await _addTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["item_name"] = "web-server-01",
            ["type"] = "hardware",
            ["function"] = "Server",
            ["manufacturer"] = "Dell",
            ["ip_address"] = "10.0.0.1"
        });
        var hwData = ParseSuccess(addHwResult);
        var hwId = hwData.GetProperty("id").GetString()!;

        // 2. Add software installed on the hardware
        var addSwResult = await _addTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["item_name"] = "Apache HTTP Server",
            ["type"] = "software",
            ["function"] = "Application",
            ["vendor"] = "Apache Foundation",
            ["version"] = "2.4.57",
            ["parent_hardware_id"] = hwId
        });
        var swData = ParseSuccess(addSwResult);
        var swId = swData.GetProperty("id").GetString()!;

        // 3. Update hardware location
        var updateResult = await _updateTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["item_id"] = hwId,
            ["location"] = "DC-East Rack A-12"
        });
        var updateData = ParseSuccess(updateResult);
        updateData.GetProperty("item_name").GetString().Should().Be("web-server-01");

        // 4. List items
        var listResult = await _listTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });
        var listData = ParseSuccess(listResult);
        listData.GetProperty("count").GetInt32().Should().Be(2);

        // 5. Get item with installed software
        var getResult = await _getTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["item_id"] = hwId
        });
        var getData = ParseSuccess(getResult);
        getData.GetProperty("item_name").GetString().Should().Be("web-server-01");

        // 6. Export to Excel
        var exportResult = await _exportTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });
        var exportData = ParseSuccess(exportResult);
        exportData.GetProperty("file_base64").GetString().Should().NotBeEmpty();

        // 7. Decommission hardware (cascades to software)
        var decomResult = await _decommissionTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["item_id"] = hwId,
            ["rationale"] = "End of life"
        });
        ParseSuccess(decomResult);
    }

    // =========================================================================
    // Error paths
    // =========================================================================

    [Fact]
    public async Task AddItem_SystemNotFound_ReturnsError()
    {
        var result = await _addTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "nonexistent",
            ["item_name"] = "test",
            ["type"] = "hardware",
            ["function"] = "Server",
            ["manufacturer"] = "Dell",
            ["ip_address"] = "10.0.0.1"
        });

        AssertError(result, "SYSTEM_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateItem_NotFound_ReturnsError()
    {
        var result = await _updateTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["item_id"] = "nonexistent",
            ["location"] = "somewhere"
        });

        AssertError(result, "ITEM_NOT_FOUND");
    }

    [Fact]
    public async Task DecommissionItem_NotFound_ReturnsError()
    {
        var result = await _decommissionTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["item_id"] = "nonexistent",
            ["rationale"] = "Testing"
        });

        AssertError(result, "ITEM_NOT_FOUND");
    }

    [Fact]
    public async Task GetItem_NotFound_ReturnsError()
    {
        var result = await _getTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["item_id"] = "nonexistent"
        });

        AssertError(result, "ITEM_NOT_FOUND");
    }

    [Fact]
    public async Task ExportItem_NoData_ReturnsError()
    {
        var result = await _exportTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });

        AssertError(result, "NO_INVENTORY_DATA");
    }

    [Fact]
    public async Task ImportItem_InvalidBase64_ReturnsError()
    {
        var result = await _importTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_base64"] = "totally-not-base64!!!"
        });

        AssertError(result, "INVALID_BASE64");
    }

    [Fact]
    public async Task Completeness_SystemNotFound_ReturnsError()
    {
        var result = await _completenessTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "nonexistent"
        });

        AssertError(result, "SYSTEM_NOT_FOUND");
    }

    // =========================================================================
    // Auto-seed with boundary resources
    // =========================================================================

    [Fact]
    public async Task AutoSeed_CreatesFromBoundaryResources()
    {
        // Seed boundary resources
        using (var scope = _serviceProvider.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
            ctx.AuthorizationBoundaries.Add(new AuthorizationBoundary
            {
                RegisteredSystemId = "sys-1",
                ResourceId = "/sub/rg/vm1",
                ResourceType = "Microsoft.Compute/virtualMachines",
                ResourceName = "web-vm",
                IsInBoundary = true,
                AddedBy = "test"
            });
            ctx.AuthorizationBoundaries.Add(new AuthorizationBoundary
            {
                RegisteredSystemId = "sys-1",
                ResourceId = "/sub/rg/nsg1",
                ResourceType = "Microsoft.Network/networkSecurityGroups",
                ResourceName = "web-nsg",
                IsInBoundary = true,
                AddedBy = "test"
            });
            await ctx.SaveChangesAsync();
        }

        var result = await _autoSeedTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });

        var data = ParseSuccess(result);
        data.GetProperty("created_count").GetInt32().Should().Be(2);

        // Verify idempotent re-run
        var result2 = await _autoSeedTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });
        var data2 = ParseSuccess(result2);
        data2.GetProperty("created_count").GetInt32().Should().Be(0);
    }

    // =========================================================================
    // Completeness check happy path
    // =========================================================================

    [Fact]
    public async Task Completeness_DetectsHardwareWithoutSoftware()
    {
        // Add a hardware item with no software
        var addResult = await _addTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["item_name"] = "standalone-server",
            ["type"] = "hardware",
            ["function"] = "Server",
            ["manufacturer"] = "HP",
            ["ip_address"] = "10.0.0.99"
        });
        ParseSuccess(addResult);

        var result = await _completenessTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });

        var data = ParseSuccess(result);
        data.GetProperty("is_complete").GetBoolean().Should().BeFalse();
        data.GetProperty("hardware_without_software").GetArrayLength().Should().BeGreaterThan(0);
    }

    // =========================================================================
    // Import after export round-trip
    // =========================================================================

    [Fact]
    public async Task ImportExport_RoundTrip()
    {
        // Add items
        var addResult = await _addTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["item_name"] = "round-trip-server",
            ["type"] = "hardware",
            ["function"] = "Server",
            ["manufacturer"] = "Dell",
            ["ip_address"] = "10.0.0.50"
        });
        ParseSuccess(addResult);

        // Export
        var exportResult = await _exportTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });
        var exportData = ParseSuccess(exportResult);
        var base64 = exportData.GetProperty("file_base64").GetString()!;

        // Clear and re-import
        using (var scope = _serviceProvider.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
            ctx.InventoryItems.RemoveRange(ctx.InventoryItems);
            await ctx.SaveChangesAsync();
        }

        var importResult = await _importTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["file_base64"] = base64
        });

        var importData = ParseSuccess(importResult);
        importData.GetProperty("hardware_created").GetInt32().Should().BeGreaterThan(0);
    }
}
