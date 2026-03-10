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
/// Unit tests for InterconnectionService — CRUD operations, status lifecycle, and validation.
/// Feature 021 Tasks: T026.
/// </summary>
public class InterconnectionServiceTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AtoCopilotContext _db;
    private readonly InterconnectionService _service;

    public InterconnectionServiceTests()
    {
        var dbName = $"InterconnectionService_{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddDbContext<AtoCopilotContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
        _db = _serviceProvider.GetRequiredService<AtoCopilotContext>();

        _service = new InterconnectionService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Mock.Of<ILogger<InterconnectionService>>());
    }

    private async Task<RegisteredSystem> SeedSystemAsync(
        string? name = null,
        bool hasNoExternalInterconnections = false)
    {
        var system = new RegisteredSystem
        {
            Name = name ?? "Test System",
            SystemType = SystemType.MajorApplication,
            MissionCriticality = MissionCriticality.MissionEssential,
            HostingEnvironment = "Azure Government",
            CreatedBy = "test-user",
            HasNoExternalInterconnections = hasNoExternalInterconnections
        };
        _db.RegisteredSystems.Add(system);
        await _db.SaveChangesAsync();
        return system;
    }

    // ─── AddInterconnectionAsync ─────────────────────────────────────────────

    [Fact]
    public async Task AddInterconnection_WithAllFields_CreatesInterconnection()
    {
        var system = await SeedSystemAsync();

        var result = await _service.AddInterconnectionAsync(
            system.Id,
            targetSystemName: "DISA SIPR Gateway",
            interconnectionType: InterconnectionType.Vpn,
            dataFlowDirection: DataFlowDirection.Bidirectional,
            dataClassification: "Secret",
            createdBy: "test-user",
            targetSystemOwner: "DISA",
            targetSystemAcronym: "SIPR",
            dataDescription: "Classified data exchange",
            protocolsUsed: ["TLS 1.3", "IPSec"],
            portsUsed: ["443", "500"],
            securityMeasures: ["AES-256", "Mutual TLS"],
            authenticationMethod: "PKI certificates");

        result.TargetSystemName.Should().Be("DISA SIPR Gateway");
        result.Status.Should().Be(InterconnectionStatus.Proposed);
        result.HasAgreement.Should().BeFalse();
        result.InterconnectionId.Should().NotBeNullOrEmpty();

        // Verify persisted entity
        _db.ChangeTracker.Clear();
        var entity = await _db.SystemInterconnections.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == result.InterconnectionId);
        entity.Should().NotBeNull();
        entity!.TargetSystemOwner.Should().Be("DISA");
        entity.TargetSystemAcronym.Should().Be("SIPR");
        entity.DataDescription.Should().Be("Classified data exchange");
        entity.ProtocolsUsed.Should().Contain("TLS 1.3");
        entity.PortsUsed.Should().Contain("443");
        entity.SecurityMeasures.Should().Contain("AES-256");
        entity.AuthenticationMethod.Should().Be("PKI certificates");
    }

    [Fact]
    public async Task AddInterconnection_ClearsHasNoExternalInterconnections()
    {
        var system = await SeedSystemAsync(hasNoExternalInterconnections: true);
        system.HasNoExternalInterconnections.Should().BeTrue();

        await _service.AddInterconnectionAsync(
            system.Id,
            targetSystemName: "External System",
            interconnectionType: InterconnectionType.Api,
            dataFlowDirection: DataFlowDirection.Outbound,
            dataClassification: "CUI",
            createdBy: "test-user");

        _db.ChangeTracker.Clear();
        var updated = await _db.RegisteredSystems.AsNoTracking()
            .FirstAsync(s => s.Id == system.Id);
        updated.HasNoExternalInterconnections.Should().BeFalse();
    }

    [Fact]
    public async Task AddInterconnection_SystemNotFound_ThrowsInvalidOperation()
    {
        var act = () => _service.AddInterconnectionAsync(
            "nonexistent-id",
            targetSystemName: "Ext",
            interconnectionType: InterconnectionType.Direct,
            dataFlowDirection: DataFlowDirection.Inbound,
            dataClassification: "Unclassified",
            createdBy: "test-user");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // ─── ListInterconnectionsAsync ──────────────────────────────────────────

    [Fact]
    public async Task ListInterconnections_WithStatusFilter_FiltersResults()
    {
        var system = await SeedSystemAsync();

        // Add two interconnections — one Proposed, one Active
        var r1 = await _service.AddInterconnectionAsync(
            system.Id, "System A", InterconnectionType.Api,
            DataFlowDirection.Inbound, "CUI", "test-user");
        var r2 = await _service.AddInterconnectionAsync(
            system.Id, "System B", InterconnectionType.Vpn,
            DataFlowDirection.Bidirectional, "Secret", "test-user");

        // Update r2 to Active
        await _service.UpdateInterconnectionAsync(
            r2.InterconnectionId, status: InterconnectionStatus.Active);

        var proposed = await _service.ListInterconnectionsAsync(
            system.Id, statusFilter: InterconnectionStatus.Proposed);
        proposed.Should().HaveCount(1);
        proposed[0].TargetSystemName.Should().Be("System A");

        var active = await _service.ListInterconnectionsAsync(
            system.Id, statusFilter: InterconnectionStatus.Active);
        active.Should().HaveCount(1);
        active[0].TargetSystemName.Should().Be("System B");
    }

    [Fact]
    public async Task ListInterconnections_Empty_ReturnsEmptyList()
    {
        var system = await SeedSystemAsync();

        var list = await _service.ListInterconnectionsAsync(system.Id);
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task ListInterconnections_SystemNotFound_ThrowsInvalidOperation()
    {
        var act = () => _service.ListInterconnectionsAsync("nonexistent-id");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // ─── UpdateInterconnectionAsync ─────────────────────────────────────────

    [Fact]
    public async Task UpdateInterconnection_UpdateDetails_PersistsChanges()
    {
        var system = await SeedSystemAsync();
        var r = await _service.AddInterconnectionAsync(
            system.Id, "System A", InterconnectionType.Api,
            DataFlowDirection.Inbound, "CUI", "test-user");

        var updated = await _service.UpdateInterconnectionAsync(
            r.InterconnectionId,
            interconnectionType: InterconnectionType.Vpn,
            dataClassification: "Secret");

        updated.Status.Should().Be(InterconnectionStatus.Proposed);

        _db.ChangeTracker.Clear();
        var entity = await _db.SystemInterconnections.AsNoTracking()
            .FirstAsync(i => i.Id == r.InterconnectionId);
        entity.InterconnectionType.Should().Be(InterconnectionType.Vpn);
        entity.DataClassification.Should().Be("Secret");
    }

    [Fact]
    public async Task UpdateInterconnection_SuspendWithReason_UpdatesStatus()
    {
        var system = await SeedSystemAsync();
        var r = await _service.AddInterconnectionAsync(
            system.Id, "System A", InterconnectionType.Api,
            DataFlowDirection.Inbound, "CUI", "test-user");

        var updated = await _service.UpdateInterconnectionAsync(
            r.InterconnectionId,
            status: InterconnectionStatus.Suspended,
            statusReason: "Security review pending");

        updated.Status.Should().Be(InterconnectionStatus.Suspended);

        _db.ChangeTracker.Clear();
        var entity = await _db.SystemInterconnections.AsNoTracking()
            .FirstAsync(i => i.Id == r.InterconnectionId);
        entity.StatusReason.Should().Be("Security review pending");
    }

    [Fact]
    public async Task UpdateInterconnection_TerminateWithReason_UpdatesStatus()
    {
        var system = await SeedSystemAsync();
        var r = await _service.AddInterconnectionAsync(
            system.Id, "System A", InterconnectionType.Api,
            DataFlowDirection.Inbound, "CUI", "test-user");

        var updated = await _service.UpdateInterconnectionAsync(
            r.InterconnectionId,
            status: InterconnectionStatus.Terminated,
            statusReason: "Decommissioned");

        updated.Status.Should().Be(InterconnectionStatus.Terminated);
    }

    [Fact]
    public async Task UpdateInterconnection_TerminateWithoutReason_ThrowsInvalidOperation()
    {
        var system = await SeedSystemAsync();
        var r = await _service.AddInterconnectionAsync(
            system.Id, "System A", InterconnectionType.Api,
            DataFlowDirection.Inbound, "CUI", "test-user");

        var act = () => _service.UpdateInterconnectionAsync(
            r.InterconnectionId,
            status: InterconnectionStatus.Terminated);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*reason*required*");
    }

    [Fact]
    public async Task UpdateInterconnection_SuspendWithoutReason_ThrowsInvalidOperation()
    {
        var system = await SeedSystemAsync();
        var r = await _service.AddInterconnectionAsync(
            system.Id, "System A", InterconnectionType.Api,
            DataFlowDirection.Inbound, "CUI", "test-user");

        var act = () => _service.UpdateInterconnectionAsync(
            r.InterconnectionId,
            status: InterconnectionStatus.Suspended);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*reason*required*");
    }

    [Fact]
    public async Task UpdateInterconnection_NotFound_ThrowsInvalidOperation()
    {
        var act = () => _service.UpdateInterconnectionAsync("nonexistent-id");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // ─── GenerateIsaAsync (T035) ────────────────────────────────────────────

    [Fact]
    public async Task GenerateIsa_ProducesSevenSectionTemplate()
    {
        var system = await SeedSystemAsync("ACME Portal");
        var r = await _service.AddInterconnectionAsync(
            system.Id, "DISA SIPR Gateway", InterconnectionType.Vpn,
            DataFlowDirection.Bidirectional, "Secret", "test-user",
            targetSystemOwner: "DISA",
            protocolsUsed: ["TLS 1.3", "IPSec"],
            portsUsed: ["443", "500"],
            securityMeasures: ["AES-256", "Mutual TLS"]);

        var result = await _service.GenerateIsaAsync(r.InterconnectionId, "test-user");

        result.AgreementId.Should().NotBeNullOrEmpty();
        result.Title.Should().Contain("ACME Portal");
        result.Title.Should().Contain("DISA SIPR Gateway");
        result.AgreementType.Should().Be(AgreementType.Isa);
        result.NarrativeDocument.Should().Contain("Introduction");
        result.NarrativeDocument.Should().Contain("System Description");
        result.NarrativeDocument.Should().Contain("Interconnection Details");
        result.NarrativeDocument.Should().Contain("Security Controls");
        result.NarrativeDocument.Should().Contain("Roles and Responsibilities");
        result.NarrativeDocument.Should().Contain("Agreement Terms");
        result.NarrativeDocument.Should().Contain("Signatures");
    }

    [Fact]
    public async Task GenerateIsa_PrePopulatesFromInterconnectionData()
    {
        var system = await SeedSystemAsync("ACME Portal");
        var r = await _service.AddInterconnectionAsync(
            system.Id, "DISA SIPR Gateway", InterconnectionType.Vpn,
            DataFlowDirection.Bidirectional, "Secret", "test-user",
            protocolsUsed: ["TLS 1.3"],
            securityMeasures: ["AES-256"]);

        var result = await _service.GenerateIsaAsync(r.InterconnectionId, "test-user");

        result.NarrativeDocument.Should().Contain("Vpn");
        result.NarrativeDocument.Should().Contain("Bidirectional");
        result.NarrativeDocument.Should().Contain("Secret");
        result.NarrativeDocument.Should().Contain("TLS 1.3");
        result.NarrativeDocument.Should().Contain("AES-256");
    }

    [Fact]
    public async Task GenerateIsa_TerminatedInterconnection_Throws()
    {
        var system = await SeedSystemAsync();
        var r = await _service.AddInterconnectionAsync(
            system.Id, "System A", InterconnectionType.Api,
            DataFlowDirection.Inbound, "CUI", "test-user");
        await _service.UpdateInterconnectionAsync(
            r.InterconnectionId,
            status: InterconnectionStatus.Terminated,
            statusReason: "Decommissioned");

        var act = () => _service.GenerateIsaAsync(r.InterconnectionId, "test-user");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*terminated*");
    }

    [Fact]
    public async Task GenerateIsa_InterconnectionNotFound_Throws()
    {
        var act = () => _service.GenerateIsaAsync("nonexistent-id", "test-user");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // ─── RegisterAgreementAsync (T035 continued) ────────────────────────────

    [Fact]
    public async Task RegisterAgreement_CreatesAgreement()
    {
        var system = await SeedSystemAsync();
        var r = await _service.AddInterconnectionAsync(
            system.Id, "System A", InterconnectionType.Api,
            DataFlowDirection.Inbound, "CUI", "test-user");

        var agreement = await _service.RegisterAgreementAsync(
            r.InterconnectionId,
            AgreementType.Isa,
            "ISA — Test ↔ System A",
            "test-user",
            status: AgreementStatus.Signed,
            effectiveDate: DateTime.UtcNow.AddDays(-30),
            expirationDate: DateTime.UtcNow.AddYears(1),
            signedByLocal: "John Doe",
            signedByRemote: "Jane Smith");

        agreement.Id.Should().NotBeNullOrEmpty();
        agreement.Title.Should().Be("ISA — Test ↔ System A");
        agreement.Status.Should().Be(AgreementStatus.Signed);
        agreement.SignedByLocal.Should().Be("John Doe");
        agreement.SignedByRemote.Should().Be("Jane Smith");
    }

    [Fact]
    public async Task RegisterAgreement_TerminatedInterconnection_Throws()
    {
        var system = await SeedSystemAsync();
        var r = await _service.AddInterconnectionAsync(
            system.Id, "System A", InterconnectionType.Api,
            DataFlowDirection.Inbound, "CUI", "test-user");
        await _service.UpdateInterconnectionAsync(
            r.InterconnectionId,
            status: InterconnectionStatus.Terminated,
            statusReason: "Done");

        var act = () => _service.RegisterAgreementAsync(
            r.InterconnectionId, AgreementType.Isa, "Title", "test-user");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*terminated*");
    }

    // ─── UpdateAgreementAsync ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateAgreement_UpdatesStatus()
    {
        var system = await SeedSystemAsync();
        var r = await _service.AddInterconnectionAsync(
            system.Id, "System A", InterconnectionType.Api,
            DataFlowDirection.Inbound, "CUI", "test-user");
        var agreement = await _service.RegisterAgreementAsync(
            r.InterconnectionId, AgreementType.Isa, "ISA Title", "test-user");

        var updated = await _service.UpdateAgreementAsync(
            agreement.Id, status: AgreementStatus.Signed,
            signedByLocal: "John Doe",
            signedByLocalDate: DateTime.UtcNow);

        updated.Status.Should().Be(AgreementStatus.Signed);
        updated.SignedByLocal.Should().Be("John Doe");
    }

    [Fact]
    public async Task UpdateAgreement_TerminatedAgreement_ThrowsExceptReviewNotes()
    {
        var system = await SeedSystemAsync();
        var r = await _service.AddInterconnectionAsync(
            system.Id, "System A", InterconnectionType.Api,
            DataFlowDirection.Inbound, "CUI", "test-user");
        var agreement = await _service.RegisterAgreementAsync(
            r.InterconnectionId, AgreementType.Isa, "ISA Title", "test-user",
            status: AgreementStatus.Terminated);

        // Status update to terminated agreement should throw
        var act = () => _service.UpdateAgreementAsync(
            agreement.Id, status: AgreementStatus.Signed);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*terminated*");

        // But review_notes update should succeed
        var updated = await _service.UpdateAgreementAsync(
            agreement.Id, reviewNotes: "Post-termination review");
        updated.ReviewNotes.Should().Be("Post-termination review");
    }

    // ─── CertifyNoInterconnectionsAsync ─────────────────────────────────────

    [Fact]
    public async Task CertifyNoInterconnections_SetsFlag()
    {
        var system = await SeedSystemAsync();

        await _service.CertifyNoInterconnectionsAsync(system.Id, true);

        _db.ChangeTracker.Clear();
        var updated = await _db.RegisteredSystems.AsNoTracking()
            .FirstAsync(s => s.Id == system.Id);
        updated.HasNoExternalInterconnections.Should().BeTrue();
    }

    [Fact]
    public async Task CertifyNoInterconnections_WithActiveInterconnections_Throws()
    {
        var system = await SeedSystemAsync();
        var r = await _service.AddInterconnectionAsync(
            system.Id, "System A", InterconnectionType.Api,
            DataFlowDirection.Inbound, "CUI", "test-user");
        await _service.UpdateInterconnectionAsync(
            r.InterconnectionId,
            status: InterconnectionStatus.Active);

        var act = () => _service.CertifyNoInterconnectionsAsync(system.Id, true);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*active interconnection*");
    }

    [Fact]
    public async Task CertifyNoInterconnections_ClearFlag()
    {
        var system = await SeedSystemAsync(hasNoExternalInterconnections: true);

        await _service.CertifyNoInterconnectionsAsync(system.Id, false);

        _db.ChangeTracker.Clear();
        var updated = await _db.RegisteredSystems.AsNoTracking()
            .FirstAsync(s => s.Id == system.Id);
        updated.HasNoExternalInterconnections.Should().BeFalse();
    }

    // ─── ValidateAgreementsAsync (T036) ─────────────────────────────────────

    [Fact]
    public async Task ValidateAgreements_AllSigned_ReturnsCompliant()
    {
        var system = await SeedSystemAsync();
        var r = await _service.AddInterconnectionAsync(
            system.Id, "System A", InterconnectionType.Api,
            DataFlowDirection.Inbound, "CUI", "test-user");
        await _service.UpdateInterconnectionAsync(
            r.InterconnectionId, status: InterconnectionStatus.Active);
        await _service.RegisterAgreementAsync(
            r.InterconnectionId, AgreementType.Isa, "ISA A", "test-user",
            status: AgreementStatus.Signed,
            expirationDate: DateTime.UtcNow.AddYears(1));

        var result = await _service.ValidateAgreementsAsync(system.Id);
        result.IsFullyCompliant.Should().BeTrue();
        result.TotalInterconnections.Should().Be(1);
        result.CompliantCount.Should().Be(1);
    }

    [Fact]
    public async Task ValidateAgreements_OneExpired_ReturnsFail()
    {
        var system = await SeedSystemAsync();
        var r = await _service.AddInterconnectionAsync(
            system.Id, "System A", InterconnectionType.Api,
            DataFlowDirection.Inbound, "CUI", "test-user");
        await _service.UpdateInterconnectionAsync(
            r.InterconnectionId, status: InterconnectionStatus.Active);
        await _service.RegisterAgreementAsync(
            r.InterconnectionId, AgreementType.Isa, "ISA A", "test-user",
            status: AgreementStatus.Signed,
            expirationDate: DateTime.UtcNow.AddDays(-10));

        var result = await _service.ValidateAgreementsAsync(system.Id);
        result.IsFullyCompliant.Should().BeFalse();
        result.ExpiredAgreementCount.Should().Be(1);
    }

    [Fact]
    public async Task ValidateAgreements_MissingAgreement_ReturnsFail()
    {
        var system = await SeedSystemAsync();
        var r = await _service.AddInterconnectionAsync(
            system.Id, "System A", InterconnectionType.Api,
            DataFlowDirection.Inbound, "CUI", "test-user");
        await _service.UpdateInterconnectionAsync(
            r.InterconnectionId, status: InterconnectionStatus.Active);

        var result = await _service.ValidateAgreementsAsync(system.Id);
        result.IsFullyCompliant.Should().BeFalse();
        result.MissingAgreementCount.Should().Be(1);
    }

    [Fact]
    public async Task ValidateAgreements_ExpiringWithin90Days_FlagsWarning()
    {
        var system = await SeedSystemAsync();
        var r = await _service.AddInterconnectionAsync(
            system.Id, "System A", InterconnectionType.Api,
            DataFlowDirection.Inbound, "CUI", "test-user");
        await _service.UpdateInterconnectionAsync(
            r.InterconnectionId, status: InterconnectionStatus.Active);
        await _service.RegisterAgreementAsync(
            r.InterconnectionId, AgreementType.Isa, "ISA A", "test-user",
            status: AgreementStatus.Signed,
            expirationDate: DateTime.UtcNow.AddDays(45));

        var result = await _service.ValidateAgreementsAsync(system.Id);
        result.IsFullyCompliant.Should().BeTrue();
        result.ExpiringWithin90DaysCount.Should().Be(1);
        result.Items[0].ValidationStatus.Should().Be("ExpiringSoon");
    }

    [Fact]
    public async Task ValidateAgreements_HasNoExternalInterconnections_ReturnsPass()
    {
        var system = await SeedSystemAsync(hasNoExternalInterconnections: true);

        var result = await _service.ValidateAgreementsAsync(system.Id);
        result.IsFullyCompliant.Should().BeTrue();
        result.TotalInterconnections.Should().Be(0);
    }

    [Fact]
    public async Task ValidateAgreements_MultipleAgreementsWithOneValid_Passes()
    {
        var system = await SeedSystemAsync();
        var r = await _service.AddInterconnectionAsync(
            system.Id, "System A", InterconnectionType.Api,
            DataFlowDirection.Inbound, "CUI", "test-user");
        await _service.UpdateInterconnectionAsync(
            r.InterconnectionId, status: InterconnectionStatus.Active);

        // One expired + one valid
        await _service.RegisterAgreementAsync(
            r.InterconnectionId, AgreementType.Isa, "ISA Old", "test-user",
            status: AgreementStatus.Signed,
            expirationDate: DateTime.UtcNow.AddDays(-10));
        await _service.RegisterAgreementAsync(
            r.InterconnectionId, AgreementType.Isa, "ISA New", "test-user",
            status: AgreementStatus.Signed,
            expirationDate: DateTime.UtcNow.AddYears(1));

        var result = await _service.ValidateAgreementsAsync(system.Id);
        result.IsFullyCompliant.Should().BeTrue();
        result.CompliantCount.Should().Be(1);
    }
}
