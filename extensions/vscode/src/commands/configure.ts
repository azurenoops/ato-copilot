import * as vscode from "vscode";

/**
 * Open ATO Copilot settings filtered to this extension (FR-029, FR-032).
 */
export async function configure(): Promise<void> {
  await vscode.commands.executeCommand(
    "workbench.action.openSettings",
    "@ext:ato-copilot.ato-copilot-vscode"
  );
}
