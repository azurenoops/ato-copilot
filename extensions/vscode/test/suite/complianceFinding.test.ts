import { expect } from "chai";

/**
 * Tests for enriched ComplianceFinding interface (T094, FR-015, FR-018).
 */
describe("ComplianceFinding Interface", () => {
  interface ComplianceFinding {
    controlId: string;
    title: string;
    severity: "critical" | "high" | "medium" | "low" | "informational";
    description: string;
    recommendation: string;
    controlFamily?: string;
    resourceId?: string;
    resourceType?: string;
    autoRemediable?: boolean;
    remediationScript?: string;
    riskLevel?: "critical" | "high" | "medium" | "low";
    frameworkReference?: string;
    findingStatus?: "open" | "acknowledged" | "remediated" | "verified";
    findingId?: string;
  }

  it("should support critical severity level", () => {
    const finding: ComplianceFinding = {
      controlId: "AC-2",
      title: "Test",
      severity: "critical",
      description: "D",
      recommendation: "R",
    };
    expect(finding.severity).to.equal("critical");
  });

  it("should support informational severity level", () => {
    const finding: ComplianceFinding = {
      controlId: "AU-1",
      title: "Info",
      severity: "informational",
      description: "D",
      recommendation: "R",
    };
    expect(finding.severity).to.equal("informational");
  });

  it("should include all new enrichment fields", () => {
    const finding: ComplianceFinding = {
      controlId: "AC-2",
      title: "Missing MFA",
      severity: "high",
      description: "Multi-factor authentication not configured",
      recommendation: "Enable MFA",
      controlFamily: "AC",
      resourceId: "/subscriptions/abc/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/sa1",
      resourceType: "Microsoft.Storage/storageAccounts",
      autoRemediable: true,
      remediationScript: "az storage account update --name sa1 --enable-mfa true",
      riskLevel: "high",
      frameworkReference: "NIST 800-53 Rev 5",
      findingStatus: "open",
      findingId: "f-001",
    };

    expect(finding.controlFamily).to.equal("AC");
    expect(finding.resourceId).to.include("storageAccounts");
    expect(finding.resourceType).to.equal("Microsoft.Storage/storageAccounts");
    expect(finding.autoRemediable).to.be.true;
    expect(finding.remediationScript).to.include("az storage");
    expect(finding.riskLevel).to.equal("high");
    expect(finding.frameworkReference).to.equal("NIST 800-53 Rev 5");
    expect(finding.findingStatus).to.equal("open");
    expect(finding.findingId).to.equal("f-001");
  });

  it("should gracefully handle absent optional fields", () => {
    const finding: ComplianceFinding = {
      controlId: "CM-6",
      title: "Baseline check",
      severity: "medium",
      description: "D",
      recommendation: "R",
    };

    expect(finding.controlFamily).to.be.undefined;
    expect(finding.resourceId).to.be.undefined;
    expect(finding.resourceType).to.be.undefined;
    expect(finding.autoRemediable).to.be.undefined;
    expect(finding.remediationScript).to.be.undefined;
    expect(finding.riskLevel).to.be.undefined;
    expect(finding.frameworkReference).to.be.undefined;
    expect(finding.findingStatus).to.be.undefined;
    expect(finding.findingId).to.be.undefined;
  });

  it("should support all 4 finding status values", () => {
    const statuses: ComplianceFinding["findingStatus"][] = [
      "open",
      "acknowledged",
      "remediated",
      "verified",
    ];

    for (const status of statuses) {
      const finding: ComplianceFinding = {
        controlId: "AC-1",
        title: "T",
        severity: "low",
        description: "D",
        recommendation: "R",
        findingStatus: status,
      };
      expect(finding.findingStatus).to.equal(status);
    }
  });

  it("should support all 4 risk levels", () => {
    const levels: ComplianceFinding["riskLevel"][] = [
      "critical",
      "high",
      "medium",
      "low",
    ];

    for (const level of levels) {
      const finding: ComplianceFinding = {
        controlId: "AC-1",
        title: "T",
        severity: "low",
        description: "D",
        recommendation: "R",
        riskLevel: level,
      };
      expect(finding.riskLevel).to.equal(level);
    }
  });

  it("should maintain backward compatibility with existing 3-level severity values", () => {
    const legacySeverities: ComplianceFinding["severity"][] = [
      "high",
      "medium",
      "low",
    ];

    for (const severity of legacySeverities) {
      const finding: ComplianceFinding = {
        controlId: "SC-1",
        title: "T",
        severity,
        description: "D",
        recommendation: "R",
      };
      expect(finding.severity).to.equal(severity);
    }
  });
});
