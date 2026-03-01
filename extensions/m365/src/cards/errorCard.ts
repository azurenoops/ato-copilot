/**
 * Error Adaptive Card Builder (FR-007, FR-042)
 *
 * Displays error with errorCode badge, message, suggestion, retry button,
 * and agent attribution footer.
 */

import { buildAgentAttribution } from "./shared";

export interface ErrorData {
  errorCode?: string;
  errorMessage: string;
  helpText?: string;
  suggestion?: string;
  agentUsed?: string;
}

export function buildErrorCard(data: ErrorData): Record<string, unknown> {
  const bodyItems: Record<string, unknown>[] = [
    {
      type: "TextBlock",
      text: "⚠️ Error",
      weight: "Bolder",
      color: "Attention",
    },
  ];

  if (data.errorCode) {
    bodyItems.push({
      type: "TextBlock",
      text: `Code: ${data.errorCode}`,
      size: "Small",
      isSubtle: true,
      spacing: "None",
    });
  }

  bodyItems.push({
    type: "TextBlock",
    text: data.errorMessage,
    wrap: true,
  });

  if (data.helpText) {
    bodyItems.push({
      type: "TextBlock",
      text: `💡 ${data.helpText}`,
      wrap: true,
      isSubtle: true,
    });
  }

  if (data.suggestion) {
    bodyItems.push({
      type: "TextBlock",
      text: `💡 ${data.suggestion}`,
      wrap: true,
      isSubtle: true,
    });
  }

  const attribution = buildAgentAttribution(data.agentUsed);
  if (attribution) bodyItems.push(attribution);

  return {
    $schema: "http://adaptivecards.io/schemas/adaptive-card.json",
    type: "AdaptiveCard",
    version: "1.5",
    body: bodyItems,
    actions: [
      {
        type: "Action.Submit",
        title: "🔄 Retry",
        data: { action: "retry" },
      },
    ],
  };
}
