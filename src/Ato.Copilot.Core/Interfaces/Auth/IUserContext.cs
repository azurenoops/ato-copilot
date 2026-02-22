namespace Ato.Copilot.Core.Interfaces.Auth;

/// <summary>
/// Represents the authenticated user's identity available to all tool invocations.
/// Provides user ID, display name, compliance role, and authentication status
/// for identity propagation and current-user task highlighting.
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// Unique identifier from authentication claims (<c>oid</c> or <c>sub</c>).
    /// Defaults to <c>"anonymous"</c> when no authentication context is present.
    /// </summary>
    string UserId { get; }

    /// <summary>
    /// Human-readable display name from <c>Identity.Name</c>.
    /// Falls back to <see cref="UserId"/> if the name claim is missing.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Highest-privilege compliance role from claim roles.
    /// One of: <c>Compliance.Administrator</c>, <c>Compliance.SecurityLead</c>,
    /// <c>Compliance.Analyst</c>, <c>Compliance.Auditor</c>,
    /// <c>Compliance.PlatformEngineer</c>, <c>Compliance.Viewer</c>.
    /// Defaults to <c>Compliance.Viewer</c>.
    /// </summary>
    string Role { get; }

    /// <summary>
    /// Whether the user has been authenticated.
    /// <c>false</c> when no authentication context is present.
    /// </summary>
    bool IsAuthenticated { get; }
}
