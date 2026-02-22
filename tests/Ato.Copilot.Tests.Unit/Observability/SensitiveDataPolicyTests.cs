using FluentAssertions;
using Xunit;
using Ato.Copilot.Core.Observability;

namespace Ato.Copilot.Tests.Unit.Observability;

/// <summary>
/// Unit tests for SensitiveDataDestructuringPolicy.
/// Validates that sensitive property values are scrubbed to "[REDACTED]"
/// while safe property values pass through unchanged (per FR-037).
/// </summary>
public class SensitiveDataPolicyTests
{
    // ─── Sensitive Properties (should be redacted) ───────────────────────────

    [Theory]
    [InlineData("ClientSecret")]
    [InlineData("ConnectionString")]
    [InlineData("AccessToken")]
    [InlineData("RefreshToken")]
    [InlineData("Password")]
    [InlineData("ApiKey")]
    [InlineData("Authorization")]
    [InlineData("BearerToken")]
    [InlineData("Token")]
    [InlineData("Secret")]
    [InlineData("Credential")]
    public void IsSensitiveProperty_ExactMatch_ShouldReturnTrue(string propertyName)
    {
        SensitiveDataDestructuringPolicy.IsSensitiveProperty(propertyName).Should().BeTrue();
    }

    [Theory]
    [InlineData("CLIENTSECRET")]
    [InlineData("clientsecret")]
    [InlineData("ClientSecret")]
    [InlineData("accesstoken")]
    [InlineData("ACCESSTOKEN")]
    public void IsSensitiveProperty_CaseInsensitive_ShouldReturnTrue(string propertyName)
    {
        SensitiveDataDestructuringPolicy.IsSensitiveProperty(propertyName).Should().BeTrue();
    }

    [Theory]
    [InlineData("AzureAd__ClientSecret")]
    [InlineData("MyPassword")]
    [InlineData("ApiKeyValue")]
    [InlineData("AuthorizationHeader")]
    [InlineData("TokenValue")]
    public void IsSensitiveProperty_PartialMatch_ShouldReturnTrue(string propertyName)
    {
        SensitiveDataDestructuringPolicy.IsSensitiveProperty(propertyName).Should().BeTrue();
    }

    // ─── Safe Properties (should pass through) ──────────────────────────────

    [Theory]
    [InlineData("RoleName")]
    [InlineData("Scope")]
    [InlineData("Justification")]
    [InlineData("UserId")]
    [InlineData("RequestId")]
    [InlineData("ToolName")]
    [InlineData("AgentName")]
    [InlineData("CorrelationId")]
    [InlineData("Status")]
    [InlineData("Duration")]
    public void IsSensitiveProperty_SafeProperties_ShouldReturnFalse(string propertyName)
    {
        SensitiveDataDestructuringPolicy.IsSensitiveProperty(propertyName).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void IsSensitiveProperty_NullOrEmpty_ShouldReturnFalse(string? propertyName)
    {
        SensitiveDataDestructuringPolicy.IsSensitiveProperty(propertyName!).Should().BeFalse();
    }

    [Fact]
    public void TryDestructure_NonDictionary_ShouldReturnFalse()
    {
        var policy = new SensitiveDataDestructuringPolicy();
        var factory = new TestPropertyValueFactory();

        var result = policy.TryDestructure("plain string", factory, out var logValue);

        result.Should().BeFalse();
        logValue.Should().BeNull();
    }

    [Fact]
    public void TryDestructure_DictionaryWithSensitiveKey_ShouldRedactValue()
    {
        var policy = new SensitiveDataDestructuringPolicy();
        var factory = new TestPropertyValueFactory();

        var dict = new Dictionary<string, object?>
        {
            ["ClientSecret"] = "super-secret-value",
            ["RoleName"] = "Contributor"
        };

        var result = policy.TryDestructure(dict, factory, out var logValue);

        result.Should().BeTrue();
        logValue.Should().NotBeNull();
        var rendered = logValue!.ToString();
        rendered.Should().Contain("[REDACTED]");
        rendered.Should().NotContain("super-secret-value");
    }

    /// <summary>
    /// Minimal ILogEventPropertyValueFactory for testing.
    /// </summary>
    private class TestPropertyValueFactory : Serilog.Core.ILogEventPropertyValueFactory
    {
        public Serilog.Events.LogEventPropertyValue CreatePropertyValue(object? value, bool destructureObjects = false)
        {
            return new Serilog.Events.ScalarValue(value);
        }
    }
}
