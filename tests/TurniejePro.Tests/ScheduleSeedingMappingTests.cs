using TurniejePro.Api.Mapping;
using TurniejePro.Contracts.Matches;
using TurniejePro.Domain.Scheduling;
using DomainSeedingMode = TurniejePro.Domain.Enums.SeedingMode;

namespace TurniejePro.Tests;

/// <summary>
/// Przekład opcjonalnego body <see cref="GenerateScheduleRequest"/> na rozstawienie z domeny (KAN-302).
/// </summary>
public class ScheduleSeedingMappingTests
{
    [Fact]
    public void ToSeeding_NoRequest_UsesRegistrationOrder()
    {
        var seeding = ((GenerateScheduleRequest?)null).ToSeeding();

        Assert.Equal(DomainSeedingMode.Registration, seeding.Mode);
        Assert.Null(seeding.TeamOrder);
    }

    [Fact]
    public void ToSeeding_RequestWithoutSeeding_UsesRegistrationOrder()
    {
        var seeding = new GenerateScheduleRequest(Seeding: null, TeamOrder: [Guid.NewGuid()]).ToSeeding();

        Assert.Equal(DomainSeedingMode.Registration, seeding.Mode);
    }

    [Fact]
    public void ToSeeding_Registration_IgnoresTeamOrder()
    {
        var seeding = new GenerateScheduleRequest(SeedingMode.Registration, [Guid.NewGuid()]).ToSeeding();

        Assert.Equal(DomainSeedingMode.Registration, seeding.Mode);
        Assert.Null(seeding.TeamOrder);
    }

    [Fact]
    public void ToSeeding_Random_MapsToRandomWithoutTeamOrder()
    {
        var seeding = new GenerateScheduleRequest(SeedingMode.Random, [Guid.NewGuid()]).ToSeeding();

        Assert.Equal(DomainSeedingMode.Random, seeding.Mode);
        Assert.Null(seeding.TeamOrder);
    }

    [Fact]
    public void ToSeeding_Manual_PassesTheTeamOrder()
    {
        var order = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        var seeding = new GenerateScheduleRequest(SeedingMode.Manual, order).ToSeeding();

        Assert.Equal(DomainSeedingMode.Manual, seeding.Mode);
        Assert.Equal(order, seeding.TeamOrder);
    }

    [Fact]
    public void ToSeeding_ManualWithoutOrder_KeepsNullSoTheDomainCanRejectIt()
    {
        var seeding = new GenerateScheduleRequest(SeedingMode.Manual, TeamOrder: null).ToSeeding();

        Assert.Equal(DomainSeedingMode.Manual, seeding.Mode);
        Assert.Null(seeding.TeamOrder);
    }

    [Fact]
    public void ToSeeding_UnknownSeedingValue_ThrowsArgumentOutOfRangeException()
    {
        var request = new GenerateScheduleRequest((SeedingMode)99, TeamOrder: null);

        Assert.Throws<ArgumentOutOfRangeException>(() => request.ToSeeding());
    }

    [Fact]
    public void ScheduleSeedingRegistration_IsTheSharedDefault()
    {
        Assert.Equal(DomainSeedingMode.Registration, ScheduleSeeding.Registration.Mode);
    }
}
