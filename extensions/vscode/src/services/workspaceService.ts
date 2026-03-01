import * as vscode from "vscode";

/**
 * Type-based folder mapping for template saves (FR-036).
 */
const TYPE_FOLDER_MAP: Record<string, string> = {
  bicep: "bicep",
  terraform: "terraform",
  kubernetes: "kubernetes",
  powershell: "scripts",
  arm: "arm",
};

/**
 * Template data from MCP response.
 */
export interface TemplateData {
  name: string;
  type: string;
  content: string;
  language: string;
}

/**
 * Save a generated IaC template to the workspace.
 * Uses type-based folder mapping and conflict resolution (US5, Scenario 5).
 */
export async function saveTemplate(template: TemplateData): Promise<void> {
  const workspaceFolders = vscode.workspace.workspaceFolders;
  if (!workspaceFolders || workspaceFolders.length === 0) {
    vscode.window.showWarningMessage(
      "No workspace folder is open — cannot save template"
    );
    return;
  }

  const rootUri = workspaceFolders[0].uri;
  const folder = TYPE_FOLDER_MAP[template.type] ?? "templates";
  const targetDir = vscode.Uri.joinPath(rootUri, folder);
  const targetFile = vscode.Uri.joinPath(targetDir, template.name);

  // Ensure directory exists
  try {
    await vscode.workspace.fs.createDirectory(targetDir);
  } catch {
    // Directory may already exist
  }

  // Check for existing file — conflict resolution
  let exists = false;
  try {
    await vscode.workspace.fs.stat(targetFile);
    exists = true;
  } catch {
    // File doesn't exist — proceed
  }

  if (exists) {
    const choice = await vscode.window.showWarningMessage(
      `${template.name} already exists in ${folder}/`,
      "Overwrite",
      "Cancel",
      "Save As New"
    );

    if (choice === "Cancel" || !choice) {
      return;
    }

    if (choice === "Save As New") {
      const uri = await vscode.window.showSaveDialog({
        defaultUri: targetFile,
      });
      if (!uri) {
        return;
      }
      await vscode.workspace.fs.writeFile(
        uri,
        Buffer.from(template.content, "utf-8")
      );
      vscode.window.showInformationMessage(`Template saved to ${uri.fsPath}`);
      return;
    }

    // Overwrite — fall through
  }

  await vscode.workspace.fs.writeFile(
    targetFile,
    Buffer.from(template.content, "utf-8")
  );
  vscode.window.showInformationMessage(
    `Template saved to ${folder}/${template.name}`
  );
}
