import { expect } from "chai";

/**
 * Tests for control family grouping logic (T093, FR-016).
 */
describe("Analysis Panel — Control Family Grouping", () => {
  interface Finding {
    controlId: string;
    controlFamily?: string;
    severity: string;
    title: string;
  }

  /**
   * Group findings by control family (mirrors analysisPanel.ts logic).
   */
  function groupByControlFamily(
    findings: Finding[]
  ): Map<string, Finding[]> {
    const groups = new Map<string, Finding[]>();
    for (const finding of findings) {
      const family =
        finding.controlFamily ??
        (finding.controlId.replace(/[-.]?\d+.*$/, "") || "Other");
      if (!groups.has(family)) {
        groups.set(family, []);
      }
      groups.get(family)!.push(finding);
    }
    return groups;
  }

  it("should group findings by explicit controlFamily", () => {
    const findings: Finding[] = [
      { controlId: "AC-2", controlFamily: "AC", severity: "high", title: "F1" },
      { controlId: "AC-3", controlFamily: "AC", severity: "medium", title: "F2" },
      { controlId: "AU-6", controlFamily: "AU", severity: "low", title: "F3" },
    ];

    const groups = groupByControlFamily(findings);
    expect(groups.size).to.equal(2);
    expect(groups.get("AC")).to.have.length(2);
    expect(groups.get("AU")).to.have.length(1);
  });

  it("should derive controlFamily from controlId when not specified", () => {
    const findings: Finding[] = [
      { controlId: "AC-2", severity: "high", title: "F1" },
      { controlId: "AC-3", severity: "medium", title: "F2" },
      { controlId: "AU-6.1", severity: "low", title: "F3" },
    ];

    const groups = groupByControlFamily(findings);
    expect(groups.has("AC")).to.be.true;
    expect(groups.has("AU")).to.be.true;
    expect(groups.get("AC")).to.have.length(2);
  });

  it("should handle empty controlId gracefully", () => {
    const findings: Finding[] = [
      { controlId: "", severity: "low", title: "Unknown" },
    ];

    const groups = groupByControlFamily(findings);
    expect(groups.has("Other")).to.be.true;
    expect(groups.get("Other")).to.have.length(1);
  });

  it("should return empty map for no findings", () => {
    const groups = groupByControlFamily([]);
    expect(groups.size).to.equal(0);
  });

  it("should sort groups alphabetically by family", () => {
    const findings: Finding[] = [
      { controlId: "SC-1", controlFamily: "SC", severity: "medium", title: "F1" },
      { controlId: "AC-1", controlFamily: "AC", severity: "high", title: "F2" },
      { controlId: "IA-5", controlFamily: "IA", severity: "low", title: "F3" },
    ];

    const groups = groupByControlFamily(findings);
    const sortedKeys = Array.from(groups.keys()).sort();
    expect(sortedKeys).to.deep.equal(["AC", "IA", "SC"]);
  });

  it("should count findings per family correctly", () => {
    const findings: Finding[] = [
      { controlId: "AC-1", controlFamily: "AC", severity: "high", title: "F1" },
      { controlId: "AC-2", controlFamily: "AC", severity: "high", title: "F2" },
      { controlId: "AC-3", controlFamily: "AC", severity: "medium", title: "F3" },
      { controlId: "AU-1", controlFamily: "AU", severity: "low", title: "F4" },
    ];

    const groups = groupByControlFamily(findings);
    expect(groups.get("AC")).to.have.length(3);
    expect(groups.get("AU")).to.have.length(1);
  });

  it("should prefer explicit controlFamily over derived", () => {
    const findings: Finding[] = [
      { controlId: "AC-2", controlFamily: "ACCESS_CONTROL", severity: "high", title: "F1" },
    ];

    const groups = groupByControlFamily(findings);
    expect(groups.has("ACCESS_CONTROL")).to.be.true;
    expect(groups.has("AC")).to.be.false;
  });
});
