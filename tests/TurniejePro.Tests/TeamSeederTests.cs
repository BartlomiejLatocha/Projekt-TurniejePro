using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Exceptions;
using TurniejePro.Domain.Scheduling;

namespace TurniejePro.Tests;

public class TeamSeederTests
{
    private static readonly DateTimeOffset FirstRegistration = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Seed_NullTeams_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TeamSeeder.Seed(null!, SeedingMode.Registration));
    }

    [Fact]
    public void Seed_UnknownMode_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TeamSeeder.Seed(CreateTeams(2), (SeedingMode)99));
    }

    // --- Stała kolejność (czas zgłoszenia) ---

    [Fact]
    public void Seed_Registration_OrdersTeamsByRegistrationTime()
    {
        var teams = CreateTeams(4);

        var seeded = TeamSeeder.Seed([teams[2], teams[0], teams[3], teams[1]], SeedingMode.Registration);

        Assert.Equal(teams.Select(team => team.Id), seeded);
    }

    [Fact]
    public void Seed_Registration_GivesTheSameOrderForAnyInputOrder()
    {
        var teams = CreateTeams(5);
        var expected = TeamSeeder.Seed(teams, SeedingMode.Registration);

        Assert.Equal(expected, TeamSeeder.Seed([.. teams.AsEnumerable().Reverse()], SeedingMode.Registration));
        Assert.Equal(expected, TeamSeeder.Seed([teams[3], teams[4], teams[0], teams[1], teams[2]], SeedingMode.Registration));
    }

    [Fact]
    public void Seed_Registration_EqualRegistrationTimes_AreOrderedById()
    {
        var low = new Team("Niższe Id", Guid.NewGuid()) { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), RegisteredAt = FirstRegistration };
        var high = new Team("Wyższe Id", Guid.NewGuid()) { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), RegisteredAt = FirstRegistration };

        Assert.Equal([low.Id, high.Id], TeamSeeder.Seed([high, low], SeedingMode.Registration));
        Assert.Equal([low.Id, high.Id], TeamSeeder.Seed([low, high], SeedingMode.Registration));
    }

    // --- Losowanie ---

    [Fact]
    public void Seed_Random_WithIndexAlwaysZero_ReturnsAKnownPermutation()
    {
        var teams = CreateTeams(4);
        var (a, b, c, d) = (teams[0].Id, teams[1].Id, teams[2].Id, teams[3].Id);

        var seeded = TeamSeeder.Seed(teams, SeedingMode.Random, nextIndex: _ => 0);

        // Fisher-Yates od końca: [a,b,c,d] -> [d,b,c,a] -> [c,b,d,a] -> [b,c,d,a]
        Assert.Equal([b, c, d, a], seeded);
    }

    [Fact]
    public void Seed_Random_WithIndexAlwaysLast_KeepsRegistrationOrder()
    {
        var teams = CreateTeams(5);

        var seeded = TeamSeeder.Seed(teams, SeedingMode.Random, nextIndex: upperBound => upperBound - 1);

        Assert.Equal(teams.Select(team => team.Id), seeded);
    }

    [Fact]
    public void Seed_Random_ReturnsEveryTeamExactlyOnce()
    {
        var teams = CreateTeams(8);

        var seeded = TeamSeeder.Seed(teams, SeedingMode.Random);

        Assert.Equal(teams.Select(team => team.Id).Order(), seeded.Order());
    }

    [Fact]
    public void Seed_Random_WithDefaultGenerator_ProducesDifferentOrders()
    {
        var teams = CreateTeams(8);

        var orders = Enumerable.Range(0, 30)
            .Select(_ => string.Join(',', TeamSeeder.Seed(teams, SeedingMode.Random)))
            .Distinct()
            .Count();

        // Szansa, że 30 losowań da tę samą kolejność ośmiu drużyn, jest pomijalna
        Assert.True(orders > 1);
    }

    [Fact]
    public void Seed_Random_DoesNotChangeTheInputTeams()
    {
        var teams = CreateTeams(4);
        var before = teams.Select(team => team.Id).ToList();

        TeamSeeder.Seed(teams, SeedingMode.Random, nextIndex: _ => 0);

        Assert.Equal(before, teams.Select(team => team.Id));
    }

    [Fact]
    public void Seed_Random_GeneratorReturningValueOutOfRange_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => TeamSeeder.Seed(CreateTeams(4), SeedingMode.Random, nextIndex: _ => 99));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Seed_Random_WithFewerThanTwoTeams_NeverAsksForARandomNumber(int teamCount)
    {
        var teams = CreateTeams(teamCount);

        var seeded = TeamSeeder.Seed(teams, SeedingMode.Random, nextIndex: _ => throw new InvalidOperationException("Nie powinno być losowania."));

        Assert.Equal(teams.Select(team => team.Id), seeded);
    }

    // --- Ręczne rozstawienie ---

    [Fact]
    public void Seed_Manual_ReturnsTheOrderGivenByTheOrganizer()
    {
        var teams = CreateTeams(4);
        var order = new List<Guid> { teams[2].Id, teams[0].Id, teams[3].Id, teams[1].Id };

        var seeded = TeamSeeder.Seed(teams, SeedingMode.Manual, order);

        Assert.Equal(order, seeded);
    }

    [Fact]
    public void Seed_Manual_IgnoresTheRegistrationOrder()
    {
        var teams = CreateTeams(3);
        var reversed = teams.Select(team => team.Id).Reverse().ToList();

        Assert.Equal(reversed, TeamSeeder.Seed(teams, SeedingMode.Manual, reversed));
    }

    [Fact]
    public void Seed_Manual_WithoutOrder_ThrowsBusinessRuleException()
    {
        Assert.Throws<BusinessRuleException>(() => TeamSeeder.Seed(CreateTeams(3), SeedingMode.Manual, manualOrder: null));
    }

    [Fact]
    public void Seed_Manual_TeamRepeated_ThrowsBusinessRuleException()
    {
        var teams = CreateTeams(3);

        Assert.Throws<BusinessRuleException>(() =>
            TeamSeeder.Seed(teams, SeedingMode.Manual, [teams[0].Id, teams[1].Id, teams[1].Id]));
    }

    [Fact]
    public void Seed_Manual_TeamOutsideTheTournament_ThrowsBusinessRuleException()
    {
        var teams = CreateTeams(3);

        Assert.Throws<BusinessRuleException>(() =>
            TeamSeeder.Seed(teams, SeedingMode.Manual, [teams[0].Id, teams[1].Id, Guid.NewGuid()]));
    }

    [Fact]
    public void Seed_Manual_MissingTeam_ThrowsBusinessRuleExceptionWithNumberOfTeams()
    {
        var teams = CreateTeams(3);

        var exception = Assert.Throws<BusinessRuleException>(() =>
            TeamSeeder.Seed(teams, SeedingMode.Manual, [teams[0].Id, teams[1].Id]));

        Assert.Contains("3", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Seed_Manual_EmptyOrderForExistingTeams_ThrowsBusinessRuleException()
    {
        Assert.Throws<BusinessRuleException>(() => TeamSeeder.Seed(CreateTeams(2), SeedingMode.Manual, []));
    }

    /// <summary>Drużyny zgłoszone co godzinę, od najwcześniejszej do najpóźniejszej.</summary>
    private static List<Team> CreateTeams(int count) =>
        Enumerable.Range(0, count)
            .Select(index => new Team($"Drużyna {index + 1}", Guid.NewGuid()) { RegisteredAt = FirstRegistration.AddHours(index) })
            .ToList();
}
