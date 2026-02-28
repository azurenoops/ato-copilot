import * as vscode from "vscode";
import { buildRmfOverviewHtml, type RmfSystemOverview } from "./rmfOverviewHtml";

export type { RmfSystemOverview };
export { buildRmfOverviewHtml };

/**
 * Create a webview panel displaying RMF system overview (US13, T172).
 *
 * Shows the system's position in the RMF lifecycle with a visual stepper,
 * key system details, categorization, baseline progress, and ATO status.
 */
export function createRmfOverviewPanel(
  systemData: RmfSystemOverview
): vscode.WebviewPanel {
  const panel = vscode.window.createWebviewPanel(
    "atoRmfOverview",
    `RMF Overview — ${systemData.acronym ?? systemData.systemName}`,
    vscode.ViewColumn.One,
    {
      enableScripts: true,
      retainContextWhenHidden: true,
    }
  );

  panel.webview.html = buildRmfOverviewHtml(systemData);

  panel.webview.onDidReceiveMessage(
    (message: { command: string; [key: string]: unknown }) => {
      switch (message.command) {
        case "viewStep":
          vscode.commands.executeCommand("ato.viewRmfStep", {
            systemName: systemData.systemName,
            step: message.step,
          });
          break;
        case "viewCompliance":
          vscode.commands.executeCommand("ato.viewCompliance", {
            systemName: systemData.systemName,
          });
          break;
        case "refresh":
          vscode.commands.executeCommand("ato.refreshRmfOverview", {
            systemName: systemData.systemName,
          });
          break;
      }
    }
  );

  return panel;
}
