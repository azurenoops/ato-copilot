/**
 * Error Adaptive Card Builder (FR-042)
 *
 * Displays error message with help text in an Adaptive Card.
 */

export interface ErrorData {
  errorMessage: string;
  helpText?: string;
}

export function buildErrorCard(data: ErrorData): Record<string, unknown> {
  return {
    $schema: "http://adaptivecards.io/schemas/adaptive-card.json",
    type: "AdaptiveCard",
    version: "1.5",
    body: [
      {
        type: "TextBlock",
        text: "⚠️ Error",
        weight: "Bolder",
        color: "Attention",
      },
      {
        type: "TextBlock",
        text: data.errorMessage,
        wrap: true,
      },
      ...(data.helpText
        ? [
            {
              type: "TextBlock",
              text: `💡 ${data.helpText}`,
              wrap: true,
              isSubtle: true,
            },
          ]
        : []),
    ],
  };
}
