import { expect } from "chai";
import * as sinon from "sinon";

describe("Analyze File Command", () => {
  describe("Prompt Construction", () => {
    function buildAnalysisPrompt(
      fileName: string,
      language: string,
      content: string
    ): string {
      return [
        `Analyze the following ${language} file for ATO compliance issues:`,
        "",
        `File: ${fileName}`,
        `Language: ${language}`,
        "",
        "```",
        content,
        "```",
        "",
        "Provide findings in the following JSON format:",
        '```json',
        '[{"severity": "high|medium|low", "title": "Finding title", "description": "Description", "line": 0, "recommendation": "How to fix"}]',
        '```',
      ].join("\n");
    }

    it("should include file name in prompt", () => {
      const prompt = buildAnalysisPrompt("main.bicep", "bicep", "resource vm 'Microsoft.Compute/virtualMachines@2023-03-01' = {}");
      expect(prompt).to.include("main.bicep");
    });

    it("should include language in prompt", () => {
      const prompt = buildAnalysisPrompt("main.bicep", "bicep", "// content");
      expect(prompt).to.include("Language: bicep");
    });

    it("should include file content in code block", () => {
      const content = "resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01'";
      const prompt = buildAnalysisPrompt("storage.bicep", "bicep", content);
      expect(prompt).to.include(content);
      expect(prompt).to.include("```");
    });

    it("should request JSON format for findings", () => {
      const prompt = buildAnalysisPrompt("test.tf", "terraform", "# content");
      expect(prompt).to.include("JSON format");
      expect(prompt).to.include("severity");
      expect(prompt).to.include("recommendation");
    });
  });

  describe("Findings Parser", () => {
    interface Finding {
      severity: "high" | "medium" | "low";
      title: string;
      description: string;
      line?: number;
      recommendation: string;
    }

    function parseFindings(response: string): Finding[] {
      // Try to extract JSON from code blocks
      const codeBlockMatch = response.match(/```(?:json)?\s*\n?([\s\S]*?)```/);
      if (codeBlockMatch) {
        try {
          return JSON.parse(codeBlockMatch[1].trim());
        } catch {
          // Not valid JSON in code block
        }
      }

      // Try to parse entire response as JSON
      try {
        const parsed = JSON.parse(response);
        return Array.isArray(parsed) ? parsed : [];
      } catch {
        return [];
      }
    }

    it("should parse findings from JSON code block", () => {
      const response = 'Here are the findings:\n```json\n[{"severity":"high","title":"Missing encryption","description":"Storage not encrypted","recommendation":"Enable encryption"}]\n```';
      const findings = parseFindings(response);
      expect(findings).to.have.length(1);
      expect(findings[0].severity).to.equal("high");
      expect(findings[0].title).to.equal("Missing encryption");
    });

    it("should parse findings from raw JSON response", () => {
      const response = '[{"severity":"medium","title":"Open port","description":"Port 22 open","recommendation":"Restrict access"}]';
      const findings = parseFindings(response);
      expect(findings).to.have.length(1);
      expect(findings[0].severity).to.equal("medium");
    });

    it("should return empty array for invalid JSON", () => {
      const response = "No findings detected in this file.";
      const findings = parseFindings(response);
      expect(findings).to.have.length(0);
    });

    it("should handle multiple findings", () => {
      const response = '```json\n[{"severity":"high","title":"F1","description":"D1","recommendation":"R1"},{"severity":"low","title":"F2","description":"D2","recommendation":"R2"}]\n```';
      const findings = parseFindings(response);
      expect(findings).to.have.length(2);
      expect(findings[0].severity).to.equal("high");
      expect(findings[1].severity).to.equal("low");
    });
  });
});

describe("Analyze Workspace Command", () => {
  describe("File Pattern Matching", () => {
    const supportedExtensions = [".bicep", ".tf", ".yaml", ".yml", ".json"];
    const excludePatterns = ["**/node_modules/**", "**/.git/**", "**/bin/**", "**/obj/**"];

    it("should support bicep files", () => {
      expect(supportedExtensions).to.include(".bicep");
    });

    it("should support terraform files", () => {
      expect(supportedExtensions).to.include(".tf");
    });

    it("should support yaml files", () => {
      expect(supportedExtensions).to.include(".yaml");
      expect(supportedExtensions).to.include(".yml");
    });

    it("should support json files", () => {
      expect(supportedExtensions).to.include(".json");
    });

    it("should exclude node_modules", () => {
      expect(excludePatterns.some(p => p.includes("node_modules"))).to.be.true;
    });

    it("should exclude .git directory", () => {
      expect(excludePatterns.some(p => p.includes(".git"))).to.be.true;
    });

    it("should exclude build output directories", () => {
      expect(excludePatterns.some(p => p.includes("bin"))).to.be.true;
      expect(excludePatterns.some(p => p.includes("obj"))).to.be.true;
    });
  });

  describe("Results Aggregation", () => {
    interface Finding {
      severity: string;
      title: string;
      file?: string;
    }

    it("should aggregate findings from multiple files", () => {
      const allFindings: Finding[] = [
        { severity: "high", title: "Issue 1", file: "a.bicep" },
        { severity: "medium", title: "Issue 2", file: "b.tf" },
        { severity: "low", title: "Issue 3", file: "a.bicep" },
      ];

      expect(allFindings).to.have.length(3);
      const highCount = allFindings.filter(f => f.severity === "high").length;
      expect(highCount).to.equal(1);
    });

    it("should group findings by file", () => {
      const findings: Finding[] = [
        { severity: "high", title: "Issue 1", file: "a.bicep" },
        { severity: "low", title: "Issue 2", file: "a.bicep" },
        { severity: "medium", title: "Issue 3", file: "b.tf" },
      ];

      const grouped = new Map<string, Finding[]>();
      for (const f of findings) {
        const file = f.file || "unknown";
        if (!grouped.has(file)) grouped.set(file, []);
        grouped.get(file)!.push(f);
      }

      expect(grouped.get("a.bicep")).to.have.length(2);
      expect(grouped.get("b.tf")).to.have.length(1);
    });
  });
});
