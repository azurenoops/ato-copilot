// ═══════════════════════════════════════════════════════════════════════════
// Feature 017 — SCAP/STIG Viewer Import: Core Import Service
// Implements IScanImportService — CKL/XCCDF import, CKL export, import mgmt.
// ═══════════════════════════════════════════════════════════════════════════

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Agents.Compliance.Services.ScanImport;

/// <summary>
/// Core import orchestration service for SCAP/STIG scan results.
/// Parses CKL/XCCDF XML, resolves STIG→CCI→NIST, creates findings and
/// effectiveness records, handles conflict resolution and dry-run mode.
/// </summary>
public class ScanImportService : IScanImportService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IStigKnowledgeService _stigService;
    private readonly IBaselineService _baselineService;
    private readonly IRmfLifecycleService _rmfService;
    private readonly IAssessmentArtifactService _artifactService;
    private readonly ICklParser _cklParser;
    private readonly IXccdfParser _xccdfParser;
    private readonly ICklGenerator _cklGenerator;
    private readonly ILogger<ScanImportService> _logger;

    public ScanImportService(
        IServiceScopeFactory scopeFactory,
        IStigKnowledgeService stigService,
        IBaselineService baselineService,
        IRmfLifecycleService rmfService,
        IAssessmentArtifactService artifactService,
        ICklParser cklParser,
        IXccdfParser xccdfParser,
        ICklGenerator cklGenerator,
        ILogger<ScanImportService> logger)
    {
        _scopeFactory = scopeFactory;
        _stigService = stigService;
        _baselineService = baselineService;
        _rmfService = rmfService;
        _artifactService = artifactService;
        _cklParser = cklParser;
        _xccdfParser = xccdfParser;
        _cklGenerator = cklGenerator;
        _logger = logger;
    }

    // ─── ImportCklAsync (T014, T015, T019, T020, T021, T025–T027) ────────

    /// <inheritdoc />
    public async Task<ImportResult> ImportCklAsync(
        string systemId,
        string? assessmentId,
        byte[] fileContent,
        string fileName,
        ImportConflictResolution resolution,
        bool dryRun,
        string importedBy,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var warnings = new List<string>();
        var unmatchedRules = new List<UnmatchedRuleInfo>();
        var fileHash = ComputeSha256(fileContent);

        _logger.LogInformation(
            "CKL import started: file={FileName}, hash={FileHash}, system={SystemId}, type=CKL, resolution={Resolution}, dryRun={DryRun}",
            fileName, fileHash, systemId, resolution, dryRun);

        // ── Step 1: Parse CKL ────────────────────────────────────────────
        ParsedCklFile parsedCkl;
        try
        {
            parsedCkl = _cklParser.Parse(fileContent, fileName);
        }
        catch (CklParseException ex)
        {
            _logger.LogWarning(ex, "CKL parse failed for file {FileName}", fileName);
            return CreateFailedResult(
                $"CKL parse error: {ex.Message}",
                fileName);
        }

        // ── Step 2: Validate system ──────────────────────────────────────
        var system = await _rmfService.GetSystemAsync(systemId, ct);
        if (system is null)
        {
            return CreateFailedResult(
                $"System '{systemId}' not found.",
                fileName);
        }

        // ── Step 3: Check RMF step (warn if < Assess) ───────────────────
        if (system.CurrentRmfStep < RmfPhase.Assess)
        {
            warnings.Add(
                $"System is in RMF step '{system.CurrentRmfStep}' (expected Assess or later). " +
                "Import will proceed, but findings may not be visible in assessment workflows.");
        }

        // ── Step 4: Get control baseline ─────────────────────────────────
        var baseline = await _baselineService.GetBaselineAsync(systemId, cancellationToken: ct);
        var baselineControlIds = baseline?.ControlIds ?? new List<string>();
        var baselineSet = new HashSet<string>(baselineControlIds, StringComparer.OrdinalIgnoreCase);

        if (baseline is null)
        {
            warnings.Add("No control baseline found for system. " +
                          "All NIST controls will be treated as out-of-baseline (no ControlEffectiveness records).");
        }

        // ── Step 5: Resolve/create assessment context ────────────────────
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        string resolvedAssessmentId;
        if (!string.IsNullOrEmpty(assessmentId))
        {
            var existing = await ctx.Assessments.FindAsync(new object[] { assessmentId }, ct);
            if (existing is null)
                return CreateFailedResult($"Assessment '{assessmentId}' not found.", fileName);
            resolvedAssessmentId = assessmentId;
        }
        else
        {
            resolvedAssessmentId = await GetOrCreateAssessmentAsync(ctx, systemId, importedBy, ct);
        }

        // ── Step 6: Duplicate detection ───────────────────────────────
        var duplicateImport = await ctx.ScanImportRecords
            .Where(r => r.FileHash == fileHash && r.RegisteredSystemId == systemId && !r.IsDryRun)
            .OrderByDescending(r => r.ImportedAt)
            .FirstOrDefaultAsync(ct);

        if (duplicateImport is not null)
        {
            warnings.Add(
                $"File previously imported on {duplicateImport.ImportedAt:yyyy-MM-dd HH:mm} UTC " +
                $"(import ID: {duplicateImport.Id}).");
        }

        // ── Step 7: Create ScanImportRecord ──────────────────────────────
        var importRecord = new ScanImportRecord
        {
            RegisteredSystemId = systemId,
            AssessmentId = resolvedAssessmentId,
            ImportType = ScanImportType.Ckl,
            FileName = fileName,
            FileHash = fileHash,
            FileSizeBytes = fileContent.Length,
            BenchmarkId = parsedCkl.StigInfo.StigId,
            BenchmarkVersion = parsedCkl.StigInfo.Version,
            BenchmarkTitle = parsedCkl.StigInfo.Title,
            TargetHostName = parsedCkl.Asset.HostName,
            TargetIpAddress = parsedCkl.Asset.HostIp,
            ScanTimestamp = null, // CKL has no scan timestamp
            ConflictResolution = resolution,
            IsDryRun = dryRun,
            ImportedBy = importedBy
        };

        // ── Step 8: Process each CKL entry ───────────────────────────────
        int openCount = 0, passCount = 0, naCount = 0, notReviewedCount = 0;
        int findingsCreated = 0, findingsUpdated = 0, skippedCount = 0, unmatchedCount = 0;
        var affectedNistControls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var importFindings = new List<ScanImportFinding>();
        var newFindings = new List<ComplianceFinding>();
        var updatedFindings = new List<ComplianceFinding>();

        // Preload existing findings for conflict detection
        var existingFindings = await ctx.Findings
            .Where(f => f.AssessmentId == resolvedAssessmentId &&
                        f.StigFinding &&
                        f.StigId != null)
            .ToListAsync(ct);

        var existingByStigId = existingFindings
            .Where(f => f.StigId is not null)
            .GroupBy(f => f.StigId!)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var entry in parsedCkl.Entries)
        {
            var importFinding = new ScanImportFinding
            {
                ScanImportRecordId = importRecord.Id,
                VulnId = entry.VulnId,
                RuleId = entry.RuleId,
                StigVersion = entry.StigVersion,
                RawStatus = entry.Status,
                RawSeverity = entry.Severity,
                FindingDetails = entry.FindingDetails,
                Comments = entry.Comments,
                SeverityOverride = entry.SeverityOverride,
                SeverityJustification = entry.SeverityJustification,
                ResolvedCciRefs = entry.CciRefs
            };

            // Count by status
            switch (entry.Status)
            {
                case "Open":
                    openCount++;
                    break;
                case "NotAFinding":
                    passCount++;
                    break;
                case "Not_Applicable":
                    naCount++;
                    break;
                case "Not_Reviewed":
                    notReviewedCount++;
                    break;
            }

            // ── Step 8a: Resolve STIG control (T014) ─────────────────────
            var stigControl = await ResolveStigControlAsync(entry, ct);

            if (stigControl is null)
            {
                importFinding.ImportAction = ImportFindingAction.Unmatched;
                unmatchedCount++;
                unmatchedRules.Add(new UnmatchedRuleInfo(
                    entry.VulnId,
                    entry.RuleId,
                    entry.RuleTitle,
                    entry.Severity));
                importFindings.Add(importFinding);
                continue;
            }

            importFinding.ResolvedStigControlId = stigControl.StigId;

            // ── Step 8b: Resolve NIST controls (T015) ────────────────────
            var nistControls = stigControl.NistControls ?? new List<string>();
            importFinding.ResolvedNistControlIds = nistControls;

            // Track out-of-baseline controls
            var outOfBaseline = nistControls
                .Where(c => !baselineSet.Contains(c))
                .ToList();
            if (outOfBaseline.Count > 0 && baseline is not null)
            {
                warnings.Add(
                    $"VULN {entry.VulnId}: NIST controls [{string.Join(", ", outOfBaseline)}] " +
                    "resolved but not in system baseline. Finding created, no effectiveness.");
            }

            // ── Step 8c: Map severity ────────────────────────────────────
            var (findingSeverity, catSeverity) = MapSeverity(
                entry.Severity, entry.SeverityOverride);
            importFinding.MappedSeverity = catSeverity;

            // ── Step 8d: Handle Not_Applicable — no finding ──────────────
            if (entry.Status == "Not_Applicable")
            {
                importFinding.ImportAction = ImportFindingAction.NotApplicable;
                importFindings.Add(importFinding);
                continue;
            }

            // ── Step 8e: Map finding status ──────────────────────────────
            var (findingStatus, findingDetails) = MapCklStatus(entry);

            // ── Step 8f: Conflict detection (T025) ───────────────────────
            var primaryControl = nistControls.FirstOrDefault() ?? string.Empty;
            var controlFamily = ExtractControlFamily(primaryControl);
            existingByStigId.TryGetValue(entry.VulnId, out var existingFinding);

            if (existingFinding is not null)
            {
                // ── Step 8g: Apply conflict resolution (T026) ────────────
                var conflictAction = ApplyConflictResolution(
                    existingFinding, entry, findingStatus, findingSeverity,
                    catSeverity, findingDetails, resolution, importRecord.Id);

                if (conflictAction == ImportFindingAction.Skipped)
                {
                    importFinding.ImportAction = ImportFindingAction.Skipped;
                    importFinding.ComplianceFindingId = existingFinding.Id;
                    skippedCount++;
                    importFindings.Add(importFinding);
                    continue;
                }

                // Overwrite or Merge updated the existing finding in-place
                importFinding.ImportAction = ImportFindingAction.Updated;
                importFinding.ComplianceFindingId = existingFinding.Id;
                findingsUpdated++;
                if (!updatedFindings.Contains(existingFinding))
                    updatedFindings.Add(existingFinding);
            }
            else
            {
                // ── Step 8h: Create new ComplianceFinding (T019) ─────────
                var newFinding = new ComplianceFinding
                {
                    ControlId = primaryControl,
                    ControlFamily = controlFamily,
                    Title = entry.RuleTitle ?? $"STIG Finding {entry.VulnId}",
                    Description = findingDetails,
                    Severity = findingSeverity,
                    Status = findingStatus,
                    ResourceId = system.Name,
                    ResourceType = "RegisteredSystem",
                    RemediationGuidance = entry.Comments ?? string.Empty,
                    Source = "CKL Import",
                    ScanSource = ScanSourceType.Combined,
                    StigFinding = true,
                    StigId = entry.VulnId,
                    CatSeverity = catSeverity,
                    AssessmentId = resolvedAssessmentId,
                    ImportRecordId = importRecord.Id
                };

                importFinding.ImportAction = entry.Status == "Not_Reviewed"
                    ? ImportFindingAction.NotReviewed
                    : ImportFindingAction.Created;
                importFinding.ComplianceFindingId = newFinding.Id;

                newFindings.Add(newFinding);
                findingsCreated++;
            }

            // Track affected NIST controls (in-baseline only) for effectiveness
            foreach (var nist in nistControls.Where(c => baselineSet.Contains(c)))
            {
                affectedNistControls.Add(nist);
            }

            importFindings.Add(importFinding);
        }

        // Log unmatched rules
        if (unmatchedRules.Count > 0)
        {
            _logger.LogWarning(
                "CKL import {FileName}: {Count} unmatched rules: {VulnIds}",
                fileName, unmatchedRules.Count,
                string.Join(", ", unmatchedRules.Select(u => u.VulnId)));
            warnings.Add(
                $"{unmatchedRules.Count} STIG rule(s) not found in curated library: " +
                string.Join(", ", unmatchedRules.Select(u => u.VulnId)));
        }

        // ── Step 9: Create evidence (T021) ───────────────────────────────
        var evidenceContent = JsonSerializer.Serialize(new
        {
            ImportType = "CKL",
            FileName = fileName,
            BenchmarkId = parsedCkl.StigInfo.StigId,
            TotalEntries = parsedCkl.Entries.Count,
            OpenCount = openCount,
            PassCount = passCount,
            NotApplicableCount = naCount,
            NotReviewedCount = notReviewedCount,
            UnmatchedCount = unmatchedCount
        });

        var evidence = new ComplianceEvidence
        {
            ControlId = affectedNistControls.FirstOrDefault() ?? string.Empty,
            SubscriptionId = string.Empty,
            EvidenceType = "StigChecklist",
            Description = $"CKL Import: {fileName} ({parsedCkl.StigInfo.Title ?? parsedCkl.StigInfo.StigId})",
            Content = evidenceContent,
            CollectedAt = DateTime.UtcNow,
            CollectedBy = importedBy,
            AssessmentId = resolvedAssessmentId,
            EvidenceCategory = EvidenceCategory.Configuration,
            ContentHash = fileHash,
            CollectionMethod = "Manual"
        };

        // ── Step 10: Upsert effectiveness (T020) ─────────────────────────
        int effectivenessCreated = 0, effectivenessUpdated = 0;
        if (!dryRun && affectedNistControls.Count > 0)
        {
            // Build aggregate status map: for each NIST control, check all current findings
            // across ALL imports (re-evaluate aggregate state)
            var allStigFindings = await ctx.Findings
                .Where(f => f.AssessmentId == resolvedAssessmentId && f.StigFinding)
                .ToListAsync(ct);

            // Include newly created findings (not yet in DB)
            var allFindings = allStigFindings.Concat(newFindings).ToList();

            // Build control → findings lookup (primary control only)
            var controlFindingMap = allFindings
                .Where(f => !string.IsNullOrEmpty(f.ControlId))
                .GroupBy(f => f.ControlId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var controlId in affectedNistControls)
            {
                controlFindingMap.TryGetValue(controlId, out var controlFindings);
                var findingsForControl = controlFindings ?? new List<ComplianceFinding>();

                // Determine aggregate effectiveness
                var anyOpen = findingsForControl.Any(f =>
                    f.Status == FindingStatus.Open || f.Status == FindingStatus.InProgress);
                var determination = anyOpen
                    ? EffectivenessDetermination.OtherThanSatisfied
                    : EffectivenessDetermination.Satisfied;

                // Find highest severity among open findings for CAT
                CatSeverity? controlCatSeverity = null;
                if (anyOpen)
                {
                    var openFindings = findingsForControl
                        .Where(f => f.Status == FindingStatus.Open || f.Status == FindingStatus.InProgress)
                        .Where(f => f.CatSeverity.HasValue)
                        .Select(f => f.CatSeverity!.Value)
                        .ToList();
                    if (openFindings.Count > 0)
                        controlCatSeverity = openFindings.Min(); // CatI < CatII < CatIII (lowest ordinal = highest severity)
                }

                // Check existing effectiveness record
                var existingEffectiveness = await ctx.ControlEffectivenessRecords
                    .Where(e => e.AssessmentId == resolvedAssessmentId &&
                                e.RegisteredSystemId == systemId &&
                                e.ControlId == controlId)
                    .FirstOrDefaultAsync(ct);

                if (existingEffectiveness is not null)
                {
                    existingEffectiveness.Determination = determination;
                    existingEffectiveness.AssessmentMethod = "Test";
                    existingEffectiveness.AssessorId = importedBy;
                    existingEffectiveness.AssessedAt = DateTime.UtcNow;
                    existingEffectiveness.CatSeverity = controlCatSeverity;
                    if (!existingEffectiveness.EvidenceIds.Contains(evidence.Id))
                        existingEffectiveness.EvidenceIds.Add(evidence.Id);
                    existingEffectiveness.Notes = $"Re-evaluated via CKL import '{fileName}'";
                    effectivenessUpdated++;
                }
                else
                {
                    var effectiveness = new ControlEffectiveness
                    {
                        AssessmentId = resolvedAssessmentId,
                        RegisteredSystemId = systemId,
                        ControlId = controlId,
                        Determination = determination,
                        AssessmentMethod = "Test",
                        EvidenceIds = new List<string> { evidence.Id },
                        AssessorId = importedBy,
                        CatSeverity = controlCatSeverity,
                        Notes = $"Auto-determined via CKL import '{fileName}'"
                    };
                    ctx.ControlEffectivenessRecords.Add(effectiveness);
                    effectivenessCreated++;
                }
            }
        }

        // Out-of-baseline summary
        var outOfBaselineControls = importFindings
            .SelectMany(f => f.ResolvedNistControlIds)
            .Where(c => !baselineSet.Contains(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (outOfBaselineControls.Count > 0)
        {
            _logger.LogInformation(
                "CKL import {FileName}: {Count} NIST controls outside baseline: {Controls}",
                fileName, outOfBaselineControls.Count, string.Join(", ", outOfBaselineControls));
        }

        // ── Step 11: Update import record counts ─────────────────────────
        importRecord.TotalEntries = parsedCkl.Entries.Count;
        importRecord.OpenCount = openCount;
        importRecord.PassCount = passCount;
        importRecord.NotApplicableCount = naCount;
        importRecord.NotReviewedCount = notReviewedCount;
        importRecord.SkippedCount = skippedCount;
        importRecord.UnmatchedCount = unmatchedCount;
        importRecord.FindingsCreated = findingsCreated;
        importRecord.FindingsUpdated = findingsUpdated;
        importRecord.EffectivenessRecordsCreated = effectivenessCreated;
        importRecord.EffectivenessRecordsUpdated = effectivenessUpdated;
        importRecord.NistControlsAffected = affectedNistControls.Count;
        importRecord.Warnings = warnings;
        importRecord.ImportStatus = warnings.Count > 0 || unmatchedCount > 0
            ? ScanImportStatus.CompletedWithWarnings
            : ScanImportStatus.Completed;

        // ── Step 12: Persist (unless dry-run) (T027) ─────────────────────
        if (!dryRun)
        {
            ctx.ScanImportRecords.Add(importRecord);
            ctx.ScanImportFindings.AddRange(importFindings);
            ctx.Findings.AddRange(newFindings);
            ctx.Evidence.Add(evidence);
            // Updated findings are already tracked by EF change tracker
            await ctx.SaveChangesAsync(ct);
        }

        sw.Stop();
        _logger.LogInformation(
            "CKL import completed: file={FileName}, duration={DurationMs}ms, findings={FindingsCreated}/{FindingsUpdated}, " +
            "effectiveness={EffCreated}/{EffUpdated}, warnings={WarningCount}",
            fileName, sw.ElapsedMilliseconds, findingsCreated, findingsUpdated,
            effectivenessCreated, effectivenessUpdated, warnings.Count);

        return new ImportResult(
            ImportRecordId: importRecord.Id,
            Status: importRecord.ImportStatus,
            BenchmarkId: parsedCkl.StigInfo.StigId ?? string.Empty,
            BenchmarkTitle: parsedCkl.StigInfo.Title,
            TotalEntries: importRecord.TotalEntries,
            OpenCount: openCount,
            PassCount: passCount,
            NotApplicableCount: naCount,
            NotReviewedCount: notReviewedCount,
            ErrorCount: 0,
            SkippedCount: skippedCount,
            UnmatchedCount: unmatchedCount,
            FindingsCreated: findingsCreated,
            FindingsUpdated: findingsUpdated,
            EffectivenessRecordsCreated: effectivenessCreated,
            EffectivenessRecordsUpdated: effectivenessUpdated,
            NistControlsAffected: affectedNistControls.Count,
            Warnings: warnings,
            UnmatchedRules: unmatchedRules,
            ErrorMessage: null);
    }

    // ─── ImportXccdfAsync (Phase 7 — T038) ───────────────────────────────

    /// <inheritdoc />
    public async Task<ImportResult> ImportXccdfAsync(
        string systemId,
        string? assessmentId,
        byte[] fileContent,
        string fileName,
        ImportConflictResolution resolution,
        bool dryRun,
        string importedBy,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var warnings = new List<string>();
        var unmatchedRules = new List<UnmatchedRuleInfo>();
        var fileHash = ComputeSha256(fileContent);

        _logger.LogInformation(
            "XCCDF import started: file={FileName}, hash={FileHash}, system={SystemId}, type=XCCDF, resolution={Resolution}, dryRun={DryRun}",
            fileName, fileHash, systemId, resolution, dryRun);

        // ── Step 1: Parse XCCDF ──────────────────────────────────────────
        ParsedXccdfFile parsedXccdf;
        try
        {
            parsedXccdf = _xccdfParser.Parse(fileContent, fileName);
        }
        catch (XccdfParseException ex)
        {
            _logger.LogWarning(ex, "XCCDF parse failed for file {FileName}", fileName);
            return CreateFailedResult($"XCCDF parse error: {ex.Message}", fileName);
        }

        // ── Step 2: Validate system ──────────────────────────────────────
        var system = await _rmfService.GetSystemAsync(systemId, ct);
        if (system is null)
            return CreateFailedResult($"System '{systemId}' not found.", fileName);

        // ── Step 3: Check RMF step ───────────────────────────────────────
        if (system.CurrentRmfStep < RmfPhase.Assess)
        {
            warnings.Add(
                $"System is in RMF step '{system.CurrentRmfStep}' (expected Assess or later). " +
                "Import will proceed, but findings may not be visible in assessment workflows.");
        }

        // ── Step 4: Get control baseline ─────────────────────────────────
        var baseline = await _baselineService.GetBaselineAsync(systemId, cancellationToken: ct);
        var baselineControlIds = baseline?.ControlIds ?? new List<string>();
        var baselineSet = new HashSet<string>(baselineControlIds, StringComparer.OrdinalIgnoreCase);

        if (baseline is null)
        {
            warnings.Add("No control baseline found for system. " +
                          "All NIST controls will be treated as out-of-baseline (no ControlEffectiveness records).");
        }

        // ── Step 5: Resolve/create assessment context ────────────────────
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        string resolvedAssessmentId;
        if (!string.IsNullOrEmpty(assessmentId))
        {
            var existing = await ctx.Assessments.FindAsync(new object[] { assessmentId }, ct);
            if (existing is null)
                return CreateFailedResult($"Assessment '{assessmentId}' not found.", fileName);
            resolvedAssessmentId = assessmentId;
        }
        else
        {
            resolvedAssessmentId = await GetOrCreateAssessmentAsync(ctx, systemId, importedBy, ct);
        }

        // ── Step 6: Extract benchmark ID ──────────────────────────────
        // Extract benchmark ID from href (e.g., "xccdf_mil.disa.stig_benchmark_Windows_Server_2022_STIG")
        var benchmarkId = ExtractBenchmarkId(parsedXccdf.BenchmarkHref);
        var benchmarkTitle = parsedXccdf.Title;

        // ── Step 7: Create ScanImportRecord ──────────────────────────────
        var importRecord = new ScanImportRecord
        {
            RegisteredSystemId = systemId,
            AssessmentId = resolvedAssessmentId,
            ImportType = ScanImportType.Xccdf,
            FileName = fileName,
            FileHash = fileHash,
            FileSizeBytes = fileContent.Length,
            BenchmarkId = benchmarkId,
            BenchmarkTitle = benchmarkTitle,
            TargetHostName = parsedXccdf.Target,
            TargetIpAddress = parsedXccdf.TargetAddress,
            ScanTimestamp = parsedXccdf.StartTime,
            XccdfScore = parsedXccdf.Score,
            ConflictResolution = resolution,
            IsDryRun = dryRun,
            ImportedBy = importedBy
        };

        // ── Step 8: Process each XCCDF rule-result ───────────────────────
        int openCount = 0, passCount = 0, naCount = 0, errorCount = 0;
        int findingsCreated = 0, findingsUpdated = 0, skippedCount = 0, unmatchedCount = 0;
        var affectedNistControls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var importFindings = new List<ScanImportFinding>();
        var newFindings = new List<ComplianceFinding>();
        var updatedFindings = new List<ComplianceFinding>();

        // Preload existing findings for conflict detection
        var existingFindings = await ctx.Findings
            .Where(f => f.AssessmentId == resolvedAssessmentId && f.StigFinding && f.StigId != null)
            .ToListAsync(ct);

        var existingByStigId = existingFindings
            .Where(f => f.StigId is not null)
            .GroupBy(f => f.StigId!)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var ruleResult in parsedXccdf.Results)
        {
            var importFinding = new ScanImportFinding
            {
                ScanImportRecordId = importRecord.Id,
                VulnId = ruleResult.ExtractedRuleId,
                RuleId = ruleResult.ExtractedRuleId,
                RawStatus = ruleResult.Result,
                RawSeverity = ruleResult.Severity
            };

            // Map XCCDF result → status/counts
            var (xccdfStatus, xccdfDetails) = MapXccdfResult(ruleResult);

            switch (ruleResult.Result)
            {
                case "fail":
                    openCount++;
                    break;
                case "pass":
                    passCount++;
                    break;
                case "notapplicable":
                    naCount++;
                    break;
                case "error":
                case "unknown":
                case "notchecked":
                    errorCount++;
                    break;
            }

            // ── Step 8a: Resolve STIG control by rule ID ─────────────────
            var stigControl = await ResolveStigControlByRuleIdAsync(ruleResult.ExtractedRuleId, ct);

            if (stigControl is null)
            {
                importFinding.ImportAction = ImportFindingAction.Unmatched;
                unmatchedCount++;
                unmatchedRules.Add(new UnmatchedRuleInfo(
                    ruleResult.RuleIdRef,
                    ruleResult.ExtractedRuleId,
                    null,
                    ruleResult.Severity));
                importFindings.Add(importFinding);
                continue;
            }

            importFinding.ResolvedStigControlId = stigControl.StigId;

            // ── Step 8b: Resolve NIST controls ───────────────────────────
            var nistControls = stigControl.NistControls ?? new List<string>();
            importFinding.ResolvedNistControlIds = nistControls;

            // ── Step 8c: Map severity ────────────────────────────────────
            var (findingSeverity, catSeverity) = MapSeverity(ruleResult.Severity, null);
            importFinding.MappedSeverity = catSeverity;

            // ── Step 8d: Handle notapplicable ────────────────────────────
            if (ruleResult.Result == "notapplicable")
            {
                importFinding.ImportAction = ImportFindingAction.NotApplicable;
                importFindings.Add(importFinding);
                continue;
            }

            // ── Step 8e: Handle error/unknown/notchecked → flag for review ──
            if (ruleResult.Result is "error" or "unknown" or "notchecked")
            {
                importFinding.ImportAction = ImportFindingAction.Error;
                importFinding.FindingDetails = xccdfDetails;
                importFindings.Add(importFinding);
                warnings.Add($"Rule {ruleResult.ExtractedRuleId}: XCCDF result '{ruleResult.Result}' flagged for manual review.");
                continue;
            }

            // ── Step 8f: Conflict detection ──────────────────────────────
            var primaryControl = nistControls.FirstOrDefault() ?? string.Empty;
            var controlFamily = ExtractControlFamily(primaryControl);
            var vulnId = stigControl.VulnId ?? ruleResult.ExtractedRuleId;
            existingByStigId.TryGetValue(vulnId, out var existingFinding);

            // Build a pseudo CKL entry for conflict resolution reuse
            var pseudoEntry = new ParsedCklEntry(
                VulnId: vulnId,
                RuleId: ruleResult.ExtractedRuleId,
                StigVersion: null,
                RuleTitle: null,
                Severity: ruleResult.Severity,
                Status: ruleResult.Result == "fail" ? "Open" : "NotAFinding",
                FindingDetails: xccdfDetails,
                Comments: ruleResult.Message,
                SeverityOverride: null,
                SeverityJustification: null,
                CciRefs: new List<string>(),
                GroupTitle: null);

            if (existingFinding is not null)
            {
                var conflictAction = ApplyConflictResolution(
                    existingFinding, pseudoEntry, xccdfStatus, findingSeverity,
                    catSeverity, xccdfDetails, resolution, importRecord.Id);

                if (conflictAction == ImportFindingAction.Skipped)
                {
                    importFinding.ImportAction = ImportFindingAction.Skipped;
                    importFinding.ComplianceFindingId = existingFinding.Id;
                    skippedCount++;
                    importFindings.Add(importFinding);
                    continue;
                }

                importFinding.ImportAction = ImportFindingAction.Updated;
                importFinding.ComplianceFindingId = existingFinding.Id;
                findingsUpdated++;
                if (!updatedFindings.Contains(existingFinding))
                    updatedFindings.Add(existingFinding);
            }
            else
            {
                // ── Step 8g: Create new ComplianceFinding ────────────────
                var newFinding = new ComplianceFinding
                {
                    ControlId = primaryControl,
                    ControlFamily = controlFamily,
                    Title = $"STIG Finding {vulnId}",
                    Description = xccdfDetails,
                    Severity = findingSeverity,
                    Status = xccdfStatus,
                    ResourceId = system.Name,
                    ResourceType = "RegisteredSystem",
                    RemediationGuidance = ruleResult.Message ?? string.Empty,
                    Source = "XCCDF Import",
                    ScanSource = ScanSourceType.Combined,
                    StigFinding = true,
                    StigId = vulnId,
                    CatSeverity = catSeverity,
                    AssessmentId = resolvedAssessmentId,
                    ImportRecordId = importRecord.Id
                };

                importFinding.ImportAction = ImportFindingAction.Created;
                importFinding.ComplianceFindingId = newFinding.Id;
                newFindings.Add(newFinding);
                findingsCreated++;
            }

            // Track affected NIST controls
            foreach (var nist in nistControls.Where(c => baselineSet.Contains(c)))
                affectedNistControls.Add(nist);

            importFindings.Add(importFinding);
        }

        // Log unmatched rules
        if (unmatchedRules.Count > 0)
        {
            _logger.LogWarning(
                "XCCDF import {FileName}: {Count} unmatched rules: {RuleIds}",
                fileName, unmatchedRules.Count,
                string.Join(", ", unmatchedRules.Select(u => u.RuleId ?? u.VulnId)));
            warnings.Add(
                $"{unmatchedRules.Count} XCCDF rule(s) not found in curated library.");
        }

        // ── Step 9: Create evidence ──────────────────────────────────────
        var evidenceContent = JsonSerializer.Serialize(new
        {
            ImportType = "XCCDF",
            FileName = fileName,
            BenchmarkId = benchmarkId,
            TotalEntries = parsedXccdf.Results.Count,
            Score = parsedXccdf.Score,
            MaxScore = parsedXccdf.MaxScore,
            OpenCount = openCount,
            PassCount = passCount,
            NotApplicableCount = naCount,
            ErrorCount = errorCount,
            UnmatchedCount = unmatchedCount
        });

        var evidence = new ComplianceEvidence
        {
            ControlId = affectedNistControls.FirstOrDefault() ?? string.Empty,
            SubscriptionId = string.Empty,
            EvidenceType = "XccdfScanResult",
            Description = $"XCCDF Import: {fileName} ({benchmarkTitle ?? benchmarkId})",
            Content = evidenceContent,
            CollectedAt = DateTime.UtcNow,
            CollectedBy = importedBy,
            AssessmentId = resolvedAssessmentId,
            EvidenceCategory = EvidenceCategory.Configuration,
            ContentHash = fileHash,
            CollectionMethod = "Automated" // XCCDF = machine-verified
        };

        // ── Step 10: Upsert effectiveness ────────────────────────────────
        int effectivenessCreated = 0, effectivenessUpdated = 0;
        if (!dryRun && affectedNistControls.Count > 0)
        {
            var allStigFindings = await ctx.Findings
                .Where(f => f.AssessmentId == resolvedAssessmentId && f.StigFinding)
                .ToListAsync(ct);

            var allFindings = allStigFindings.Concat(newFindings).ToList();

            var controlFindingMap = allFindings
                .Where(f => !string.IsNullOrEmpty(f.ControlId))
                .GroupBy(f => f.ControlId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var controlId in affectedNistControls)
            {
                controlFindingMap.TryGetValue(controlId, out var controlFindings);
                var findingsForControl = controlFindings ?? new List<ComplianceFinding>();

                var anyOpen = findingsForControl.Any(f =>
                    f.Status == FindingStatus.Open || f.Status == FindingStatus.InProgress);
                var determination = anyOpen
                    ? EffectivenessDetermination.OtherThanSatisfied
                    : EffectivenessDetermination.Satisfied;

                CatSeverity? controlCatSeverity = null;
                if (anyOpen)
                {
                    var openCats = findingsForControl
                        .Where(f => f.Status == FindingStatus.Open || f.Status == FindingStatus.InProgress)
                        .Where(f => f.CatSeverity.HasValue)
                        .Select(f => f.CatSeverity!.Value)
                        .ToList();
                    if (openCats.Count > 0)
                        controlCatSeverity = openCats.Min();
                }

                var existingEffectiveness = await ctx.ControlEffectivenessRecords
                    .Where(e => e.AssessmentId == resolvedAssessmentId &&
                                e.RegisteredSystemId == systemId &&
                                e.ControlId == controlId)
                    .FirstOrDefaultAsync(ct);

                if (existingEffectiveness is not null)
                {
                    existingEffectiveness.Determination = determination;
                    existingEffectiveness.AssessmentMethod = "Test";
                    existingEffectiveness.AssessorId = importedBy;
                    existingEffectiveness.AssessedAt = DateTime.UtcNow;
                    existingEffectiveness.CatSeverity = controlCatSeverity;
                    if (!existingEffectiveness.EvidenceIds.Contains(evidence.Id))
                        existingEffectiveness.EvidenceIds.Add(evidence.Id);
                    existingEffectiveness.Notes = $"Re-evaluated via XCCDF import '{fileName}'";
                    effectivenessUpdated++;
                }
                else
                {
                    var effectiveness = new ControlEffectiveness
                    {
                        AssessmentId = resolvedAssessmentId,
                        RegisteredSystemId = systemId,
                        ControlId = controlId,
                        Determination = determination,
                        AssessmentMethod = "Test",
                        EvidenceIds = new List<string> { evidence.Id },
                        AssessorId = importedBy,
                        CatSeverity = controlCatSeverity,
                        Notes = $"Auto-determined via XCCDF import '{fileName}'"
                    };
                    ctx.ControlEffectivenessRecords.Add(effectiveness);
                    effectivenessCreated++;
                }
            }
        }

        // ── Step 11: Update import record ────────────────────────────────
        importRecord.TotalEntries = parsedXccdf.Results.Count;
        importRecord.OpenCount = openCount;
        importRecord.PassCount = passCount;
        importRecord.NotApplicableCount = naCount;
        importRecord.ErrorCount = errorCount;
        importRecord.SkippedCount = skippedCount;
        importRecord.UnmatchedCount = unmatchedCount;
        importRecord.FindingsCreated = findingsCreated;
        importRecord.FindingsUpdated = findingsUpdated;
        importRecord.EffectivenessRecordsCreated = effectivenessCreated;
        importRecord.EffectivenessRecordsUpdated = effectivenessUpdated;
        importRecord.NistControlsAffected = affectedNistControls.Count;
        importRecord.Warnings = warnings;
        importRecord.ImportStatus = warnings.Count > 0 || unmatchedCount > 0
            ? ScanImportStatus.CompletedWithWarnings
            : ScanImportStatus.Completed;

        // ── Step 12: Persist (unless dry-run) ────────────────────────────
        if (!dryRun)
        {
            ctx.ScanImportRecords.Add(importRecord);
            ctx.ScanImportFindings.AddRange(importFindings);
            ctx.Findings.AddRange(newFindings);
            ctx.Evidence.Add(evidence);
            await ctx.SaveChangesAsync(ct);
        }

        sw.Stop();
        _logger.LogInformation(
            "XCCDF import completed: file={FileName}, duration={DurationMs}ms, findings={FindingsCreated}/{FindingsUpdated}, " +
            "effectiveness={EffCreated}/{EffUpdated}, score={Score}, warnings={WarningCount}",
            fileName, sw.ElapsedMilliseconds, findingsCreated, findingsUpdated,
            effectivenessCreated, effectivenessUpdated, parsedXccdf.Score, warnings.Count);

        return new ImportResult(
            ImportRecordId: importRecord.Id,
            Status: importRecord.ImportStatus,
            BenchmarkId: benchmarkId ?? string.Empty,
            BenchmarkTitle: benchmarkTitle,
            TotalEntries: importRecord.TotalEntries,
            OpenCount: openCount,
            PassCount: passCount,
            NotApplicableCount: naCount,
            NotReviewedCount: 0, // XCCDF has no "not reviewed" state
            ErrorCount: errorCount,
            SkippedCount: skippedCount,
            UnmatchedCount: unmatchedCount,
            FindingsCreated: findingsCreated,
            FindingsUpdated: findingsUpdated,
            EffectivenessRecordsCreated: effectivenessCreated,
            EffectivenessRecordsUpdated: effectivenessUpdated,
            NistControlsAffected: affectedNistControls.Count,
            Warnings: warnings,
            UnmatchedRules: unmatchedRules,
            ErrorMessage: null);
    }

    // ─── ExportCklAsync (Phase 8 — T044) ─────────────────────────────────

    /// <inheritdoc />
    public async Task<string> ExportCklAsync(
        string systemId,
        string benchmarkId,
        string? assessmentId,
        CancellationToken ct = default)
    {
        // ── Step 1: Validate system ──────────────────────────────────────
        var system = await _rmfService.GetSystemAsync(systemId, ct);
        if (system is null)
            throw new InvalidOperationException($"System '{systemId}' not found.");

        // ── Step 2: Get all STIG controls for benchmark ──────────────────
        var stigControls = await _stigService.GetStigControlsByBenchmarkAsync(benchmarkId, ct);
        if (stigControls.Count == 0)
            throw new InvalidOperationException(
                $"No STIG controls found for benchmark '{benchmarkId}'. " +
                "Verify the benchmark ID matches the curated STIG library.");

        // ── Step 3: Query findings for the system/assessment ─────────────
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        IQueryable<ComplianceFinding> findingsQuery = ctx.Findings
            .Where(f => f.StigFinding && f.StigId != null);

        if (!string.IsNullOrEmpty(assessmentId))
        {
            findingsQuery = findingsQuery.Where(f => f.AssessmentId == assessmentId);
        }
        else
        {
            // Find the latest assessment for the system
            var latestAssessment = await ctx.Assessments
                .Where(a => a.RegisteredSystemId == systemId)
                .OrderByDescending(a => a.AssessedAt)
                .FirstOrDefaultAsync(ct);

            if (latestAssessment is not null)
            {
                findingsQuery = findingsQuery.Where(f => f.AssessmentId == latestAssessment.Id);
            }
        }

        var findingsList = await findingsQuery.ToListAsync(ct);

        // Build lookup dictionary by StigId (VulnId)
        var findingsDict = findingsList
            .Where(f => f.StigId is not null)
            .GroupBy(f => f.StigId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // ── Step 4: Determine benchmark metadata ─────────────────────────
        var firstControl = stigControls.First();
        var benchmarkTitle = firstControl.StigFamily is not null
            ? $"{firstControl.StigFamily} Security Technical Implementation Guide"
            : benchmarkId;
        var benchmarkVersion = "1"; // Default; StigControl doesn't carry benchmark version

        // ── Step 5: Generate CKL XML ─────────────────────────────────────
        var cklXml = _cklGenerator.Generate(
            system, stigControls, findingsDict,
            benchmarkId, benchmarkVersion, benchmarkTitle);

        // ── Step 6: Return base64 ────────────────────────────────────────
        var xmlBytes = System.Text.Encoding.UTF8.GetBytes(cklXml);
        var base64 = Convert.ToBase64String(xmlBytes);

        _logger.LogInformation(
            "CKL export completed: system={SystemId}, benchmark={BenchmarkId}, " +
            "vulnCount={VulnCount}, findingsPresent={FindingsPresent}",
            systemId, benchmarkId, stigControls.Count, findingsDict.Count);

        return base64;
    }

    // ─── ListImportsAsync (Phase 9 — T048) ──────────────────────────────

    /// <inheritdoc />
    public async Task<(List<ScanImportRecord> Records, int TotalCount)> ListImportsAsync(
        string systemId,
        int page,
        int pageSize,
        string? benchmarkId,
        string? importType,
        bool includeDryRuns,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        var query = ctx.ScanImportRecords
            .Where(r => r.RegisteredSystemId == systemId);

        if (!includeDryRuns)
            query = query.Where(r => !r.IsDryRun);

        if (!string.IsNullOrEmpty(benchmarkId))
            query = query.Where(r => r.BenchmarkId == benchmarkId);

        if (!string.IsNullOrEmpty(importType) &&
            Enum.TryParse<ScanImportType>(importType, true, out var typeFilter))
            query = query.Where(r => r.ImportType == typeFilter);

        if (fromDate.HasValue)
            query = query.Where(r => r.ImportedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(r => r.ImportedAt <= toDate.Value);

        var totalCount = await query.CountAsync(ct);

        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var records = await query
            .OrderByDescending(r => r.ImportedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (records, totalCount);
    }

    // ─── GetImportSummaryAsync (Phase 9 — T049) ─────────────────────────

    /// <inheritdoc />
    public async Task<(ScanImportRecord Record, List<ScanImportFinding> Findings)?> GetImportSummaryAsync(
        string importId,
        CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        var record = await ctx.ScanImportRecords.FindAsync(new object[] { importId }, ct);
        if (record is null)
            return null;

        var findings = await ctx.ScanImportFindings
            .Where(f => f.ScanImportRecordId == importId)
            .ToListAsync(ct);

        return (record, findings);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Private helpers
    // ═══════════════════════════════════════════════════════════════════════

    // ─── STIG Resolution (T014) ──────────────────────────────────────────

    /// <summary>
    /// Resolve a parsed CKL entry to a StigControl record.
    /// Tries: VulnId → RuleId → StigVersion (fallback chain).
    /// </summary>
    private async Task<StigControl?> ResolveStigControlAsync(
        ParsedCklEntry entry,
        CancellationToken ct)
    {
        // Primary: VulnId (e.g., "V-254239")
        var control = await _stigService.GetStigControlAsync(entry.VulnId, ct);
        if (control is not null)
            return control;

        // Fallback: RuleId (e.g., "SV-254239r849090_rule")
        if (!string.IsNullOrEmpty(entry.RuleId))
        {
            control = await _stigService.GetStigControlByRuleIdAsync(entry.RuleId, ct);
            if (control is not null)
                return control;
        }

        // Fallback: StigVersion as StigId (e.g., "WN22-AU-000010")
        if (!string.IsNullOrEmpty(entry.StigVersion))
        {
            control = await _stigService.GetStigControlAsync(entry.StigVersion, ct);
            if (control is not null)
                return control;
        }

        return null;
    }

    // ─── Severity Mapping ────────────────────────────────────────────────

    /// <summary>
    /// Map CKL severity string → FindingSeverity + CatSeverity.
    /// Applies severity override if present.
    /// </summary>
    private static (FindingSeverity Severity, CatSeverity? Cat) MapSeverity(
        string rawSeverity,
        string? severityOverride)
    {
        var effective = !string.IsNullOrWhiteSpace(severityOverride)
            ? severityOverride
            : rawSeverity;

        return effective.ToLowerInvariant() switch
        {
            "high" => (FindingSeverity.High, CatSeverity.CatI),
            "medium" => (FindingSeverity.Medium, CatSeverity.CatII),
            "low" => (FindingSeverity.Low, CatSeverity.CatIII),
            _ => (FindingSeverity.Medium, CatSeverity.CatII) // default to medium
        };
    }

    // ─── CKL Status Mapping (T019) ──────────────────────────────────────

    /// <summary>
    /// Map CKL STATUS to FindingStatus and finding details text.
    /// </summary>
    private static (FindingStatus Status, string Details) MapCklStatus(ParsedCklEntry entry)
    {
        return entry.Status switch
        {
            "Open" => (FindingStatus.Open,
                entry.FindingDetails ?? $"Open finding from STIG rule {entry.VulnId}"),

            "NotAFinding" => (FindingStatus.Remediated,
                entry.FindingDetails ?? $"Verified compliant for STIG rule {entry.VulnId}"),

            "Not_Reviewed" => (FindingStatus.Open,
                !string.IsNullOrEmpty(entry.FindingDetails)
                    ? $"Not yet reviewed. {entry.FindingDetails}"
                    : $"Not yet reviewed — STIG rule {entry.VulnId} has not been evaluated."),

            _ => (FindingStatus.Open,
                entry.FindingDetails ?? $"Unknown status '{entry.Status}' for STIG rule {entry.VulnId}")
        };
    }

    // ─── XCCDF Result Mapping (T038) ────────────────────────────────────

    /// <summary>
    /// Map XCCDF result string → FindingStatus and finding details text.
    /// </summary>
    private static (FindingStatus Status, string Details) MapXccdfResult(ParsedXccdfResult ruleResult)
    {
        return ruleResult.Result switch
        {
            "fail" => (FindingStatus.Open,
                ruleResult.Message ?? $"XCCDF scan failure for rule {ruleResult.ExtractedRuleId}"),

            "pass" => (FindingStatus.Remediated,
                ruleResult.Message ?? $"XCCDF scan passed for rule {ruleResult.ExtractedRuleId}"),

            "error" => (FindingStatus.Open,
                $"XCCDF scan error for rule {ruleResult.ExtractedRuleId}: {ruleResult.Message ?? "check failed with error"}"),

            "notchecked" => (FindingStatus.Open,
                $"XCCDF rule {ruleResult.ExtractedRuleId} was not checked."),

            "unknown" => (FindingStatus.Open,
                $"XCCDF result unknown for rule {ruleResult.ExtractedRuleId}."),

            _ => (FindingStatus.Open,
                ruleResult.Message ?? $"Unrecognized XCCDF result '{ruleResult.Result}' for rule {ruleResult.ExtractedRuleId}")
        };
    }

    /// <summary>
    /// Extract benchmark ID from XCCDF benchmark href.
    /// E.g., "xccdf_mil.disa.stig_benchmark_Windows_Server_2022_STIG" → "Windows_Server_2022_STIG"
    /// </summary>
    private static string? ExtractBenchmarkId(string? href)
    {
        if (string.IsNullOrEmpty(href)) return null;

        const string prefix = "xccdf_mil.disa.stig_benchmark_";
        if (href.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return href[prefix.Length..];

        return href;
    }

    /// <summary>
    /// Resolve a STIG control using the XCCDF extracted rule ID.
    /// Tries: RuleId → VulnId (derived from rule ID) fallback chain.
    /// </summary>
    private async Task<StigControl?> ResolveStigControlByRuleIdAsync(
        string extractedRuleId,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(extractedRuleId)) return null;

        // Primary: RuleId lookup
        var control = await _stigService.GetStigControlByRuleIdAsync(extractedRuleId, ct);
        if (control is not null) return control;

        // Fallback: try as VulnId (extract V-XXXXXX from SV-XXXXXX)
        if (extractedRuleId.StartsWith("SV-", StringComparison.OrdinalIgnoreCase))
        {
            var vulnPart = extractedRuleId[1..]; // Remove "S" → "V-XXXXXX..."
            var dashIdx = vulnPart.IndexOf('r');
            var vulnId = dashIdx > 0 ? vulnPart[..dashIdx] : vulnPart;
            control = await _stigService.GetStigControlAsync(vulnId, ct);
            if (control is not null) return control;
        }

        // Direct lookup as-is
        return await _stigService.GetStigControlAsync(extractedRuleId, ct);
    }

    // ─── Conflict Resolution (T025–T026) ─────────────────────────────────

    /// <summary>
    /// Apply conflict resolution strategy to an existing finding.
    /// Returns the action taken: Skipped or Updated.
    /// For Overwrite/Merge, modifies the existing finding in-place.
    /// </summary>
    private static ImportFindingAction ApplyConflictResolution(
        ComplianceFinding existing,
        ParsedCklEntry entry,
        FindingStatus importedStatus,
        FindingSeverity importedSeverity,
        CatSeverity? importedCat,
        string importedDetails,
        ImportConflictResolution resolution,
        string importRecordId)
    {
        switch (resolution)
        {
            case ImportConflictResolution.Skip:
                return ImportFindingAction.Skipped;

            case ImportConflictResolution.Overwrite:
                existing.Status = importedStatus;
                existing.Severity = importedSeverity;
                existing.CatSeverity = importedCat;
                existing.Description = importedDetails;
                existing.RemediationGuidance = entry.Comments ?? existing.RemediationGuidance;
                existing.ImportRecordId = importRecordId;
                existing.Title = entry.RuleTitle ?? existing.Title;
                return ImportFindingAction.Updated;

            case ImportConflictResolution.Merge:
                // Keep more-recent status (Open takes precedence for safety)
                if (importedStatus == FindingStatus.Open && existing.Status != FindingStatus.Open)
                    existing.Status = importedStatus;
                else if (importedStatus == FindingStatus.Remediated && existing.Status == FindingStatus.Open)
                {
                    // If imported says remediated but existing is open, keep open (more conservative)
                    // unless existing was already remediated
                }
                else if (existing.Status == FindingStatus.Remediated && importedStatus == FindingStatus.Remediated)
                {
                    // Both say remediated — keep as-is
                }

                // Use imported severity only if higher (CatI > CatII > CatIII)
                if (importedCat.HasValue && existing.CatSeverity.HasValue)
                {
                    if (importedCat.Value < existing.CatSeverity.Value) // lower enum = higher severity
                    {
                        existing.Severity = importedSeverity;
                        existing.CatSeverity = importedCat;
                    }
                }
                else if (importedCat.HasValue && !existing.CatSeverity.HasValue)
                {
                    existing.Severity = importedSeverity;
                    existing.CatSeverity = importedCat;
                }

                // Append finding details if different
                if (!string.IsNullOrEmpty(importedDetails) &&
                    !existing.Description.Contains(importedDetails, StringComparison.OrdinalIgnoreCase))
                {
                    existing.Description = string.IsNullOrEmpty(existing.Description)
                        ? importedDetails
                        : $"{existing.Description}\n\n[Merged from import]: {importedDetails}";
                }

                existing.ImportRecordId = importRecordId;
                return ImportFindingAction.Updated;

            default:
                return ImportFindingAction.Skipped;
        }
    }

    // ─── Assessment Context ──────────────────────────────────────────────

    /// <summary>
    /// Get an existing active assessment for the system, or create one.
    /// </summary>
    private static async Task<string> GetOrCreateAssessmentAsync(
        AtoCopilotContext ctx,
        string systemId,
        string importedBy,
        CancellationToken ct)
    {
        // Try to find an active (non-completed, non-cancelled) assessment for this system
        var existing = await ctx.Assessments
            .Where(a => a.RegisteredSystemId == systemId &&
                        a.Status != AssessmentStatus.Completed &&
                        a.Status != AssessmentStatus.Cancelled &&
                        a.Status != AssessmentStatus.Failed)
            .OrderByDescending(a => a.AssessedAt)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
            return existing.Id;

        // Create a new assessment for STIG/SCAP import
        var assessment = new ComplianceAssessment
        {
            RegisteredSystemId = systemId,
            Framework = "NIST80053",
            ScanType = "combined",
            Status = AssessmentStatus.InProgress,
            InitiatedBy = importedBy,
            ProgressMessage = "Created for STIG/SCAP import"
        };

        ctx.Assessments.Add(assessment);
        await ctx.SaveChangesAsync(ct);

        return assessment.Id;
    }

    // ─── Utilities ───────────────────────────────────────────────────────

    /// <summary>Compute SHA-256 hash of raw bytes, returned as lowercase hex.</summary>
    internal static string ComputeSha256(byte[] data)
    {
        var hashBytes = SHA256.HashData(data);
        return Convert.ToHexStringLower(hashBytes);
    }

    /// <summary>Extract control family prefix (e.g., "AC" from "AC-2").</summary>
    private static string ExtractControlFamily(string controlId)
    {
        if (string.IsNullOrEmpty(controlId))
            return string.Empty;
        var dashIndex = controlId.IndexOf('-');
        return dashIndex > 0 ? controlId[..dashIndex] : controlId;
    }

    /// <summary>Create a failed ImportResult with error message.</summary>
    private static ImportResult CreateFailedResult(string error, string fileName)
    {
        return new ImportResult(
            ImportRecordId: string.Empty,
            Status: ScanImportStatus.Failed,
            BenchmarkId: string.Empty,
            BenchmarkTitle: null,
            TotalEntries: 0,
            OpenCount: 0,
            PassCount: 0,
            NotApplicableCount: 0,
            NotReviewedCount: 0,
            ErrorCount: 0,
            SkippedCount: 0,
            UnmatchedCount: 0,
            FindingsCreated: 0,
            FindingsUpdated: 0,
            EffectivenessRecordsCreated: 0,
            EffectivenessRecordsUpdated: 0,
            NistControlsAffected: 0,
            Warnings: new List<string>(),
            UnmatchedRules: new List<UnmatchedRuleInfo>(),
            ErrorMessage: error);
    }
}
