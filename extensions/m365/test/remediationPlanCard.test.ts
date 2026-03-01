import { expect } from "chai";
import { buildRemediationPlanCard } from "../src/cards/remediationPlanCard";

describe("Remediation Plan Card (FR-010a)", () => {
  it("should produce Adaptive Card v1.5 JSON", () => {
    const card = buildRemediationPlanCard({});
    expect(card.type).to.equal("AdaptiveCard");
    expect(card.version).to.equal("1.5");
  });

  it("should display risk reduction percentage", () => {
    const card = buildRemediationPlanCard({ riskReduction: 42 });
    const body = card.body as any[];
    const reductionBlock = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("42%")
    );
    expect(reductionBlock).to.exist;
  });

  it("should list prioritized findings", () => {
    const card = buildRemediationPlanCard({
      findings: [
        { title: "Encryption missing", severity: "Critical" },
        { title: "MFA disabled", severity: "High" },
      ],
    });
    const body = card.body as any[];
    const findingText = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("Encryption missing")
    );
    expect(findingText).to.exist;
  });

  it("should display phased timeline when phases provided", () => {
    const card = buildRemediationPlanCard({
      phases: [
        { name: "Phase 1", duration: "1 week", findings: 3 },
        { name: "Phase 2", duration: "2 weeks", findings: 5 },
      ],
    });
    const body = card.body as any[];
    const factSet = body.find((b: any) => b.type === "FactSet");
    expect(factSet).to.exist;
    const phase1 = factSet.facts.find((f: any) => f.title === "Phase 1");
    expect(phase1).to.exist;
  });

  it("should include Start Remediation action", () => {
    const card = buildRemediationPlanCard({ planId: "P-001" });
    const actions = card.actions as any[];
    const startAction = actions?.find((a: any) => a.title === "Start Remediation");
    expect(startAction).to.exist;
    expect(startAction.data.action).to.equal("remediate");
  });

  it("should include agent attribution", () => {
    const card = buildRemediationPlanCard({ agentUsed: "ComplianceAgent" });
    const body = card.body as any[];
    const attr = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("Processed by:")
    );
    expect(attr).to.exist;
  });
});
