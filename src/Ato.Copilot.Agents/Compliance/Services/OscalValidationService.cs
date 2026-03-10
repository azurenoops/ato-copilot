using System.Text.Json;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Validates OSCAL 1.1.2 SSP JSON for structural correctness (Feature 022).
/// Runs 7 structural checks and reports errors, warnings, and statistics.
/// </summary>
public class OscalValidationService : IOscalValidationService
{
    private static readonly string[] RequiredChildSections =
        ["metadata", "import-profile", "system-characteristics",
         "system-implementation", "control-implementation"];

    /// <inheritdoc />
    public Task<OscalValidationResult> ValidateSspAsync(
        string oscalJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oscalJson, nameof(oscalJson));

        var errors = new List<string>();
        var warnings = new List<string>();
        var stats = new OscalStatistics(0, 0, 0, 0, 0);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(oscalJson);
        }
        catch (JsonException ex)
        {
            errors.Add($"Invalid JSON: {ex.Message}");
            return Task.FromResult(new OscalValidationResult(false, errors, warnings, stats));
        }

        var root = doc.RootElement;

        // Check 1: top-level "system-security-plan" key present
        if (!root.TryGetProperty("system-security-plan", out var ssp))
        {
            errors.Add("Missing required top-level key 'system-security-plan'.");
            return Task.FromResult(new OscalValidationResult(false, errors, warnings, stats));
        }

        // Check 2: required child sections present
        foreach (var section in RequiredChildSections)
        {
            if (!ssp.TryGetProperty(section, out _))
                errors.Add($"Required section '{section}' is missing.");
        }

        // Check 7: oscal-version equals "1.1.2"
        if (ssp.TryGetProperty("metadata", out var metadata) &&
            metadata.TryGetProperty("oscal-version", out var oscalVersion))
        {
            if (oscalVersion.GetString() != "1.1.2")
                warnings.Add($"oscal-version is '{oscalVersion.GetString()}', expected '1.1.2'.");
        }

        // Collect component UUIDs for cross-ref check (Check 5)
        var componentUuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ssp.TryGetProperty("system-implementation", out var si) &&
            si.TryGetProperty("components", out var components) &&
            components.ValueKind == JsonValueKind.Array)
        {
            foreach (var comp in components.EnumerateArray())
            {
                if (comp.TryGetProperty("uuid", out var uuid))
                    componentUuids.Add(uuid.GetString() ?? "");
            }
        }

        // Collect party UUIDs for cross-ref check (Check 6)
        var partyUuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ssp.TryGetProperty("metadata", out var meta) &&
            meta.TryGetProperty("parties", out var parties) &&
            parties.ValueKind == JsonValueKind.Array)
        {
            foreach (var party in parties.EnumerateArray())
            {
                if (party.TryGetProperty("uuid", out var uuid))
                    partyUuids.Add(uuid.GetString() ?? "");
            }
        }

        // Check 3: UUID format validation
        ValidateUuids(ssp, errors);

        // Collect statistics and do cross-ref checks
        int controlCount = 0, componentCount = componentUuids.Count, inventoryCount = 0, userCount = 0, backMatterCount = 0;

        // Process control-implementation
        if (ssp.TryGetProperty("control-implementation", out var ci) &&
            ci.TryGetProperty("implemented-requirements", out var reqs) &&
            reqs.ValueKind == JsonValueKind.Array)
        {
            foreach (var req in reqs.EnumerateArray())
            {
                controlCount++;

                // Check 5: by-components cross-refs
                if (req.TryGetProperty("by-components", out var byComps) &&
                    byComps.ValueKind == JsonValueKind.Array)
                {
                    foreach (var bc in byComps.EnumerateArray())
                    {
                        if (bc.TryGetProperty("component-uuid", out var compUuid))
                        {
                            var cuVal = compUuid.GetString() ?? "";
                            if (!string.IsNullOrEmpty(cuVal) && !componentUuids.Contains(cuVal))
                                warnings.Add($"by-component references component UUID '{cuVal}' not found in system-implementation.components.");
                        }
                    }
                }
            }
        }

        // Check 6: party UUIDs in responsible-parties
        if (ssp.TryGetProperty("metadata", out var meta2) &&
            meta2.TryGetProperty("responsible-parties", out var rps) &&
            rps.ValueKind == JsonValueKind.Array)
        {
            foreach (var rp in rps.EnumerateArray())
            {
                if (rp.TryGetProperty("party-uuids", out var puuids) &&
                    puuids.ValueKind == JsonValueKind.Array)
                {
                    foreach (var pu in puuids.EnumerateArray())
                    {
                        var puVal = pu.GetString() ?? "";
                        if (!string.IsNullOrEmpty(puVal) && !partyUuids.Contains(puVal))
                            warnings.Add($"responsible-party references party UUID '{puVal}' not found in metadata.parties.");
                    }
                }
            }
        }

        // Count users
        if (ssp.TryGetProperty("system-implementation", out var si2) &&
            si2.TryGetProperty("users", out var users) &&
            users.ValueKind == JsonValueKind.Array)
        {
            userCount = users.GetArrayLength();
        }

        // Count inventory items
        if (ssp.TryGetProperty("system-implementation", out var si3) &&
            si3.TryGetProperty("inventory-items", out var inv) &&
            inv.ValueKind == JsonValueKind.Array)
        {
            inventoryCount = inv.GetArrayLength();
        }

        // Count back-matter resources
        if (ssp.TryGetProperty("back-matter", out var bm) &&
            bm.TryGetProperty("resources", out var res) &&
            res.ValueKind == JsonValueKind.Array)
        {
            backMatterCount = res.GetArrayLength();
        }

        stats = new OscalStatistics(controlCount, componentCount, inventoryCount, userCount, backMatterCount);

        var isValid = errors.Count == 0;
        return Task.FromResult(new OscalValidationResult(isValid, errors, warnings, stats));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static void ValidateUuids(JsonElement element, List<string> errors)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Name == "uuid" && prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var val = prop.Value.GetString() ?? "";
                        if (!Guid.TryParse(val, out _))
                            errors.Add($"Invalid UUID format: '{val}'.");
                    }
                    else
                    {
                        ValidateUuids(prop.Value, errors);
                    }
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    ValidateUuids(item, errors);
                break;
        }
    }
}
