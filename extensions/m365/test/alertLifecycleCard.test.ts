import { expect } from "chai";
import { buildAlertLifecycleCard } from "../src/cards/alertLifecycleCard";

describe("Alert Lifecycle Card (FR-010a)", () => {
  it("should produce Adaptive Card v1.5 JSON", () => {
    const card = buildAlertLifecycleCard({ alertId: "A-001", severity: "High" });
    expect(card.type).to.equal("AdaptiveCard");
    expect(card.version).to.equal("1.5");
  });

  it("should display severity in body and alertId in actions", () => {
    const card = buildAlertLifecycleCard({ alertId: "ALT-42", severity: "Critical" });
    const body = card.body as any[];
    // Severity is in a ColumnSet
    const columnSet = body.find((b: any) => b.type === "ColumnSet");
    expect(columnSet).to.exist;
    const severityText = columnSet.columns[0].items[0].text;
    expect(severityText).to.include("Critical");
    // AlertId is in action data
    const actions = card.actions as any[];
    const ackAction = actions.find((a: any) => a.title === "Acknowledge");
    expect(ackAction.data.actionContext.alertId).to.equal("ALT-42");
  });

  it("should list affected resources", () => {
    const card = buildAlertLifecycleCard({
      alertId: "A-1",
      severity: "Medium",
      affectedResources: ["vm-prod-1", "storage-logs"],
    });
    const body = card.body as any[];
    const resourceText = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("vm-prod-1")
    );
    expect(resourceText).to.exist;
  });

  it("should show SLA deadline when provided", () => {
    const card = buildAlertLifecycleCard({
      alertId: "A-1",
      severity: "High",
      slaDeadline: "2025-03-15T12:00:00Z",
    });
    const body = card.body as any[];
    const slaBlock = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("2025-03-15")
    );
    expect(slaBlock).to.exist;
  });

  it("should include Acknowledge, Dismiss, and Escalate actions", () => {
    const card = buildAlertLifecycleCard({ alertId: "A-1", severity: "High" });
    const actions = card.actions as any[];
    expect(actions).to.exist;
    expect(actions.length).to.be.greaterThanOrEqual(3);
    const titles = actions.map((a: any) => a.title);
    expect(titles).to.include("Acknowledge");
    expect(titles).to.include("Dismiss");
    expect(titles).to.include("Escalate");
  });

  it("should include agent attribution", () => {
    const card = buildAlertLifecycleCard({
      alertId: "A-1",
      severity: "Low",
      agentUsed: "ComplianceAgent",
    });
    const body = card.body as any[];
    const attr = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("Processed by:")
    );
    expect(attr).to.exist;
  });
});
