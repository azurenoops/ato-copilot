/**
 * Resource List Adaptive Card Builder
 *
 * Displays a table of discovered resources with name, type, and status.
 */

export interface ResourceData {
  resources: Array<{ name: string; type: string; status: string }>;
  response: string;
}

export function buildResourceCard(data: ResourceData): Record<string, unknown> {
  const bodyItems: Record<string, unknown>[] = [
    {
      type: "TextBlock",
      text: "ATO Copilot — Resource Discovery",
      weight: "Bolder",
      size: "Large",
    },
    {
      type: "TextBlock",
      text: data.response,
      wrap: true,
    },
    {
      type: "TextBlock",
      text: `${data.resources.length} resource(s) found`,
      weight: "Bolder",
      spacing: "Medium",
    },
  ];

  // Header row
  bodyItems.push({
    type: "ColumnSet",
    columns: [
      {
        type: "Column",
        width: "stretch",
        items: [{ type: "TextBlock", text: "Name", weight: "Bolder" }],
      },
      {
        type: "Column",
        width: "stretch",
        items: [{ type: "TextBlock", text: "Type", weight: "Bolder" }],
      },
      {
        type: "Column",
        width: "auto",
        items: [{ type: "TextBlock", text: "Status", weight: "Bolder" }],
      },
    ],
  });

  // Data rows
  for (const resource of data.resources) {
    const statusColor =
      resource.status === "Running" || resource.status === "Healthy"
        ? "Good"
        : resource.status === "Stopped" || resource.status === "Unhealthy"
          ? "Attention"
          : "Default";

    bodyItems.push({
      type: "ColumnSet",
      separator: true,
      columns: [
        {
          type: "Column",
          width: "stretch",
          items: [{ type: "TextBlock", text: resource.name }],
        },
        {
          type: "Column",
          width: "stretch",
          items: [{ type: "TextBlock", text: resource.type }],
        },
        {
          type: "Column",
          width: "auto",
          items: [
            { type: "TextBlock", text: resource.status, color: statusColor },
          ],
        },
      ],
    });
  }

  return {
    $schema: "http://adaptivecards.io/schemas/adaptive-card.json",
    type: "AdaptiveCard",
    version: "1.5",
    body: bodyItems,
  };
}
