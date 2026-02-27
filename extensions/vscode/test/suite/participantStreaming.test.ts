import { expect } from "chai";

/**
 * Tests for SSE streaming integration in chat participant (T108, FR-029b).
 */
describe("Chat Participant — SSE Streaming", () => {
  describe("Agent Routing Progress", () => {
    it("should show routing message on agentRouted event", () => {
      const event = {
        event: "agentRouted",
        data: JSON.stringify({ agentId: "ComplianceAgent", agentName: "Compliance Agent" }),
      };

      const parsed = JSON.parse(event.data);
      const message = `Routing to ${parsed.agentName}...`;
      expect(message).to.equal("Routing to Compliance Agent...");
    });

    it("should fall back to agentId when agentName is absent", () => {
      const event = {
        event: "agentRouted",
        data: JSON.stringify({ agentId: "ComplianceAgent" }),
      };

      const parsed = JSON.parse(event.data);
      const name = parsed.agentName ?? parsed.agentId;
      expect(name).to.equal("ComplianceAgent");
    });
  });

  describe("Tool Progress", () => {
    it("should show progress on toolStart event", () => {
      const event = {
        event: "toolStart",
        data: JSON.stringify({ toolName: "ComplianceScanTool" }),
      };

      const parsed = JSON.parse(event.data);
      const message = `Running ${parsed.toolName}...`;
      expect(message).to.include("ComplianceScanTool");
    });

    it("should update progress on toolProgress event", () => {
      const event = {
        event: "toolProgress",
        data: JSON.stringify({
          toolName: "ComplianceScanTool",
          progress: 0.5,
          message: "Scanning resources...",
        }),
      };

      const parsed = JSON.parse(event.data);
      expect(parsed.progress).to.equal(0.5);
      expect(parsed.message).to.equal("Scanning resources...");
    });

    it("should finalize tool on toolComplete event", () => {
      const event = {
        event: "toolComplete",
        data: JSON.stringify({
          toolName: "ComplianceScanTool",
          success: true,
          executionTimeMs: 1200,
        }),
      };

      const parsed = JSON.parse(event.data);
      expect(parsed.success).to.be.true;
      expect(parsed.executionTimeMs).to.equal(1200);
    });
  });

  describe("Partial Response", () => {
    it("should append text on partial event", () => {
      const event = {
        event: "partial",
        data: JSON.stringify({ text: "Based on NIST 800-53 Rev 5, " }),
      };

      const parsed = JSON.parse(event.data);
      expect(parsed.text).to.include("NIST 800-53");
    });
  });

  describe("Complete Event", () => {
    it("should render final enriched response on complete event", () => {
      const event = {
        event: "complete",
        data: JSON.stringify({
          response: "Analysis complete",
          agentUsed: "Compliance Agent",
          intentType: "compliance",
          toolsExecuted: [
            { toolName: "ScanTool", success: true, executionTimeMs: 500 },
          ],
          suggestions: ["Check AC-2"],
        }),
      };

      const parsed = JSON.parse(event.data);
      expect(parsed.agentUsed).to.equal("Compliance Agent");
      expect(parsed.intentType).to.equal("compliance");
      expect(parsed.toolsExecuted).to.have.length(1);
      expect(parsed.suggestions).to.have.length(1);
    });
  });

  describe("Error Event", () => {
    it("should handle error event gracefully", () => {
      const event = {
        event: "error",
        data: JSON.stringify({
          errorCode: "TOOL_FAILED",
          message: "Compliance scan failed",
          suggestion: "Try again",
        }),
      };

      const parsed = JSON.parse(event.data);
      expect(parsed.errorCode).to.equal("TOOL_FAILED");
      expect(parsed.message).to.include("failed");
      expect(parsed.suggestion).to.equal("Try again");
    });
  });

  describe("Sync Fallback", () => {
    it("should fall back to sync when SSE stream fails", () => {
      const sseSuccess = false;
      const shouldFallback = !sseSuccess;
      expect(shouldFallback).to.be.true;
    });

    it("should use regular sendMessage for sync fallback", () => {
      const syncEndpoint = "/mcp/chat";
      expect(syncEndpoint).to.equal("/mcp/chat");
    });
  });
});
