using Xunit;
using FluentAssertions;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Models;

/// <summary>
/// Tests for RegisteredSystem entity validation, defaults, and
/// AzureEnvironmentProfile owned entity behaviour.
/// </summary>
public class RegisteredSystemTests
{
    // ─── Default Values ──────────────────────────────────────────────────

    [Fact]
    public void RegisteredSystem_Defaults_Id_IsNonEmpty()
    {
        var system = new RegisteredSystem();
        system.Id.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(system.Id, out _).Should().BeTrue("Id should be a valid GUID string");
    }

    [Fact]
    public void RegisteredSystem_Defaults_Name_IsEmpty()
    {
        var system = new RegisteredSystem();
        system.Name.Should().BeEmpty();
    }

    [Fact]
    public void RegisteredSystem_Defaults_CurrentRmfStep_IsPrepare()
    {
        var system = new RegisteredSystem();
        system.CurrentRmfStep.Should().Be(RmfPhase.Prepare);
    }

    [Fact]
    public void RegisteredSystem_Defaults_IsActive_IsTrue()
    {
        var system = new RegisteredSystem();
        system.IsActive.Should().BeTrue();
    }

    [Fact]
    public void RegisteredSystem_Defaults_CreatedAt_IsRecent()
    {
        var before = DateTime.UtcNow;
        var system = new RegisteredSystem();
        var after = DateTime.UtcNow;

        system.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void RegisteredSystem_Defaults_RmfStepUpdatedAt_IsRecent()
    {
        var before = DateTime.UtcNow;
        var system = new RegisteredSystem();
        var after = DateTime.UtcNow;

        system.RmfStepUpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void RegisteredSystem_Defaults_ModifiedAt_IsNull()
    {
        var system = new RegisteredSystem();
        system.ModifiedAt.Should().BeNull();
    }

    [Fact]
    public void RegisteredSystem_Defaults_AzureProfile_IsNull()
    {
        var system = new RegisteredSystem();
        system.AzureProfile.Should().BeNull();
    }

    [Fact]
    public void RegisteredSystem_Defaults_NavigationCollections_AreEmpty()
    {
        var system = new RegisteredSystem();
        system.AuthorizationBoundaries.Should().BeEmpty();
        system.RmfRoleAssignments.Should().BeEmpty();
    }

    [Fact]
    public void RegisteredSystem_Defaults_SecurityCategorization_IsNull()
    {
        var system = new RegisteredSystem();
        system.SecurityCategorization.Should().BeNull();
    }

    [Fact]
    public void RegisteredSystem_Defaults_ControlBaseline_IsNull()
    {
        var system = new RegisteredSystem();
        system.ControlBaseline.Should().BeNull();
    }

    // ─── Property Assignment ─────────────────────────────────────────────

    [Fact]
    public void RegisteredSystem_CanSetAllProperties()
    {
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        var system = new RegisteredSystem
        {
            Id = id,
            Name = "ACME Portal",
            Acronym = "ACME",
            SystemType = SystemType.MajorApplication,
            Description = "A test system",
            MissionCriticality = MissionCriticality.MissionCritical,
            IsNationalSecuritySystem = true,
            ClassifiedDesignation = "Secret",
            HostingEnvironment = "Azure Government",
            CurrentRmfStep = RmfPhase.Categorize,
            RmfStepUpdatedAt = now,
            CreatedBy = "user@example.com",
            CreatedAt = now,
            ModifiedAt = now,
            IsActive = false
        };

        system.Id.Should().Be(id);
        system.Name.Should().Be("ACME Portal");
        system.Acronym.Should().Be("ACME");
        system.SystemType.Should().Be(SystemType.MajorApplication);
        system.Description.Should().Be("A test system");
        system.MissionCriticality.Should().Be(MissionCriticality.MissionCritical);
        system.IsNationalSecuritySystem.Should().BeTrue();
        system.ClassifiedDesignation.Should().Be("Secret");
        system.HostingEnvironment.Should().Be("Azure Government");
        system.CurrentRmfStep.Should().Be(RmfPhase.Categorize);
        system.RmfStepUpdatedAt.Should().Be(now);
        system.CreatedBy.Should().Be("user@example.com");
        system.CreatedAt.Should().Be(now);
        system.ModifiedAt.Should().Be(now);
        system.IsActive.Should().BeFalse();
    }

    [Theory]
    [InlineData(RmfPhase.Prepare)]
    [InlineData(RmfPhase.Categorize)]
    [InlineData(RmfPhase.Select)]
    [InlineData(RmfPhase.Implement)]
    [InlineData(RmfPhase.Assess)]
    [InlineData(RmfPhase.Authorize)]
    [InlineData(RmfPhase.Monitor)]
    public void RegisteredSystem_CanSetAllRmfPhases(RmfPhase phase)
    {
        var system = new RegisteredSystem { CurrentRmfStep = phase };
        system.CurrentRmfStep.Should().Be(phase);
    }

    [Theory]
    [InlineData(SystemType.MajorApplication)]
    [InlineData(SystemType.Enclave)]
    [InlineData(SystemType.PlatformIt)]
    public void RegisteredSystem_CanSetAllSystemTypes(SystemType type)
    {
        var system = new RegisteredSystem { SystemType = type };
        system.SystemType.Should().Be(type);
    }

    [Theory]
    [InlineData(MissionCriticality.MissionCritical)]
    [InlineData(MissionCriticality.MissionEssential)]
    [InlineData(MissionCriticality.MissionSupport)]
    public void RegisteredSystem_CanSetAllMissionCriticalities(MissionCriticality criticality)
    {
        var system = new RegisteredSystem { MissionCriticality = criticality };
        system.MissionCriticality.Should().Be(criticality);
    }

    // ─── Unique IDs ──────────────────────────────────────────────────────

    [Fact]
    public void RegisteredSystem_MultipleInstances_HaveUniqueIds()
    {
        var systems = Enumerable.Range(0, 100).Select(_ => new RegisteredSystem()).ToList();
        systems.Select(s => s.Id).Distinct().Should().HaveCount(100);
    }

    // ─── AzureEnvironmentProfile ─────────────────────────────────────────

    [Fact]
    public void AzureEnvironmentProfile_Defaults_SubscriptionIds_IsEmpty()
    {
        var profile = new AzureEnvironmentProfile();
        profile.SubscriptionIds.Should().BeEmpty();
    }

    [Fact]
    public void AzureEnvironmentProfile_CanSetAllProperties()
    {
        var profile = new AzureEnvironmentProfile
        {
            CloudEnvironment = AzureCloudEnvironment.Government,
            ArmEndpoint = "https://management.usgovcloudapi.net",
            AuthenticationEndpoint = "https://login.microsoftonline.us",
            DefenderEndpoint = "https://defender.azure.us",
            PolicyEndpoint = "https://policy.azure.us",
            ProxyUrl = "https://proxy.local:8080",
            SubscriptionIds = new List<string> { "sub-1", "sub-2", "sub-3" }
        };

        profile.CloudEnvironment.Should().Be(AzureCloudEnvironment.Government);
        profile.ArmEndpoint.Should().Be("https://management.usgovcloudapi.net");
        profile.AuthenticationEndpoint.Should().Be("https://login.microsoftonline.us");
        profile.DefenderEndpoint.Should().Be("https://defender.azure.us");
        profile.PolicyEndpoint.Should().Be("https://policy.azure.us");
        profile.ProxyUrl.Should().Be("https://proxy.local:8080");
        profile.SubscriptionIds.Should().HaveCount(3).And.Contain("sub-2");
    }

    [Theory]
    [InlineData(AzureCloudEnvironment.Commercial)]
    [InlineData(AzureCloudEnvironment.Government)]
    [InlineData(AzureCloudEnvironment.GovernmentAirGappedIl5)]
    [InlineData(AzureCloudEnvironment.GovernmentAirGappedIl6)]
    public void AzureEnvironmentProfile_CanSetAllCloudEnvironments(AzureCloudEnvironment env)
    {
        var profile = new AzureEnvironmentProfile { CloudEnvironment = env };
        profile.CloudEnvironment.Should().Be(env);
    }

    [Fact]
    public void RegisteredSystem_CanAssignAzureProfile()
    {
        var system = new RegisteredSystem
        {
            Name = "Test System",
            AzureProfile = new AzureEnvironmentProfile
            {
                CloudEnvironment = AzureCloudEnvironment.GovernmentAirGappedIl5,
                ArmEndpoint = "https://management.azure.eaglex.ic.gov",
                AuthenticationEndpoint = "https://login.microsoftonline.eaglex.ic.gov",
                SubscriptionIds = new List<string> { "il5-sub-1" }
            }
        };

        system.AzureProfile.Should().NotBeNull();
        system.AzureProfile!.CloudEnvironment.Should().Be(AzureCloudEnvironment.GovernmentAirGappedIl5);
        system.AzureProfile.SubscriptionIds.Should().ContainSingle().Which.Should().Be("il5-sub-1");
    }

    // ─── AuthorizationBoundary ───────────────────────────────────────────

    [Fact]
    public void AuthorizationBoundary_Defaults_AreCorrect()
    {
        var boundary = new AuthorizationBoundary();
        boundary.Id.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(boundary.Id, out _).Should().BeTrue();
        boundary.IsInBoundary.Should().BeTrue();
        boundary.AddedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void AuthorizationBoundary_CanSetAllProperties()
    {
        var boundary = new AuthorizationBoundary
        {
            RegisteredSystemId = Guid.NewGuid().ToString(),
            ResourceId = "/subscriptions/sub-1/resourceGroups/rg-1/providers/Microsoft.Compute/virtualMachines/vm-1",
            ResourceType = "Microsoft.Compute/virtualMachines",
            ResourceName = "vm-1",
            IsInBoundary = false,
            ExclusionRationale = "Out of scope for this ATO",
            InheritanceProvider = "Azure (Microsoft)",
            AddedBy = "so@example.com"
        };

        boundary.ResourceType.Should().Be("Microsoft.Compute/virtualMachines");
        boundary.IsInBoundary.Should().BeFalse();
        boundary.ExclusionRationale.Should().NotBeNull();
        boundary.InheritanceProvider.Should().Be("Azure (Microsoft)");
    }

    // ─── RmfRoleAssignment ───────────────────────────────────────────────

    [Fact]
    public void RmfRoleAssignment_Defaults_AreCorrect()
    {
        var assignment = new RmfRoleAssignment();
        assignment.Id.Should().NotBeNullOrWhiteSpace();
        assignment.IsActive.Should().BeTrue();
        assignment.AssignedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(RmfRole.AuthorizingOfficial)]
    [InlineData(RmfRole.Issm)]
    [InlineData(RmfRole.Isso)]
    [InlineData(RmfRole.Sca)]
    [InlineData(RmfRole.SystemOwner)]
    public void RmfRoleAssignment_CanSetAllRoles(RmfRole role)
    {
        var assignment = new RmfRoleAssignment
        {
            RegisteredSystemId = Guid.NewGuid().ToString(),
            RmfRole = role,
            UserId = "user@example.com",
            UserDisplayName = "Test User",
            AssignedBy = "admin@example.com"
        };

        assignment.RmfRole.Should().Be(role);
        assignment.UserId.Should().Be("user@example.com");
    }
}
