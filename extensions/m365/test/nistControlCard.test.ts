import { expect } from "chai";
import { buildNistControlCard } from "../src/cards/nistControlCard";

describe("NIST Control Card (FR-010a)", () => {
  it("should produce Adaptive Card v1.5 JSON", () => {
    const card = buildNistControlCard({ controlId: "AC-2", statement: "The organization manages accounts." });
    expect(card.type).to.equal("AdaptiveCard");
    expect(card.version).to.equal("1.5");
  });

  it("should display controlId and statement", () => {
    const card = buildNistControlCard({
      controlId: "AC-2",
      statement: "The organization manages information system accounts.",
    });
    const body = card.body as any[];
    const idBlock = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("AC-2")
    );
    expect(idBlock).to.exist;
    const stmtBlock = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("manages information system accounts")
    );
    expect(stmtBlock).to.exist;
  });

  it("should display implementation guidance", () => {
    const card = buildNistControlCard({
      controlId: "AC-2",
      statement: "Statement",
      implementationGuidance: "Use Azure AD for identity management",
    });
    const body = card.body as any[];
    const guidanceBlock = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("Azure AD")
    );
    expect(guidanceBlock).to.exist;
  });

  it("should list STIGs when provided", () => {
    const card = buildNistControlCard({
      controlId: "AC-2",
      statement: "Statement",
      stigs: ["STIG-001", "STIG-002"],
    });
    const body = card.body as any[];
    const stigBlock = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("STIG-001")
    );
    expect(stigBlock).to.exist;
  });

  it("should show FedRAMP baseline", () => {
    const card = buildNistControlCard({
      controlId: "AC-2",
      statement: "Statement",
      fedRampBaseline: "High",
    });
    const body = card.body as any[];
    const factSet = body.find((b: any) => b.type === "FactSet");
    expect(factSet).to.exist;
    const baselineFact = factSet.facts.find((f: any) => f.value === "High");
    expect(baselineFact).to.exist;
  });

  it("should include Show Related Controls action when controlFamily provided", () => {
    const card = buildNistControlCard({ controlId: "AC-2", statement: "Statement", controlFamily: "Access Control" });
    const actions = card.actions as any[];
    const relatedAction = actions?.find((a: any) => a.title === "Show Related Controls");
    expect(relatedAction).to.exist;
  });

  it("should include agent attribution", () => {
    const card = buildNistControlCard({
      controlId: "AC-2",
      statement: "Statement",
      agentUsed: "KnowledgeBaseAgent",
    });
    const body = card.body as any[];
    const attr = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("Processed by:")
    );
    expect(attr).to.exist;
  });
});
