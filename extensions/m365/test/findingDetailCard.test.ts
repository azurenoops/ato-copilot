import { expect } from "chai";
import { buildFindingDetailCard } from "../src/cards/findingDetailCard";

describe("Finding Detail Card (FR-010a)", () => {
  it("should produce Adaptive Card v1.5 JSON", () => {
    const card = buildFindingDetailCard({
      title: "Missing encryption",
      severity: "High",
    });
    expect(card.type).to.equal("AdaptiveCard");
    expect(card.version).to.equal("1.5");
  });

  it("should display finding title", () => {
    const card = buildFindingDetailCard({
      title: "Storage not encrypted",
      severity: "Critical",
    });
    const body = card.body as any[];
    const titleBlock = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("Storage not encrypted")
    );
    expect(titleBlock).to.exist;
  });

  it("should show severity with correct color for Critical", () => {
    const card = buildFindingDetailCard({
      title: "Finding",
      severity: "Critical",
    });
    const body = card.body as any[];
    const columnSet = body.find((b: any) => b.type === "ColumnSet");
    expect(columnSet).to.exist;
    const severityItem = columnSet.columns[0].items[0];
    expect(severityItem.text).to.include("Critical");
    expect(severityItem.color).to.equal("Default");
  });

  it("should show severity with correct color for High", () => {
    const card = buildFindingDetailCard({
      title: "Finding",
      severity: "High",
    });
    const body = card.body as any[];
    const columnSet = body.find((b: any) => b.type === "ColumnSet");
    const severityItem = columnSet.columns[0].items[0];
    expect(severityItem.text).to.include("High");
    expect(severityItem.color).to.equal("Attention");
  });

  it("should show severity with Warning color for Medium", () => {
    const card = buildFindingDetailCard({
      title: "Finding",
      severity: "Medium",
    });
    const body = card.body as any[];
    const columnSet = body.find((b: any) => b.type === "ColumnSet");
    const severityItem = columnSet.columns[0].items[0];
    expect(severityItem.text).to.include("Medium");
    expect(severityItem.color).to.equal("Warning");
  });

  it("should show auto-remediable badge when true", () => {
    const card = buildFindingDetailCard({
      title: "Finding",
      severity: "Medium",
      autoRemediable: true,
    });
    const body = card.body as any[];
    const columnSet = body.find((b: any) => b.type === "ColumnSet");
    expect(columnSet).to.exist;
    // Auto-remediable badge is the second column
    const badgeCol = columnSet.columns.find(
      (c: any) => c.items && c.items[0].text && c.items[0].text.includes("Auto-Remediable")
    );
    expect(badgeCol).to.exist;
  });

  it("should include Apply Fix action when auto-remediable", () => {
    const card = buildFindingDetailCard({
      title: "Finding",
      severity: "High",
      autoRemediable: true,
      findingId: "F-001",
    });
    const actions = card.actions as any[];
    const applyFix = actions?.find((a: any) => a.title === "Apply Fix");
    expect(applyFix).to.exist;
    expect(applyFix.data.action).to.equal("remediate");
  });

  it("should include Azure Portal link when resourceId provided", () => {
    const card = buildFindingDetailCard({
      title: "Finding",
      severity: "Low",
      resourceId: "/subscriptions/123/rg/test",
    });
    const actions = card.actions as any[];
    const portalAction = actions?.find((a: any) => a.title === "View in Azure Portal");
    expect(portalAction).to.exist;
    expect(portalAction.url).to.include("portal.azure.us");
  });

  it("should include agent attribution and suggestion buttons", () => {
    const card = buildFindingDetailCard({
      title: "Finding",
      severity: "High",
      agentUsed: "ComplianceAgent",
      suggestions: ["Show remediation"],
    });
    const body = card.body as any[];
    const attr = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("Processed by:")
    );
    expect(attr).to.exist;

    const actions = card.actions as any[];
    const suggBtn = actions?.find((a: any) => a.title === "Show remediation");
    expect(suggBtn).to.exist;
  });
});
