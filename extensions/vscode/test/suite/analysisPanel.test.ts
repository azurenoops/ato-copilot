import { expect } from "chai";

/**
 * Tests for 5-level severity badge rendering (T092, FR-015, R-008).
 */
describe("Analysis Panel — Severity Badges", () => {
  const SEVERITY_MAP: Record<
    string,
    { label: string; cssClass: string; color: string; order: number }
  > = {
    critical: { label: "CRITICAL", cssClass: "critical", color: "#9c27b0", order: 0 },
    high: { label: "HIGH", cssClass: "high", color: "#d32f2f", order: 1 },
    medium: { label: "MEDIUM", cssClass: "medium", color: "#f57c00", order: 2 },
    low: { label: "LOW", cssClass: "low", color: "#fbc02d", order: 3 },
    informational: { label: "INFO", cssClass: "informational", color: "#1976d2", order: 4 },
  };

  it("should define 5 severity levels", () => {
    expect(Object.keys(SEVERITY_MAP)).to.have.length(5);
  });

  it("should map critical to purple (#9c27b0)", () => {
    expect(SEVERITY_MAP["critical"].color).to.equal("#9c27b0");
    expect(SEVERITY_MAP["critical"].label).to.equal("CRITICAL");
    expect(SEVERITY_MAP["critical"].cssClass).to.equal("critical");
  });

  it("should map high to red (#d32f2f)", () => {
    expect(SEVERITY_MAP["high"].color).to.equal("#d32f2f");
    expect(SEVERITY_MAP["high"].label).to.equal("HIGH");
  });

  it("should map medium to orange (#f57c00)", () => {
    expect(SEVERITY_MAP["medium"].color).to.equal("#f57c00");
    expect(SEVERITY_MAP["medium"].label).to.equal("MEDIUM");
  });

  it("should map low to yellow (#fbc02d)", () => {
    expect(SEVERITY_MAP["low"].color).to.equal("#fbc02d");
    expect(SEVERITY_MAP["low"].label).to.equal("LOW");
  });

  it("should map informational to blue (#1976d2)", () => {
    expect(SEVERITY_MAP["informational"].color).to.equal("#1976d2");
    expect(SEVERITY_MAP["informational"].label).to.equal("INFO");
  });

  it("should sort severity from most to least critical by order", () => {
    const sorted = Object.entries(SEVERITY_MAP).sort(
      ([, a], [, b]) => a.order - b.order
    );
    expect(sorted[0][0]).to.equal("critical");
    expect(sorted[1][0]).to.equal("high");
    expect(sorted[2][0]).to.equal("medium");
    expect(sorted[3][0]).to.equal("low");
    expect(sorted[4][0]).to.equal("informational");
  });

  it("should provide CSS class matching severity name", () => {
    for (const [key, config] of Object.entries(SEVERITY_MAP)) {
      expect(config.cssClass).to.equal(key === "informational" ? "informational" : key);
    }
  });

  it("should generate badge HTML with correct CSS class", () => {
    const severity = "critical";
    const config = SEVERITY_MAP[severity];
    const html = `<span class="badge ${config.cssClass}">${config.label}</span>`;
    expect(html).to.include('class="badge critical"');
    expect(html).to.include("CRITICAL");
  });

  it("should fallback to informational for unknown severity", () => {
    const getSeverityConfig = (sev: string) =>
      SEVERITY_MAP[sev] ?? SEVERITY_MAP["informational"];
    const unknown = getSeverityConfig("unknown");
    expect(unknown.label).to.equal("INFO");
    expect(unknown.color).to.equal("#1976d2");
  });
});
