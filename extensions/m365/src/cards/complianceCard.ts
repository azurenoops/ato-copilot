/**
 * Compliance Assessment Adaptive Card Builder (FR-043)
 *
 * Builds an Adaptive Card v1.5 showing compliance score,
 * passed/warning/failed control counts, and action buttons.
 *
 * Score color thresholds:
 * - ≥80% → "Good" (green)
 * - ≥60% → "Warning" (orange)
 * - <60% → "Attention" (red)
 */

export interface ComplianceData {
  complianceScore: number;
  passedControls: number;
  warningControls: number;
  failedControls: number;
  response?: string;
}

function getScoreColor(score: number): string {
  if (score >= 80) return "Good";
  if (score >= 60) return "Warning";
  return "Attention";
}

export function buildComplianceCard(data: ComplianceData): Record<string, unknown> {
  const scoreColor = getScoreColor(data.complianceScore);

  return {
    $schema: "http://adaptivecards.io/schemas/adaptive-card.json",
    type: "AdaptiveCard",
    version: "1.5",
    body: [
      {
        type: "TextBlock",
        text: "ATO Copilot — Compliance Assessment",
        weight: "Bolder",
        size: "Large",
      },
      ...(data.response
        ? [{ type: "TextBlock", text: data.response, wrap: true }]
        : []),
      {
        type: "TextBlock",
        text: "Overall Compliance Score",
        weight: "Bolder",
        spacing: "Medium",
      },
      {
        type: "TextBlock",
        text: `${data.complianceScore}%`,
        size: "ExtraLarge",
        color: scoreColor,
        weight: "Bolder",
      },
      {
        type: "ColumnSet",
        columns: [
          {
            type: "Column",
            width: "stretch",
            items: [
              { type: "TextBlock", text: "✅ Passed", weight: "Bolder" },
              { type: "TextBlock", text: `${data.passedControls}` },
            ],
          },
          {
            type: "Column",
            width: "stretch",
            items: [
              { type: "TextBlock", text: "⚠️ Warning", weight: "Bolder" },
              { type: "TextBlock", text: `${data.warningControls}` },
            ],
          },
          {
            type: "Column",
            width: "stretch",
            items: [
              { type: "TextBlock", text: "❌ Failed", weight: "Bolder" },
              { type: "TextBlock", text: `${data.failedControls}` },
            ],
          },
        ],
      },
    ],
    actions: [
      {
        type: "Action.OpenUrl",
        title: "View Full Report",
        url: "https://ato-copilot.azurewebsites.us/reports/latest",
      },
      {
        type: "Action.Submit",
        title: "Generate Remediation Plan",
        data: { action: "remediate" },
      },
    ],
  };
}
