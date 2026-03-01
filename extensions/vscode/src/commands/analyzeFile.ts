import * as vscode from "vscode";
import { McpClient, McpChatRequest, McpError } from "../services/mcpClient";
import { createAnalysisPanel } from "../webview/analysisPanel";

/**
 * Compliance finding from analysis response.
 */
export interface ComplianceFinding {
  controlId: string;
  title: string;
  severity: "high" | "medium" | "low";
  description: string;
  recommendation: string;
}

/**
 * Analyze the current editor file for NIST 800-53 compliance issues (FR-030).
 * Sends file content to MCP Server and opens a webview panel with findings.
 */
export async function analyzeCurrentFile(mcpClient: McpClient): Promise<void> {
  const editor = vscode.window.activeTextEditor;
  if (!editor) {
    vscode.window.showWarningMessage(
      "No file is currently open in the editor"
    );
    return;
  }

  const document = editor.document;
  const fileName = document.fileName.split("/").pop() ?? document.fileName;
  const language = document.languageId;
  const fileContent = document.getText();

  const request: McpChatRequest = {
    conversationId: `analysis-${Date.now()}-${Math.random().toString(36).substring(2, 9)}`,
    message: `Analyze the following ${language} file for NIST 800-53 compliance issues:\n\nFile: ${fileName}\n\n\`\`\`${language}\n${fileContent}\n\`\`\`\n\nReturn findings as JSON with fields: controlId, title, severity (high/medium/low), description, recommendation.`,
    conversationHistory: [],
    context: {
      source: "vscode-copilot",
      platform: "VSCode",
      metadata: {
        fileName,
        language,
        analysisType: "file",
      },
    },
  };

  try {
    await vscode.window.withProgress(
      {
        location: vscode.ProgressLocation.Notification,
        title: `Analyzing ${fileName} for compliance...`,
        cancellable: false,
      },
      async () => {
        const response = await mcpClient.sendMessage(request);

        // Try to parse findings from the response
        const findings = parseFindings(response.response);

        createAnalysisPanel(
          `Compliance Analysis: ${fileName}`,
          findings,
          fileName
        );
      }
    );
  } catch (error) {
    const mcpError = error as McpError;
    const action = await vscode.window.showErrorMessage(
      mcpError.message ?? "Failed to analyze file",
      mcpError.actionButton ?? "Configure Connection"
    );

    if (action === "Configure Connection") {
      vscode.commands.executeCommand(
        "workbench.action.openSettings",
        "@ext:ato-copilot.ato-copilot-vscode"
      );
    }
  }
}

/**
 * Parse compliance findings from the MCP response string.
 * Attempts JSON extraction from the response, falls back to empty array.
 */
export function parseFindings(response: string): ComplianceFinding[] {
  try {
    // Try direct JSON parse
    const parsed = JSON.parse(response);
    if (Array.isArray(parsed)) {
      return parsed as ComplianceFinding[];
    }
    if (parsed.findings && Array.isArray(parsed.findings)) {
      return parsed.findings as ComplianceFinding[];
    }
  } catch {
    // Try to extract JSON from Markdown code block
    const jsonMatch = response.match(/```(?:json)?\s*\n?([\s\S]*?)```/);
    if (jsonMatch?.[1]) {
      try {
        const parsed = JSON.parse(jsonMatch[1].trim());
        if (Array.isArray(parsed)) {
          return parsed as ComplianceFinding[];
        }
      } catch {
        // Fallthrough to empty
      }
    }
  }

  return [];
}
