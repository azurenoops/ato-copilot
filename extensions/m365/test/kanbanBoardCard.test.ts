import { expect } from "chai";
import { buildKanbanBoardCard } from "../src/cards/kanbanBoardCard";

describe("Kanban Board Card (FR-010a)", () => {
  it("should produce Adaptive Card v1.5 JSON", () => {
    const card = buildKanbanBoardCard({});
    expect(card.type).to.equal("AdaptiveCard");
    expect(card.version).to.equal("1.5");
  });

  it("should display board title", () => {
    const card = buildKanbanBoardCard({ boardTitle: "Remediation Board" });
    const body = card.body as any[];
    const titleBlock = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("Remediation Board")
    );
    expect(titleBlock).to.exist;
  });

  it("should group tasks by status", () => {
    const card = buildKanbanBoardCard({
      tasks: [
        { taskId: "T1", title: "Fix encryption", status: "todo", severity: "High" },
        { taskId: "T2", title: "Enable MFA", status: "in-progress", severity: "Critical" },
        { taskId: "T3", title: "Update firewall", status: "done", severity: "Medium" },
      ],
    });
    const body = card.body as any[];
    // Should have column headers for each status
    const todoHeader = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("To Do")
    );
    expect(todoHeader).to.exist;

    const inProgressHeader = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("In Progress")
    );
    expect(inProgressHeader).to.exist;

    const doneHeader = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("Done")
    );
    expect(doneHeader).to.exist;
  });

  it("should display task titles", () => {
    const card = buildKanbanBoardCard({
      tasks: [
        { taskId: "T1", title: "Fix encryption", status: "todo" },
      ],
    });
    const body = card.body as any[];
    const taskBlock = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("Fix encryption")
    );
    expect(taskBlock).to.exist;
  });

  it("should show severity badges", () => {
    const card = buildKanbanBoardCard({
      tasks: [
        { taskId: "T1", title: "Fix it", status: "todo", severity: "Critical" },
      ],
    });
    const body = card.body as any[];
    // Severity appears as emoji badge in task text
    const taskBlock = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("🟣")
    );
    expect(taskBlock).to.exist;
  });

  it("should include action buttons for todo tasks", () => {
    const card = buildKanbanBoardCard({
      tasks: [
        { taskId: "T1", title: "Fix it", status: "todo" },
      ],
    });
    const actions = card.actions as any[];
    expect(actions).to.exist;
    const moveAction = actions?.find(
      (a: any) => typeof a.title === "string" && a.title.includes("Move")
    );
    expect(moveAction).to.exist;
  });

  it("should include agent attribution", () => {
    const card = buildKanbanBoardCard({ agentUsed: "ComplianceAgent" });
    const body = card.body as any[];
    const attr = body.find(
      (b: any) => typeof b.text === "string" && b.text.includes("Processed by:")
    );
    expect(attr).to.exist;
  });
});
