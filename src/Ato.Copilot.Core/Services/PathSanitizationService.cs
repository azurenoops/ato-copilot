using Ato.Copilot.Core.Interfaces;

namespace Ato.Copilot.Core.Services;

/// <summary>
/// Validates file paths against base directories to prevent path traversal attacks.
/// Handles relative traversal, URL-encoded sequences, null bytes, and UNC paths.
/// </summary>
public class PathSanitizationService : IPathSanitizationService
{
    /// <inheritdoc />
    public PathValidationResult ValidatePathWithinBase(string candidatePath, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
            return PathValidationResult.Invalid("Path cannot be empty or whitespace.");

        if (string.IsNullOrWhiteSpace(baseDirectory))
            return PathValidationResult.Invalid("Base directory cannot be empty.");

        // Reject null bytes
        if (candidatePath.Contains('\0'))
            return PathValidationResult.Invalid("Path contains null byte.");

        // URL-decode and re-check for traversal after decoding
        var decoded = Uri.UnescapeDataString(candidatePath);
        if (decoded.Contains('\0'))
            return PathValidationResult.Invalid("Decoded path contains null byte.");

        // Reject UNC paths
        if (decoded.StartsWith("\\\\") || decoded.StartsWith("//"))
            return PathValidationResult.Invalid("UNC paths are not allowed.");

        // Normalize separators for cross-platform
        decoded = decoded.Replace('\\', Path.DirectorySeparatorChar)
                         .Replace('/', Path.DirectorySeparatorChar);

        // Resolve to absolute path
        string fullPath;
        try
        {
            fullPath = Path.IsPathRooted(decoded)
                ? Path.GetFullPath(decoded)
                : Path.GetFullPath(Path.Combine(baseDirectory, decoded));
        }
        catch (Exception ex)
        {
            return PathValidationResult.Invalid($"Invalid path: {ex.Message}");
        }

        // Ensure base directory ends with separator for correct StartsWith check
        var normalizedBase = Path.GetFullPath(baseDirectory);
        if (!normalizedBase.EndsWith(Path.DirectorySeparatorChar))
            normalizedBase += Path.DirectorySeparatorChar;

        // Check containment
        if (!fullPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
            return PathValidationResult.Invalid("Path resolves outside the allowed base directory.");

        return PathValidationResult.Valid(fullPath);
    }
}
