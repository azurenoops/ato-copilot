import { expect } from "chai";
import { buildClarificationCard } from "../src/cards/clarificationCard";

describe("Clarification Card (FR-010a)", () => {
  it("should produce Adaptive Card v1.5 JSON", () => {
    const card = buildClarificationCard({
      followUpPrompt: "Please provide more details",
      missingFields: ["subscriptionId"],
    });
    expect(card.type).to.equal("AdaptiveCard");
    expect(card.version).to.equal("1.5");
  });

  it("should display follow-up prompt text", () => {
    const card = buildClarificationCard({
      followUpPrompt: "Which subscription should I scan?",
      missingFields: ["subscription"],
    });
    const body = card.body as any[];
    const prompt = body.find(
      (b: any) => b.text === "Which subscription should I scan?"
    );
    expect(prompt).to.exist;
  });

  it("should create Input.Text fields for each missing field", () => {
    const card = buildClarificationCard({
      followUpPrompt: "Need info",
      missingFields: ["subscriptionId", "resourceGroup", "environment"],
    });
    const body = card.body as any[];
    const inputs = body.filter((b: any) => b.type === "Input.Text");
    expect(inputs).to.have.length(3);
    expect(inputs[0].id).to.equal("subscriptionId");
    expect(inputs[1].id).to.equal("resourceGroup");
    expect(inputs[2].id).to.equal("environment");
  });

  it("should include Submit action", () => {
    const card = buildClarificationCard({
      followUpPrompt: "More info needed",
      missingFields: ["field1"],
    });
    const actions = card.actions as any[];
    const submitAction = actions?.find((a: any) => a.title === "Submit");
    expect(submitAction).to.exist;
    expect(submitAction.data.action).to.equal("drillDown");
  });

  it("should include agent attribution", () => {
    const card = buildClarificationCard({
      followUpPrompt: "Prompt",
      missingFields: ["field"],
      agentUsed: "ComplianceAgent",
    });
    const body = card.body as any[];
    const attr = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("Processed by:")
    );
    expect(attr).to.exist;
  });
});
