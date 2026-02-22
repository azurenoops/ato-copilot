using System.Text.Json;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Agents.Compliance.Agents;

namespace Ato.Copilot.Tests.Unit.Agents;

/// <summary>
/// Unit tests for ComplianceAgent context-aware task resolution (US14).
/// Verifies ordinal references ("first", "second", "last") resolve
/// to correct task IDs from the last stored task list.
/// </summary>
public class KanbanContextResolutionTests
{
    // ─── ResolveTaskFromContext ──────────────────────────────────────────────

    [Fact]
    public void ResolveTask_First_ReturnsFirstTaskId()
    {
        var context = CreateContextWithTasks("task-aaa", "task-bbb", "task-ccc");

        var result = ComplianceAgent.ResolveTaskFromContext("move the first one to in progress", context);

        result.Should().Be("task-aaa");
    }

    [Fact]
    public void ResolveTask_Second_ReturnsSecondTaskId()
    {
        var context = CreateContextWithTasks("task-aaa", "task-bbb", "task-ccc");

        var result = ComplianceAgent.ResolveTaskFromContext("assign the second task to john", context);

        result.Should().Be("task-bbb");
    }

    [Fact]
    public void ResolveTask_Third_ReturnsThirdTaskId()
    {
        var context = CreateContextWithTasks("task-aaa", "task-bbb", "task-ccc");

        var result = ComplianceAgent.ResolveTaskFromContext("show the third one", context);

        result.Should().Be("task-ccc");
    }

    [Fact]
    public void ResolveTask_Last_ReturnsLastTaskId()
    {
        var context = CreateContextWithTasks("task-aaa", "task-bbb", "task-ccc", "task-ddd");

        var result = ComplianceAgent.ResolveTaskFromContext("move the last one to done", context);

        result.Should().Be("task-ddd");
    }

    [Fact]
    public void ResolveTask_NoContext_ReturnsNull()
    {
        var context = new AgentConversationContext();

        var result = ComplianceAgent.ResolveTaskFromContext("move the first one", context);

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveTask_EmptyTaskList_ReturnsNull()
    {
        var context = new AgentConversationContext();
        context.WorkflowState[ComplianceAgent.LastResultsKey] = new List<string>();

        var result = ComplianceAgent.ResolveTaskFromContext("move the first one", context);

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveTask_OutOfRange_ReturnsNull()
    {
        var context = CreateContextWithTasks("task-aaa");

        var result = ComplianceAgent.ResolveTaskFromContext("move the fifth task", context);

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveTask_NoOrdinal_ReturnsNull()
    {
        var context = CreateContextWithTasks("task-aaa", "task-bbb");

        var result = ComplianceAgent.ResolveTaskFromContext("move task rem-001 to done", context);

        result.Should().BeNull();
    }

    // ─── StoreTaskListResults ───────────────────────────────────────────────

    [Fact]
    public void StoreResults_ArrayFormat_StoresTaskIds()
    {
        var json = JsonSerializer.Serialize(new
        {
            status = "success",
            data = new[]
            {
                new { id = "id-1", title = "Task 1" },
                new { id = "id-2", title = "Task 2" },
                new { id = "id-3", title = "Task 3" }
            }
        });

        var context = new AgentConversationContext();
        ComplianceAgent.StoreTaskListResults(json, context);

        context.WorkflowState.Should().ContainKey(ComplianceAgent.LastResultsKey);
        var ids = context.WorkflowState[ComplianceAgent.LastResultsKey] as List<string>;
        ids.Should().BeEquivalentTo(["id-1", "id-2", "id-3"]);
    }

    [Fact]
    public void StoreResults_ObjectWithTasksArray_StoresTaskIds()
    {
        var json = JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                boardId = "board-1",
                tasks = new[]
                {
                    new { id = "t-1", title = "A" },
                    new { id = "t-2", title = "B" }
                }
            }
        });

        var context = new AgentConversationContext();
        ComplianceAgent.StoreTaskListResults(json, context);

        var ids = context.WorkflowState[ComplianceAgent.LastResultsKey] as List<string>;
        ids.Should().BeEquivalentTo(["t-1", "t-2"]);
    }

    [Fact]
    public void StoreResults_NonJsonInput_DoesNotThrow()
    {
        var context = new AgentConversationContext();

        var act = () => ComplianceAgent.StoreTaskListResults("not json at all", context);

        act.Should().NotThrow();
        context.WorkflowState.Should().NotContainKey(ComplianceAgent.LastResultsKey);
    }

    [Fact]
    public void StoreResults_NoDataProperty_DoesNotStore()
    {
        var json = JsonSerializer.Serialize(new { status = "error", message = "not found" });

        var context = new AgentConversationContext();
        ComplianceAgent.StoreTaskListResults(json, context);

        context.WorkflowState.Should().NotContainKey(ComplianceAgent.LastResultsKey);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static AgentConversationContext CreateContextWithTasks(params string[] taskIds)
    {
        var context = new AgentConversationContext();
        context.WorkflowState[ComplianceAgent.LastResultsKey] = new List<string>(taskIds);
        return context;
    }
}
