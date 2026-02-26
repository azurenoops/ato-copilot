/**
 * Deployment Result Adaptive Card Builder
 *
 * Shows deployment status, logs, and result details.
 */

export interface DeploymentData {
  deploymentStatus: string;
  response: string;
  deploymentId?: string;
  logs?: string[];
}

export function buildDeploymentCard(data: DeploymentData): Record<string, unknown> {
  const statusColor =
    data.deploymentStatus === "Succeeded"
      ? "Good"
      : data.deploymentStatus === "Failed"
        ? "Attention"
        : "Warning";

  const bodyItems: Record<string, unknown>[] = [
    {
      type: "TextBlock",
      text: "ATO Copilot — Deployment Result",
      weight: "Bolder",
      size: "Large",
    },
    {
      type: "ColumnSet",
      columns: [
        {
          type: "Column",
          width: "auto",
          items: [
            { type: "TextBlock", text: "Status:", weight: "Bolder" },
          ],
        },
        {
          type: "Column",
          width: "stretch",
          items: [
            {
              type: "TextBlock",
              text: data.deploymentStatus,
              color: statusColor,
              weight: "Bolder",
            },
          ],
        },
      ],
    },
    {
      type: "TextBlock",
      text: data.response,
      wrap: true,
    },
  ];

  if (data.deploymentId) {
    bodyItems.push({
      type: "FactSet",
      facts: [{ title: "Deployment ID", value: data.deploymentId }],
    });
  }

  if (data.logs && data.logs.length > 0) {
    bodyItems.push({
      type: "TextBlock",
      text: "Deployment Logs",
      weight: "Bolder",
      spacing: "Medium",
    });
    bodyItems.push({
      type: "TextBlock",
      text: data.logs.join("\n"),
      wrap: true,
      fontType: "Monospace",
      size: "Small",
    });
  }

  return {
    $schema: "http://adaptivecards.io/schemas/adaptive-card.json",
    type: "AdaptiveCard",
    version: "1.5",
    body: bodyItems,
  };
}
