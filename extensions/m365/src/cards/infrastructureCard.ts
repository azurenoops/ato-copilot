/**
 * Infrastructure Result Adaptive Card Builder (FR-044)
 *
 * Shows infrastructure provisioning results with a
 * "View in Azure Portal" button linking to portal.azure.us.
 */

export interface InfrastructureData {
  resourceId: string;
  response: string;
  resourceType?: string;
  status?: string;
}

export function buildInfrastructureCard(data: InfrastructureData): Record<string, unknown> {
  const portalUrl = `https://portal.azure.us/#resource/${data.resourceId}`;

  return {
    $schema: "http://adaptivecards.io/schemas/adaptive-card.json",
    type: "AdaptiveCard",
    version: "1.5",
    body: [
      {
        type: "TextBlock",
        text: "ATO Copilot — Infrastructure Result",
        weight: "Bolder",
        size: "Large",
      },
      {
        type: "TextBlock",
        text: data.response,
        wrap: true,
      },
      ...(data.resourceType
        ? [
            {
              type: "FactSet",
              facts: [
                { title: "Resource Type", value: data.resourceType },
                { title: "Resource ID", value: data.resourceId },
                ...(data.status
                  ? [{ title: "Status", value: data.status }]
                  : []),
              ],
            },
          ]
        : []),
    ],
    actions: [
      {
        type: "Action.OpenUrl",
        title: "View in Azure Portal",
        url: portalUrl,
      },
    ],
  };
}
