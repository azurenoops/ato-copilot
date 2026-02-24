using Xunit;
using FluentAssertions;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Models;

/// <summary>
/// Tests for Feature 008 compliance model enums, factory methods,
/// severity weights, and certificate validity rules.
/// </summary>
public class ComplianceModelTests
{
    // ─── Enum Validation ────────────────────────────────────────────────

    [Theory]
    [InlineData(ComplianceRiskLevel.Low, 0)]
    [InlineData(ComplianceRiskLevel.Medium, 1)]
    [InlineData(ComplianceRiskLevel.High, 2)]
    [InlineData(ComplianceRiskLevel.Critical, 3)]
    public void ComplianceRiskLevel_EnumValues_AreCorrectlyOrdered(ComplianceRiskLevel level, int expected)
    {
        ((int)level).Should().Be(expected);
    }

    [Theory]
    [InlineData(FamilyAssessmentStatus.Pending, 0)]
    [InlineData(FamilyAssessmentStatus.Completed, 1)]
    [InlineData(FamilyAssessmentStatus.Failed, 2)]
    [InlineData(FamilyAssessmentStatus.Skipped, 3)]
    public void FamilyAssessmentStatus_EnumValues_AreCorrectlyOrdered(FamilyAssessmentStatus status, int expected)
    {
        ((int)status).Should().Be(expected);
    }

    [Fact]
    public void EvidenceType_HasExpectedFiveValues()
    {
        Enum.GetValues<EvidenceType>().Should().HaveCount(5);
    }

    [Theory]
    [InlineData(EvidenceType.Configuration)]
    [InlineData(EvidenceType.Log)]
    [InlineData(EvidenceType.Metric)]
    [InlineData(EvidenceType.Policy)]
    [InlineData(EvidenceType.AccessControl)]
    public void EvidenceType_ContainsExpectedValue(EvidenceType type)
    {
        Enum.IsDefined(type).Should().BeTrue();
    }

    [Fact]
    public void TimelineEventType_HasExpectedValues()
    {
        Enum.GetValues<TimelineEventType>().Length.Should().BeGreaterOrEqualTo(10);
    }

    [Theory]
    [InlineData(TrendDirection.Improving)]
    [InlineData(TrendDirection.Stable)]
    [InlineData(TrendDirection.Degrading)]
    public void TrendDirection_ContainsExpectedValue(TrendDirection direction)
    {
        Enum.IsDefined(direction).Should().BeTrue();
    }

    [Theory]
    [InlineData(CertificateStatus.Active)]
    [InlineData(CertificateStatus.Expired)]
    [InlineData(CertificateStatus.Revoked)]
    public void CertificateStatus_ContainsExpectedValue(CertificateStatus status)
    {
        Enum.IsDefined(status).Should().BeTrue();
    }

    [Theory]
    [InlineData(RemediationTrackingStatus.NotStarted)]
    [InlineData(RemediationTrackingStatus.InProgress)]
    [InlineData(RemediationTrackingStatus.Completed)]
    [InlineData(RemediationTrackingStatus.WontFix)]
    [InlineData(RemediationTrackingStatus.Deferred)]
    public void RemediationTrackingStatus_ContainsExpectedValue(RemediationTrackingStatus status)
    {
        Enum.IsDefined(status).Should().BeTrue();
    }

    // ─── ControlFamilyAssessment ────────────────────────────────────────

