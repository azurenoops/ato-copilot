using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.State.Abstractions;

namespace Ato.Copilot.Agents.Compliance.Tools;

/// <summary>
/// Chat tool with conversation memory for natural language compliance interaction.
/// Maintains conversation context via <see cref="IConversationStateManager"/>.
/// </summary>
public class ComplianceChatTool : BaseTool
{
    private readonly IConversationStateManager _conversationManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplianceChatTool"/> class.
    /// </summary>
    public ComplianceChatTool(
        IConversationStateManager conversationManager,
        ILogger<ComplianceChatTool> logger)
        : base(logger)
    {
        _conversationManager = conversationManager;
    }

    /// <inheritdoc />
    public override string Name => "compliance_chat";

    /// <inheritdoc />
    public override string Description => "Natural language conversation about compliance topics with context memory.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["message"] = new() { Name = "message", Description = "User message", Type = "string", Required = true },
        ["conversation_id"] = new() { Name = "conversation_id", Description = "Conversation ID for context continuity", Type = "string" }
    };

    /// <inheritdoc />
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var message = GetArg<string>(arguments, "message") ?? "";
        var conversationId = GetArg<string>(arguments, "conversation_id");

        // Get or create conversation
        ConversationState conversation;
        if (!string.IsNullOrEmpty(conversationId))
        {
            conversation = await _conversationManager.GetConversationAsync(conversationId, cancellationToken)
                           ?? new ConversationState { Id = conversationId };
        }
        else
        {
            var newId = await _conversationManager.CreateConversationAsync(cancellationToken);
            conversation = new ConversationState { Id = newId };
        }

        // Add user message to conversation history
        conversation.Messages.Add(new ConversationMessage
        {
            Role = "user",
            Content = message
        });

        // Generate contextual response based on conversation history
        var response = GenerateResponse(message, conversation);

        // Add assistant response to conversation history
        conversation.Messages.Add(new ConversationMessage
        {
            Role = "assistant",
            Content = response
        });

        // Save updated conversation
        conversation.LastActivityAt = DateTime.UtcNow;
        await _conversationManager.SaveConversationAsync(conversation, cancellationToken);

        return $"**Conversation**: {conversation.Id}\n\n{response}";
    }

    /// <summary>
    /// Generates a contextual response based on the message and conversation history.
    /// </summary>
    private static string GenerateResponse(string message, ConversationState conversation)
    {
        var lower = message.ToLowerInvariant();
        var hasContext = conversation.Messages.Count > 1;

        if (ContainsAny(lower, "how to start", "getting started", "first steps"))
        {
            return "## Getting Started with Compliance\n\n" +
                   "1. **Configure**: Set your subscription and framework\n" +
                   "2. **Assess**: Run `compliance_assess` for a compliance scan\n" +
                   "3. **Review**: Check findings with `compliance_status`\n" +
                   "4. **Remediate**: Fix issues with `compliance_remediate`\n" +
                   "5. **Evidence**: Collect artifacts with `compliance_collect_evidence`\n" +
                   "6. **Document**: Generate SSP/SAR/POA&M with `compliance_generate_document`";
        }

        if (ContainsAny(lower, "help", "what can you do", "commands", "tools"))
        {
            return "## Available Compliance Tools\n\n" +
                   "- **compliance_assess** — Run compliance assessment\n" +
                   "- **compliance_status** — View current compliance posture\n" +
                   "- **compliance_remediate** — Fix compliance findings\n" +
                   "- **compliance_collect_evidence** — Gather audit evidence\n" +
                   "- **compliance_generate_document** — Generate SSP/SAR/POA&M\n" +
                   "- **compliance_monitoring** — Continuous monitoring status\n" +
                   "- **compliance_history** — View compliance trends\n" +
                   "- **compliance_audit_log** — View audit trail";
        }

        if (hasContext)
        {
            return $"I understand your question about \"{message}\". In the context of our conversation, " +
                   "I can help with compliance assessments, remediation, evidence collection, and documentation. " +
                   "Please use one of the specific compliance tools for detailed operations, or ask me " +
                   "about NIST 800-53, FedRAMP, or ATO processes.";
        }

        return $"I can help with Azure Government compliance for NIST 800-53 / FedRAMP. " +
               $"Your question: \"{message}\"\n\n" +
               "Try asking about:\n" +
               "- \"What is NIST 800-53?\"\n" +
               "- \"How to get started with compliance?\"\n" +
               "- \"What tools are available?\"\n\n" +
               "Or use a specific tool like `compliance_assess` or `compliance_status`.";
    }

    /// <summary>Returns true if the text contains any of the specified keywords (case-insensitive).</summary>
    private static bool ContainsAny(string text, params string[] keywords) =>
        keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
}
