import { expect } from "chai";
import { buildEvidenceCollectionCard } from "../src/cards/evidenceCollectionCard";

describe("Evidence Collection Card (FR-010a)", () => {
  it("should produce Adaptive Card v1.5 JSON", () => {
    const card = buildEvidenceCollectionCard({ completeness: 0, items: [] });
    expect(card.type).to.equal("AdaptiveCard");
    expect(card.version).to.equal("1.5");
  });

  it("should display progress bar", () => {
    const card = buildEvidenceCollectionCard({ completeness: 75, items: [] });
    const body = card.body as any[];
    const progressBlock = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("█")
    );
    expect(progressBlock).to.exist;
  });

  it("should display completeness percentage", () => {
    const card = buildEvidenceCollectionCard({ completeness: 60, items: [] });
    const body = card.body as any[];
    const pctBlock = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("60%")
    );
    expect(pctBlock).to.exist;
  });

  it("should list evidence items", () => {
    const card = buildEvidenceCollectionCard({
      completeness: 50,
      items: [
        { name: "SSP Document", hash: "abc123def456", status: "Collected" },
        { name: "Scan Report", status: "Pending" },
      ],
    });
    const body = card.body as any[];
    const itemBlock = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("SSP Document")
    );
    expect(itemBlock).to.exist;
  });

  it("should truncate hash values", () => {
    const card = buildEvidenceCollectionCard({
      completeness: 50,
      items: [
        { name: "Doc", hash: "0123456789abcdef0123456789abcdef", status: "Collected" },
      ],
    });
    const body = card.body as any[];
    const itemBlock = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("01234567...")
    );
    expect(itemBlock).to.exist;
  });

  it("should include Collect More action", () => {
    const card = buildEvidenceCollectionCard({ completeness: 40, items: [] });
    const actions = card.actions as any[];
    const collectAction = actions?.find((a: any) => a.title === "Collect More");
    expect(collectAction).to.exist;
  });

  it("should include agent attribution", () => {
    const card = buildEvidenceCollectionCard({
      completeness: 100,
      items: [],
      agentUsed: "ComplianceAgent",
    });
    const body = card.body as any[];
    const attr = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("Processed by:")
    );
    expect(attr).to.exist;
  });
});
