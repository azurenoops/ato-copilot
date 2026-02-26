/**
 * Generic Response Adaptive Card Builder
 *
 * Fallback card for unclassified intent types — renders
 * the plain-text response from the MCP server.
 */

export interface GenericData {
  response: string;
  agentUsed?: string;
}

export function buildGenericCard(data: GenericData): Record<string, unknown> {
  const bodyItems: Record<string, unknown>[] = [
    {
      type: "TextBlock",
      text: "ATO Copilot",
      weight: "Bolder",
      size: "Large",
    },
    {
      type: "TextBlock",
      text: data.response,
      wrap: true,
    },
  ];

  if (data.agentUsed) {
    bodyItems.push({
      type: "TextBlock",
      text: `Powered by ${data.agentUsed}`,
      isSubtle: true,
      size: "Small",
      spacing: "Medium",
    });
  }

  return {
    $schema: "http://adaptivecards.io/schemas/adaptive-card.json",
    type: "AdaptiveCard",
    version: "1.5",
    body: bodyItems,
  };
}
