# Interface Contract: INistControlsService

**Feature Branch**: `007-nist-controls` | **Date**: 2026-02-23

## Overview

`INistControlsService` is the primary internal interface for accessing the NIST SP 800-53 Rev 5 catalog. It is consumed by agents, tools, validation services, and the health check. This contract documents the expanded 7-method interface (up from the current 3 methods).

Location: `src/Ato.Copilot.Core/Interfaces/Compliance/IComplianceInterfaces.cs`

## Interface Definition

```csharp
/// <summary>
/// NIST 800-53 Rev 5 controls catalog service.
/// Provides access to the full OSCAL catalog with caching, resilience,
/// and offline fallback capabilities.
/// </summary>
public interface INistControlsService
{
    /// <summary>
    /// Returns the full NIST catalog from cache, fetching on cache miss.
    /// </summary>
    /// <returns>The catalog, or null if unavailable.</returns>
    Task<NistCatalog?> GetCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a single control by ID (case-insensitive).
    /// Supports base controls ("AC-2") and enhancements ("AC-2(1)", "ac-2.1").
    /// </summary>
    /// <param name="controlId">The control identifier.</param>
    /// <returns>The matching control, or null if not found.</returns>
    Task<NistControl?> GetControlAsync(
        string controlId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all controls in a given family (case-insensitive prefix match).
    /// </summary>
    /// <param name="familyId">The 2-letter family prefix (e.g., "AC", "SC").</param>
    /// <param name="includeControls">If false, returns summary-only (no nested enhancements).</param>
    /// <returns>List of controls in the family, or empty list.</returns>
    Task<List<NistControl>> GetControlFamilyAsync(
        string familyId,
        bool includeControls = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Full-text search across control IDs, titles, and statement/guidance prose.
    /// </summary>
    /// <param name="query">Search query string.</param>
    /// <param name="controlFamily">Optional family filter.</param>
    /// <param name="impactLevel">Optional impact level filter.</param>
    /// <param name="maxResults">Maximum results to return (default: 10).</param>
    /// <returns>Matching controls, or empty list.</returns>
    Task<List<NistControl>> SearchControlsAsync(
        string query,
        string? controlFamily = null,
        string? impactLevel = null,
        int maxResults = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the catalog version string from metadata.
    /// </summary>
    /// <returns>Version string (e.g., "5.2.0"), or "Unknown" if unavailable.</returns>
    Task<string> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts the statement, guidance, and assessment objectives for a control.
    /// </summary>
    /// <param name="controlId">The control identifier.</param>
    /// <returns>Enriched enhancement view, or null if control not found.</returns>
    Task<ControlEnhancement?> GetControlEnhancementAsync(
        string controlId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether a control ID exists in the loaded catalog.
    /// </summary>
    /// <param name="controlId">The control identifier to validate.</param>
    /// <returns>True if the control exists, false otherwise.</returns>
    Task<bool> ValidateControlIdAsync(
        string controlId,
        CancellationToken cancellationToken = default);
}
```

## Method Contracts

### GetCatalogAsync

| Aspect | Contract |
|--------|----------|
| Input | None (only CancellationToken) |
| Output | `NistCatalog?` — full catalog with metadata + 20 groups |
| Cache behavior | Returns from IMemoryCache on hit; fetches from remote (with Polly retry) on miss |
| Fallback | Embedded resource if remote fetch fails |
| Error handling | Returns `null` on total failure (no throw) |
| Performance | < 50ms on cache hit; < 15s with retries on cache miss |

### GetControlAsync

| Aspect | Contract |
|--------|----------|
| Input | `controlId` — non-null, non-empty string |
| Output | `NistControl?` — matching control or null |
| Matching | Case-insensitive; supports `AC-2`, `ac-2`, `AC-2(1)`, `ac-2.1` |
| Validation | Throws `ArgumentException` if `controlId` is null/empty |
| Error handling | Returns `null` if catalog unavailable |

### GetControlFamilyAsync

| Aspect | Contract |
|--------|----------|
| Input | `familyId` — 2-letter prefix (e.g., `"AC"`, `"sc"`) |
| Output | `List<NistControl>` — never null, may be empty |
| Matching | Case-insensitive prefix |
| `includeControls=false` | Returns summary objects (ID, family, title, impact, baselines only) |
| Validation | Throws `ArgumentException` if `familyId` is null/empty |

### SearchControlsAsync

| Aspect | Contract |
|--------|----------|
| Input | `query` — non-null, non-empty search string |
| Output | `List<NistControl>` — never null, max `maxResults` items |
| Matching | Case-insensitive across ID, title, description, statement prose, guidance prose |
| Filters | Optional `controlFamily` and `impactLevel` narrow results |
| Validation | Throws `ArgumentException` if `query` is null/empty |

### GetVersionAsync

| Aspect | Contract |
|--------|----------|
| Input | None (only CancellationToken) |
| Output | `string` — version string or `"Unknown"` |
| Error handling | Returns `"Unknown"` if catalog unavailable (no throw) |

### GetControlEnhancementAsync

| Aspect | Contract |
|--------|----------|
| Input | `controlId` — non-null, non-empty string |
| Output | `ControlEnhancement?` with Statement, Guidance, Objectives |
| Extraction | Walks control's `parts` hierarchy: `name="statement"` → Statement, `name="guidance"` → Guidance, `name="assessment-objective"` → Objectives list |
| Validation | Throws `ArgumentException` if `controlId` is null/empty |
| Error handling | Returns `null` if control not found |

### ValidateControlIdAsync

| Aspect | Contract |
|--------|----------|
| Input | `controlId` — non-null, non-empty string |
| Output | `bool` — true if exists, false if not |
| Matching | Case-insensitive full ID match |
| Validation | Throws `ArgumentException` if `controlId` is null/empty |

## Backward Compatibility

The existing 3 methods (`GetControlAsync`, `GetControlFamilyAsync`, `SearchControlsAsync`) retain their **exact signatures**. 4 new methods are added. No existing consumer code requires changes.

| Method | Status | Signature Change |
|--------|--------|-----------------|
| `GetControlAsync` | Preserved | None |
| `GetControlFamilyAsync` | Preserved | None |
| `SearchControlsAsync` | Preserved | None |
| `GetCatalogAsync` | **New** | — |
| `GetVersionAsync` | **New** | — |
| `GetControlEnhancementAsync` | **New** | — |
| `ValidateControlIdAsync` | **New** | — |
