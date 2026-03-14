using Ato.Copilot.Core.Interfaces;
using Ato.Copilot.Core.Services;
using FluentAssertions;
using Xunit;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for PathSanitizationService (US3 / FR-011).
/// Validates path traversal rejection, URL-encoded traversal, null bytes,
/// UNC paths, and valid path acceptance.
/// </summary>
public class PathSanitizationServiceTests
{
    private readonly IPathSanitizationService _service = new PathSanitizationService();
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "test-uploads");

    /// <summary>
    /// Verifies that relative path traversal (../) is rejected (FR-011).
    /// </summary>
    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
    [InlineData("foo/../../bar/../../../etc/shadow")]
    public void ValidatePathWithinBase_RejectsTraversalPaths(string candidatePath)
    {
        var result = _service.ValidatePathWithinBase(candidatePath, _baseDir);

        result.IsValid.Should().BeFalse();
        result.Reason.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Verifies that URL-encoded traversal sequences (%2e%2e%2f) are rejected (FR-011).
    /// </summary>
    [Theory]
    [InlineData("%2e%2e%2fetc%2fpasswd")]
    [InlineData("%2e%2e/%2e%2e/etc/passwd")]
    [InlineData("..%2f..%2f..%2fetc%2fpasswd")]
    public void ValidatePathWithinBase_RejectsUrlEncodedTraversal(string candidatePath)
    {
        var result = _service.ValidatePathWithinBase(candidatePath, _baseDir);

        result.IsValid.Should().BeFalse();
        result.Reason.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Verifies that paths containing null bytes are rejected (FR-011).
    /// </summary>
    [Fact]
    public void ValidatePathWithinBase_RejectsNullBytes()
    {
        var result = _service.ValidatePathWithinBase("file.txt\0.exe", _baseDir);

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Contain("null");
    }

    /// <summary>
    /// Verifies that UNC paths (\\server\share) are rejected (FR-011).
    /// </summary>
    [Theory]
    [InlineData("\\\\server\\share\\file.txt")]
    [InlineData("//server/share/file.txt")]
    public void ValidatePathWithinBase_RejectsUncPaths(string candidatePath)
    {
        var result = _service.ValidatePathWithinBase(candidatePath, _baseDir);

        result.IsValid.Should().BeFalse();
        result.Reason.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Verifies that a valid path within the base directory is accepted (FR-011).
    /// </summary>
    [Fact]
    public void ValidatePathWithinBase_AcceptsValidPath()
    {
        var validPath = Path.Combine(_baseDir, "documents", "report.pdf");
        var result = _service.ValidatePathWithinBase(validPath, _baseDir);

        result.IsValid.Should().BeTrue();
        result.CanonicalPath.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Verifies that a relative filename within base directory is accepted.
    /// </summary>
    [Fact]
    public void ValidatePathWithinBase_AcceptsRelativeFilename()
    {
        var result = _service.ValidatePathWithinBase("report.pdf", _baseDir);

        result.IsValid.Should().BeTrue();
        result.CanonicalPath.Should().StartWith(_baseDir);
    }

    /// <summary>
    /// Verifies cross-platform path separators are handled correctly.
    /// </summary>
    [Theory]
    [InlineData("subdir/file.txt")]
    [InlineData("subdir\\file.txt")]
    public void ValidatePathWithinBase_HandlesCrossPlatformSeparators(string candidatePath)
    {
        var result = _service.ValidatePathWithinBase(candidatePath, _baseDir);

        result.IsValid.Should().BeTrue();
        result.CanonicalPath.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Verifies that empty or null paths are rejected.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidatePathWithinBase_RejectsEmptyPaths(string candidatePath)
    {
        var result = _service.ValidatePathWithinBase(candidatePath, _baseDir);

        result.IsValid.Should().BeFalse();
    }
}
