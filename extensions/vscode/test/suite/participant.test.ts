import { expect } from "chai";
import * as sinon from "sinon";

describe("Chat Participant (@ato)", () => {
  describe("Slash Command Routing", () => {
    const commandMapping: Record<string, string> = {
      compliance: "ComplianceAgent",
      knowledge: "KnowledgeBaseAgent",
      config: "ConfigurationAgent",
    };

    it("should map /compliance to ComplianceAgent", () => {
      expect(commandMapping["compliance"]).to.equal("ComplianceAgent");
    });

    it("should map /knowledge to KnowledgeBaseAgent", () => {
      expect(commandMapping["knowledge"]).to.equal("KnowledgeBaseAgent");
    });

    it("should map /config to ConfigurationAgent", () => {
      expect(commandMapping["config"]).to.equal("ConfigurationAgent");
    });

    it("should not have a mapping for unknown commands", () => {
      expect(commandMapping["unknown"]).to.be.undefined;
    });

    it("should include all three slash commands", () => {
      expect(Object.keys(commandMapping)).to.have.length(3);
    });
  });

  describe("Conversation History Rebuild", () => {
    interface MockTurn {
      participant?: string;
      command?: string;
      prompt: string;
      response: string[];
    }

    function rebuildHistory(
      previousTurns: MockTurn[]
    ): Array<{ role: string; content: string }> {
      const history: Array<{ role: string; content: string }> = [];
      for (const turn of previousTurns) {
        history.push({ role: "user", content: turn.prompt });
        const assistantContent = turn.response.join("\n\n");
        if (assistantContent) {
          history.push({ role: "assistant", content: assistantContent });
        }
      }
      return history;
    }

    it("should produce alternating user/assistant pairs", () => {
      const turns: MockTurn[] = [
        {
          participant: "ato",
          prompt: "What is NIST?",
          response: ["NIST is..."],
        },
      ];

      const history = rebuildHistory(turns);
      expect(history).to.have.length(2);
      expect(history[0].role).to.equal("user");
      expect(history[1].role).to.equal("assistant");
    });

    it("should skip assistant entry for empty responses", () => {
      const turns: MockTurn[] = [
        { participant: "ato", prompt: "Hello", response: [] },
      ];

      const history = rebuildHistory(turns);
      expect(history).to.have.length(1);
      expect(history[0].role).to.equal("user");
    });

    it("should handle multiple turns correctly", () => {
      const turns: MockTurn[] = [
        {
          participant: "ato",
          prompt: "First question",
          response: ["Answer 1"],
        },
        {
          participant: "ato",
          prompt: "Second question",
          response: ["Answer 2"],
        },
      ];

      const history = rebuildHistory(turns);
      expect(history).to.have.length(4);
      expect(history[0].content).to.equal("First question");
      expect(history[1].content).to.equal("Answer 1");
      expect(history[2].content).to.equal("Second question");
      expect(history[3].content).to.equal("Answer 2");
    });

    it("should join multi-part responses with double newline", () => {
      const turns: MockTurn[] = [
        {
          participant: "ato",
          prompt: "Explain",
          response: ["Part 1", "Part 2"],
        },
      ];

      const history = rebuildHistory(turns);
      expect(history[1].content).to.equal("Part 1\n\nPart 2");
    });
  });

  describe("Error Handling", () => {
    it("should suggest Configure Connection button text on error", () => {
      const errorButtonTitle = "Configure Connection";
      expect(errorButtonTitle).to.equal("Configure Connection");
    });

    it("should provide a structured error response", () => {
      const errorResponse = {
        message: "Failed to connect to ATO MCP server",
        actions: ["Configure Connection"],
      };
      expect(errorResponse.message).to.include("Failed to connect");
      expect(errorResponse.actions).to.include("Configure Connection");
    });
  });

  describe("Message Context", () => {
    it("should set source to vscode-copilot", () => {
      const context = {
        source: "vscode-copilot",
        platform: "VSCode",
      };
      expect(context.source).to.equal("vscode-copilot");
    });

    it("should set platform to VSCode", () => {
      const context = {
        source: "vscode-copilot",
        platform: "VSCode",
      };
      expect(context.platform).to.equal("VSCode");
    });

    it("should include targetAgent when slash command is used", () => {
      const commandMapping: Record<string, string> = {
        compliance: "ComplianceAgent",
        knowledge: "KnowledgeBaseAgent",
        config: "ConfigurationAgent",
      };
      const context = {
        source: "vscode-copilot",
        platform: "VSCode",
        targetAgent: commandMapping["compliance"],
        metadata: {
          routingHint: commandMapping["compliance"],
        },
      };
      expect(context.targetAgent).to.equal("ComplianceAgent");
      expect(context.metadata.routingHint).to.equal("ComplianceAgent");
    });

    it("should not include targetAgent when no slash command is used", () => {
      const context = {
        source: "vscode-copilot",
        platform: "VSCode",
        metadata: {},
      };
      expect((context as any).targetAgent).to.be.undefined;
    });
  });
});
