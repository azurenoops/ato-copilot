using System.ComponentModel.DataAnnotations;

namespace Ato.Copilot.Core.Models;

/// <summary>
/// Configuration for server-side pagination enforcement on collection responses.
/// </summary>
public class PaginationOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Pagination";

    /// <summary>Default page size when not specified by client.</summary>
    [Range(1, 1000)]
    public int DefaultPageSize { get; set; } = 50;

    /// <summary>Maximum page size; client requests exceeding this are clamped.</summary>
    [Range(1, 1000)]
    public int MaxPageSize { get; set; } = 100;
}
