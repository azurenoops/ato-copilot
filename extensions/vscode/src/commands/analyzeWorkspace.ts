import * as vscode from "vscode";
import { McpClient, McpChatRequest, McpError } from "../services/mcpClient";
import { ComplianceFinding, parseFindings } from "./analyzeFile";
import { createAnalysisPanel } from "../webview/analysisPanel";

/**
 * File patterns to scan for compliance analysis.
 */
const INCLUDE_PATTERNS = [
  "**/*.bicep",
  "**/*.tf",
  "**/*.yaml",
  "**/*.yml",
  "**/*.json",
];

/**
 * Excluded directories (FR-031).
 */
const EXCLUDE_PATTERN = "{**/node_modules/**,**/.git/**,**/bin/**,**/obj/**}";

/**
 * Analyze all IaC files in the workspace for NIST 800-53 compliance (FR-031).
 * Scans matching files, sends each to /mcp/chat, aggregates findings.
 */
export async function analyzeWorkspace(mcpClient: McpClient): Promise<void> {
  const workspaceFolders = vscode.workspace.workspaceFolders;
  if (!workspaceFolders || workspaceFolders.length === 0) {
    vscode.window.showWarningMessage("No workspace folder is open");
    return;
  }

  try {
    await vscode.window.withProgress(
      {
        location: vscode.ProgressLocation.Notification,
        title: "Analyzing workspace for compliance...",
        cancellable: true,
      },
      async (progress, token) => {
        // Find all matching files
        const allFiles: vscode.Uri[] = [];
        for (const pattern of INCLUDE_PATTERNS) {
          const files = await vscode.workspace.findFiles(
            pattern,
            EXCLUDE_PATTERN,
            500
          );
          allFiles.push(...files);
        }

        // Deduplicate by URI
        const uniqueFiles = [
          ...new Map(allFiles.map((f) => [f.toString(), f])).values(),
        ];

        if (uniqueFiles.length === 0) {
          vscode.window.showInformationMessage(
            "No IaC files found in workspace"
          );
          return;
        }

        progress.report({
          message: `Found ${uniqueFiles.length} files to analyze`,
        });

        const allFindings: ComplianceFinding[] = [];
        let analyzed = 0;

        for (const fileUri of uniqueFiles) {
          if (token.isCancellationRequested) {
            break;
          }

          const document =
            await vscode.workspace.openTextDocument(fileUri);
          const fileName =
            fileUri.fsPath.split("/").pop() ?? fileUri.fsPath;
          const language = document.languageId;
          const fileContent = document.getText();

          progress.report({
            message: `Analyzing ${fileName} (${++analyzed}/${uniqueFiles.length})`,
            increment: 100 / uniqueFiles.length,
          });

          const request: McpChatRequest = {
            conversationId: `workspace-analysis-${Date.now()}`,
            message: `Analyze the following ${language} file for NIST 800-53 compliance issues:\n\nFile: ${fileName}\n\n\`\`\`${language}\n${fileContent}\n\`\`\`\n\nReturn findings as JSON with fields: controlId, title, severity (high/medium/low), description, recommendation.`,
            conversationHistory: [],
            context: {
              source: "vscode-copilot",
              platform: "VSCode",
              metadata: {
                fileName,
                language,
                analysisType: "workspace",
              },
            },
          };

          try {
            const response = await mcpClient.sendMessage(request);
            const findings = parseFindings(response.response);
            allFindings.push(...findings);
          } catch {
            // Skip files that fail — continue with rest
          }
        }

        createAnalysisPanel(
          "Workspace Compliance Analysis",
          allFindings,
          "workspace"
        );
      }
    );
  } catch (error) {
    const mcpError = error as McpError;
    const action = await vscode.window.showErrorMessage(
      mcpError.message ?? "Failed to analyze workspace",
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
