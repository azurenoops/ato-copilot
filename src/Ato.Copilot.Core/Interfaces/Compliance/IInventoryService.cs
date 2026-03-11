using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Service for managing hardware and software inventory items within
/// registered systems' authorization boundaries.
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Add a hardware or software inventory item to a system.
    /// </summary>
    /// <param name="registeredSystemId">Target system GUID.</param>
    /// <param name="input">Item data.</param>
    /// <param name="addedBy">User identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created inventory item.</returns>
    Task<InventoryItem> AddItemAsync(
        string registeredSystemId,
        InventoryItemInput input,
        string addedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update fields on an existing inventory item. Null fields in input are not changed.
    /// </summary>
    /// <param name="itemId">Inventory item GUID.</param>
    /// <param name="input">Updated fields.</param>
    /// <param name="modifiedBy">User identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated inventory item.</returns>
    Task<InventoryItem> UpdateItemAsync(
        string itemId,
        InventoryItemInput input,
        string modifiedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-delete an inventory item. If the item is hardware with active software
    /// children, all children are also decommissioned with the same rationale.
    /// </summary>
    /// <param name="itemId">Inventory item GUID.</param>
    /// <param name="rationale">Reason for decommissioning.</param>
    /// <param name="decommissionedBy">User identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The decommissioned inventory item.</returns>
    Task<InventoryItem> DecommissionItemAsync(
        string itemId,
        string rationale,
        string decommissionedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve a single inventory item by ID, including installed software (for HW items).
    /// </summary>
    /// <param name="itemId">Inventory item GUID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The inventory item with InstalledSoftware populated, or null.</returns>
    Task<InventoryItem?> GetItemAsync(
        string itemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List and filter inventory items for a system with pagination.
    /// </summary>
    /// <param name="registeredSystemId">System GUID.</param>
    /// <param name="options">Filter and pagination options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Filtered list of inventory items.</returns>
    Task<IReadOnlyList<InventoryItem>> ListItemsAsync(
        string registeredSystemId,
        InventoryListOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Export inventory as eMASS-compatible Excel workbook bytes.
    /// </summary>
    /// <param name="registeredSystemId">System GUID.</param>
    /// <param name="options">Export options (type filter, include decommissioned).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Excel workbook bytes (.xlsx).</returns>
    Task<byte[]> ExportToExcelAsync(
        string registeredSystemId,
        InventoryExportOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Import inventory items from an eMASS-format Excel workbook.
    /// </summary>
    /// <param name="fileBytes">Excel workbook bytes.</param>
    /// <param name="registeredSystemId">Target system GUID.</param>
    /// <param name="dryRun">If true, validate only — don't persist.</param>
    /// <param name="importedBy">User identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Import result with created/skipped counts and errors.</returns>
    Task<InventoryImportResult> ImportFromExcelAsync(
        byte[] fileBytes,
        string registeredSystemId,
        bool dryRun,
        string importedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run completeness check on a system's inventory.
    /// </summary>
    /// <param name="registeredSystemId">System GUID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Completeness result with issues and score.</returns>
    Task<InventoryCompleteness> CheckCompletenessAsync(
        string registeredSystemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create inventory items from authorization boundary resources.
    /// Only creates items for previously unmatched boundary resources (idempotent).
    /// </summary>
    /// <param name="registeredSystemId">System GUID.</param>
    /// <param name="seededBy">User identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of newly created inventory items.</returns>
    Task<IReadOnlyList<InventoryItem>> AutoSeedFromBoundaryAsync(
        string registeredSystemId,
        string seededBy,
        CancellationToken cancellationToken = default);
}
