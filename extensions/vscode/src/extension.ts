import * as vscode from "vscode";
import { McpClient } from "./services/mcpClient";
import { createParticipantHandler } from "./participant";
import { checkHealth } from "./commands/health";
import { configure } from "./commands/configure";

let outputChannel: vscode.OutputChannel;

export function activate(context: vscode.ExtensionContext): void {
  outputChannel = vscode.window.createOutputChannel("ATO Copilot");
  const mcpClient = new McpClient(outputChannel);

  // Register @ato chat participant
  const participant = vscode.chat.createChatParticipant(
    "ato",
    createParticipantHandler(mcpClient)
  );
  participant.iconPath = vscode.Uri.joinPath(
    context.extensionUri,
    "media",
    "icon.png"
  );
  // isSticky keeps the participant selected across chat interactions
  (participant as any).isSticky = true;
  context.subscriptions.push(participant);

  // Register commands
  context.subscriptions.push(
    vscode.commands.registerCommand("ato.checkHealth", () =>
      checkHealth(mcpClient)
    )
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("ato.configure", () => configure())
  );

  context.subscriptions.push(
    vscode.commands.registerCommand(
      "ato.analyzeCurrentFile",
      async () => {
        const { analyzeCurrentFile } = await import(
          "./commands/analyzeFile"
        );
        await analyzeCurrentFile(mcpClient);
      }
    )
  );

  context.subscriptions.push(
    vscode.commands.registerCommand(
      "ato.analyzeWorkspace",
      async () => {
        const { analyzeWorkspace } = await import(
          "./commands/analyzeWorkspace"
        );
        await analyzeWorkspace(mcpClient);
      }
    )
  );

  // Save template internal command (for stream.button() actions)
  context.subscriptions.push(
    vscode.commands.registerCommand(
      "ato.saveTemplate",
      async (template: {
        name: string;
        type: string;
        content: string;
        language: string;
      }) => {
        const { saveTemplate } = await import(
          "./services/workspaceService"
        );
        await saveTemplate(template);
      }
    )
  );

  // Refresh client when settings change
  context.subscriptions.push(
    vscode.workspace.onDidChangeConfiguration((e) => {
      if (e.affectsConfiguration("ato-copilot")) {
        mcpClient.refreshClient();
        outputChannel.appendLine("Configuration updated — client refreshed");
      }
    })
  );

  // Silent background health check on activation (FR-034)
  checkHealth(mcpClient, true);

  outputChannel.appendLine("ATO Copilot extension activated");
}

export function deactivate(): void {
  if (outputChannel) {
    outputChannel.dispose();
  }
}
