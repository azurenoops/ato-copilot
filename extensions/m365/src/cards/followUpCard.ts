/**
 * Follow-Up Adaptive Card Builder (FR-005, FR-041)
 *
 * Prompts for missing information with numbered fields,
 * quick-reply action buttons, and agent attribution footer.
 */

import { buildAgentAttribution, buildSuggestionButtons } from "./shared";

export interface FollowUpData {
  followUpPrompt: string;
  missingFields: string[];
  agentUsed?: string;
  suggestions?: string[];
  conversationId?: string;
}

export function buildFollowUpCard(data: FollowUpData): Record<string, unknown> {
  const numberedFields = data.missingFields
    .map((field, i) => `${i + 1}. ${field}`)
    .join("\n");

  const quickReplyActions = data.missingFields.map((field) => ({
    type: "Action.Submit",
    title: field,
    data: { quickReply: field, conversationId: data.conversationId ?? "" },
  }));

  const bodyItems: Record<string, unknown>[] = [
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
  ];

  const attribution = buildAgentAttribution(data.agentUsed);
  if (attribution) bodyItems.push(attribution);

  const suggestionActions = buildSuggestionButtons(data.suggestions, data.conversationId);

  return {
    $schema: "http://adaptivecards.io/schemas/adaptive-card.json",
    type: "AdaptiveCard",
    version: "1.5",
    body: bodyItems,
    actions: [...quickReplyActions, ...suggestionActions],
  };
}
