using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Models.Compliance;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Ato.Copilot.Tests.Unit.Integration;

/// <summary>
/// Unit tests for ConMon ISA and PIA expiration monitoring.
/// Feature 021 Task: T047.
/// </summary>
public class ConMonIsaTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AtoCopilotContext _db;
    private readonly ConMonService _service;

    public ConMonIsaTests()
    {
        var dbName = $"ConMonIsa_{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddDbContext<AtoCopilotContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
        _db = _serviceProvider.GetRequiredService<AtoCopilotContext>();

        _service = new ConMonService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Mock.Of<ILogger<ConMonService>>());
    }

    private async Task<RegisteredSystem> SeedSystemAsync(string? name = null)
    {
        var system = new RegisteredSystem
        {
            Name = name ?? "ConMon ISA Test System",
            SystemType = SystemType.MajorApplication,
            MissionCriticality = MissionCriticality.MissionEssential,
            HostingEnvironment = "Azure Government",
            CreatedBy = "test-user"
        };
        _db.RegisteredSystems.Add(system);
        await _db.SaveChangesAsync();
        return system;
    }

    // ─── ISA Expiration Monitoring ──────────────────────────────────────

    [Fact]
    public async Task CheckAgreementExpirations_ExpiringSoon_ReturnsAdvisory()
    {
        var system = await SeedSystemAsync();
        var ic = new SystemInterconnection
        {
            RegisteredSystemId = system.Id,
            TargetSystemName = "Partner System",
            InterconnectionType = InterconnectionType.Api,
            DataFlowDirection = DataFlowDirection.Bidirectional,
            DataClassification = "CUI",
            Status = InterconnectionStatus.Active,
            CreatedBy = "test-user"
        };
        ic.Agreements.Add(new InterconnectionAgreement
        {
            SystemInterconnectionId = ic.Id,
            Title = "Partner ISA",
            AgreementType = AgreementType.Isa,
            Status = AgreementStatus.Signed,
            ExpirationDate = DateTime.UtcNow.AddDays(60), // within 90 days
            CreatedBy = "test-user"
        });
        _db.SystemInterconnections.Add(ic);
        await _db.SaveChangesAsync();

        var results = await _service.CheckAgreementExpirationsAsync(system.Id);

        results.Should().HaveCount(1);
        results[0].AlertLevel.Should().Be("Warning");
        results[0].AgreementTitle.Should().Be("Partner ISA");
    }

    [Fact]
    public async Task CheckAgreementExpirations_ExpiredIsa_CreatesSignificantChange()
    {
        var system = await SeedSystemAsync();
        var ic = new SystemInterconnection
        {
            RegisteredSystemId = system.Id,
            TargetSystemName = "Legacy Gateway",
            InterconnectionType = InterconnectionType.Vpn,
            DataFlowDirection = DataFlowDirection.Inbound,
            DataClassification = "Secret",
            Status = InterconnectionStatus.Active,
            CreatedBy = "test-user"
        };
        ic.Agreements.Add(new InterconnectionAgreement
        {
            SystemInterconnectionId = ic.Id,
            Title = "Legacy ISA",
            AgreementType = AgreementType.Isa,
            Status = AgreementStatus.Signed,
            ExpirationDate = DateTime.UtcNow.AddDays(-10), // expired
            CreatedBy = "test-user"
        });
        _db.SystemInterconnections.Add(ic);
        await _db.SaveChangesAsync();

        var results = await _service.CheckAgreementExpirationsAsync(system.Id);

        results.Should().HaveCount(1);
        results[0].AlertLevel.Should().Be("Expired");

        // Verify a SignificantChange record was created
        var changes = await _db.SignificantChanges
            .Where(c => c.RegisteredSystemId == system.Id)
            .ToListAsync();
        changes.Should().ContainSingle();
        changes[0].ChangeType.Should().Be("ISA Expiration");
    }

    [Fact]
    public async Task CheckAgreementExpirations_MultipleAgreements_OnlySomeExpired()
    {
        var system = await SeedSystemAsync();

        // Active interconnection with valid agreement
        var ic1 = new SystemInterconnection
        {
            RegisteredSystemId = system.Id,
            TargetSystemName = "Valid Partner",
            InterconnectionType = InterconnectionType.Api,
            DataFlowDirection = DataFlowDirection.Outbound,
            DataClassification = "Unclassified",
            Status = InterconnectionStatus.Active,
            CreatedBy = "test-user"
        };
        ic1.Agreements.Add(new InterconnectionAgreement
        {
            SystemInterconnectionId = ic1.Id,
            Title = "Valid ISA",
            AgreementType = AgreementType.Isa,
            Status = AgreementStatus.Signed,
            ExpirationDate = DateTime.UtcNow.AddYears(1),
            CreatedBy = "test-user"
        });

        // Active interconnection with expired agreement
        var ic2 = new SystemInterconnection
        {
            RegisteredSystemId = system.Id,
            TargetSystemName = "Expired Partner",
            InterconnectionType = InterconnectionType.Direct,
            DataFlowDirection = DataFlowDirection.Bidirectional,
            DataClassification = "CUI",
            Status = InterconnectionStatus.Active,
            CreatedBy = "test-user"
        };
        ic2.Agreements.Add(new InterconnectionAgreement
        {
            SystemInterconnectionId = ic2.Id,
            Title = "Expired ISA",
            AgreementType = AgreementType.Isa,
            Status = AgreementStatus.Signed,
            ExpirationDate = DateTime.UtcNow.AddDays(-5),
            CreatedBy = "test-user"
        });

        _db.SystemInterconnections.AddRange(ic1, ic2);
        await _db.SaveChangesAsync();

        var results = await _service.CheckAgreementExpirationsAsync(system.Id);

        // Only the expired one should appear
        results.Should().HaveCount(1);
        results[0].AgreementTitle.Should().Be("Expired ISA");
        results[0].AlertLevel.Should().Be("Expired");
    }

    // ─── PIA Expiration Monitoring ──────────────────────────────────────

    [Fact]
    public async Task CheckAgreementExpirations_ExpiredPia_SetsExpiredStatus()
    {
        var system = await SeedSystemAsync();
        var pia = new PrivacyImpactAssessment
        {
            RegisteredSystemId = system.Id,
            PtaId = Guid.NewGuid().ToString(),
            Status = PiaStatus.Approved,
            Version = 1,
            ExpirationDate = DateTime.UtcNow.AddDays(-15), // expired
            CreatedBy = "test-user",
            CreatedAt = DateTime.UtcNow.AddYears(-2)
        };
        _db.PrivacyImpactAssessments.Add(pia);
        await _db.SaveChangesAsync();

        var results = await _service.CheckAgreementExpirationsAsync(system.Id);

        // Verify expired PIA alert is returned
        var piaAlert = results.Should().ContainSingle(r => r.ItemType == "PIA").Subject;
        piaAlert.AlertLevel.Should().Be("Expired");
        piaAlert.AgreementTitle.Should().Be("Privacy Impact Assessment");
        piaAlert.DaysUntilExpiration.Should().BeNegative();

        // Verify SignificantChange was created for PIA expiration
        _db.ChangeTracker.Clear();
        var changes = await _db.SignificantChanges
            .Where(c => c.RegisteredSystemId == system.Id && c.ChangeType == "PIA Expiration")
            .ToListAsync();
        changes.Should().ContainSingle();
    }
}
