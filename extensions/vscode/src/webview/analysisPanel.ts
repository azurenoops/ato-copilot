import * as vscode from "vscode";
import { ComplianceFinding } from "../commands/analyzeFile";

/**
 * Create a webview panel displaying compliance analysis findings (T035).
 * Groups findings by severity with color-coded badges.
 */
export function createAnalysisPanel(
  title: string,
  findings: ComplianceFinding[],
  source: string
): vscode.WebviewPanel {
  const panel = vscode.window.createWebviewPanel(
    "atoComplianceAnalysis",
    title,
    vscode.ViewColumn.Beside,
    {
      enableScripts: false,
      retainContextWhenHidden: true,
    }
  );

  panel.webview.html = buildHtml(title, findings, source);

  return panel;
}

function buildHtml(
  title: string,
  findings: ComplianceFinding[],
  source: string
): string {
  const high = findings.filter((f) => f.severity === "high");
  const medium = findings.filter((f) => f.severity === "medium");
  const low = findings.filter((f) => f.severity === "low");

  const summaryTable = `
    <table class="summary">
      <tr>
        <td class="badge high">${high.length} High</td>
        <td class="badge medium">${medium.length} Medium</td>
        <td class="badge low">${low.length} Low</td>
        <td><strong>${findings.length} Total</strong></td>
      </tr>
    </table>
  `;

  const renderGroup = (
    groupFindings: ComplianceFinding[],
    severityLabel: string,
    badgeClass: string
  ): string => {
    if (groupFindings.length === 0) {
      return "";
    }

    const items = groupFindings
      .map(
        (f) => `
      <div class="finding">
        <div class="finding-header">
          <span class="badge ${badgeClass}">${severityLabel}</span>
          <code>${f.controlId}</code>
          <strong>${f.title}</strong>
        </div>
        <p class="description">${escapeHtml(f.description)}</p>
        <div class="recommendation">
          <strong>Recommendation:</strong> ${escapeHtml(f.recommendation)}
        </div>
      </div>
    `
      )
      .join("");

    return `
      <h2><span class="badge ${badgeClass}">${severityLabel}</span> ${groupFindings.length} Finding(s)</h2>
      ${items}
    `;
  };

  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>${escapeHtml(title)}</title>
  <style>
    body { font-family: var(--vscode-font-family, sans-serif); padding: 16px; color: var(--vscode-foreground); background: var(--vscode-editor-background); }
    h1 { font-size: 1.4em; margin-bottom: 4px; }
    h2 { font-size: 1.1em; margin-top: 24px; }
    .meta { color: var(--vscode-descriptionForeground); font-size: 0.85em; margin-bottom: 16px; }
    .summary { margin-bottom: 16px; }
    .summary td { padding: 4px 12px; }
    .badge { display: inline-block; padding: 2px 8px; border-radius: 4px; font-size: 0.85em; font-weight: bold; color: #fff; }
    .badge.high { background: #d32f2f; }
    .badge.medium { background: #f57c00; }
    .badge.low { background: #388e3c; }
    .finding { border: 1px solid var(--vscode-panel-border); border-radius: 6px; padding: 12px; margin-bottom: 12px; }
    .finding-header { display: flex; align-items: center; gap: 8px; margin-bottom: 8px; }
    .finding-header code { font-family: var(--vscode-editor-font-family, monospace); background: var(--vscode-textCodeBlock-background); padding: 2px 6px; border-radius: 3px; }
    .description { margin: 4px 0; }
    .recommendation { background: rgba(56, 142, 60, 0.1); padding: 8px 12px; border-radius: 4px; margin-top: 8px; }
    .no-findings { text-align: center; padding: 32px; color: var(--vscode-descriptionForeground); }
  </style>
</head>
<body>
  <h1>${escapeHtml(title)}</h1>
  <div class="meta">Source: ${escapeHtml(source)} | Generated: ${new Date().toISOString()}</div>
  ${summaryTable}
  ${
    findings.length === 0
      ? '<div class="no-findings">No compliance findings detected</div>'
      : [
          renderGroup(high, "HIGH", "high"),
          renderGroup(medium, "MEDIUM", "medium"),
          renderGroup(low, "LOW", "low"),
        ].join("")
  }
</body>
</html>`;
}

function escapeHtml(text: string): string {
  return text
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}
