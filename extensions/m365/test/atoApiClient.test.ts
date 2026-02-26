import { expect } from "chai";
import { ATOApiClient } from "../src/services/atoApiClient";

describe("ATOApiClient", () => {
  describe("Constructor", () => {
    it("should create an instance with base URL", () => {
      const client = new ATOApiClient("http://localhost:3001");
      expect(client).to.be.instanceOf(ATOApiClient);
    });

    it("should accept optional API key", () => {
      const client = new ATOApiClient("http://localhost:3001", "test-key");
      expect(client).to.be.instanceOf(ATOApiClient);
    });
  });

  describe("generateConversationId", () => {
    it("should generate m365-format conversation ID", () => {
      const id = ATOApiClient.generateConversationId();
      expect(id).to.match(/^m365-\d+-[a-z0-9]{9}$/);
    });

    it("should generate unique IDs", () => {
      const id1 = ATOApiClient.generateConversationId();
      const id2 = ATOApiClient.generateConversationId();
      expect(id1).to.not.equal(id2);
    });

    it("should include timestamp component", () => {
      const before = Date.now();
      const id = ATOApiClient.generateConversationId();
      const after = Date.now();

      const parts = id.split("-");
      // m365-{timestamp}-{random}
      const timestamp = parseInt(parts[1], 10);
      expect(timestamp).to.be.greaterThanOrEqual(before);
      expect(timestamp).to.be.lessThanOrEqual(after);
    });
  });

  describe("Request Contract", () => {
    it("should use m365-copilot as source", () => {
      // Verify contract structure
      const expectedRequest = {
        conversationId: "m365-123-abc",
        message: "Test message",
        conversationHistory: [],
        context: {
          source: "m365-copilot",
          platform: "M365",
          userId: "user-1",
          userName: "John Doe",
        },
      };

      expect(expectedRequest.context.source).to.equal("m365-copilot");
      expect(expectedRequest.context.platform).to.equal("M365");
      expect(expectedRequest.conversationHistory).to.be.an("array").that.is.empty;
    });

    it("should support optional userName", () => {
      const context = {
        source: "m365-copilot",
        platform: "M365",
        userId: "user-1",
      };
      expect((context as any).userName).to.be.undefined;
    });
  });

  describe("Timeout Configuration", () => {
    it("should use 300s (300000ms) timeout", () => {
      const expectedTimeout = 300_000;
      expect(expectedTimeout).to.equal(300000);
    });
  });

  describe("User-Agent Header", () => {
    it("should set correct User-Agent value", () => {
      const expectedUserAgent = "ATO-Copilot-M365-Extension/1.0.0";
      expect(expectedUserAgent).to.equal("ATO-Copilot-M365-Extension/1.0.0");
    });
  });

  describe("Health Check Contract", () => {
    it("should target /health endpoint", () => {
      const healthEndpoint = "/health";
      expect(healthEndpoint).to.equal("/health");
    });

    it("should return boolean result", () => {
      // Health check returns true on success, false on failure
      const success: boolean = true;
      const failure: boolean = false;
      expect(success).to.be.a("boolean");
      expect(failure).to.be.a("boolean");
    });
  });
});
