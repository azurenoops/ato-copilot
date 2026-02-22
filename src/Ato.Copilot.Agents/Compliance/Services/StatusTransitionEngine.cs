using Ato.Copilot.Core.Models.Kanban;
using TaskStatus = Ato.Copilot.Core.Models.Kanban.TaskStatus;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Encodes the 16 allowed Kanban status transitions and their rules.
/// Implements the state machine defined in data-model.md.
/// </summary>
public static class StatusTransitionEngine
{
    /// <summary>
    /// Rules that govern a specific status transition.
    /// </summary>
    public class TransitionRule
    {
        /// <summary>Whether a comment is required for this transition (e.g., → Blocked).</summary>
        public bool RequiresComment { get; init; }

        /// <summary>Whether a resolution comment is required (e.g., Blocked →).</summary>
        public bool RequiresResolutionComment { get; init; }

        /// <summary>Whether validation must pass before this transition (e.g., → Done).</summary>
        public bool RequiresValidation { get; init; }

        /// <summary>Whether a CO can skip validation for this transition.</summary>
        public bool AllowSkipValidation { get; init; }

        /// <summary>Whether this transition triggers a validation scan (e.g., → InReview).</summary>
        public bool TriggersValidation { get; init; }

        /// <summary>Whether this transition auto-assigns the task if unassigned.</summary>
        public bool AutoAssign { get; init; }
    }

    private static readonly Dictionary<(TaskStatus From, TaskStatus To), TransitionRule> Transitions = new()
    {
        // Backlog transitions
        [(TaskStatus.Backlog, TaskStatus.ToDo)] = new TransitionRule(),
        [(TaskStatus.Backlog, TaskStatus.InProgress)] = new TransitionRule { AutoAssign = true },
        [(TaskStatus.Backlog, TaskStatus.Blocked)] = new TransitionRule { RequiresComment = true },

        // ToDo transitions
        [(TaskStatus.ToDo, TaskStatus.InProgress)] = new TransitionRule { AutoAssign = true },
        [(TaskStatus.ToDo, TaskStatus.Blocked)] = new TransitionRule { RequiresComment = true },
        [(TaskStatus.ToDo, TaskStatus.Backlog)] = new TransitionRule(),

        // InProgress transitions
        [(TaskStatus.InProgress, TaskStatus.InReview)] = new TransitionRule { TriggersValidation = true },
        [(TaskStatus.InProgress, TaskStatus.Blocked)] = new TransitionRule { RequiresComment = true },
        [(TaskStatus.InProgress, TaskStatus.ToDo)] = new TransitionRule(),

        // InReview transitions
        [(TaskStatus.InReview, TaskStatus.Done)] = new TransitionRule { RequiresValidation = true, AllowSkipValidation = true },
        [(TaskStatus.InReview, TaskStatus.Blocked)] = new TransitionRule { RequiresComment = true },
        [(TaskStatus.InReview, TaskStatus.InProgress)] = new TransitionRule(),

        // Blocked transitions (all require resolution comment)
        [(TaskStatus.Blocked, TaskStatus.Backlog)] = new TransitionRule { RequiresResolutionComment = true },
        [(TaskStatus.Blocked, TaskStatus.ToDo)] = new TransitionRule { RequiresResolutionComment = true },
        [(TaskStatus.Blocked, TaskStatus.InProgress)] = new TransitionRule { RequiresResolutionComment = true },
        [(TaskStatus.Blocked, TaskStatus.InReview)] = new TransitionRule { RequiresResolutionComment = true },
    };

    /// <summary>
    /// Checks whether a status transition is allowed.
    /// </summary>
    /// <param name="from">Current task status.</param>
    /// <param name="to">Target task status.</param>
    /// <returns>True if the transition is in the allowed set.</returns>
    public static bool IsTransitionAllowed(TaskStatus from, TaskStatus to)
    {
        return Transitions.ContainsKey((from, to));
    }

    /// <summary>
    /// Gets the transition rule for a specific status change.
    /// </summary>
    /// <param name="from">Current task status.</param>
    /// <param name="to">Target task status.</param>
    /// <returns>The transition rule, or null if the transition is not allowed.</returns>
    public static TransitionRule? GetTransitionRule(TaskStatus from, TaskStatus to)
    {
        return Transitions.TryGetValue((from, to), out var rule) ? rule : null;
    }

    /// <summary>
    /// Gets all allowed target statuses from a given status.
    /// </summary>
    /// <param name="from">Current task status.</param>
    /// <returns>List of valid target statuses.</returns>
    public static IReadOnlyList<TaskStatus> GetAllowedTransitions(TaskStatus from)
    {
        return Transitions.Keys
            .Where(k => k.From == from)
            .Select(k => k.To)
            .ToList()
            .AsReadOnly();
    }
}
