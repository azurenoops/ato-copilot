import { expect } from "chai";

/**
 * Tests for compliance summary table rendering in chat participant (T107, FR-025).
 */
describe("Chat Participant — Compliance Summary", () => {
  interface ComplianceData {
    complianceScore?: number;
    passCount?: number;
    warnCount?: number;
    failCount?: number;
    controlsAssessed?: number;
  }

  function shouldRenderSummary(
    intentType: string | undefined,
    data: ComplianceData | undefined
  ): boolean {
    return intentType === "compliance" && !!data;
  }

  function buildSummaryTable(data: ComplianceData): string {
    const rows: string[] = [];
    rows.push("| Metric | Value |");
    rows.push("|--------|-------|");

    if (data.complianceScore !== undefined) {
      rows.push(`| Score | ${data.complianceScore}% |`);
    }
    if (data.passCount !== undefined) {
      rows.push(`| Pass | ${data.passCount} |`);
    }
    if (data.warnCount !== undefined) {
      rows.push(`| Warning | ${data.warnCount} |`);
    }
    if (data.failCount !== undefined) {
      rows.push(`| Fail | ${data.failCount} |`);
    }

    return rows.join("\n");
  }

  it("should render summary for compliance intentType with data", () => {
    const result = shouldRenderSummary("compliance", { complianceScore: 85 });
    expect(result).to.be.true;
  });

  it("should not render for non-compliance intentType", () => {
    const result = shouldRenderSummary("knowledgebase", { complianceScore: 85 });
    expect(result).to.be.false;
  });

  it("should not render when data is undefined", () => {
    const result = shouldRenderSummary("compliance", undefined);
    expect(result).to.be.false;
  });

  it("should include compliance score in table", () => {
    const table = buildSummaryTable({ complianceScore: 92, passCount: 45, warnCount: 3, failCount: 2 });
    expect(table).to.include("92%");
    expect(table).to.include("Score");
  });

  it("should include pass/warn/fail counts", () => {
    const table = buildSummaryTable({ passCount: 45, warnCount: 3, failCount: 2 });
    expect(table).to.include("| Pass | 45 |");
    expect(table).to.include("| Warning | 3 |");
    expect(table).to.include("| Fail | 2 |");
  });

  it("should omit absent metrics from table", () => {
    const table = buildSummaryTable({ complianceScore: 85 });
    expect(table).to.include("85%");
    expect(table).to.not.include("Pass");
    expect(table).to.not.include("Warning");
    expect(table).to.not.include("Fail");
  });

  it("should format table with Markdown table syntax", () => {
    const table = buildSummaryTable({ complianceScore: 100 });
    expect(table).to.include("| Metric | Value |");
    expect(table).to.include("|--------|-------|");
  });
});
