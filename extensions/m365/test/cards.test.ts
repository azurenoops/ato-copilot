import { expect } from "chai";
import {
  buildComplianceCard,
  buildGenericCard,
  buildErrorCard,
  buildFollowUpCard,
} from "../src/cards";

describe("Adaptive Card Builders", () => {
  describe("Common Card Properties", () => {
    it("should produce Adaptive Card v1.5 JSON for all card types", () => {
      const cards = [
        buildComplianceCard({ complianceScore: 85, passedControls: 10, warningControls: 2, failedControls: 1 }),
        buildGenericCard({ response: "Hello" }),
        buildErrorCard({ errorMessage: "Error occurred" }),
        buildFollowUpCard({ followUpPrompt: "Need more info", missingFields: ["subscription"] }),
      ];

      for (const card of cards) {
        expect(card.type).to.equal("AdaptiveCard");
        expect(card.version).to.equal("1.5");
        expect(card.$schema).to.equal("http://adaptivecards.io/schemas/adaptive-card.json");
        expect(card.body).to.be.an("array").that.is.not.empty;
      }
    });
  });

  describe("Compliance Card (FR-043)", () => {
    it("should show score ≥80% in Good (green) color", () => {
      const card = buildComplianceCard({
        complianceScore: 85,
        passedControls: 17,
        warningControls: 2,
        failedControls: 1,
      });
      const body = card.body as any[];
      const scoreBlock = body.find(
        (b: any) => b.type === "TextBlock" && b.text === "85%"
      );
      expect(scoreBlock).to.exist;
      expect(scoreBlock.color).to.equal("Good");
    });

    it("should show score ≥60% <80% in Warning (orange) color", () => {
      const card = buildComplianceCard({
        complianceScore: 65,
        passedControls: 13,
        warningControls: 4,
        failedControls: 3,
      });
      const body = card.body as any[];
      const scoreBlock = body.find(
        (b: any) => b.type === "TextBlock" && b.text === "65%"
      );
      expect(scoreBlock).to.exist;
      expect(scoreBlock.color).to.equal("Warning");
    });

    it("should show score <60% in Attention (red) color", () => {
      const card = buildComplianceCard({
        complianceScore: 45,
        passedControls: 9,
        warningControls: 3,
        failedControls: 8,
      });
      const body = card.body as any[];
      const scoreBlock = body.find(
        (b: any) => b.type === "TextBlock" && b.text === "45%"
      );
      expect(scoreBlock).to.exist;
      expect(scoreBlock.color).to.equal("Attention");
    });

    it("should include passed/warning/failed column counts", () => {
      const card = buildComplianceCard({
        complianceScore: 80,
        passedControls: 16,
        warningControls: 2,
        failedControls: 2,
      });
      const body = card.body as any[];
      const columnSet = body.find((b: any) => b.type === "ColumnSet");
      expect(columnSet).to.exist;
      expect(columnSet.columns).to.have.length(3);

      const passedCol = columnSet.columns[0];
      expect(passedCol.items[0].text).to.include("Passed");
      expect(passedCol.items[1].text).to.equal("16");

      const warningCol = columnSet.columns[1];
      expect(warningCol.items[0].text).to.include("Warning");
      expect(warningCol.items[1].text).to.equal("2");

      const failedCol = columnSet.columns[2];
      expect(failedCol.items[0].text).to.include("Failed");
      expect(failedCol.items[1].text).to.equal("2");
    });

    it("should include View Full Report and Generate Remediation Plan actions", () => {
      const card = buildComplianceCard({
        complianceScore: 90,
        passedControls: 18,
        warningControls: 1,
        failedControls: 1,
      });
      const actions = card.actions as any[];
      expect(actions).to.have.length(2);
      expect(actions[0].title).to.equal("View Full Report");
      expect(actions[0].type).to.equal("Action.OpenUrl");
      expect(actions[1].title).to.equal("Generate Remediation Plan");
      expect(actions[1].type).to.equal("Action.Submit");
      expect(actions[1].data.action).to.equal("remediate");
    });
  });

  describe("Generic Card", () => {
    it("should display response text", () => {
      const card = buildGenericCard({ response: "Hello from ATO Copilot" });
      const body = card.body as any[];
      const textBlock = body.find(
        (b: any) => b.text === "Hello from ATO Copilot"
      );
      expect(textBlock).to.exist;
    });

    it("should show agent attribution when provided", () => {
      const card = buildGenericCard({
        response: "Answer",
        agentUsed: "ComplianceAgent",
      });
      const body = card.body as any[];
      const attribution = body.find(
        (b: any) =>
          b.type === "TextBlock" && typeof b.text === "string" && b.text.includes("Processed by: ComplianceAgent")
      );
      expect(attribution).to.exist;
    });
  });

  describe("Error Card", () => {
    it("should display error message", () => {
      const card = buildErrorCard({ errorMessage: "Something went wrong" });
      const body = card.body as any[];
      const errorBlock = body.find(
        (b: any) => b.text === "Something went wrong"
      );
      expect(errorBlock).to.exist;
    });

    it("should show help text when provided", () => {
      const card = buildErrorCard({
        errorMessage: "Error",
        helpText: "Try again later",
      });
      const body = card.body as any[];
      const helpBlock = body.find(
        (b: any) => typeof b.text === "string" && b.text.includes("Try again later")
      );
      expect(helpBlock).to.exist;
    });

    it("should display error header in Attention color", () => {
      const card = buildErrorCard({ errorMessage: "Error" });
      const body = card.body as any[];
      const header = body.find(
        (b: any) => b.type === "TextBlock" && b.color === "Attention"
      );
      expect(header).to.exist;
    });
  });

  describe("Follow-Up Card (FR-041)", () => {
    it("should render missing fields as numbered list", () => {
      const card = buildFollowUpCard({
        followUpPrompt: "I need more details",
        missingFields: ["Subscription ID", "Resource Group"],
      });
      const body = card.body as any[];
      const fieldsBlock = body.find(
        (b: any) =>
          b.type === "TextBlock" && typeof b.text === "string" && b.text.includes("1. Subscription ID")
      );
      expect(fieldsBlock).to.exist;
      expect(fieldsBlock.text).to.include("2. Resource Group");
    });

    it("should create quick-reply actions for each missing field", () => {
      const card = buildFollowUpCard({
        followUpPrompt: "Need more info",
        missingFields: ["field1", "field2", "field3"],
      });
      const actions = card.actions as any[];
      expect(actions).to.have.length(3);
      expect(actions[0].title).to.equal("field1");
      expect(actions[0].data.quickReply).to.equal("field1");
      expect(actions[1].title).to.equal("field2");
      expect(actions[2].title).to.equal("field3");
    });

    it("should display follow-up prompt text", () => {
      const card = buildFollowUpCard({
        followUpPrompt: "What subscription should I scan?",
        missingFields: ["subscription"],
      });
      const body = card.body as any[];
      const promptBlock = body.find(
        (b: any) => b.text === "What subscription should I scan?"
      );
      expect(promptBlock).to.exist;
    });
  });
});
