import { expect } from "chai";
import { buildKnowledgeBaseCard } from "../src/cards/knowledgeBaseCard";

describe("Knowledge Base Card (FR-010a)", () => {
  it("should produce Adaptive Card v1.5 JSON", () => {
    const card = buildKnowledgeBaseCard({ answer: "NIST AC-2 requires…" });
    expect(card.type).to.equal("AdaptiveCard");
    expect(card.version).to.equal("1.5");
    expect(card.$schema).to.equal("http://adaptivecards.io/schemas/adaptive-card.json");
  });

  it("should display answer text", () => {
    const card = buildKnowledgeBaseCard({ answer: "Access control answer" });
    const body = card.body as any[];
    const answerBlock = body.find((b: any) => b.text === "Access control answer");
    expect(answerBlock).to.exist;
    expect(answerBlock.wrap).to.be.true;
  });

  it("should render sources list when provided", () => {
    const card = buildKnowledgeBaseCard({
      answer: "Info",
      sources: [
        { title: "NIST SP 800-53", url: "https://nist.gov/800-53" },
        { title: "FedRAMP Guide", url: "https://fedramp.gov" },
      ],
    });
    const body = card.body as any[];
    const sourceText = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("NIST SP 800-53")
    );
    expect(sourceText).to.exist;
  });

  it("should show controlId and controlFamily in FactSet", () => {
    const card = buildKnowledgeBaseCard({
      answer: "Info",
      controlId: "AC-2",
      controlFamily: "Access Control",
    });
    const body = card.body as any[];
    const factSet = body.find((b: any) => b.type === "FactSet");
    expect(factSet).to.exist;
    const controlFact = factSet.facts.find((f: any) => f.value === "AC-2");
    expect(controlFact).to.exist;
  });

  it("should include agent attribution when provided", () => {
    const card = buildKnowledgeBaseCard({
      answer: "Info",
      agentUsed: "KnowledgeBaseAgent",
    });
    const body = card.body as any[];
    const attr = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("Processed by: KnowledgeBaseAgent")
    );
    expect(attr).to.exist;
  });

  it("should include Learn More action when sources provided", () => {
    const card = buildKnowledgeBaseCard({
      answer: "Info",
      controlId: "AC-2",
      sources: [{ title: "NIST", url: "https://nist.gov" }],
    });
    const actions = card.actions as any[];
    expect(actions).to.exist;
    const learnMore = actions.find((a: any) => a.title === "Learn More");
    expect(learnMore).to.exist;
  });

  it("should include suggestion buttons when provided", () => {
    const card = buildKnowledgeBaseCard({
      answer: "Info",
      suggestions: ["Show related controls", "Run assessment"],
    });
    const actions = card.actions as any[];
    const suggBtn = actions?.find((a: any) => a.title === "Show related controls");
    expect(suggBtn).to.exist;
    expect(suggBtn.data.message).to.equal("Show related controls");
  });
});
