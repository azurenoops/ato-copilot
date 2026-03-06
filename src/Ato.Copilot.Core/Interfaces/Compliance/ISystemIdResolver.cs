namespace Ato.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Resolves a system identifier that may be a GUID, a system name, or an acronym
/// into the canonical RegisteredSystem GUID.
/// This eliminates the need for the LLM to call compliance_list_systems before
/// every tool invocation — tools auto-resolve names transparently.
/// </summary>
public interface ISystemIdResolver
{
    /// <summary>
    /// Resolves <paramref name="systemIdOrName"/> to a RegisteredSystem GUID.
    /// <list type="bullet">
    ///   <item>If the value is already a valid GUID, it is returned as-is.</item>
    ///   <item>Otherwise a case-insensitive lookup by Name then Acronym is performed.</item>
    /// </list>
    /// </summary>
    /// <param name="systemIdOrName">GUID, system name, or system acronym.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The canonical RegisteredSystem GUID.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no system matches or multiple systems match the provided name/acronym.
    /// </exception>
    Task<string> ResolveAsync(string systemIdOrName, CancellationToken cancellationToken = default);
}
