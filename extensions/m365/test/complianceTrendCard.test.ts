import { expect } from "chai";
import { buildComplianceTrendCard } from "../src/cards/complianceTrendCard";

describe("Compliance Trend Card (FR-010a)", () => {
  it("should produce Adaptive Card v1.5 JSON", () => {
    const card = buildComplianceTrendCard({ dataPoints: [], direction: "stable" });
    expect(card.type).to.equal("AdaptiveCard");
    expect(card.version).to.equal("1.5");
  });

  it("should render sparkline from data points", () => {
    const card = buildComplianceTrendCard({
      dataPoints: [
        { date: "2025-01-01", score: 60 },
        { date: "2025-02-01", score: 70 },
        { date: "2025-03-01", score: 85 },
      ],
      direction: "improving",
    });
    const body = card.body as any[];
    // Sparkline uses Unicode block chars
    const sparkBlock = body.find(
      (b: any) => typeof b.text === "string" && /[▁▂▃▄▅▆▇█]/.test(b.text)
    );
    expect(sparkBlock).to.exist;
  });

  it("should show direction indicator for improving", () => {
    const card = buildComplianceTrendCard({
      dataPoints: [{ date: "2025-01-01", score: 80 }],
      direction: "improving",
    });
    const body = card.body as any[];
    // Indicator is nested in a ColumnSet
    const columnSet = body.find((b: any) => b.type === "ColumnSet");
    expect(columnSet).to.exist;
    const indicatorText = columnSet.columns[0].items[0].text;
    expect(indicatorText).to.include("📈");
  });

  it("should show direction indicator for declining", () => {
    const card = buildComplianceTrendCard({
      dataPoints: [{ date: "2025-01-01", score: 50 }],
      direction: "declining",
    });
    const body = card.body as any[];
    const columnSet = body.find((b: any) => b.type === "ColumnSet");
    expect(columnSet).to.exist;
    const indicatorText = columnSet.columns[0].items[0].text;
    expect(indicatorText).to.include("📉");
  });

  it("should display significant events when provided", () => {
    const card = buildComplianceTrendCard({
      dataPoints: [{ date: "2025-01-01", score: 75 }],
      direction: "stable",
      significantEvents: [{ date: "2025-01-15", event: "Policy updated" }],
    });
    const body = card.body as any[];
    const eventBlock = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("Policy updated")
    );
    expect(eventBlock).to.exist;
  });

  it("should include agent attribution", () => {
    const card = buildComplianceTrendCard({
      dataPoints: [],
      direction: "stable",
      agentUsed: "ComplianceAgent",
    });
    const body = card.body as any[];
    const attr = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("Processed by:")
    );
    expect(attr).to.exist;
  });
});
