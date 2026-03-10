using System.Text.Json;
using Ato.Copilot.Agents.Compliance.Services;
using FluentAssertions;
using Xunit;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for OscalValidationService (Feature 022 — T041).
/// Tests 7 structural checks on OSCAL SSP JSON.
/// </summary>
public class OscalValidationServiceTests
{
    private readonly OscalValidationService _service = new();

    // ─── Valid SSP ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateSsp_ValidJson_ReturnsIsValidTrue()
    {
        var json = BuildValidSsp();

        var result = await _service.ValidateSspAsync(json);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateSsp_ValidJson_PopulatesStatistics()
    {
        var json = BuildValidSsp();

        var result = await _service.ValidateSspAsync(json);

        result.Statistics.ControlCount.Should().Be(1);
        result.Statistics.ComponentCount.Should().Be(1);
        result.Statistics.InventoryItemCount.Should().Be(1);
        result.Statistics.UserCount.Should().Be(1);
        result.Statistics.BackMatterResourceCount.Should().Be(1);
    }

    // ─── Check 1: Missing system-security-plan ────────────────────────────────

    [Fact]
    public async Task ValidateSsp_MissingTopLevelKey_Error()
    {
        var json = JsonSerializer.Serialize(new { something = "else" });

        var result = await _service.ValidateSspAsync(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("system-security-plan");
    }

    // ─── Check 2: Missing required child sections ─────────────────────────────

    [Theory]
    [InlineData("metadata")]
    [InlineData("import-profile")]
    [InlineData("system-characteristics")]
    [InlineData("system-implementation")]
    [InlineData("control-implementation")]
    public async Task ValidateSsp_MissingRequiredSection_Error(string section)
    {
        var ssp = BuildValidSspObject();
        ssp.Remove(section);
        var json = WrapSsp(ssp);

        var result = await _service.ValidateSspAsync(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains(section));
    }

    // ─── Check 3: Invalid UUID format ─────────────────────────────────────────

    [Fact]
    public async Task ValidateSsp_InvalidUuid_Error()
    {
        var ssp = BuildValidSspObject();
        ssp["uuid"] = "not-a-valid-uuid";
        var json = WrapSsp(ssp);

        var result = await _service.ValidateSspAsync(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid UUID") && e.Contains("not-a-valid-uuid"));
    }

    // ─── Check 5: Component UUID not found ────────────────────────────────────

    [Fact]
    public async Task ValidateSsp_ComponentUuidNotFound_Warning()
    {
        var ssp = BuildValidSspObject();
        var ci = (Dictionary<string, object>)ssp["control-implementation"];
        var reqs = (List<Dictionary<string, object>>)ci["implemented-requirements"];
        reqs[0]["by-components"] = new List<Dictionary<string, object>>
        {
            new() { ["component-uuid"] = Guid.NewGuid().ToString(), ["uuid"] = Guid.NewGuid().ToString() }
        };
        var json = WrapSsp(ssp);

        var result = await _service.ValidateSspAsync(json);

        result.Warnings.Should().Contain(w => w.Contains("component UUID") && w.Contains("not found"));
    }

    // ─── Check 6: Party UUID not found ────────────────────────────────────────

    [Fact]
    public async Task ValidateSsp_PartyUuidNotFound_Warning()
    {
        var ssp = BuildValidSspObject();
        var meta = (Dictionary<string, object>)ssp["metadata"];
        meta["responsible-parties"] = new List<Dictionary<string, object>>
        {
            new()
            {
                ["role-id"] = "system-owner",
                ["party-uuids"] = new List<string> { Guid.NewGuid().ToString() }
            }
        };
        var json = WrapSsp(ssp);

        var result = await _service.ValidateSspAsync(json);

        result.Warnings.Should().Contain(w => w.Contains("party UUID") && w.Contains("not found"));
    }

    // ─── Check 7: Wrong oscal-version ─────────────────────────────────────────

    [Fact]
    public async Task ValidateSsp_WrongOscalVersion_Warning()
    {
        var ssp = BuildValidSspObject();
        var meta = (Dictionary<string, object>)ssp["metadata"];
        meta["oscal-version"] = "1.0.6";
        var json = WrapSsp(ssp);

        var result = await _service.ValidateSspAsync(json);

        result.Warnings.Should().Contain(w => w.Contains("oscal-version") && w.Contains("1.0.6"));
    }

    // ─── Invalid JSON ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateSsp_InvalidJson_Error()
    {
        var result = await _service.ValidateSspAsync("{not valid json}");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("Invalid JSON");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string BuildValidSsp()
    {
        return WrapSsp(BuildValidSspObject());
    }

    private static string WrapSsp(Dictionary<string, object> ssp)
    {
        var root = new Dictionary<string, object> { ["system-security-plan"] = ssp };
        return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = false });
    }

    private static Dictionary<string, object> BuildValidSspObject()
    {
        var partyUuid = Guid.NewGuid().ToString();
        var compUuid = Guid.NewGuid().ToString();

        return new Dictionary<string, object>
        {
            ["uuid"] = Guid.NewGuid().ToString(),
            ["metadata"] = new Dictionary<string, object>
            {
                ["title"] = "Test SSP",
                ["oscal-version"] = "1.1.2",
                ["roles"] = new List<Dictionary<string, object>>
                {
                    new() { ["id"] = "system-owner", ["title"] = "System Owner" }
                },
                ["parties"] = new List<Dictionary<string, object>>
                {
                    new() { ["uuid"] = partyUuid, ["type"] = "person", ["name"] = "Test User" }
                },
                ["responsible-parties"] = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["role-id"] = "system-owner",
                        ["party-uuids"] = new List<string> { partyUuid }
                    }
                }
            },
            ["import-profile"] = new Dictionary<string, object>
            {
                ["href"] = "https://example.com/profile.json"
            },
            ["system-characteristics"] = new Dictionary<string, object>
            {
                ["system-name"] = "Test System",
                ["description"] = "A test system"
            },
            ["system-implementation"] = new Dictionary<string, object>
            {
                ["users"] = new List<Dictionary<string, object>>
                {
                    new() { ["uuid"] = partyUuid, ["role-ids"] = new[] { "system-owner" } }
                },
                ["components"] = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["uuid"] = compUuid,
                        ["type"] = "this-system",
                        ["title"] = "App Server",
                        ["status"] = new Dictionary<string, string> { ["state"] = "operational" }
                    }
                },
                ["inventory-items"] = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["uuid"] = Guid.NewGuid().ToString(),
                        ["description"] = "App Server"
                    }
                }
            },
            ["control-implementation"] = new Dictionary<string, object>
            {
                ["description"] = "Control narratives",
                ["implemented-requirements"] = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["uuid"] = Guid.NewGuid().ToString(),
                        ["control-id"] = "ac-1",
                        ["description"] = "Access control policy",
                        ["by-components"] = new List<Dictionary<string, object>>
                        {
                            new()
                            {
                                ["component-uuid"] = compUuid,
                                ["uuid"] = Guid.NewGuid().ToString()
                            }
                        }
                    }
                }
            },
            ["back-matter"] = new Dictionary<string, object>
            {
                ["resources"] = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["uuid"] = Guid.NewGuid().ToString(),
                        ["title"] = "ISA Document"
                    }
                }
            }
        };
    }
}
