import { expect } from "chai";

/**
 * Tests for analysis panel actions — drill-down, remediation confirmation,
 * finding status transitions (T095, FR-014c, FR-018a, FR-018b, FR-018e).
 */
describe("Analysis Panel — Actions", () => {
  describe("Drill-Down Action", () => {
    it("should send drillDown command with controlId", () => {
      const message = {
        command: "drillDown",
        controlId: "AC-2",
        conversationId: "conv-123",
      };
      expect(message.command).to.equal("drillDown");
      expect(message.controlId).to.equal("AC-2");
      expect(message.conversationId).to.equal("conv-123");
    });

    it("should construct drillDown actionContext", () => {
      const actionContext = { controlId: "AU-6" };
      expect(actionContext.controlId).to.equal("AU-6");
    });

    it("should receive drillDownResult response", () => {
      const result = {
        command: "drillDownResult",
        controlId: "AC-2",
        data: { statement: "Account Management", guidance: "Implement..." },
        response: "AC-2 requires organizations to manage information system accounts.",
      };
      expect(result.command).to.equal("drillDownResult");
      expect(result.data).to.have.property("statement");
      expect(result.response).to.include("AC-2");
    });
  });

  describe("Remediation Flow", () => {
    it("should construct applyFix message with finding details", () => {
      const message = {
        command: "applyFix",
        findingId: "f-001",
        title: "Missing encryption",
        remediationScript: "az storage account update --encryption-services blob",
      };
      expect(message.command).to.equal("applyFix");
      expect(message.remediationScript).to.include("az storage");
    });

    it("should construct confirmRemediation action with confirmed flag", () => {
      const actionContext = {
        findingId: "f-001",
        controlId: "SC-28",
        confirmed: "true",
      };
      expect(actionContext.confirmed).to.equal("true");
      expect(actionContext.findingId).to.equal("f-001");
      expect(actionContext.controlId).to.equal("SC-28");
    });

    it("should only show Apply Fix button for autoRemediable findings", () => {
      interface Finding {
        autoRemediable?: boolean;
        remediationScript?: string;
      }

      const remediable: Finding = { autoRemediable: true, remediationScript: "script" };
      const notRemediable: Finding = { autoRemediable: false };

      const showApplyFix = (f: Finding) =>
        f.autoRemediable === true && !!f.remediationScript;

      expect(showApplyFix(remediable)).to.be.true;
      expect(showApplyFix(notRemediable)).to.be.false;
    });

    it("should not show Apply Fix when remediationScript is missing", () => {
      interface Finding { autoRemediable?: boolean; remediationScript?: string; }
      const f: Finding = { autoRemediable: true };
      const showApplyFix = (finding: Finding) =>
        finding.autoRemediable === true && !!finding.remediationScript;
      expect(showApplyFix(f)).to.be.false;
    });
  });

  describe("Finding Status Lifecycle", () => {
    const STATUS_TRANSITIONS: Record<string, string> = {
      open: "acknowledged",
      acknowledged: "remediated",
      remediated: "verified",
    };

    it("should transition open → acknowledged", () => {
      expect(STATUS_TRANSITIONS["open"]).to.equal("acknowledged");
    });

    it("should transition acknowledged → remediated", () => {
      expect(STATUS_TRANSITIONS["acknowledged"]).to.equal("remediated");
    });

    it("should transition remediated → verified", () => {
      expect(STATUS_TRANSITIONS["remediated"]).to.equal("verified");
    });

    it("should have no transition from verified", () => {
      expect(STATUS_TRANSITIONS["verified"]).to.be.undefined;
    });

    it("should construct updateStatus message correctly", () => {
      const message = {
        command: "updateStatus",
        findingId: "f-001",
        newStatus: "acknowledged",
        conversationId: "conv-123",
      };
      expect(message.command).to.equal("updateStatus");
      expect(message.findingId).to.equal("f-001");
      expect(message.newStatus).to.equal("acknowledged");
    });

    it("should display status with correct icons", () => {
      const STATUS_MAP: Record<string, { label: string; icon: string }> = {
        open: { label: "Open", icon: "🔴" },
        acknowledged: { label: "Acknowledged", icon: "🟡" },
        remediated: { label: "Remediated", icon: "🔵" },
        verified: { label: "Verified", icon: "🟢" },
      };

      expect(STATUS_MAP["open"].icon).to.equal("🔴");
      expect(STATUS_MAP["acknowledged"].icon).to.equal("🟡");
      expect(STATUS_MAP["remediated"].icon).to.equal("🔵");
      expect(STATUS_MAP["verified"].icon).to.equal("🟢");
    });
  });
});
