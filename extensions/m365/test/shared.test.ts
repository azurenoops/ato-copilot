import { expect } from "chai";
import { buildAgentAttribution, buildSuggestionButtons } from "../src/cards/shared";

describe("Shared Card Utilities (FR-013, FR-023a)", () => {
  describe("buildAgentAttribution", () => {
    it("should return null when no agent provided", () => {
      expect(buildAgentAttribution()).to.be.null;
      expect(buildAgentAttribution(undefined)).to.be.null;
    });

    it("should return TextBlock with agent name", () => {
      const attr = buildAgentAttribution("ComplianceAgent");
      expect(attr).to.not.be.null;
      expect(attr!.type).to.equal("TextBlock");
      expect(attr!.text).to.equal("Processed by: ComplianceAgent");
      expect(attr!.color).to.equal("Accent");
      expect(attr!.size).to.equal("Small");
    });

    it("should right-align the attribution", () => {
      const attr = buildAgentAttribution("TestAgent");
      expect(attr!.horizontalAlignment).to.equal("Right");
    });
  });

  describe("buildSuggestionButtons", () => {
    it("should return empty array when no suggestions", () => {
      expect(buildSuggestionButtons()).to.deep.equal([]);
      expect(buildSuggestionButtons(undefined)).to.deep.equal([]);
      expect(buildSuggestionButtons([])).to.deep.equal([]);
    });

    it("should create Action.Submit for each suggestion", () => {
      const buttons = buildSuggestionButtons(["Run scan", "Show report"]);
      expect(buttons).to.have.length(2);
      expect(buttons[0].type).to.equal("Action.Submit");
      expect(buttons[0].title).to.equal("Run scan");
      expect(buttons[0].data).to.deep.include({ message: "Run scan" });
    });

    it("should include conversationId in button data", () => {
      const buttons = buildSuggestionButtons(["Action"], "conv-123");
      expect(buttons[0].data).to.deep.include({
        message: "Action",
        conversationId: "conv-123",
      });
    });

    it("should default conversationId to empty string", () => {
      const buttons = buildSuggestionButtons(["Action"]);
      expect((buttons[0].data as any).conversationId).to.equal("");
    });
  });
});
