/**
 * RMF Overview HTML Builder (US13, T172)
 *
 * Pure HTML/CSS rendering logic for the RMF overview panel.
 * No vscode dependency — can be tested in plain mocha.
 */

/**
 * RMF system data for the overview panel.
 */
export interface RmfSystemOverview {
  systemName: string;
  acronym?: string;
  systemType?: string;
  hostingEnvironment?: string;
  currentRmfStep?: string;
  rmfStepNumber?: number;
  missionCriticality?: string;
  impactLevel?: string;
  complianceScore?: number;
  activeAlerts?: number;
  isActive?: boolean;
  atoStatus?: string;
  atoExpiration?: string;
  categorization?: {
    fipsCategory?: string;
    confidentialityImpact?: string;
    integrityImpact?: string;
    availabilityImpact?: string;
  };
  controlBaseline?: {
    baselineName?: string;
    totalControls?: number;
    implementedControls?: number;
  };
}

/**
 * RMF step metadata for visual rendering.
 */
const RMF_STEPS = [
  { name: "Prepare", number: 1, icon: "📋", description: "Essential activities to manage security and privacy risks" },
  { name: "Categorize", number: 2, icon: "🏷️", description: "Categorize the system and information" },
  { name: "Select", number: 3, icon: "✅", description: "Select, tailor, and document controls" },
  { name: "Implement", number: 4, icon: "🔧", description: "Implement controls and document implementation" },
  { name: "Assess", number: 5, icon: "🔍", description: "Assess control effectiveness" },
  { name: "Authorize", number: 6, icon: "🛡️", description: "Authorization decision based on risk determination" },
  { name: "Monitor", number: 7, icon: "📊", description: "Continuous monitoring of controls and risk" },
];

/**
 * Build the complete HTML for the RMF overview webview.
 */
