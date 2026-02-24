using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Scanners;

/// <summary>
/// Abstract base class for compliance scanners using template method pattern.
/// Provides timing, scoring, error handling, and family name resolution.
/// Subclasses implement <see cref="ScanFamilyAsync"/> for family-specific logic.
/// </summary>
public abstract class BaseComplianceScanner : IComplianceScanner
{
    /// <summary>Logger instance for the derived scanner.</summary>
    protected readonly ILogger Logger;

    /// <inheritdoc />
    public abstract string FamilyCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseComplianceScanner"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    protected BaseComplianceScanner(ILogger logger)
    {
        Logger = logger;
    }

    /// <inheritdoc />
    public async Task<ControlFamilyAssessment> ScanAsync(
        string subscriptionId,
        string? resourceGroup,
        IEnumerable<NistControl> controls,
        CancellationToken cancellationToken = default)
    {
        var controlList = controls.ToList();
        var familyName = ControlFamilies.FamilyNames.TryGetValue(FamilyCode, out var name)
            ? name
            : FamilyCode;

        var result = new ControlFamilyAssessment
        {
            FamilyCode = FamilyCode,
            FamilyName = familyName,
            TotalControls = controlList.Count,
            ScannerName = GetType().Name,
            Status = FamilyAssessmentStatus.Pending
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            Logger.LogInformation("Scanning family {Family} ({Name}) with {Controls} controls for Sub={Sub}",
                FamilyCode, familyName, controlList.Count, subscriptionId);

            var findings = await ScanFamilyAsync(
                subscriptionId, resourceGroup, controlList, cancellationToken);

            stopwatch.Stop();

            result.Findings = findings;
            result.FailedControls = findings
                .Select(f => f.ControlId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            result.PassedControls = result.TotalControls - result.FailedControls;
            result.ComplianceScore = result.TotalControls > 0
                ? (double)result.PassedControls / result.TotalControls * 100.0
                : 100.0;
            result.AssessmentDuration = stopwatch.Elapsed;
            result.Status = FamilyAssessmentStatus.Completed;

            Logger.LogInformation(
                "Family {Family} scan complete: {Passed}/{Total} controls passed ({Score:F1}%) in {Duration}ms",
                FamilyCode, result.PassedControls, result.TotalControls,
                result.ComplianceScore, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            result.Status = FamilyAssessmentStatus.Skipped;
            result.AssessmentDuration = stopwatch.Elapsed;
            result.ErrorMessage = "Scan was cancelled";
            Logger.LogWarning("Family {Family} scan cancelled after {Duration}ms",
                FamilyCode, stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Family {Family} scan failed after {Duration}ms",
                FamilyCode, stopwatch.ElapsedMilliseconds);
            return ControlFamilyAssessment.Failed(FamilyCode, ex.Message);
        }
    }

    /// <summary>
    /// Family-specific scanning logic. Implemented by derived classes.
    /// </summary>
    /// <param name="subscriptionId">Azure subscription ID.</param>
    /// <param name="resourceGroup">Optional resource group constraint.</param>
    /// <param name="controls">NIST controls for this family.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of compliance findings.</returns>
    protected abstract Task<List<ComplianceFinding>> ScanFamilyAsync(
        string subscriptionId,
        string? resourceGroup,
        List<NistControl> controls,
        CancellationToken cancellationToken);
}
