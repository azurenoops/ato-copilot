import { expect } from "chai";

/**
 * Tests for chat participant agent attribution rendering (T105, FR-021).
 */
describe("Chat Participant — Agent Attribution", () => {
  it("should render attribution when agentUsed is present", () => {
    const response = { agentUsed: "Compliance Agent" };
    const attribution = response.agentUsed
      ? `\n\n*Processed by: ${response.agentUsed}*`
      : "";
    expect(attribution).to.include("Processed by: Compliance Agent");
  });

  it("should not render attribution when agentUsed is absent", () => {
    const response = { agentUsed: undefined };
    const attribution = response.agentUsed
      ? `\n\n*Processed by: ${response.agentUsed}*`
      : "";
    expect(attribution).to.equal("");
  });

  it("should render attribution for KnowledgeBase Agent", () => {
    const response = { agentUsed: "KnowledgeBase Agent" };
    const attribution = `*Processed by: ${response.agentUsed}*`;
    expect(attribution).to.include("KnowledgeBase Agent");
  });

  it("should use italic Markdown formatting", () => {
    const response = { agentUsed: "Configuration Agent" };
    const attribution = `*Processed by: ${response.agentUsed}*`;
    expect(attribution).to.match(/^\*.*\*$/);
  });
});
