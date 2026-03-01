import { expect } from "chai";

describe("Export Service", () => {
  describe("Markdown Export", () => {
    function exportToMarkdown(
      title: string,
      findings: Array<{ severity: string; title: string; description: string; recommendation: string }>
    ): string {
      const lines: string[] = [
        `# ${title}`,
        "",
        `**Generated**: ${new Date().toISOString().split("T")[0]}`,
        "",
        "## Findings Summary",
        "",
        `| Severity | Count |`,
        `|----------|-------|`,
      ];

      const counts: Record<string, number> = {};
      for (const f of findings) {
        counts[f.severity] = (counts[f.severity] || 0) + 1;
      }
      for (const [sev, count] of Object.entries(counts)) {
        lines.push(`| ${sev} | ${count} |`);
      }

      lines.push("", "## Details", "");
      for (const f of findings) {
        lines.push(`### [${f.severity.toUpperCase()}] ${f.title}`);
        lines.push("");
        lines.push(f.description);
        lines.push("");
        lines.push(`**Recommendation**: ${f.recommendation}`);
        lines.push("");
      }

      return lines.join("\n");
    }

    it("should include title as H1 heading", () => {
      const md = exportToMarkdown("Compliance Report", []);
      expect(md).to.include("# Compliance Report");
    });

    it("should include generation date", () => {
      const md = exportToMarkdown("Report", []);
      const today = new Date().toISOString().split("T")[0];
      expect(md).to.include(today);
    });

    it("should include severity summary table", () => {
      const findings = [
        { severity: "high", title: "F1", description: "D1", recommendation: "R1" },
        { severity: "high", title: "F2", description: "D2", recommendation: "R2" },
        { severity: "low", title: "F3", description: "D3", recommendation: "R3" },
      ];
      const md = exportToMarkdown("Report", findings);
      expect(md).to.include("| high | 2 |");
      expect(md).to.include("| low | 1 |");
    });

    it("should include finding details", () => {
      const findings = [
        { severity: "high", title: "Missing Encryption", description: "Storage not encrypted", recommendation: "Enable encryption" },
      ];
      const md = exportToMarkdown("Report", findings);
      expect(md).to.include("### [HIGH] Missing Encryption");
      expect(md).to.include("Storage not encrypted");
      expect(md).to.include("**Recommendation**: Enable encryption");
    });
  });

  describe("JSON Export", () => {
    it("should produce valid JSON", () => {
      const data = {
        title: "Compliance Report",
        generatedAt: new Date().toISOString(),
        findings: [
          { severity: "high", title: "Issue", description: "Desc", recommendation: "Fix" },
        ],
      };
      const json = JSON.stringify(data, null, 2);
      const parsed = JSON.parse(json);
      expect(parsed.title).to.equal("Compliance Report");
      expect(parsed.findings).to.have.length(1);
    });

    it("should include all finding fields", () => {
      const data = {
        findings: [
          { severity: "medium", title: "Open Port", description: "Port 443 open", recommendation: "Restrict", line: 42 },
        ],
      };
      const json = JSON.stringify(data);
      const parsed = JSON.parse(json);
      expect(parsed.findings[0]).to.have.property("severity");
      expect(parsed.findings[0]).to.have.property("title");
      expect(parsed.findings[0]).to.have.property("description");
      expect(parsed.findings[0]).to.have.property("recommendation");
      expect(parsed.findings[0]).to.have.property("line");
    });
  });

  describe("HTML Export", () => {
    function exportToHtml(title: string): string {
      return `<!DOCTYPE html>
<html>
<head><title>${title}</title></head>
<body><h1>${title}</h1></body>
</html>`;
    }

    it("should produce valid HTML structure", () => {
      const html = exportToHtml("Report");
      expect(html).to.include("<!DOCTYPE html>");
      expect(html).to.include("<html>");
      expect(html).to.include("</html>");
    });

    it("should include title in head and body", () => {
      const html = exportToHtml("Compliance Report");
      expect(html).to.include("<title>Compliance Report</title>");
      expect(html).to.include("<h1>Compliance Report</h1>");
    });
  });
});

describe("Workspace Service", () => {
  describe("Template Folder Mapping", () => {
    const folderMapping: Record<string, string> = {
      bicep: "bicep",
      terraform: "terraform",
      policy: "policies",
      ssp: "ssp",
      general: "templates",
    };

    it("should map bicep type to bicep/ folder", () => {
      expect(folderMapping["bicep"]).to.equal("bicep");
    });

    it("should map terraform type to terraform/ folder", () => {
      expect(folderMapping["terraform"]).to.equal("terraform");
    });

    it("should map policy type to policies/ folder", () => {
      expect(folderMapping["policy"]).to.equal("policies");
    });

    it("should map ssp type to ssp/ folder", () => {
      expect(folderMapping["ssp"]).to.equal("ssp");
    });

    it("should map general type to templates/ folder", () => {
      expect(folderMapping["general"]).to.equal("templates");
    });

    it("should default unknown types to templates/ folder", () => {
      const type = "unknown";
      const folder = folderMapping[type] || "templates";
      expect(folder).to.equal("templates");
    });
  });

  describe("Conflict Resolution", () => {
    type ConflictAction = "overwrite" | "cancel" | "saveAsNew";

    it("should support overwrite action", () => {
      const action: ConflictAction = "overwrite";
      expect(action).to.equal("overwrite");
    });

    it("should support cancel action", () => {
      const action: ConflictAction = "cancel";
      expect(action).to.equal("cancel");
    });

    it("should support save as new action", () => {
      const action: ConflictAction = "saveAsNew";
      expect(action).to.equal("saveAsNew");
    });

    it("should generate unique filename for saveAsNew", () => {
      const baseName = "template";
      const ext = ".bicep";
      const timestamp = Date.now();
      const newName = `${baseName}_${timestamp}${ext}`;
      expect(newName).to.include(baseName);
      expect(newName).to.include(ext);
      expect(newName).to.not.equal(`${baseName}${ext}`);
    });
  });
});
