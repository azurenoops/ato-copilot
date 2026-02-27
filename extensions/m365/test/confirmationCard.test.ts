import { expect } from "chai";
import { buildConfirmationCard } from "../src/cards/confirmationCard";

describe("Confirmation Card (FR-018e)", () => {
  it("should produce Adaptive Card v1.5 JSON", () => {
    const card = buildConfirmationCard({ findingId: "F-001" });
    expect(card.type).to.equal("AdaptiveCard");
    expect(card.version).to.equal("1.5");
  });

  it("should display finding ID in FactSet", () => {
    const card = buildConfirmationCard({ findingId: "F-042" });
    const body = card.body as any[];
    const factSet = body.find((b: any) => b.type === "FactSet");
    expect(factSet).to.exist;
    const findingFact = factSet.facts.find((f: any) => f.value === "F-042");
    expect(findingFact).to.exist;
  });

  it("should show script preview when provided", () => {
    const card = buildConfirmationCard({
      findingId: "F-001",
      scriptPreview: "Set-AzStorageAccount -EnableEncryption $true",
    });
    const body = card.body as any[];
    const scriptBlock = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("Set-AzStorageAccount")
    );
    expect(scriptBlock).to.exist;
  });

  it("should display risk level", () => {
    const card = buildConfirmationCard({
      findingId: "F-001",
      riskLevel: "High",
    });
    const body = card.body as any[];
    const factSet = body.find((b: any) => b.type === "FactSet");
    expect(factSet).to.exist;
    const riskFact = factSet.facts.find((f: any) => f.value === "High");
    expect(riskFact).to.exist;
  });

  it("should include Confirm and Cancel actions", () => {
    const card = buildConfirmationCard({ findingId: "F-001" });
    const actions = card.actions as any[];
    expect(actions).to.have.length(2);
    expect(actions[0].title).to.equal("Confirm Remediation");
    expect(actions[0].data.action).to.equal("remediate");
    expect(actions[1].title).to.equal("Cancel");
    expect(actions[1].data).to.deep.equal({});
  });

  it("should include agent attribution", () => {
    const card = buildConfirmationCard({
      findingId: "F-001",
      agentUsed: "ComplianceAgent",
    });
    const body = card.body as any[];
    const attr = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("Processed by:")
    );
    expect(attr).to.exist;
  });
});
