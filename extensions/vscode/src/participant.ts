import * as vscode from "vscode";
import {
  McpClient,
  McpChatRequest,
  McpChatResponse,
  McpError,
} from "./services/mcpClient";

/**
 * Slash command → target agent mapping
 */
const SLASH_COMMAND_AGENT_MAP: Record<string, string> = {
  compliance: "ComplianceAgent",
  knowledge: "KnowledgeBaseAgent",
  config: "ConfigurationAgent",
};

/**
 * Per-session conversation ID for maintaining history context.
 */
let sessionConversationId: string | undefined;

function getConversationId(): string {
  if (!sessionConversationId) {
    sessionConversationId = `vscode-${Date.now()}-${Math.random().toString(36).substring(2, 9)}`;
  }
  return sessionConversationId;
}

/**
 * Rebuild conversation history from VS Code ChatContext.
 */
function buildHistory(
  context: vscode.ChatContext
): Array<{ role: "user" | "assistant"; content: string }> {
  const history: Array<{ role: "user" | "assistant"; content: string }> = [];

  for (const entry of context.history) {
    if (entry instanceof vscode.ChatRequestTurn) {
      history.push({ role: "user", content: entry.prompt });
    } else if (entry instanceof vscode.ChatResponseTurn) {
      // Concatenate response parts into a single string
      const parts: string[] = [];
      for (const part of entry.response) {
        if (part instanceof vscode.ChatResponseMarkdownPart) {
          parts.push(part.value.value);
        }
      }
      if (parts.length > 0) {
        history.push({ role: "assistant", content: parts.join("") });
      }
    }
  }

  return history;
}

/**
 * Get active editor file information for context.
 */
function getEditorContext(): {
  fileName?: string;
  language?: string;
} {
  const editor = vscode.window.activeTextEditor;
  if (!editor) {
    return {};
  }

  return {
    fileName: editor.document.fileName,
    language: editor.document.languageId,
  };
}

/**
 * Render tool execution summary as a Markdown table (FR-022).
 */
function renderToolsSummary(
  response: McpChatResponse,
  stream: vscode.ChatResponseStream
): void {
  if (!response.toolsExecuted?.length) {
    return;
  }

  stream.markdown("\n\n**🔧 Tools Used**\n\n");
  stream.markdown("| Tool | Time | Status |\n|------|------|--------|\n");
  for (const tool of response.toolsExecuted) {
    const status = tool.success ? "✓" : "✗";
    stream.markdown(
      `| ${tool.toolName} | ${tool.executionTimeMs}ms | ${status} |\n`
    );
  }
}

/**
 * Render compliance summary table when applicable (FR-025).
 */
function renderComplianceSummary(
  response: McpChatResponse,
  stream: vscode.ChatResponseStream
): void {
  if (response.intentType !== "compliance" || !response.data) {
    return;
  }

  const data = response.data;
  const score = data.complianceScore as number | undefined;
  const passCount = data.passCount as number | undefined;
  const warnCount = data.warnCount as number | undefined;
  const failCount = data.failCount as number | undefined;

  if (score === undefined && passCount === undefined) {
    return;
  }

  stream.markdown("\n\n**Compliance Summary**\n\n");
  stream.markdown("| Metric | Value |\n|--------|-------|\n");

  if (score !== undefined) {
    stream.markdown(`| Score | ${score}% |\n`);
  }
  if (passCount !== undefined) {
    stream.markdown(`| Pass | ${passCount} |\n`);
  }
  if (warnCount !== undefined) {
    stream.markdown(`| Warning | ${warnCount} |\n`);
  }
  if (failCount !== undefined) {
    stream.markdown(`| Fail | ${failCount} |\n`);
  }
}

/**
 * Render follow-up prompt with reply button (FR-024).
 */
