using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Models.Compliance;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for InventoryService — Feature 025 HW/SW Inventory.
/// T026: Covers AddItemAsync, UpdateItemAsync, DecommissionItemAsync, GetItemAsync,
///       AutoSeedFromBoundaryAsync, ListItemsAsync, ExportToExcelAsync, ImportFromExcelAsync,
///       CheckCompletenessAsync.
/// </summary>
public class InventoryServiceTests : IDisposable
{
    private const string TestSystemId = "sys-001";
    private const string TestSystemName = "Test System";

    private readonly ServiceProvider _serviceProvider;
    private readonly InventoryService _service;

    private readonly RegisteredSystem _testSystem = new()
    {
        Id = TestSystemId,
        Name = TestSystemName,
        Acronym = "TSYS",
        CurrentRmfStep = RmfPhase.Implement
    };

    public InventoryServiceTests()
    {
        var services = new ServiceCollection();
        var dbName = $"InventoryServiceTests_{Guid.NewGuid()}";
        services.AddDbContext<AtoCopilotContext>(options =>
            options.UseInMemoryDatabase(dbName));
        _serviceProvider = services.BuildServiceProvider();

        using var initScope = _serviceProvider.CreateScope();
        var ctx = initScope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
        ctx.Database.EnsureCreated();
        ctx.RegisteredSystems.Add(_testSystem);
        ctx.SaveChanges();

        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        _service = new InventoryService(scopeFactory, NullLogger<InventoryService>.Instance);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        GC.SuppressFinalize(this);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private AtoCopilotContext GetContext()
    {
        var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
    }

    private InventoryItemInput MakeHardwareInput(
        string name = "web-server-01",
        string? manufacturer = "Dell",
        HardwareFunction function = HardwareFunction.Server,
        string? ip = "10.0.0.1") => new()
        {
            ItemName = name,
            Type = InventoryItemType.Hardware,
            HardwareFunction = function,
            Manufacturer = manufacturer,
            IpAddress = ip
        };

    private InventoryItemInput MakeSoftwareInput(
        string name = "Apache HTTP Server",
        string? vendor = "Apache Foundation",
        string? version = "2.4.57",
        SoftwareFunction function = SoftwareFunction.Application,
        string? parentId = null) => new()
        {
            ItemName = name,
            Type = InventoryItemType.Software,
            SoftwareFunction = function,
            Vendor = vendor,
            Version = version,
            ParentHardwareId = parentId
        };

    // ═══════════════════════════════════════════════════════════════════════
    // AddItemAsync
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AddItemAsync_ValidHardware_CreatesItem()
    {
        var input = MakeHardwareInput();
        var item = await _service.AddItemAsync(TestSystemId, input, "tester");

        item.Should().NotBeNull();
        item.ItemName.Should().Be("web-server-01");
        item.Type.Should().Be(InventoryItemType.Hardware);
        item.HardwareFunction.Should().Be(HardwareFunction.Server);
        item.Status.Should().Be(InventoryItemStatus.Active);
        item.CreatedBy.Should().Be("tester");
    }

    [Fact]
    public async Task AddItemAsync_ValidSoftware_CreatesItem()
    {
        var input = MakeSoftwareInput();
        var item = await _service.AddItemAsync(TestSystemId, input, "tester");

        item.Should().NotBeNull();
        item.Type.Should().Be(InventoryItemType.Software);
        item.Vendor.Should().Be("Apache Foundation");
    }

    [Fact]
    public async Task AddItemAsync_ServerWithoutIp_Throws()
    {
        var input = MakeHardwareInput(ip: null);

        var act = async () => await _service.AddItemAsync(TestSystemId, input, "tester");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*VALIDATION_FAILED*ip_address*");
    }

    [Fact]
    public async Task AddItemAsync_DuplicateIp_Throws()
    {
        var input1 = MakeHardwareInput(name: "server-1", ip: "10.0.0.1");
        await _service.AddItemAsync(TestSystemId, input1, "tester");

        var input2 = MakeHardwareInput(name: "server-2", ip: "10.0.0.1");
        var act = async () => await _service.AddItemAsync(TestSystemId, input2, "tester");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*DUPLICATE_IP*");
    }

    [Fact]
    public async Task AddItemAsync_SoftwareWithMissingParent_Throws()
    {
        var input = MakeSoftwareInput(parentId: "nonexistent");

        var act = async () => await _service.AddItemAsync(TestSystemId, input, "tester");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*PARENT*");
    }

    [Fact]
    public async Task AddItemAsync_SystemNotFound_Throws()
    {
        var input = MakeHardwareInput();

        var act = async () => await _service.AddItemAsync("bad-id", input, "tester");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*SYSTEM_NOT_FOUND*");
    }

    [Fact]
    public async Task AddItemAsync_WorkstationWithoutIp_Succeeds()
    {
        var input = MakeHardwareInput(function: HardwareFunction.Workstation, ip: null);

        var item = await _service.AddItemAsync(TestSystemId, input, "tester");

        item.Should().NotBeNull();
        item.HardwareFunction.Should().Be(HardwareFunction.Workstation);
        item.IpAddress.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UpdateItemAsync
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateItemAsync_PartialUpdate_OnlyChangesProvidedFields()
    {
        var created = await _service.AddItemAsync(TestSystemId, MakeHardwareInput(), "tester");

        var updates = new InventoryItemInput { Location = "Rack A-12" };
        var updated = await _service.UpdateItemAsync(created.Id, updates, "updater");

        updated.Location.Should().Be("Rack A-12");
        updated.ItemName.Should().Be("web-server-01"); // unchanged
        updated.ModifiedBy.Should().Be("updater");
        updated.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateItemAsync_DuplicateIp_Throws()
    {
        await _service.AddItemAsync(TestSystemId, MakeHardwareInput(name: "s1", ip: "10.0.0.1"), "t");
        var s2 = await _service.AddItemAsync(TestSystemId, MakeHardwareInput(name: "s2", ip: "10.0.0.2"), "t");

        var updates = new InventoryItemInput { IpAddress = "10.0.0.1" };
        var act = async () => await _service.UpdateItemAsync(s2.Id, updates, "t");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*DUPLICATE_IP*");
    }

    [Fact]
    public async Task UpdateItemAsync_NotFound_Throws()
    {
        var act = async () => await _service.UpdateItemAsync("bad-id", new InventoryItemInput(), "t");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*ITEM_NOT_FOUND*");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DecommissionItemAsync
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DecommissionItemAsync_SetsStatusAndCascadesToChildSoftware()
    {
        var hw = await _service.AddItemAsync(TestSystemId, MakeHardwareInput(), "tester");
        var sw = await _service.AddItemAsync(TestSystemId, MakeSoftwareInput(parentId: hw.Id), "tester");

        var result = await _service.DecommissionItemAsync(hw.Id, "End of life", "decom-user");

        result.Status.Should().Be(InventoryItemStatus.Decommissioned);

        // Child SW also decommissioned
        var childSw = await _service.GetItemAsync(sw.Id);
        childSw!.Status.Should().Be(InventoryItemStatus.Decommissioned);
    }

    [Fact]
    public async Task DecommissionItemAsync_AlreadyDecommissioned_Throws()
    {
        var hw = await _service.AddItemAsync(TestSystemId, MakeHardwareInput(), "tester");
        await _service.DecommissionItemAsync(hw.Id, "EOL", "d");

        var act = async () => await _service.DecommissionItemAsync(hw.Id, "EOL", "d");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*ALREADY_DECOMMISSIONED*");
    }

    [Fact]
    public async Task DecommissionItemAsync_NotFound_Throws()
    {
        var act = async () => await _service.DecommissionItemAsync("bad-id", "EOL", "d");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*ITEM_NOT_FOUND*");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GetItemAsync
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetItemAsync_ExistingItem_ReturnsWithInstalledSoftware()
    {
        var hw = await _service.AddItemAsync(TestSystemId, MakeHardwareInput(), "tester");
        await _service.AddItemAsync(TestSystemId, MakeSoftwareInput(parentId: hw.Id), "tester");

        var result = await _service.GetItemAsync(hw.Id);

        result.Should().NotBeNull();
        result!.InstalledSoftware.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetItemAsync_NotFound_ReturnsNull()
    {
        var result = await _service.GetItemAsync("nonexistent");

        result.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ListItemsAsync
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ListItemsAsync_FiltersByType()
    {
        await _service.AddItemAsync(TestSystemId, MakeHardwareInput(), "t");
        await _service.AddItemAsync(TestSystemId, MakeSoftwareInput(), "t");

        var hwList = await _service.ListItemsAsync(TestSystemId,
            new InventoryListOptions { Type = InventoryItemType.Hardware });

        hwList.Should().HaveCount(1);
        hwList[0].Type.Should().Be(InventoryItemType.Hardware);
    }

    [Fact]
    public async Task ListItemsAsync_Pagination()
    {
        for (var i = 0; i < 5; i++)
            await _service.AddItemAsync(TestSystemId, MakeHardwareInput(name: $"server-{i}", ip: $"10.0.0.{i}"), "t");

        var page = await _service.ListItemsAsync(TestSystemId,
            new InventoryListOptions { PageSize = 2, PageNumber = 2 });

        page.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListItemsAsync_SearchText()
    {
        await _service.AddItemAsync(TestSystemId, MakeHardwareInput(name: "web-server-01"), "t");
        await _service.AddItemAsync(TestSystemId, MakeHardwareInput(name: "db-server-01", ip: "10.0.0.2"), "t");

        var results = await _service.ListItemsAsync(TestSystemId,
            new InventoryListOptions { SearchText = "web" });

        results.Should().HaveCount(1);
        results[0].ItemName.Should().Contain("web");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AutoSeedFromBoundaryAsync
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AutoSeedFromBoundaryAsync_CreateFromBoundaryResources()
    {
        // Seed boundary resources
        using (var ctx = GetContext())
        {
            ctx.AuthorizationBoundaries.Add(new AuthorizationBoundary
            {
                RegisteredSystemId = TestSystemId,
                ResourceId = "/sub/rg/vm1",
                ResourceType = "Microsoft.Compute/virtualMachines",
                ResourceName = "web-vm",
                IsInBoundary = true,
                AddedBy = "test"
            });
            await ctx.SaveChangesAsync();
        }

        var items = await _service.AutoSeedFromBoundaryAsync(TestSystemId, "seeder");

        items.Should().HaveCount(1);
        items[0].ItemName.Should().Be("web-vm");
        items[0].HardwareFunction.Should().Be(HardwareFunction.Server);
    }

    [Fact]
    public async Task AutoSeedFromBoundaryAsync_Idempotent()
    {
        using (var ctx = GetContext())
        {
            ctx.AuthorizationBoundaries.Add(new AuthorizationBoundary
            {
                Id = "br-1",
                RegisteredSystemId = TestSystemId,
                ResourceId = "/sub/rg/vm1",
                ResourceType = "Microsoft.Compute/virtualMachines",
                ResourceName = "web-vm",
                IsInBoundary = true,
                AddedBy = "test"
            });
            await ctx.SaveChangesAsync();
        }

        var first = await _service.AutoSeedFromBoundaryAsync(TestSystemId, "seeder");
        var second = await _service.AutoSeedFromBoundaryAsync(TestSystemId, "seeder");

        first.Should().HaveCount(1);
        second.Should().HaveCount(0);
    }

    [Fact]
    public async Task AutoSeedFromBoundaryAsync_NoBoundary_Throws()
    {
        var act = async () => await _service.AutoSeedFromBoundaryAsync(TestSystemId, "seeder");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*NO_BOUNDARY_DATA*");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ExportToExcelAsync
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExportToExcelAsync_ReturnsWorkbookBytes()
    {
        await _service.AddItemAsync(TestSystemId, MakeHardwareInput(), "t");
        await _service.AddItemAsync(TestSystemId, MakeSoftwareInput(), "t");

        var options = new InventoryExportOptions { ExportType = "all" };
        var bytes = await _service.ExportToExcelAsync(TestSystemId, options);

        bytes.Should().NotBeEmpty();
        // Verify it's a valid XLSX (starts with PK zip header)
        bytes[0].Should().Be(0x50);
        bytes[1].Should().Be(0x4B);
    }

    [Fact]
    public async Task ExportToExcelAsync_SystemNotFound_Throws()
    {
        var act = async () => await _service.ExportToExcelAsync("bad-id", new InventoryExportOptions());

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*SYSTEM_NOT_FOUND*");
    }

    [Fact]
    public async Task ExportToExcelAsync_NoItems_Throws()
    {
        var act = async () => await _service.ExportToExcelAsync(TestSystemId, new InventoryExportOptions());

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*NO_INVENTORY_DATA*");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ImportFromExcelAsync
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ImportFromExcelAsync_ValidWorkbook_ImportsItems()
    {
        // Generate a workbook to import
        await _service.AddItemAsync(TestSystemId, MakeHardwareInput(), "t");
        await _service.AddItemAsync(TestSystemId, MakeSoftwareInput(), "t");
        var exportBytes = await _service.ExportToExcelAsync(TestSystemId, new InventoryExportOptions { ExportType = "all" });

        // Clear inventory
        using (var ctx = GetContext())
        {
            ctx.InventoryItems.RemoveRange(ctx.InventoryItems);
            await ctx.SaveChangesAsync();
        }

        var result = await _service.ImportFromExcelAsync(exportBytes, TestSystemId, false, "importer");

        result.HardwareCreated.Should().BeGreaterThan(0);
        result.DryRun.Should().BeFalse();
    }

    [Fact]
    public async Task ImportFromExcelAsync_DryRun_DoesNotPersist()
    {
        await _service.AddItemAsync(TestSystemId, MakeHardwareInput(), "t");
        var exportBytes = await _service.ExportToExcelAsync(TestSystemId, new InventoryExportOptions { ExportType = "hardware" });

        using (var ctx = GetContext())
        {
            ctx.InventoryItems.RemoveRange(ctx.InventoryItems);
            await ctx.SaveChangesAsync();
        }

        var result = await _service.ImportFromExcelAsync(exportBytes, TestSystemId, true, "importer");

        result.HardwareCreated.Should().BeGreaterThan(0);
        result.DryRun.Should().BeTrue();

        using (var ctx = GetContext())
        {
            var count = await ctx.InventoryItems.CountAsync();
            count.Should().Be(0);
        }
    }

    [Fact]
    public async Task ImportFromExcelAsync_SystemNotFound_Throws()
    {
        var act = async () => await _service.ImportFromExcelAsync(new byte[] { 0 }, "bad-id", false, "t");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*SYSTEM_NOT_FOUND*");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CheckCompletenessAsync
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CheckCompletenessAsync_CompleteInventory_ReturnsIsComplete()
    {
        var hw = await _service.AddItemAsync(TestSystemId, MakeHardwareInput(), "t");
        await _service.AddItemAsync(TestSystemId, MakeSoftwareInput(parentId: hw.Id), "t");

        var result = await _service.CheckCompletenessAsync(TestSystemId);

        result.TotalItems.Should().Be(2);
        result.HardwareCount.Should().Be(1);
        result.SoftwareCount.Should().Be(1);
        result.CompletenessScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CheckCompletenessAsync_HardwareWithoutSoftware_ReportsIssue()
    {
        await _service.AddItemAsync(TestSystemId, MakeHardwareInput(), "t");

        var result = await _service.CheckCompletenessAsync(TestSystemId);

        result.HardwareWithoutSoftware.Should().HaveCount(1);
        result.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task CheckCompletenessAsync_UnmatchedBoundary_ReportsIssue()
    {
        using (var ctx = GetContext())
        {
            ctx.AuthorizationBoundaries.Add(new AuthorizationBoundary
            {
                RegisteredSystemId = TestSystemId,
                ResourceId = "/sub/rg/vm1",
                ResourceType = "Microsoft.Compute/virtualMachines",
                ResourceName = "unmatched-vm",
                IsInBoundary = true,
                AddedBy = "test"
            });
            await ctx.SaveChangesAsync();
        }

        var result = await _service.CheckCompletenessAsync(TestSystemId);

        result.UnmatchedBoundaryResources.Should().HaveCount(1);
        result.UnmatchedBoundaryResources[0].ResourceName.Should().Be("unmatched-vm");
    }

    [Fact]
    public async Task CheckCompletenessAsync_SystemNotFound_Throws()
    {
        var act = async () => await _service.CheckCompletenessAsync("bad-id");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*SYSTEM_NOT_FOUND*");
    }

    [Fact]
    public async Task CheckCompletenessAsync_EmptyInventory_Returns100Percent()
    {
        var result = await _service.CheckCompletenessAsync(TestSystemId);

        result.TotalItems.Should().Be(0);
        result.CompletenessScore.Should().Be(100.0);
        result.IsComplete.Should().BeTrue();
    }
}
