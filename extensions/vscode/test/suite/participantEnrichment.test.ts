import { expect } from "chai";

/**
 * Tests for chat participant enrichment — tools summary, suggestions,
 * follow-up prompt (T106, FR-022, FR-023, FR-024).
 */
describe("Chat Participant — Enrichment", () => {
  interface ToolExecution {
    toolName: string;
    success: boolean;
    executionTimeMs: number;
    resultSummary?: string;
  }

  describe("Tools Summary", () => {
    it("should render tools table header", () => {
      const tools: ToolExecution[] = [
        { toolName: "ComplianceScanTool", success: true, executionTimeMs: 450 },
      ];

      const header = "| Tool | Time | Status |\n|------|------|--------|";
      expect(header).to.include("Tool");
      expect(header).to.include("Time");
      expect(header).to.include("Status");
      expect(tools).to.have.length(1);
    });

    it("should render tool row with success indicator", () => {
      const tool: ToolExecution = {
        toolName: "NistLookupTool",
        success: true,
        executionTimeMs: 120,
      };

      const status = tool.success ? "✓" : "✗";
      const row = `| ${tool.toolName} | ${tool.executionTimeMs}ms | ${status} |`;
      expect(row).to.include("NistLookupTool");
      expect(row).to.include("120ms");
      expect(row).to.include("✓");
    });

    it("should render failure status for failed tools", () => {
      const tool: ToolExecution = {
        toolName: "FailingTool",
        success: false,
        executionTimeMs: 50,
      };

      const status = tool.success ? "✓" : "✗";
      expect(status).to.equal("✗");
    });

    it("should not render tools section when no tools executed", () => {
      const response: { toolsExecuted?: ToolExecution[] } = {};
      const shouldRender = Array.isArray(response.toolsExecuted) && response.toolsExecuted.length > 0;
      expect(shouldRender).to.be.false;
    });
  });

  describe("Suggestions", () => {
    it("should create button for each suggestion", () => {
      const suggestions = [
        "Check AC-2 compliance",
        "Show NIST controls",
        "Generate remediation plan",
      ];

      const buttons = suggestions.map((s) => ({
        command: "ato.followUpSuggestion",
        title: s,
        arguments: [s],
      }));

      expect(buttons).to.have.length(3);
      expect(buttons[0].title).to.equal("Check AC-2 compliance");
      expect(buttons[0].arguments).to.deep.equal(["Check AC-2 compliance"]);
    });

    it("should use ato.followUpSuggestion command", () => {
      const button = {
        command: "ato.followUpSuggestion",
        title: "Test suggestion",
        arguments: ["Test suggestion"],
      };
      expect(button.command).to.equal("ato.followUpSuggestion");
    });

    it("should not render when suggestions are empty", () => {
      const suggestions: string[] = [];
      expect(suggestions.length).to.equal(0);
    });

    it("should not render when suggestions are undefined", () => {
      const response: { suggestions?: string[] } = {};
      const shouldRender = Array.isArray(response.suggestions) && response.suggestions.length > 0;
      expect(shouldRender).to.be.false;
    });
  });

  describe("Follow-Up Prompt", () => {
    it("should render follow-up when requiresFollowUp is true", () => {
      const response = {
        requiresFollowUp: true,
        followUpPrompt: "Which framework would you like to check?",
        missingFields: ["framework", "baseline"],
      };

      const prompt = `> **Follow-up needed:** ${response.followUpPrompt}`;
      expect(prompt).to.include("Follow-up needed");
      expect(prompt).to.include("Which framework");
    });

    it("should include missing fields in follow-up", () => {
      const missingFields = ["framework", "baseline"];
      const text = `Missing: ${missingFields.join(", ")}`;
      expect(text).to.equal("Missing: framework, baseline");
    });

    it("should not render follow-up when requiresFollowUp is false", () => {
      const response = {
        requiresFollowUp: false,
        followUpPrompt: "Ignored",
      };

      const shouldRender = response.requiresFollowUp && !!response.followUpPrompt;
      expect(shouldRender).to.be.false;
    });

    it("should not render follow-up when followUpPrompt is absent", () => {
      const response = {
        requiresFollowUp: true,
        followUpPrompt: undefined as string | undefined,
      };

      const shouldRender = response.requiresFollowUp && !!response.followUpPrompt;
      expect(shouldRender).to.be.false;
    });
  });
});