export function buildRmfOverviewHtml(data: RmfSystemOverview): string {
  const currentStep = RMF_STEPS.find(
    (s) => s.name.toLowerCase() === (data.currentRmfStep ?? "").toLowerCase()
  );
  const currentStepIndex = currentStep ? RMF_STEPS.indexOf(currentStep) : -1;

  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>RMF Overview</title>
  <style>
    ${getRmfOverviewStyles()}
  </style>
</head>
<body>
  <div class="container">
    ${buildHeaderSection(data)}
    ${buildRmfStepper(currentStepIndex)}
    ${buildDetailsGrid(data)}
    ${buildCategorizationSection(data)}
    ${buildBaselineSection(data)}
    ${buildAtoSection(data)}
    ${buildActionsSection()}
  </div>
  <script>
    ${getWebviewScript()}
  </script>
</body>
</html>`;
}

function buildHeaderSection(data: RmfSystemOverview): string {
  const statusClass = data.isActive !== false ? "badge-good" : "badge-attention";
  const statusText = data.isActive !== false ? "Active" : "Inactive";

  return `
    <div class="header">
      <div class="header-left">
        <h1>${escapeHtml(data.systemName)}</h1>
        ${data.acronym ? `<span class="acronym">(${escapeHtml(data.acronym)})</span>` : ""}
      </div>
      <div class="header-right">
        <span class="badge ${statusClass}">${statusText}</span>
      </div>
    </div>`;
}

function buildRmfStepper(currentStepIndex: number): string {
  const steps = RMF_STEPS.map((step, idx) => {
    let stepClass = "step";
    if (idx < currentStepIndex) stepClass += " step-complete";
    else if (idx === currentStepIndex) stepClass += " step-current";
    else stepClass += " step-upcoming";

    return `
      <div class="${stepClass}" data-step="${step.name}" onclick="handleStepClick('${step.name}')">
        <div class="step-icon">${step.icon}</div>
        <div class="step-name">${step.name}</div>
        <div class="step-number">Step ${step.number}</div>
      </div>`;
  }).join('<div class="step-connector"></div>');

  return `
    <div class="rmf-stepper">
      <h2>RMF Lifecycle</h2>
      <div class="stepper-track">${steps}</div>
    </div>`;
}

function buildDetailsGrid(data: RmfSystemOverview): string {
  const items = [
    { label: "System Type", value: data.systemType ?? "—" },
    { label: "Hosting", value: data.hostingEnvironment ?? "—" },
    { label: "Mission Criticality", value: data.missionCriticality ?? "—" },
    { label: "Impact Level", value: data.impactLevel ?? "—" },
    { label: "Current RMF Step", value: data.currentRmfStep ?? "—" },
    {
      label: "Compliance Score",
      value: data.complianceScore != null ? `${data.complianceScore}%` : "—",
      colorClass: data.complianceScore != null ? getScoreCssClass(data.complianceScore) : "",
    },
  ];

  const cells = items
    .map(
      (item) => `
      <div class="detail-item">
        <span class="detail-label">${item.label}</span>
        <span class="detail-value ${(item as { colorClass?: string }).colorClass ?? ""}">${escapeHtml(item.value)}</span>
      </div>`
    )
    .join("");

  return `
    <div class="details-grid">
      <h2>System Details</h2>
      <div class="grid">${cells}</div>
    </div>`;
}

function buildCategorizationSection(data: RmfSystemOverview): string {
  if (!data.categorization) return "";

  const cat = data.categorization;
  return `
    <div class="section">
      <h2>Security Categorization</h2>
      <div class="categorization-row">
        <div class="cat-item">
          <span class="cat-label">FIPS 199</span>
          <span class="cat-value">${escapeHtml(cat.fipsCategory ?? "N/A")}</span>
        </div>
        <div class="cia-grid">
          <div class="cia-item ${getImpactCssClass(cat.confidentialityImpact)}">
            <span class="cia-label">C</span>
            <span class="cia-value">${escapeHtml(cat.confidentialityImpact ?? "—")}</span>
          </div>
          <div class="cia-item ${getImpactCssClass(cat.integrityImpact)}">
            <span class="cia-label">I</span>
            <span class="cia-value">${escapeHtml(cat.integrityImpact ?? "—")}</span>
          </div>
          <div class="cia-item ${getImpactCssClass(cat.availabilityImpact)}">
            <span class="cia-label">A</span>
            <span class="cia-value">${escapeHtml(cat.availabilityImpact ?? "—")}</span>
          </div>
        </div>
      </div>
    </div>`;
}

function buildBaselineSection(data: RmfSystemOverview): string {
  if (!data.controlBaseline) return "";

  const bl = data.controlBaseline;
  const total = bl.totalControls ?? 0;
  const implemented = bl.implementedControls ?? 0;
  const pct = total > 0 ? Math.round((implemented / total) * 100) : 0;

  return `
    <div class="section">
      <h2>Control Baseline</h2>
      <div class="baseline-info">
        <span class="baseline-name">${escapeHtml(bl.baselineName ?? "N/A")}</span>
        <div class="progress-bar">
          <div class="progress-fill ${getScoreCssClass(pct)}" style="width: ${pct}%"></div>
        </div>
        <span class="progress-label">${implemented} / ${total} controls implemented (${pct}%)</span>
      </div>
    </div>`;
}

function buildAtoSection(data: RmfSystemOverview): string {
  if (!data.atoStatus) return "";

  const statusClass =
    data.atoStatus.toLowerCase() === "active"
      ? "badge-good"
      : data.atoStatus.toLowerCase() === "expired"
        ? "badge-attention"
        : "badge-warning";

  return `
    <div class="section">
      <h2>Authorization Status</h2>
      <div class="ato-row">
        <span class="badge ${statusClass}">${escapeHtml(data.atoStatus)}</span>
        ${data.atoExpiration ? `<span class="ato-expiry">Expires: ${escapeHtml(data.atoExpiration)}</span>` : ""}
        ${data.activeAlerts != null ? `<span class="alerts-badge ${(data.activeAlerts ?? 0) > 0 ? "badge-attention" : "badge-good"}">${data.activeAlerts} active alert${data.activeAlerts !== 1 ? "s" : ""}</span>` : ""}
      </div>
    </div>`;
}

function buildActionsSection(): string {
  return `
    <div class="actions">
      <button class="action-btn action-primary" onclick="handleAction('viewCompliance')">View Compliance Details</button>
      <button class="action-btn action-secondary" onclick="handleAction('refresh')">Refresh Data</button>
    </div>`;
}

export function getScoreCssClass(score: number): string {
  if (score >= 80) return "score-good";
  if (score >= 60) return "score-warning";
  return "score-attention";
}

export function getImpactCssClass(impact?: string): string {
  if (!impact) return "";
  const normalized = impact.toLowerCase();
  if (normalized === "high") return "impact-high";
  if (normalized === "moderate") return "impact-moderate";
  if (normalized === "low") return "impact-low";
  return "";
}

export function escapeHtml(str: string): string {
  return str
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}

function getRmfOverviewStyles(): string {
  return `
    :root {
      --bg: var(--vscode-editor-background);
      --fg: var(--vscode-editor-foreground);
      --border: var(--vscode-panel-border, #444);
      --accent: var(--vscode-textLink-foreground, #4fc1ff);
      --good: #4caf50;
      --warning: #ff9800;
      --attention: #f44336;
      --subtle: var(--vscode-descriptionForeground, #999);
    }

    body {
      background: var(--bg);
      color: var(--fg);
      font-family: var(--vscode-font-family, 'Segoe UI', sans-serif);
      font-size: var(--vscode-font-size, 13px);
      margin: 0;
      padding: 0;
    }

    .container { max-width: 900px; margin: 0 auto; padding: 20px; }

    .header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 20px; }
    .header-left { display: flex; align-items: baseline; gap: 8px; }
    .header-left h1 { margin: 0; font-size: 1.6em; }
    .acronym { color: var(--subtle); font-size: 0.9em; }

    .badge { padding: 3px 10px; border-radius: 12px; font-size: 0.8em; font-weight: bold; }
    .badge-good { background: var(--good); color: #fff; }
    .badge-warning { background: var(--warning); color: #000; }
    .badge-attention { background: var(--attention); color: #fff; }

    h2 { font-size: 1.1em; margin: 16px 0 8px; border-bottom: 1px solid var(--border); padding-bottom: 4px; }

    /* RMF Stepper */
    .rmf-stepper { margin: 20px 0; }
    .stepper-track { display: flex; align-items: center; justify-content: center; gap: 0; flex-wrap: wrap; }
    .step { display: flex; flex-direction: column; align-items: center; padding: 10px; border-radius: 8px; cursor: pointer; min-width: 70px; transition: background 0.2s; }
    .step:hover { background: rgba(255, 255, 255, 0.05); }
    .step-icon { font-size: 1.5em; margin-bottom: 4px; }
    .step-name { font-size: 0.85em; font-weight: bold; }
    .step-number { font-size: 0.7em; color: var(--subtle); }
    .step-complete .step-name { color: var(--good); }
    .step-current { background: rgba(79, 193, 255, 0.12); border: 1px solid var(--accent); }
    .step-current .step-name { color: var(--accent); font-weight: bold; }
    .step-upcoming .step-name { color: var(--subtle); }
    .step-connector { width: 24px; height: 2px; background: var(--border); align-self: center; }

    /* Details Grid */
    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: 12px; }
    .detail-item { display: flex; flex-direction: column; gap: 2px; }
    .detail-label { font-size: 0.8em; color: var(--subtle); }
    .detail-value { font-weight: bold; }
    .score-good { color: var(--good); }
    .score-warning { color: var(--warning); }
    .score-attention { color: var(--attention); }

    /* Categorization */
    .categorization-row { display: flex; align-items: center; gap: 24px; flex-wrap: wrap; }
    .cat-item { display: flex; flex-direction: column; gap: 2px; }
    .cat-label { font-size: 0.8em; color: var(--subtle); }
    .cat-value { font-size: 1.4em; font-weight: bold; }
    .cia-grid { display: flex; gap: 16px; }
    .cia-item { display: flex; flex-direction: column; align-items: center; padding: 6px 12px; border-radius: 6px; border: 1px solid var(--border); }
    .cia-label { font-size: 0.75em; color: var(--subtle); font-weight: bold; }
    .cia-value { font-weight: bold; }
    .impact-high { border-color: var(--attention); }
    .impact-high .cia-value { color: var(--attention); }
    .impact-moderate { border-color: var(--warning); }
    .impact-moderate .cia-value { color: var(--warning); }
    .impact-low { border-color: var(--good); }
    .impact-low .cia-value { color: var(--good); }

    /* Baseline */
    .baseline-info { display: flex; flex-direction: column; gap: 6px; }
    .baseline-name { font-weight: bold; }
    .progress-bar { height: 8px; background: var(--border); border-radius: 4px; overflow: hidden; }
    .progress-fill { height: 100%; border-radius: 4px; transition: width 0.3s; }
    .progress-fill.score-good { background: var(--good); }
    .progress-fill.score-warning { background: var(--warning); }
    .progress-fill.score-attention { background: var(--attention); }
    .progress-label { font-size: 0.8em; color: var(--subtle); }

    /* ATO */
    .ato-row { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
    .ato-expiry { color: var(--subtle); font-size: 0.9em; }
    .alerts-badge { font-size: 0.8em; }

    /* Actions */
    .actions { margin-top: 20px; display: flex; gap: 8px; }
    .action-btn { padding: 8px 16px; border: none; border-radius: 4px; cursor: pointer; font-size: 0.9em; }
    .action-primary { background: var(--accent); color: #000; font-weight: bold; }
    .action-secondary { background: transparent; border: 1px solid var(--border); color: var(--fg); }
    .action-btn:hover { opacity: 0.9; }

    .section { margin: 16px 0; }
  `;
}

function getWebviewScript(): string {
  return `
    const vscode = acquireVsCodeApi();

    function handleStepClick(step) {
      vscode.postMessage({ command: 'viewStep', step: step });
    }

    function handleAction(action) {
      vscode.postMessage({ command: action });
    }
  `;
}
