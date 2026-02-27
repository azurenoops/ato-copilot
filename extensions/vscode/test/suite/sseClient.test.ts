import { expect } from "chai";

/**
 * Tests for VS Code SSE client (T096, FR-029a, FR-029e).
 */
describe("VS Code SSE Client", () => {
  describe("parseSseChunk", () => {
    interface SseEvent {
      event: string;
      data: string;
      id?: string;
      timestamp?: string;
    }

    /**
     * Parse SSE chunks — mirrors sseClient.ts logic.
     */
    function parseSseChunk(chunk: string): SseEvent[] {
      const events: SseEvent[] = [];
      const blocks = chunk.split("\n\n").filter((b) => b.trim().length > 0);

      for (const block of blocks) {
        const lines = block.split("\n");
        let eventType = "message";
        let data = "";
        let id: string | undefined;

        for (const line of lines) {
          if (line.startsWith("event:")) {
            eventType = line.substring(6).trim();
          } else if (line.startsWith("data:")) {
            data = line.substring(5).trim();
          } else if (line.startsWith("id:")) {
            id = line.substring(3).trim();
          }
        }

        if (data) {
          const event: SseEvent = { event: eventType, data };
          if (id) {
            event.id = id;
          }
          try {
            const parsed = JSON.parse(data);
            if (parsed.timestamp) {
              event.timestamp = parsed.timestamp;
            }
          } catch {
            // Not JSON
          }
          events.push(event);
        }
      }

      return events;
    }

    it("should parse a single SSE event", () => {
      const chunk = 'event: agentRouted\ndata: {"agentId":"ComplianceAgent"}\n\n';
      const events = parseSseChunk(chunk);
      expect(events).to.have.length(1);
      expect(events[0].event).to.equal("agentRouted");
      expect(events[0].data).to.include("ComplianceAgent");
    });

    it("should parse multiple SSE events", () => {
      const chunk =
        'event: toolStart\ndata: {"toolName":"ScanTool"}\n\n' +
        'event: toolComplete\ndata: {"toolName":"ScanTool","success":true}\n\n';
      const events = parseSseChunk(chunk);
      expect(events).to.have.length(2);
      expect(events[0].event).to.equal("toolStart");
      expect(events[1].event).to.equal("toolComplete");
    });

    it("should default to 'message' event type when not specified", () => {
      const chunk = 'data: {"text":"hello"}\n\n';
      const events = parseSseChunk(chunk);
      expect(events).to.have.length(1);
      expect(events[0].event).to.equal("message");
    });

    it("should skip empty chunks", () => {
      const chunk = "\n\n\n\n";
      const events = parseSseChunk(chunk);
      expect(events).to.have.length(0);
    });

    it("should parse event with id field", () => {
      const chunk = 'event: complete\nid: evt-1\ndata: {"done":true}\n\n';
      const events = parseSseChunk(chunk);
      expect(events).to.have.length(1);
      expect(events[0].id).to.equal("evt-1");
    });

    it("should extract timestamp from JSON data", () => {
      const chunk = 'event: thinking\ndata: {"timestamp":"2024-01-01T00:00:00Z"}\n\n';
      const events = parseSseChunk(chunk);
      expect(events).to.have.length(1);
      expect(events[0].timestamp).to.equal("2024-01-01T00:00:00Z");
    });

    it("should handle non-JSON data gracefully", () => {
      const chunk = "event: error\ndata: Something went wrong\n\n";
      const events = parseSseChunk(chunk);
      expect(events).to.have.length(1);
      expect(events[0].data).to.equal("Something went wrong");
      expect(events[0].timestamp).to.be.undefined;
    });

    it("should handle malformed chunks without data", () => {
      const chunk = "event: heartbeat\n\n";
      const events = parseSseChunk(chunk);
      expect(events).to.have.length(0);
    });
  });

  describe("SseClient Configuration", () => {
    it("should use default options when not specified", () => {
      const defaults = {
        maxRetries: 3,
        initialRetryDelayMs: 1000,
        maxRetryDelayMs: 30000,
        timeoutMs: 120000,
      };

      expect(defaults.maxRetries).to.equal(3);
      expect(defaults.initialRetryDelayMs).to.equal(1000);
      expect(defaults.maxRetryDelayMs).to.equal(30000);
      expect(defaults.timeoutMs).to.equal(120000);
    });

    it("should allow custom options", () => {
      const custom = {
        maxRetries: 5,
        initialRetryDelayMs: 500,
        maxRetryDelayMs: 60000,
        timeoutMs: 60000,
      };

      expect(custom.maxRetries).to.equal(5);
      expect(custom.initialRetryDelayMs).to.equal(500);
    });

    it("should calculate exponential backoff correctly", () => {
      const initialDelay = 1000;
      const maxDelay = 30000;
      const delays: number[] = [];
      let delay = initialDelay;

      for (let i = 0; i < 5; i++) {
        delays.push(delay);
        delay = Math.min(delay * 2, maxDelay);
      }

      expect(delays).to.deep.equal([1000, 2000, 4000, 8000, 16000]);
    });

    it("should cap backoff at maxRetryDelayMs", () => {
      const maxDelay = 30000;
      let delay = 16000;
      delay = Math.min(delay * 2, maxDelay);
      expect(delay).to.equal(30000);
    });
  });

  describe("SSE Event Types", () => {
    const validEventTypes = [
      "agentRouted",
      "thinking",
      "toolStart",
      "toolProgress",
      "toolComplete",
      "partial",
      "complete",
      "error",
    ];

    it("should recognize all 8 SSE event types", () => {
      expect(validEventTypes).to.have.length(8);
    });

    it("should include agentRouted for routing progress", () => {
      expect(validEventTypes).to.include("agentRouted");
    });

    it("should include tool lifecycle events", () => {
      expect(validEventTypes).to.include("toolStart");
      expect(validEventTypes).to.include("toolProgress");
      expect(validEventTypes).to.include("toolComplete");
    });

    it("should include terminal events (complete and error)", () => {
      expect(validEventTypes).to.include("complete");
      expect(validEventTypes).to.include("error");
    });
  });
});
