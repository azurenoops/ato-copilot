import { expect } from "chai";
import { buildConfigurationCard } from "../src/cards/configurationCard";

describe("Configuration Card (FR-010a)", () => {
  it("should produce Adaptive Card v1.5 JSON", () => {
    const card = buildConfigurationCard({});
    expect(card.type).to.equal("AdaptiveCard");
    expect(card.version).to.equal("1.5");
  });

  it("should display framework in FactSet", () => {
    const card = buildConfigurationCard({ framework: "NIST 800-53 Rev 5" });
    const body = card.body as any[];
    const factSet = body.find((b: any) => b.type === "FactSet");
    expect(factSet).to.exist;
    const frameworkFact = factSet.facts.find((f: any) => f.value === "NIST 800-53 Rev 5");
    expect(frameworkFact).to.exist;
  });

  it("should display baseline and environment", () => {
    const card = buildConfigurationCard({
      baseline: "FedRAMP High",
      cloudEnvironment: "AzureGovernment",
    });
    const body = card.body as any[];
    const factSet = body.find((b: any) => b.type === "FactSet");
    const baselineFact = factSet.facts.find((f: any) => f.value === "FedRAMP High");
    expect(baselineFact).to.exist;
    const envFact = factSet.facts.find((f: any) => f.value === "AzureGovernment");
    expect(envFact).to.exist;
  });

  it("should include Update actions", () => {
    const card = buildConfigurationCard({ framework: "NIST" });
    const actions = card.actions as any[];
    expect(actions).to.exist;
    expect(actions.length).to.be.greaterThan(0);
    const updateAction = actions.find((a: any) => typeof a.title === "string" && a.title.includes("Update"));
    expect(updateAction).to.exist;
  });

  it("should include agent attribution when provided", () => {
    const card = buildConfigurationCard({ agentUsed: "ConfigAgent" });
    const body = card.body as any[];
    const attr = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("Processed by: ConfigAgent")
    );
    expect(attr).to.exist;
  });
});
