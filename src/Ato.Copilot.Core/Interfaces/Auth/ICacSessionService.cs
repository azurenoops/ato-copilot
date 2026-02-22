using Ato.Copilot.Core.Models.Auth;

namespace Ato.Copilot.Core.Interfaces.Auth;

/// <summary>
/// Service interface for CAC/PIV authentication session management.
/// All async methods accept CancellationToken per Constitution VIII.
/// </summary>
public interface ICacSessionService
{
    /// <summary>
    /// Creates a new CAC session from a validated JWT token.
    /// </summary>
    /// <param name="userId">Azure AD object ID of the user.</param>
    /// <param name="displayName">Display name of the user.</param>
    /// <param name="email">Email address of the user.</param>
    /// <param name="tokenHash">SHA-256 hash of the JWT token.</param>
    /// <param name="clientType">Client surface type.</param>
    /// <param name="ipAddress">IP address of the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created CacSession.</returns>
    Task<CacSession> CreateSessionAsync(
        string userId, string displayName, string email,
        string tokenHash, ClientType clientType, string ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active session for a user, if one exists and is not expired.
    /// </summary>
    /// <param name="userId">Azure AD object ID of the user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active CacSession, or null if none exists.</returns>
    Task<CacSession?> GetActiveSessionAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Terminates an active session (sign-out).
    /// </summary>
    /// <param name="sessionId">The session ID to terminate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task TerminateSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the timeout for an active session.
    /// </summary>
    /// <param name="sessionId">The session ID to update.</param>
    /// <param name="timeoutHours">New timeout in hours (1-24).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated CacSession.</returns>
    Task<CacSession> UpdateTimeoutAsync(Guid sessionId, int timeoutHours, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a user has an active (non-expired) session.
    /// </summary>
    /// <param name="userId">Azure AD object ID of the user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if an active session exists.</returns>
    Task<bool> IsSessionActiveAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all sessions for a user (active, expired, and terminated).
    /// </summary>
    /// <param name="userId">Azure AD object ID of the user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of CacSessions for the user.</returns>
    Task<List<CacSession>> GetSessionsByUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks for expiration warnings on the user's active session and PIM roles.
    /// Returns warnings for sessions/roles expiring within the configured warning threshold.
    /// </summary>
    /// <param name="userId">Azure AD object ID of the user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of expiration warning strings, empty if no warnings.</returns>
    Task<List<string>> GetExpirationWarningsAsync(string userId, CancellationToken cancellationToken = default);
}