function renderFollowUp(
  response: McpChatResponse,
  stream: vscode.ChatResponseStream
): void {
  if (!response.requiresFollowUp || !response.followUpPrompt) {
    return;
  }

  stream.markdown(`\n\n> **Follow-up needed:** ${response.followUpPrompt}`);

  if (response.missingFields?.length) {
    stream.markdown(`\n> Missing: ${response.missingFields.join(", ")}`);
  }
}

/**
 * Render suggestion buttons as follow-up commands (FR-023).
 * Supports both structured suggestedActions and legacy string suggestions.
 */
function renderSuggestions(
  response: McpChatResponse,
  stream: vscode.ChatResponseStream
): void {
  // Prefer structured suggestedActions (title + prompt)
  if (response.suggestedActions?.length) {
    for (const action of response.suggestedActions) {
      stream.button({
        command: "ato.followUpSuggestion",
        title: action.title,
        arguments: [action.prompt],
      });
    }
    return;
  }

  // Fallback to legacy string suggestions
  if (!response.suggestions?.length) {
    return;
  }

  for (const suggestion of response.suggestions) {
    stream.button({
      command: "ato.followUpSuggestion",
      title: suggestion,
      arguments: [suggestion],
    });
  }
}

/**
 * Creates the @ato chat participant handler.
 *
 * Routes slash commands to the appropriate agent, rebuilds conversation history
 * from ChatContext.history, and renders streamed Markdown responses with agent attribution,
 * tool execution summaries, compliance tables, suggestion buttons, and follow-up prompts.
 */
export function createParticipantHandler(
  mcpClient: McpClient
): vscode.ChatRequestHandler {
  return async (
    request: vscode.ChatRequest,
    context: vscode.ChatContext,
    stream: vscode.ChatResponseStream,
    token: vscode.CancellationToken
  ): Promise<vscode.ChatResult> => {
    const command = request.command;
    const targetAgent = command
      ? SLASH_COMMAND_AGENT_MAP[command]
      : undefined;

    const editorContext = getEditorContext();
    const conversationHistory = buildHistory(context);

    const chatRequest: McpChatRequest = {
      conversationId: getConversationId(),
      message: request.prompt,
      conversationHistory,
      context: {
        source: "vscode-copilot",
        platform: "VSCode",
        targetAgent,
        metadata: {
          routingHint: targetAgent,
          fileName: editorContext.fileName,
          language: editorContext.language,
        },
      },
    };

    try {
      stream.progress("Connecting to ATO Copilot...");

      const response: McpChatResponse =
        await mcpClient.sendMessageWithProgress(chatRequest, (step) => {
          stream.progress(step);
        });

      // Render main response as Markdown
      stream.markdown(response.response);

      // Render templates as fenced code blocks with Save buttons
      if (response.templates && response.templates.length > 0) {
        for (const template of response.templates) {
          stream.markdown(
            `\n\n### ${template.name}\n\n\`\`\`${template.language}\n${template.content}\n\`\`\``
          );
          stream.button({
            command: "ato.saveTemplate",
            title: `$(file-add) Save ${template.name}`,
            arguments: [template],
          });
        }
      }

      // Compliance summary (FR-025)
      renderComplianceSummary(response, stream);

      // Tool execution summary (FR-022)
      renderToolsSummary(response, stream);

      // Follow-up prompt (FR-024)
      renderFollowUp(response, stream);

      // Agent attribution is conveyed through the tools summary table
      // and the response metadata — no separate text needed.

      // Suggestion buttons (FR-023)
      renderSuggestions(response, stream);

      return {};
    } catch (error) {
      const mcpError = error as McpError;
      stream.markdown(
        `**Error**: ${mcpError.message ?? "An unexpected error occurred"}`
      );

      if (mcpError.actionButton === "Configure Connection") {
        stream.button({
          command: "ato.configure",
          title: "Configure Connection",
        });
      }

      return {};
    }
  };
}

/**
 * Reset the session conversation ID (for testing or new sessions).
 */
export function resetConversationId(): void {
  sessionConversationId = undefined;
}
