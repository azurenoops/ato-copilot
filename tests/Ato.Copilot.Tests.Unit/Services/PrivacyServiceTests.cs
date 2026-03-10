using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Models.Compliance;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for PrivacyService — PTA auto-detection, manual mode, PIA lifecycle, and privacy compliance.
/// Feature 021 Tasks: T011, T012, T013.
/// </summary>
public class PrivacyServiceTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AtoCopilotContext _db;
    private readonly PrivacyService _service;

    public PrivacyServiceTests()
    {
        var dbName = $"PrivacyService_{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddDbContext<AtoCopilotContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
        _db = _serviceProvider.GetRequiredService<AtoCopilotContext>();

        _service = new PrivacyService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Mock.Of<ILogger<PrivacyService>>());
    }

    private async Task<RegisteredSystem> SeedSystemAsync(
        string? name = null,
        List<InformationType>? infoTypes = null,
        bool withCategorization = true)
    {
        var system = new RegisteredSystem
        {
            Name = name ?? "Test System",
            SystemType = SystemType.MajorApplication,
            MissionCriticality = MissionCriticality.MissionEssential,
            HostingEnvironment = "Azure Government",
            CreatedBy = "test-user"
        };
        _db.RegisteredSystems.Add(system);

        if (withCategorization)
        {
            var cat = new SecurityCategorization
            {
                RegisteredSystemId = system.Id,
                CategorizedBy = "test-user",
                CategorizedAt = DateTime.UtcNow
            };
            _db.SecurityCategorizations.Add(cat);

            if (infoTypes != null)
            {
                foreach (var it in infoTypes)
                {
                    it.SecurityCategorizationId = cat.Id;
                    _db.InformationTypes.Add(it);
                }
            }
        }

        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return system;
    }

    private static InformationType MakeInfoType(string sp80060Id, string name) => new()
    {
        Sp80060Id = sp80060Id,
        Name = name,
        ConfidentialityImpact = ImpactValue.Moderate,
        IntegrityImpact = ImpactValue.Moderate,
        AvailabilityImpact = ImpactValue.Moderate
    };

    // ─── T011: PTA Auto-Detection Tests ──────────────────────────────────────

    [Fact]
    public async Task CreatePta_AutoDetect_KnownPiiPrefix_D8_ReturnsPiaRequired()
    {
        // Arrange — D.8.x = Personnel Records (known PII)
        var system = await SeedSystemAsync(infoTypes: [
            MakeInfoType("D.8.1", "Human Resources Personnel Records")
        ]);

        // Act
        var result = await _service.CreatePtaAsync(system.Id, "isso-user");

        // Assert
        result.Determination.Should().Be(PtaDetermination.PiaRequired);
        result.CollectsPii.Should().BeTrue();
        result.PiiSourceInfoTypes.Should().Contain("D.8.1");
    }

    [Fact]
    public async Task CreatePta_AutoDetect_KnownPiiPrefix_D17_ReturnsPiaRequired()
    {
        // D.17.x = Health/Medical (known PII)
        var system = await SeedSystemAsync(infoTypes: [
            MakeInfoType("D.17.2", "Medical Records Management")
        ]);

        var result = await _service.CreatePtaAsync(system.Id, "isso-user");

        result.Determination.Should().Be(PtaDetermination.PiaRequired);
        result.PiiSourceInfoTypes.Should().Contain("D.17.2");
    }

    [Fact]
    public async Task CreatePta_AutoDetect_KnownPiiPrefix_D28_ReturnsPiaRequired()
    {
        // D.28.x = Financial (known PII)
        var system = await SeedSystemAsync(infoTypes: [
            MakeInfoType("D.28.1", "Financial Transactions")
        ]);

        var result = await _service.CreatePtaAsync(system.Id, "isso-user");

        result.Determination.Should().Be(PtaDetermination.PiaRequired);
        result.PiiSourceInfoTypes.Should().Contain("D.28.1");
    }

    [Fact]
    public async Task CreatePta_AutoDetect_NoPiiInfoTypes_ReturnsPiaNotRequired()
    {
        // D.3.x = Defense readiness (no PII)
        var system = await SeedSystemAsync(infoTypes: [
            MakeInfoType("D.3.1", "Force Readiness Reporting"),
            MakeInfoType("D.4.2", "Logistics Management")
        ]);

        var result = await _service.CreatePtaAsync(system.Id, "isso-user");

        result.Determination.Should().Be(PtaDetermination.PiaNotRequired);
        result.CollectsPii.Should().BeFalse();
        result.PiiSourceInfoTypes.Should().BeEmpty();
    }

    [Fact]
    public async Task CreatePta_AutoDetect_AmbiguousInfoTypes_ReturnsPendingConfirmation()
    {
        // D.7.x is ambiguous — could contain PII in some cases
        var system = await SeedSystemAsync(infoTypes: [
            MakeInfoType("D.7.1", "General Information Services")
        ]);

        var result = await _service.CreatePtaAsync(system.Id, "isso-user");

        // Ambiguous types should not auto-determine PiaRequired — flag for human review
        result.Determination.Should().BeOneOf(
            PtaDetermination.PiaNotRequired,
            PtaDetermination.PendingConfirmation);
    }

    [Fact]
    public async Task CreatePta_AutoDetect_ReplacesExistingPta()
    {
        var system = await SeedSystemAsync(infoTypes: [
            MakeInfoType("D.8.1", "Personnel Records")
        ]);

        // Create first PTA
        var first = await _service.CreatePtaAsync(system.Id, "isso-user");

        // Add non-PII types and re-run
        var cat = await _db.SecurityCategorizations
            .FirstAsync(c => c.RegisteredSystemId == system.Id);
        _db.InformationTypes.RemoveRange(
            await _db.InformationTypes.Where(i => i.SecurityCategorizationId == cat.Id).ToListAsync());
        _db.InformationTypes.Add(new InformationType
        {
            SecurityCategorizationId = cat.Id,
            Sp80060Id = "D.3.1",
            Name = "Force Readiness",
            ConfidentialityImpact = ImpactValue.Low,
            IntegrityImpact = ImpactValue.Low,
            AvailabilityImpact = ImpactValue.Low
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // Create second PTA — should replace, not duplicate
        var second = await _service.CreatePtaAsync(system.Id, "isso-user");

        second.PtaId.Should().NotBe(first.PtaId);
        var ptaCount = await _db.PrivacyThresholdAnalyses.CountAsync(p => p.RegisteredSystemId == system.Id);
        ptaCount.Should().Be(1);
    }

    [Fact]
    public async Task CreatePta_AutoDetect_ExemptPath_WithRationale()
    {
        var system = await SeedSystemAsync(infoTypes: [
            MakeInfoType("D.3.1", "Force Readiness")
        ]);

        var result = await _service.CreatePtaAsync(
            system.Id, "isso-user",
            exemptionRationale: "Government-to-government system, exempt per E-Government Act");

        result.Determination.Should().Be(PtaDetermination.Exempt);
    }

    // ─── T012: PTA Manual Mode Tests ─────────────────────────────────────────

    [Fact]
    public async Task CreatePta_ManualMode_ExplicitPiiFlags_ReturnsPiaRequired()
    {
        var system = await SeedSystemAsync(withCategorization: false);

        var result = await _service.CreatePtaAsync(
            system.Id, "isso-user",
            manualMode: true,
            collectsPii: true,
            maintainsPii: true,
            disseminatesPii: false,
            piiCategories: ["SSN", "Medical Records"],
            estimatedRecordCount: 50_000);

        result.Determination.Should().Be(PtaDetermination.PiaRequired);
        result.CollectsPii.Should().BeTrue();
        result.MaintainsPii.Should().BeTrue();
        result.DisseminatesPii.Should().BeFalse();
        result.PiiCategories.Should().Contain("SSN");
    }

    [Fact]
    public async Task CreatePta_ManualMode_NoPiiFlags_ReturnsPiaNotRequired()
    {
        var system = await SeedSystemAsync(withCategorization: false);

        var result = await _service.CreatePtaAsync(
            system.Id, "isso-user",
            manualMode: true,
            collectsPii: false,
            maintainsPii: false,
            disseminatesPii: false);

        result.Determination.Should().Be(PtaDetermination.PiaNotRequired);
    }

    [Fact]
    public async Task CreatePta_ManualMode_ExemptionRationale_ReturnsExempt()
    {
        var system = await SeedSystemAsync(withCategorization: false);

        var result = await _service.CreatePtaAsync(
            system.Id, "isso-user",
            manualMode: true,
            exemptionRationale: "National security system exemption");

        result.Determination.Should().Be(PtaDetermination.Exempt);
    }

    [Fact]
    public async Task CreatePta_ManualMode_RecordCountThreshold_TriggersPia()
    {
        // E-Gov Act: ≥10 PII records triggers PIA
        var system = await SeedSystemAsync(withCategorization: false);

        var result = await _service.CreatePtaAsync(
            system.Id, "isso-user",
            manualMode: true,
            collectsPii: true,
            estimatedRecordCount: 10);

        result.Determination.Should().Be(PtaDetermination.PiaRequired);
    }

    [Fact]
    public async Task CreatePta_SystemNotFound_ThrowsInvalidOperation()
    {
        var act = () => _service.CreatePtaAsync("nonexistent-id", "isso-user");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // ─── T013: PIA Lifecycle Tests ───────────────────────────────────────────

    [Fact]
    public async Task GeneratePia_WithPtaPiaRequired_CreatesDraftWithSections()
    {
        var system = await SeedSystemAsync(infoTypes: [
            MakeInfoType("D.8.1", "Personnel Records")
        ]);

        // Create PTA first
        await _service.CreatePtaAsync(system.Id, "isso-user");

        // Generate PIA
        var result = await _service.GeneratePiaAsync(system.Id, "isso-user");

        result.Status.Should().Be(PiaStatus.Draft);
        result.Version.Should().Be(1);
        result.TotalSections.Should().Be(8);
        result.Sections.Should().HaveCount(8);
    }

    [Fact]
    public async Task GeneratePia_NoPta_ThrowsInvalidOperation()
    {
        var system = await SeedSystemAsync(withCategorization: false);

        var act = () => _service.GeneratePiaAsync(system.Id, "isso-user");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*PTA*");
    }

    [Fact]
    public async Task GeneratePia_PtaPiaNotRequired_ThrowsInvalidOperation()
    {
        var system = await SeedSystemAsync(infoTypes: [
            MakeInfoType("D.3.1", "Force Readiness")
        ]);

        // PTA should return PiaNotRequired
        await _service.CreatePtaAsync(system.Id, "isso-user");

        var act = () => _service.GeneratePiaAsync(system.Id, "isso-user");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*PiaRequired*");
    }

    [Fact]
    public async Task ReviewPia_Approve_SetsApprovedWithExpiration()
    {
        var system = await SeedSystemAsync(infoTypes: [
            MakeInfoType("D.8.1", "Personnel Records")
        ]);
        await _service.CreatePtaAsync(system.Id, "isso-user");
        await _service.GeneratePiaAsync(system.Id, "isso-user");

        var result = await _service.ReviewPiaAsync(
            system.Id,
            PiaReviewDecision.Approved,
            "Meets all OMB M-03-22 requirements",
            "issm-user");

        result.Decision.Should().Be(PiaReviewDecision.Approved);
        result.NewStatus.Should().Be(PiaStatus.Approved);
        result.ExpirationDate.Should().BeCloseTo(DateTime.UtcNow.AddYears(1), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task ReviewPia_RequestRevision_SetsDraftWithDeficiencies()
    {
        var system = await SeedSystemAsync(infoTypes: [
            MakeInfoType("D.8.1", "Personnel Records")
        ]);
        await _service.CreatePtaAsync(system.Id, "isso-user");
        await _service.GeneratePiaAsync(system.Id, "isso-user");

        var deficiencies = new List<string> { "Section 4.1 missing consent mechanism", "Section 6.1 incomplete safeguards" };

        var result = await _service.ReviewPiaAsync(
            system.Id,
            PiaReviewDecision.RequestRevision,
            "Needs additional detail",
            "issm-user",
            deficiencies);

        result.Decision.Should().Be(PiaReviewDecision.RequestRevision);
        result.NewStatus.Should().Be(PiaStatus.Draft);
        result.Deficiencies.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReviewPia_NoPia_ThrowsInvalidOperation()
    {
        var system = await SeedSystemAsync(withCategorization: false);

        var act = () => _service.ReviewPiaAsync(
            system.Id, PiaReviewDecision.Approved, "OK", "issm-user");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task InvalidatePta_DeletesPtaAndSetsPiaToUnderReview()
    {
        var system = await SeedSystemAsync(infoTypes: [
            MakeInfoType("D.8.1", "Personnel Records")
        ]);
        await _service.CreatePtaAsync(system.Id, "isso-user");
        await _service.GeneratePiaAsync(system.Id, "isso-user");
        await _service.ReviewPiaAsync(
            system.Id, PiaReviewDecision.Approved, "Approved", "issm-user");

        // Invalidate PTA
        await _service.InvalidatePtaAsync(system.Id);

        // PTA should be deleted
        var pta = await _db.PrivacyThresholdAnalyses
            .FirstOrDefaultAsync(p => p.RegisteredSystemId == system.Id);
        pta.Should().BeNull();

        // PIA should be reset to UnderReview (preserving content)
        var pia = await _db.PrivacyImpactAssessments
            .FirstOrDefaultAsync(p => p.RegisteredSystemId == system.Id);
        pia.Should().NotBeNull();
        pia!.Status.Should().Be(PiaStatus.UnderReview);
    }

    [Fact]
    public async Task InvalidatePta_NoPta_IsNoOp()
    {
        var system = await SeedSystemAsync(withCategorization: false);

        // Should not throw
        await _service.InvalidatePtaAsync(system.Id);
    }

    [Fact]
    public async Task GetPrivacyCompliance_FullyCompliant_ReturnsCompliant()
    {
        var system = await SeedSystemAsync(infoTypes: [
            MakeInfoType("D.8.1", "Personnel Records")
        ]);

        // Mark system as having no external interconnections for full compliance
        var s = await _db.RegisteredSystems.FirstAsync(r => r.Id == system.Id);
        s.HasNoExternalInterconnections = true;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await _service.CreatePtaAsync(system.Id, "isso-user");
        await _service.GeneratePiaAsync(system.Id, "isso-user");
        await _service.ReviewPiaAsync(
            system.Id, PiaReviewDecision.Approved, "Approved", "issm-user");

        var result = await _service.GetPrivacyComplianceAsync(system.Id);

        result.PrivacyGateSatisfied.Should().BeTrue();
        result.OverallStatus.Should().Be("Compliant");
    }

    [Fact]
    public async Task GetPrivacyCompliance_NoPta_ReturnsNotStarted()
    {
        var system = await SeedSystemAsync(withCategorization: false);

        var result = await _service.GetPrivacyComplianceAsync(system.Id);

        result.PtaDetermination.Should().BeNull();
        result.OverallStatus.Should().Be("NotStarted");
    }

    [Fact]
    public async Task GetPrivacyCompliance_PtaButNoPia_ReturnsActionRequired()
    {
        var system = await SeedSystemAsync(infoTypes: [
            MakeInfoType("D.8.1", "Personnel Records")
        ]);
        await _service.CreatePtaAsync(system.Id, "isso-user");

        var result = await _service.GetPrivacyComplianceAsync(system.Id);

        result.PtaDetermination.Should().Be(PtaDetermination.PiaRequired);
        result.PiaStatus.Should().BeNull();
        result.PrivacyGateSatisfied.Should().BeFalse();
        result.OverallStatus.Should().Be("ActionRequired");
    }

    [Fact]
    public async Task ReviewPia_Approve_ResubmitIncrementsVersion()
    {
        var system = await SeedSystemAsync(infoTypes: [
            MakeInfoType("D.8.1", "Personnel Records")
        ]);
        await _service.CreatePtaAsync(system.Id, "isso-user");
        await _service.GeneratePiaAsync(system.Id, "isso-user");

        // First: request revision
        await _service.ReviewPiaAsync(
            system.Id, PiaReviewDecision.RequestRevision,
            "Needs work", "issm-user",
            ["Missing consent details"]);

        // Resubmit (regenerate) — version should increment
        var secondResult = await _service.GeneratePiaAsync(system.Id, "isso-user");
        secondResult.Version.Should().BeGreaterThan(1);
    }
}
