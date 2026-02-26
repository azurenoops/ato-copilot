/**
 * Follow-Up Adaptive Card Builder (FR-041)
 *
 * Prompts for missing information with numbered fields
 * and quick-reply action buttons.
 */

export interface FollowUpData {
  followUpPrompt: string;
  missingFields: string[];
}

export function buildFollowUpCard(data: FollowUpData): Record<string, unknown> {
  const numberedFields = data.missingFields
    .map((field, i) => `${i + 1}. ${field}`)
    .join("\n");

  const actions = data.missingFields.map((field) => ({
    type: "Action.Submit",
    title: field,
    data: { quickReply: field },
  }));

  return {
    $schema: "http://adaptivecards.io/schemas/adaptive-card.json",
    type: "AdaptiveCard",
    version: "1.5",
    body: [
      {
        type: "TextBlock",
        text: data.followUpPrompt,
        wrap: true,
      },
      {
        type: "TextBlock",
        text: "Missing information:",
        weight: "Bolder",
        spacing: "Medium",
      },
      {
        type: "TextBlock",
        text: numberedFields,
        wrap: true,
      },
    ],
    actions,
  };
}
