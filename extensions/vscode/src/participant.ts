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
 * Creates the @ato chat participant handler.
 *
 * Routes slash commands to the appropriate agent, rebuilds conversation history
 * from ChatContext.history, and renders streamed Markdown responses with agent attribution.
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
      const response: McpChatResponse =
        await mcpClient.sendMessage(chatRequest);

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

      // Agent attribution (FR-028)
      if (response.agentUsed) {
        stream.markdown(`\n\n*Processed by: ${response.agentUsed}*`);
      }

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
