import { expect } from "chai";
import {
  buildRmfOverviewHtml,
  type RmfSystemOverview,
} from "../../src/panels/rmfOverviewHtml";

/**
 * Tests for RMF Overview Panel (US13, T174).
 *
 * Tests the HTML rendering logic of the RMF overview webview panel
 * without requiring vscode API (uses buildRmfOverviewHtml directly).
 */
describe("RMF Overview Panel", () => {
  const baseData: RmfSystemOverview = {
    systemName: "ACME Portal",
    acronym: "AP",
    systemType: "Major Application",
    hostingEnvironment: "Azure Commercial",
    currentRmfStep: "Implement",
    rmfStepNumber: 4,
    missionCriticality: "Mission Essential",
    impactLevel: "IL4",
    complianceScore: 82,
    activeAlerts: 3,
    isActive: true,
    atoStatus: "Active",
    atoExpiration: "2027-01-15",
  };

  describe("HTML Structure", () => {
    it("should produce valid HTML with DOCTYPE", () => {
      const html = buildRmfOverviewHtml(baseData);
      expect(html).to.include("<!DOCTYPE html>");
      expect(html).to.include("<html");
      expect(html).to.include("</html>");
    });

    it("should include CSS styles", () => {
      const html = buildRmfOverviewHtml(baseData);
      expect(html).to.include("<style>");
      expect(html).to.include("</style>");
    });

    it("should include webview script", () => {
      const html = buildRmfOverviewHtml(baseData);
      expect(html).to.include("<script>");
      expect(html).to.include("acquireVsCodeApi");
    });
  });

  describe("Header Section", () => {
    it("should display system name", () => {
      const html = buildRmfOverviewHtml(baseData);
      expect(html).to.include("ACME Portal");
    });

    it("should display acronym", () => {
      const html = buildRmfOverviewHtml(baseData);
      expect(html).to.include("(AP)");
    });

    it("should not display acronym when not provided", () => {
      const html = buildRmfOverviewHtml({ ...baseData, acronym: undefined });
      expect(html).not.to.include("(AP)");
    });

    it("should show Active badge for active systems", () => {
      const html = buildRmfOverviewHtml(baseData);
      expect(html).to.include("badge-good");
      expect(html).to.include(">Active<");
    });

    it("should show Inactive badge for inactive systems", () => {
      const html = buildRmfOverviewHtml({ ...baseData, isActive: false });
      expect(html).to.include("badge-attention");
      expect(html).to.include(">Inactive<");
    });
  });

  describe("RMF Stepper", () => {
    it("should render all 7 RMF steps", () => {
      const html = buildRmfOverviewHtml(baseData);
      const steps = ["Prepare", "Categorize", "Select", "Implement", "Assess", "Authorize", "Monitor"];
      for (const step of steps) {
        expect(html).to.include(step);
      }
    });

    it("should mark current step with step-current class", () => {
      const html = buildRmfOverviewHtml(baseData);
      // Implement is the current step
      expect(html).to.include('class="step step-current" data-step="Implement"');
    });

    it("should mark prior steps with step-complete class", () => {
      const html = buildRmfOverviewHtml(baseData);
      // Prepare, Categorize, Select are before Implement
      expect(html).to.include('class="step step-complete" data-step="Prepare"');
      expect(html).to.include('class="step step-complete" data-step="Categorize"');
      expect(html).to.include('class="step step-complete" data-step="Select"');
    });

    it("should mark future steps with step-upcoming class", () => {
      const html = buildRmfOverviewHtml(baseData);
      // Assess, Authorize, Monitor are after Implement
      expect(html).to.include('class="step step-upcoming" data-step="Assess"');
      expect(html).to.include('class="step step-upcoming" data-step="Authorize"');
      expect(html).to.include('class="step step-upcoming" data-step="Monitor"');
    });

    it("should include step emojis", () => {
      const html = buildRmfOverviewHtml(baseData);
      expect(html).to.include("📋"); // Prepare
      expect(html).to.include("🔧"); // Implement
      expect(html).to.include("📊"); // Monitor
    });

    it("should include onclick handlers for step navigation", () => {
      const html = buildRmfOverviewHtml(baseData);
      expect(html).to.include("handleStepClick('Prepare')");
      expect(html).to.include("handleStepClick('Monitor')");
    });
  });

  describe("Details Grid", () => {
    it("should display system type", () => {
      const html = buildRmfOverviewHtml(baseData);
      expect(html).to.include("Major Application");
    });

    it("should display hosting environment", () => {
      const html = buildRmfOverviewHtml(baseData);
      expect(html).to.include("Azure Commercial");
    });

    it("should display mission criticality", () => {
      const html = buildRmfOverviewHtml(baseData);
      expect(html).to.include("Mission Essential");
    });

    it("should display impact level", () => {
      const html = buildRmfOverviewHtml(baseData);
      expect(html).to.include("IL4");
    });

    it("should display compliance score with color class", () => {
      const html = buildRmfOverviewHtml(baseData);
      expect(html).to.include("82%");
      expect(html).to.include("score-good");
    });

    it("should use score-warning for scores 60-79", () => {
      const html = buildRmfOverviewHtml({ ...baseData, complianceScore: 65 });
      expect(html).to.include("65%");
      expect(html).to.include("score-warning");
    });

    it("should use score-attention for scores <60", () => {
      const html = buildRmfOverviewHtml({ ...baseData, complianceScore: 45 });
      expect(html).to.include("45%");
      expect(html).to.include("score-attention");
    });

    it("should show dash for missing values", () => {
      const html = buildRmfOverviewHtml({
        systemName: "Test",
        isActive: true,
      });
      // Multiple dashes for missing fields
      expect(html).to.include("—");
    });
  });

  describe("Categorization Section", () => {
    it("should render categorization when provided", () => {
      const html = buildRmfOverviewHtml({
        ...baseData,
        categorization: {
          fipsCategory: "Moderate",
          confidentialityImpact: "Moderate",
          integrityImpact: "Low",
          availabilityImpact: "High",
        },
      });
      expect(html).to.include("Security Categorization");
      expect(html).to.include("FIPS 199");
      expect(html).to.include("Moderate");
    });

    it("should not render categorization section when not provided", () => {
      const html = buildRmfOverviewHtml({ ...baseData, categorization: undefined });
      expect(html).not.to.include("Security Categorization");
    });

    it("should apply impact CSS classes for C/I/A levels", () => {
      const html = buildRmfOverviewHtml({
        ...baseData,
        categorization: {
          confidentialityImpact: "High",
          integrityImpact: "Moderate",
          availabilityImpact: "Low",
        },
      });
      expect(html).to.include("impact-high");
      expect(html).to.include("impact-moderate");
      expect(html).to.include("impact-low");
    });
  });

  describe("Baseline Section", () => {
    it("should render baseline progress when provided", () => {
      const html = buildRmfOverviewHtml({
        ...baseData,
        controlBaseline: {
          baselineName: "DoD IL4 Moderate",
          totalControls: 200,
          implementedControls: 160,
        },
      });
      expect(html).to.include("Control Baseline");
      expect(html).to.include("DoD IL4 Moderate");
      expect(html).to.include("160 / 200 controls implemented (80%)");
    });

    it("should not render baseline section when not provided", () => {
      const html = buildRmfOverviewHtml({ ...baseData, controlBaseline: undefined });
      expect(html).not.to.include("Control Baseline");
    });

    it("should render progress bar with percentage width", () => {
      const html = buildRmfOverviewHtml({
        ...baseData,
        controlBaseline: {
          totalControls: 100,
          implementedControls: 75,
        },
      });
      expect(html).to.include('style="width: 75%"');
    });
  });

  describe("ATO Section", () => {
    it("should render ATO status when provided", () => {
      const html = buildRmfOverviewHtml(baseData);
      expect(html).to.include("Authorization Status");
      expect(html).to.include("Active");
    });

    it("should show badge-good for Active status", () => {
      const html = buildRmfOverviewHtml(baseData);
      // ATO section has badge-good for active
      const atoSection = html.split("Authorization Status")[1] ?? "";
      expect(atoSection).to.include("badge-good");
    });

    it("should show expiration date", () => {
      const html = buildRmfOverviewHtml(baseData);
      expect(html).to.include("2027-01-15");
    });

    it("should show active alerts count", () => {
      const html = buildRmfOverviewHtml(baseData);
      expect(html).to.include("3 active alerts");
    });

    it("should not render ATO section when status not provided", () => {
      const html = buildRmfOverviewHtml({ ...baseData, atoStatus: undefined });
      expect(html).not.to.include("Authorization Status");
    });
  });

  describe("Actions", () => {
    it("should include View Compliance Details button", () => {
      const html = buildRmfOverviewHtml(baseData);
      expect(html).to.include("View Compliance Details");
      expect(html).to.include("handleAction('viewCompliance')");
    });

    it("should include Refresh Data button", () => {
      const html = buildRmfOverviewHtml(baseData);
      expect(html).to.include("Refresh Data");
      expect(html).to.include("handleAction('refresh')");
    });
  });

  describe("HTML Escaping", () => {
    it("should escape HTML special characters in system name", () => {
      const html = buildRmfOverviewHtml({
        ...baseData,
        systemName: "System <script>alert('xss')</script>",
      });
      expect(html).not.to.include("<script>alert");
      expect(html).to.include("&lt;script&gt;");
    });

    it("should escape ampersands", () => {
      const html = buildRmfOverviewHtml({
        ...baseData,
        systemName: "A&B System",
      });
      expect(html).to.include("A&amp;B System");
    });
  });

  describe("CSS Themes", () => {
    it("should use VS Code CSS variables for theming", () => {
      const html = buildRmfOverviewHtml(baseData);
      expect(html).to.include("--vscode-editor-background");
      expect(html).to.include("--vscode-editor-foreground");
      expect(html).to.include("--vscode-textLink-foreground");
    });
  });
});
