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
/// Unit tests for SSP §10 System Interconnections section generation.
/// Feature 021 Task: T046.
/// </summary>
public class SspInterconnectionTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly AtoCopilotContext _db;
    private readonly SspService _service;

    public SspInterconnectionTests()
    {
        var dbName = $"SspIntConn_{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddDbContext<AtoCopilotContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
        _db = _serviceProvider.GetRequiredService<AtoCopilotContext>();

        _service = new SspService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Mock.Of<ILogger<SspService>>());
    }

    private async Task<RegisteredSystem> SeedSystemAsync(string? name = null)
    {
        var system = new RegisteredSystem
        {
            Name = name ?? "SSP Test System",
            SystemType = SystemType.MajorApplication,
            MissionCriticality = MissionCriticality.MissionEssential,
            HostingEnvironment = "Azure Government",
            CreatedBy = "test-user"
        };
        _db.RegisteredSystems.Add(system);
        await _db.SaveChangesAsync();
        return system;
    }

    [Fact]
    public async Task GenerateSsp_ActiveInterconnections_ProducesTable()
    {
        var system = await SeedSystemAsync();
        var ic = new SystemInterconnection
        {
            RegisteredSystemId = system.Id,
            TargetSystemName = "DISA SIPR Gateway",
            InterconnectionType = InterconnectionType.Vpn,
            DataFlowDirection = DataFlowDirection.Bidirectional,
            DataClassification = "Secret",
            Status = InterconnectionStatus.Active,
            SecurityMeasures = ["AES-256", "TLS 1.3"],
            CreatedBy = "test-user"
        };
        ic.Agreements.Add(new InterconnectionAgreement
        {
            SystemInterconnectionId = ic.Id,
            Title = "DISA ISA",
            AgreementType = AgreementType.Isa,
            Status = AgreementStatus.Signed,
            CreatedBy = "test-user"
        });
        _db.SystemInterconnections.Add(ic);
        await _db.SaveChangesAsync();

        var doc = await _service.GenerateSspAsync(system.Id, sections: new[] { "interconnections" });

        doc.Content.Should().Contain("System Interconnections");
        doc.Content.Should().Contain("DISA SIPR Gateway");
        doc.Content.Should().Contain("Vpn");
        doc.Content.Should().Contain("Bidirectional");
        doc.Content.Should().Contain("Secret");
        doc.Content.Should().Contain("Signed");
        doc.Sections.Should().Contain("interconnections");
    }

    [Fact]
    public async Task GenerateSsp_TerminatedInterconnections_Excluded()
    {
        var system = await SeedSystemAsync();
        _db.SystemInterconnections.Add(new SystemInterconnection
        {
            RegisteredSystemId = system.Id,
            TargetSystemName = "Decommissioned System",
            InterconnectionType = InterconnectionType.Api,
            DataFlowDirection = DataFlowDirection.Outbound,
            DataClassification = "Unclassified",
            Status = InterconnectionStatus.Terminated,
            CreatedBy = "test-user"
        });
        _db.SystemInterconnections.Add(new SystemInterconnection
        {
            RegisteredSystemId = system.Id,
            TargetSystemName = "Active System",
            InterconnectionType = InterconnectionType.Direct,
            DataFlowDirection = DataFlowDirection.Inbound,
            DataClassification = "CUI",
            Status = InterconnectionStatus.Active,
            CreatedBy = "test-user"
        });
        await _db.SaveChangesAsync();

        var doc = await _service.GenerateSspAsync(system.Id, sections: new[] { "interconnections" });

        doc.Content.Should().NotContain("Decommissioned System");
        doc.Content.Should().Contain("Active System");
    }

    [Fact]
    public async Task GenerateSsp_NoInterconnections_ProducesNote()
    {
        var system = await SeedSystemAsync();

        var doc = await _service.GenerateSspAsync(system.Id, sections: new[] { "interconnections" });

        doc.Content.Should().Contain("no interconnections");
    }
}
