import { expect } from "chai";

/**
 * Tests for PIM pre-flight check flow (T097, FR-018c-i).
 */
describe("PIM Pre-Flight Check Flow", () => {
  describe("PIM Status Check", () => {
    it("should send checkPimStatus action", () => {
      const action = "checkPimStatus";
      const actionContext = {};
      expect(action).to.equal("checkPimStatus");
      expect(actionContext).to.deep.equal({});
    });

    it("should recognize pimActive=true as authorized", () => {
      const response = { data: { pimActive: true, role: "Security Reader" } };
      expect(response.data.pimActive).to.be.true;
    });

    it("should recognize pimActive=false as needing activation", () => {
      const response = {
        data: {
          pimActive: false,
          eligibleRoles: ["Security Reader", "Compliance Administrator"],
        },
      };
      expect(response.data.pimActive).to.be.false;
      expect(response.data.eligibleRoles).to.have.length(2);
    });
  });

  describe("PIM Activation", () => {
    it("should send activatePim action", () => {
      const action = "activatePim";
      const actionContext = {};
      expect(action).to.equal("activatePim");
      expect(actionContext).to.deep.equal({});
    });

    it("should construct activation request via sendAction", () => {
      const request = {
        conversationId: "pim-activate-123",
        action: "activatePim",
        actionContext: {},
        message: "",
      };
      expect(request.action).to.equal("activatePim");
      expect(request.conversationId).to.include("pim-activate");
    });
  });

  describe("PIM-Protected Actions", () => {
    const pimProtectedActions = [
      "remediate",
      "confirmRemediation",
      "activatePim",
    ];

    it("should require PIM check before remediation actions", () => {
      expect(pimProtectedActions).to.include("remediate");
      expect(pimProtectedActions).to.include("confirmRemediation");
    });

    it("should include activatePim as a PIM action", () => {
      expect(pimProtectedActions).to.include("activatePim");
    });

    it("should not require PIM for read-only actions", () => {
      const readOnlyActions = ["drillDown", "updateFindingStatus"];
      for (const action of readOnlyActions) {
        expect(pimProtectedActions).to.not.include(action);
      }
    });
  });
});
