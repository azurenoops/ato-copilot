using Ato.Copilot.Core.Models.Auth;

namespace Ato.Copilot.Core.Configuration;

/// <summary>
/// Static registry classifying MCP tools into Tier 1, Tier 2a, and Tier 2b.
/// Tier 1 (PimTier.None): Local/cached operations that never touch Azure APIs.
/// Tier 2a (PimTier.Read): Read-only Azure operations requiring CAC + Reader PIM.
/// Tier 2b (PimTier.Write): Write Azure operations requiring CAC + Contributor+ PIM.
/// Per R-007: Unknown tools default to Tier 1 for backward compatibility.
/// Per R-010: Tier 2 is sub-divided into 2a (read) and 2b (write) for granular PIM enforcement.
/// </summary>
public static class AuthTierClassification
{
    /// <summary>
    /// Tier 2a tools: read-only Azure operations requiring CAC + Reader-level PIM.
    /// </summary>
    private static readonly HashSet<string> Tier2aTools = new(StringComparer.OrdinalIgnoreCase)
    {
        // ─── Compliance read operations ──────────────────────────────────
        "run_assessment",
        "collect_evidence",
        "discover_resources",
        "compliance_assess",
        "compliance_collect_evidence",
        "compliance_monitoring",

        // ─── PIM/JIT read operations ─────────────────────────────────────
        "pim_list_eligible",
        "pim_list_active",
        "pim_history",
        "jit_list_sessions"
    };

    /// <summary>
    /// Tier 2b tools: write Azure operations requiring CAC + Contributor-level (or higher) PIM.
    /// </summary>
    private static readonly HashSet<string> Tier2bTools = new(StringComparer.OrdinalIgnoreCase)
    {
        // ─── Compliance write operations ─────────────────────────────────
        "execute_remediation",
        "validate_remediation",
        "deploy_template",
        "compliance_remediate",
        "compliance_validate_remediation",

        // ─── Kanban write operations ─────────────────────────────────────
        "kanban_remediate_task",
        "kanban_validate_task",
        "kanban_collect_evidence",

        // ─── PIM write operations ────────────────────────────────────────
        "pim_activate_role",
        "pim_deactivate_role",
        "pim_extend_role",
        "pim_approve_request",
        "pim_deny_request",

        // ─── JIT write operations ────────────────────────────────────────
        "jit_request_access",
        "jit_revoke_access",

        // ─── CAC session management ──────────────────────────────────────
        "cac_sign_out",
        "cac_set_timeout",
        "cac_map_certificate"
    };

    /// <summary>
    /// Checks whether the specified tool requires Tier 2 (CAC-authenticated) access.
    /// A tool is Tier 2 if it is either Tier 2a (read) or Tier 2b (write).
    /// Unknown tools default to Tier 1 (no auth required).
    /// </summary>
    /// <param name="toolName">The MCP tool name to classify.</param>
    /// <returns>True if the tool is Tier 2 (requires CAC auth); false for Tier 1.</returns>
    public static bool IsTier2(string toolName) =>
        !string.IsNullOrEmpty(toolName) && (Tier2aTools.Contains(toolName) || Tier2bTools.Contains(toolName));

    /// <summary>
    /// Checks whether the specified tool requires Tier 2a (Reader PIM) access.
    /// Tier 2a tools perform read-only Azure operations (assessments, evidence, PIM queries).
    /// </summary>
    /// <param name="toolName">The MCP tool name to classify.</param>
    /// <returns>True if the tool is Tier 2a (requires Reader PIM).</returns>
    public static bool IsTier2a(string toolName) =>
        !string.IsNullOrEmpty(toolName) && Tier2aTools.Contains(toolName);

    /// <summary>
    /// Checks whether the specified tool requires Tier 2b (Contributor+ PIM) access.
    /// Tier 2b tools perform write Azure operations (remediations, PIM activations, JIT).
    /// </summary>
    /// <param name="toolName">The MCP tool name to classify.</param>
    /// <returns>True if the tool is Tier 2b (requires Contributor+ PIM).</returns>
    public static bool IsTier2b(string toolName) =>
        !string.IsNullOrEmpty(toolName) && Tier2bTools.Contains(toolName);

    /// <summary>
    /// Gets the required <see cref="PimTier"/> for the specified tool.
    /// Returns PimTier.None for Tier 1 (unknown/local), PimTier.Read for Tier 2a, PimTier.Write for Tier 2b.
    /// </summary>
    /// <param name="toolName">The MCP tool name to classify.</param>
    /// <returns>The PimTier required to invoke the tool.</returns>
    public static PimTier GetRequiredPimTier(string toolName)
    {
        if (string.IsNullOrEmpty(toolName)) return PimTier.None;
        if (Tier2bTools.Contains(toolName)) return PimTier.Write;
        if (Tier2aTools.Contains(toolName)) return PimTier.Read;
        return PimTier.None;
    }
}
