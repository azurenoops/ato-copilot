namespace Ato.Copilot.Core.Interfaces;

/// <summary>
/// Service for validating file paths against base directories to prevent path traversal attacks.
/// All file operations involving user-supplied paths MUST use this service.
/// </summary>
public interface IPathSanitizationService
{
    /// <summary>
    /// Validates that a candidate path resolves within the specified base directory
    /// after canonicalization. Detects and rejects path traversal sequences, null bytes,
    /// URL-encoded traversal, and UNC paths.
    /// </summary>
    /// <param name="candidatePath">The user-supplied file path to validate.</param>
    /// <param name="baseDirectory">The base directory that the path must resolve within.</param>
    /// <returns>A result indicating whether the path is valid and the canonical resolved path.</returns>
    PathValidationResult ValidatePathWithinBase(string candidatePath, string baseDirectory);
}

/// <summary>
/// Result of a path validation operation.
/// </summary>
public class PathValidationResult
{
    /// <summary>Whether the path is valid and within the base directory.</summary>
    public bool IsValid { get; init; }

    /// <summary>Description of why validation failed, or null on success.</summary>
    public string? Reason { get; init; }

    /// <summary>The canonicalized absolute path, or null if validation failed.</summary>
    public string? CanonicalPath { get; init; }

    /// <summary>Creates a successful validation result.</summary>
    public static PathValidationResult Valid(string canonicalPath) =>
        new() { IsValid = true, CanonicalPath = canonicalPath };

    /// <summary>Creates a failed validation result with a reason.</summary>
    public static PathValidationResult Invalid(string reason) =>
        new() { IsValid = false, Reason = reason };
}
