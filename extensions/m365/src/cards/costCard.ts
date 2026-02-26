/**
 * Cost Estimate Adaptive Card Builder
 *
 * Displays cost breakdown for cloud resource estimates.
 */

export interface CostData {
  estimatedCost: number;
  response: string;
  breakdown?: Array<{ item: string; cost: number }>;
  currency?: string;
}

export function buildCostCard(data: CostData): Record<string, unknown> {
  const currency = data.currency || "USD";

  const bodyItems: Record<string, unknown>[] = [
    {
      type: "TextBlock",
      text: "ATO Copilot — Cost Estimate",
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
      text: "Estimated Monthly Cost",
      weight: "Bolder",
      spacing: "Medium",
    },
    {
      type: "TextBlock",
      text: `$${data.estimatedCost.toLocaleString()} ${currency}`,
      size: "ExtraLarge",
      weight: "Bolder",
      color: "Good",
    },
  ];

  if (data.breakdown && data.breakdown.length > 0) {
    bodyItems.push({
      type: "FactSet",
      facts: data.breakdown.map((item) => ({
        title: item.item,
        value: `$${item.cost.toLocaleString()} ${currency}`,
      })),
    });
  }

  return {
    $schema: "http://adaptivecards.io/schemas/adaptive-card.json",
    type: "AdaptiveCard",
    version: "1.5",
    body: bodyItems,
  };
}