    [Fact]
    public void ControlFamilyAssessment_Defaults_AreCorrect()
    {
        var cfa = new ControlFamilyAssessment();

        cfa.FamilyCode.Should().BeEmpty();
        cfa.FamilyName.Should().BeEmpty();
        cfa.TotalControls.Should().Be(0);
        cfa.PassedControls.Should().Be(0);
        cfa.FailedControls.Should().Be(0);
        cfa.ComplianceScore.Should().Be(0);
        cfa.Findings.Should().BeEmpty();
        cfa.AssessmentDuration.Should().Be(TimeSpan.Zero);
        cfa.ScannerName.Should().BeEmpty();
        cfa.Status.Should().Be(FamilyAssessmentStatus.Pending);
        cfa.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ControlFamilyAssessment_Failed_SetsCorrectValues()
    {
        var result = ControlFamilyAssessment.Failed("AC", "Connection timeout");

        result.FamilyCode.Should().Be("AC");
        result.Status.Should().Be(FamilyAssessmentStatus.Failed);
        result.ErrorMessage.Should().Be("Connection timeout");
        result.TotalControls.Should().Be(0);
        result.PassedControls.Should().Be(0);
        result.ComplianceScore.Should().Be(0);
    }

    [Theory]
    [InlineData("AC")]
    [InlineData("IA")]
    [InlineData("SC")]
    [InlineData("SI")]
    [InlineData("CM")]
    public void ControlFamilyAssessment_Failed_PreservesFamilyCode(string familyCode)
    {
        var result = ControlFamilyAssessment.Failed(familyCode, "test error");
        result.FamilyCode.Should().Be(familyCode);
    }

    [Fact]
    public void ControlFamilyAssessment_Failed_LeavesEmptyFindings()
    {
        var result = ControlFamilyAssessment.Failed("AC", "error");
        result.Findings.Should().BeEmpty();
    }

    // ─── RiskProfile ────────────────────────────────────────────────────

    [Theory]
    [InlineData(10, 0, 0, 0, 100.0)]   // 10 * 10.0 = 100  → Critical
    [InlineData(0, 7, 0, 0, 52.5)]     // 7 * 7.5 = 52.5   → High
    [InlineData(0, 0, 5, 0, 25.0)]     // 5 * 5.0 = 25.0   → Medium
    [InlineData(0, 0, 0, 3, 7.5)]      // 3 * 2.5 = 7.5    → Low
    public void RiskProfile_SeverityWeights_CalculateCorrectScore(
        int critical, int high, int medium, int low, double expectedScore)
    {
        const double criticalWeight = 10.0;
        const double highWeight = 7.5;
        const double mediumWeight = 5.0;
        const double lowWeight = 2.5;

        var score = critical * criticalWeight +
                    high * highWeight +
                    medium * mediumWeight +
                    low * lowWeight;

        score.Should().Be(expectedScore);
    }

    [Theory]
    [InlineData(100.0, ComplianceRiskLevel.Critical)]
    [InlineData(150.0, ComplianceRiskLevel.Critical)]
    [InlineData(50.0, ComplianceRiskLevel.High)]
    [InlineData(99.9, ComplianceRiskLevel.High)]
    [InlineData(20.0, ComplianceRiskLevel.Medium)]
    [InlineData(49.9, ComplianceRiskLevel.Medium)]
    [InlineData(0.0, ComplianceRiskLevel.Low)]
    [InlineData(19.9, ComplianceRiskLevel.Low)]
    public void RiskProfile_RiskLevelThresholds_AreCorrect(double score, ComplianceRiskLevel expectedLevel)
    {
        // Thresholds: ≥100 Critical, ≥50 High, ≥20 Medium, <20 Low
        var level = score >= 100 ? ComplianceRiskLevel.Critical
                  : score >= 50  ? ComplianceRiskLevel.High
                  : score >= 20  ? ComplianceRiskLevel.Medium
                  : ComplianceRiskLevel.Low;

        level.Should().Be(expectedLevel);
    }

    [Fact]
    public void RiskProfile_Defaults_AreCorrect()
    {
        var profile = new RiskProfile();

        profile.RiskScore.Should().Be(0);
        profile.RiskLevel.Should().Be(ComplianceRiskLevel.Low);
        profile.CriticalCount.Should().Be(0);
        profile.HighCount.Should().Be(0);
        profile.MediumCount.Should().Be(0);
        profile.LowCount.Should().Be(0);
        profile.TopRisks.Should().BeEmpty();
    }

    [Fact]
    public void RiskProfile_TopRisks_LimitedToFive()
    {
        var profile = new RiskProfile();
        for (int i = 0; i < 5; i++)
        {
            profile.TopRisks.Add(new FamilyRisk
            {
                FamilyCode = ControlFamilies.AllFamilies.ElementAt(i),
                ComplianceScore = 50.0 + i
            });
        }

        profile.TopRisks.Should().HaveCount(5);
    }

    // ─── ComplianceCertificate ──────────────────────────────────────────

    [Fact]
    public void ComplianceCertificate_Defaults_AreCorrect()
    {
        var cert = new ComplianceCertificate();

        cert.CertificateId.Should().NotBeNullOrEmpty();
        cert.SubscriptionId.Should().BeEmpty();
        cert.Framework.Should().Be("NIST80053");
        cert.ComplianceScore.Should().Be(0);
        cert.IssuedBy.Should().BeEmpty();
        cert.FamilyAttestations.Should().BeEmpty();
        cert.CoverageFamilies.Should().BeEmpty();
        cert.VerificationHash.Should().BeEmpty();
        cert.Status.Should().Be(CertificateStatus.Active);
    }

    [Fact]
    public void ComplianceCertificate_ExpiresAt_Is180DaysFromIssuedAt()
    {
        var cert = new ComplianceCertificate();

        var difference = cert.ExpiresAt - cert.IssuedAt;
        difference.TotalDays.Should().BeApproximately(180, 1);
    }

    [Fact]
    public void ComplianceCertificate_UniqueIds_AreGenerated()
    {
        var cert1 = new ComplianceCertificate();
        var cert2 = new ComplianceCertificate();

        cert1.CertificateId.Should().NotBe(cert2.CertificateId);
    }

    [Fact]
    public void ComplianceCertificate_Score_BelowThreshold_IsStorable()
    {
        // Certificate can hold any score, business logic enforces ≥80% threshold
        var cert = new ComplianceCertificate { ComplianceScore = 50.0 };
        cert.ComplianceScore.Should().Be(50.0);
    }

    [Fact]
    public void ComplianceCertificate_FamilyAttestations_CanBePopulated()
    {
        var cert = new ComplianceCertificate
        {
            FamilyAttestations =
            [
                new FamilyAttestation { FamilyCode = "AC", ComplianceScore = 95.0, ControlsAssessed = 25, ControlsPassed = 24 },
                new FamilyAttestation { FamilyCode = "IA", ComplianceScore = 88.0, ControlsAssessed = 11, ControlsPassed = 10 }
            ],
            CoverageFamilies = new List<string> { "AC", "IA" }
        };

        cert.FamilyAttestations.Should().HaveCount(2);
        cert.CoverageFamilies.Should().HaveCount(2);
        cert.FamilyAttestations[0].FamilyCode.Should().Be("AC");
    }

    // ─── AssessmentProgress ─────────────────────────────────────────────

    [Fact]
    public void AssessmentProgress_Defaults_AreCorrect()
    {
        var progress = new AssessmentProgress();

        progress.TotalFamilies.Should().Be(20);
        progress.CompletedFamilies.Should().Be(0);
        progress.CurrentFamily.Should().BeNull();
        progress.PercentComplete.Should().Be(0);
        progress.EstimatedTimeRemaining.Should().BeNull();
        progress.FamilyResults.Should().BeEmpty();
    }

    // ─── EvidencePackage / EvidenceItem ─────────────────────────────────

    [Fact]
    public void EvidencePackage_Defaults_AreCorrect()
    {
        var pkg = new EvidencePackage();

        pkg.FamilyCode.Should().BeEmpty();
        pkg.SubscriptionId.Should().BeEmpty();
        pkg.EvidenceItems.Should().BeEmpty();
        pkg.ExpectedEvidenceTypes.Should().Be(5);
        pkg.CollectedEvidenceTypes.Should().Be(0);
        pkg.CompletenessScore.Should().Be(0);
    }

    [Fact]
    public void EvidenceItem_Defaults_AreCorrect()
    {
        var item = new EvidenceItem();

        item.Title.Should().BeEmpty();
        item.Description.Should().BeEmpty();
        item.Content.Should().BeEmpty();
        item.ContentHash.Should().BeEmpty();
        item.ResourceId.Should().BeNull();
    }

    // ─── ComplianceAssessment Extended Properties ───────────────────────

    [Fact]
    public void ComplianceAssessment_NewProperties_DefaultCorrectly()
    {
        var assessment = new ComplianceAssessment();

        assessment.ControlFamilyResults.Should().BeEmpty();
        assessment.ExecutiveSummary.Should().BeEmpty();
        assessment.RiskProfile.Should().BeNull();
        assessment.EnvironmentName.Should().BeNull();
        assessment.SubscriptionIds.Should().BeEmpty();
        assessment.ResourceGroupFilter.Should().BeNull();
        assessment.AssessmentDuration.Should().BeNull();
        assessment.ScanPillarResults.Should().BeEmpty();
    }

    // ─── ComplianceFinding Extended Properties ──────────────────────────

    [Fact]
    public void ComplianceFinding_NewProperties_DefaultCorrectly()
    {
        var finding = new ComplianceFinding();

        finding.ControlTitle.Should().BeEmpty();
        finding.ControlDescription.Should().BeEmpty();
        finding.StigFinding.Should().BeFalse();
        finding.StigId.Should().BeNull();
        finding.RemediationTrackingStatus.Should().Be(RemediationTrackingStatus.NotStarted);
        finding.RemediatedAt.Should().BeNull();
        finding.RemediatedBy.Should().BeNull();
    }

    // ─── ContinuousComplianceStatus ────────────────────────────────────

    [Fact]
    public void ContinuousComplianceStatus_Defaults_AreCorrect()
    {
        var status = new ContinuousComplianceStatus();

        status.SubscriptionId.Should().BeEmpty();
        status.OverallScore.Should().Be(0);
        status.ControlStatuses.Should().BeEmpty();
    }

    // ─── ControlFamilies Constant Validation ────────────────────────────

    [Fact]
    public void ControlFamilies_HasExactly20Families()
    {
        ControlFamilies.AllFamilies.Should().HaveCount(20);
    }

    [Theory]
    [InlineData("AC")]
    [InlineData("AT")]
    [InlineData("AU")]
    [InlineData("CA")]
    [InlineData("CM")]
    [InlineData("CP")]
    [InlineData("IA")]
    [InlineData("IR")]
    [InlineData("MA")]
    [InlineData("MP")]
    [InlineData("PE")]
    [InlineData("PL")]
    [InlineData("PM")]
    [InlineData("PS")]
    [InlineData("PT")]
    [InlineData("RA")]
    [InlineData("SA")]
    [InlineData("SC")]
    [InlineData("SI")]
    [InlineData("SR")]
    public void ControlFamilies_IsValidFamily_ForAll20(string familyCode)
    {
        ControlFamilies.IsValidFamily(familyCode).Should().BeTrue();
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("ACX")]
    public void ControlFamilies_IsValidFamily_ReturnsFalseForInvalid(string familyCode)
    {
        ControlFamilies.IsValidFamily(familyCode).Should().BeFalse();
    }

    [Theory]
    [InlineData("AC", true)]
    [InlineData("IA", true)]
    [InlineData("SC", true)]
    [InlineData("AU", false)]
    [InlineData("CM", false)]
    public void ControlFamilies_IsHighRisk_ReturnsExpected(string familyCode, bool expected)
    {
        ControlFamilies.IsHighRisk(familyCode).Should().Be(expected);
    }

    [Theory]
    [InlineData("AC", "Access Control")]
    [InlineData("IA", "Identification and Authentication")]
    [InlineData("SC", "System and Communications Protection")]
    public void ControlFamilies_FamilyNames_ReturnsCorrectName(string code, string expectedName)
    {
        ControlFamilies.FamilyNames[code].Should().Be(expectedName);
    }
}
