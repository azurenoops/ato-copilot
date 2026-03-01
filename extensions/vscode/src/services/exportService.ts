import * as vscode from "vscode";
import { ComplianceFinding } from "../commands/analyzeFile";

/**
 * Export formats supported by the export service (FR-035).
 */
export type ExportFormat = "markdown" | "json" | "html";

/**
 * Export compliance analysis results in Markdown, JSON, or HTML format.
 */
export async function exportFindings(
  findings: ComplianceFinding[],
  format: ExportFormat,
  title: string
): Promise<void> {
  let content: string;
  let extension: string;
  let languageId: string;

  switch (format) {
    case "markdown":
      content = toMarkdown(findings, title);
      extension = ".md";
      languageId = "markdown";
      break;
    case "json":
      content = toJson(findings);
      extension = ".json";
      languageId = "json";
      break;
    case "html":
      content = toHtml(findings, title);
      extension = ".html";
      languageId = "html";
      break;
  }

  const action = await vscode.window.showQuickPick(
    [
      { label: "$(file) Save to File", value: "save" },
      { label: "$(clippy) Copy to Clipboard", value: "clipboard" },
      { label: "$(eye) Preview in Editor", value: "preview" },
    ],
    { placeHolder: `Export as ${format.toUpperCase()}` }
  );

  if (!action) {
    return;
  }

  switch (action.value) {
    case "save": {
      const uri = await vscode.window.showSaveDialog({
        defaultUri: vscode.Uri.file(`compliance-report${extension}`),
        filters: {
          [format.toUpperCase()]: [extension.replace(".", "")],
        },
      });
      if (uri) {
        await vscode.workspace.fs.writeFile(
          uri,
          Buffer.from(content, "utf-8")
        );
        vscode.window.showInformationMessage(`Report saved to ${uri.fsPath}`);
      }
      break;
    }
    case "clipboard":
      await vscode.env.clipboard.writeText(content);
      vscode.window.showInformationMessage("Report copied to clipboard");
      break;
    case "preview": {
      const doc = await vscode.workspace.openTextDocument({
        language: languageId,
        content,
      });
      await vscode.window.showTextDocument(doc);
      break;
    }
  }
}

function toMarkdown(findings: ComplianceFinding[], title: string): string {
  const lines: string[] = [
    `# ${title}`,
    "",
    `**Generated**: ${new Date().toISOString()}`,
    `**Total Findings**: ${findings.length}`,
    "",
    "## Summary",
    "",
    "| Severity | Count |",
    "|----------|-------|",
    `| High | ${findings.filter((f) => f.severity === "high").length} |`,
    `| Medium | ${findings.filter((f) => f.severity === "medium").length} |`,
    `| Low | ${findings.filter((f) => f.severity === "low").length} |`,
    "",
    "## Findings",
    "",
  ];

  for (const f of findings) {
    lines.push(`### ${f.controlId}: ${f.title}`);
    lines.push("");
    lines.push(`**Severity**: ${f.severity.toUpperCase()}`);
    lines.push("");
    lines.push(f.description);
    lines.push("");
    lines.push(`**Recommendation**: ${f.recommendation}`);
    lines.push("");
    lines.push("---");
    lines.push("");
  }

  return lines.join("\n");
}

function toJson(findings: ComplianceFinding[]): string {
  return JSON.stringify(findings, null, 2);
}

function toHtml(findings: ComplianceFinding[], title: string): string {
  const severityColor = (s: string) =>
    s === "high" ? "#d32f2f" : s === "medium" ? "#f57c00" : "#388e3c";

  const rows = findings
    .map(
      (f) => `
    <tr>
      <td><span style="background:${severityColor(f.severity)};color:#fff;padding:2px 8px;border-radius:4px;font-weight:bold">${f.severity.toUpperCase()}</span></td>
      <td><code>${escapeHtml(f.controlId)}</code></td>
      <td>${escapeHtml(f.title)}</td>
      <td>${escapeHtml(f.description)}</td>
      <td>${escapeHtml(f.recommendation)}</td>
    </tr>`
    )
    .join("");

  return `<!DOCTYPE html>
<html><head><meta charset="UTF-8"><title>${escapeHtml(title)}</title>
<style>body{font-family:sans-serif;padding:24px}table{border-collapse:collapse;width:100%}th,td{border:1px solid #ddd;padding:8px;text-align:left}th{background:#f5f5f5}code{background:#eee;padding:2px 4px;border-radius:3px}</style>
</head><body>
<h1>${escapeHtml(title)}</h1>
<p>Generated: ${new Date().toISOString()}</p>
<table><thead><tr><th>Severity</th><th>Control</th><th>Title</th><th>Description</th><th>Recommendation</th></tr></thead>
<tbody>${rows}</tbody></table>
</body></html>`;
}

function escapeHtml(text: string): string {
  return text
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}
