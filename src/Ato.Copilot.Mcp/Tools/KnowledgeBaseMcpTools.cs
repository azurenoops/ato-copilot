using Ato.Copilot.Agents.KnowledgeBase.Tools;

namespace Ato.Copilot.Mcp.Tools;

/// <summary>
/// MCP tools for KnowledgeBase operations. Wraps Agent Framework KB tools
/// for exposure via the MCP protocol (GitHub Copilot, Claude Desktop, etc.)
/// </summary>
public class KnowledgeBaseMcpTools
{
    private readonly ExplainNistControlTool _explainNistControlTool;
    private readonly SearchNistControlsTool _searchNistControlsTool;
    private readonly ExplainStigTool _explainStigTool;
    private readonly SearchStigsTool _searchStigsTool;
    private readonly ExplainRmfTool _explainRmfTool;
    private readonly ExplainImpactLevelTool _explainImpactLevelTool;
    private readonly GetFedRampTemplateGuidanceTool _getFedRampTemplateGuidanceTool;

    public KnowledgeBaseMcpTools(
        ExplainNistControlTool explainNistControlTool,
        SearchNistControlsTool searchNistControlsTool,
        ExplainStigTool explainStigTool,
        SearchStigsTool searchStigsTool,
        ExplainRmfTool explainRmfTool,
        ExplainImpactLevelTool explainImpactLevelTool,
        GetFedRampTemplateGuidanceTool getFedRampTemplateGuidanceTool)
    {
        _explainNistControlTool = explainNistControlTool;
        _searchNistControlsTool = searchNistControlsTool;
        _explainStigTool = explainStigTool;
        _searchStigsTool = searchStigsTool;
        _explainRmfTool = explainRmfTool;
        _explainImpactLevelTool = explainImpactLevelTool;
        _getFedRampTemplateGuidanceTool = getFedRampTemplateGuidanceTool;
    }

    /// <summary>
    /// Explain a NIST 800-53 control with Azure implementation guidance.
    /// </summary>
    public async Task<string> ExplainNistControlAsync(string controlId)
    {
        var args = new Dictionary<string, object?>
        {
            ["control_id"] = controlId
        };

        return await _explainNistControlTool.ExecuteAsync(args);
    }

    /// <summary>
    /// Search NIST 800-53 controls by keyword with optional family filtering.
    /// </summary>
    public async Task<string> SearchNistControlsAsync(string searchTerm, string? family, int? maxResults)
    {
        var args = new Dictionary<string, object?>
        {
            ["search_term"] = searchTerm,
            ["family"] = family,
            ["max_results"] = maxResults
        };

        return await _searchNistControlsTool.ExecuteAsync(args);
    }

    /// <summary>
    /// Explain a DISA STIG finding with severity, NIST mappings, and Azure guidance.
    /// </summary>
    public async Task<string> ExplainStigAsync(string stigId)
    {
        var args = new Dictionary<string, object?>
        {
            ["stig_id"] = stigId
        };

        return await _explainStigTool.ExecuteAsync(args);
    }

    /// <summary>
    /// Search DISA STIG findings by keyword and/or severity.
    /// </summary>
    public async Task<string> SearchStigsAsync(string searchTerm, string? severity, int? maxResults)
    {
        var args = new Dictionary<string, object?>
        {
            ["search_term"] = searchTerm,
            ["severity"] = severity,
            ["max_results"] = maxResults
        };

        return await _searchStigsTool.ExecuteAsync(args);
    }

    /// <summary>
    /// Explain RMF process, steps, service guidance, DoD instructions, or workflows.
    /// </summary>
    public async Task<string> ExplainRmfAsync(string? topic, int? stepNumber, string? organization, string? instructionId)
    {
        var args = new Dictionary<string, object?>
        {
            ["topic"] = topic,
            ["step_number"] = stepNumber,
            ["organization"] = organization,
            ["instruction_id"] = instructionId
        };

        return await _explainRmfTool.ExecuteAsync(args);
    }

    /// <summary>
    /// Explain DoD Impact Levels (IL2-IL6) and FedRAMP baselines.
    /// </summary>
    public async Task<string> ExplainImpactLevelAsync(string level)
    {
        var args = new Dictionary<string, object?>
        {
            ["level"] = level
        };

        return await _explainImpactLevelTool.ExecuteAsync(args);
    }

    /// <summary>
    /// Get FedRAMP authorization package template guidance.
    /// </summary>
    public async Task<string> GetFedRampTemplateGuidanceAsync(string? templateType, string? baseline)
    {
        var args = new Dictionary<string, object?>
        {
            ["template_type"] = templateType,
            ["baseline"] = baseline
        };

        return await _getFedRampTemplateGuidanceTool.ExecuteAsync(args);
    }
}
