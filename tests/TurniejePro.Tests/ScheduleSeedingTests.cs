using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Exceptions;
using TurniejePro.Domain.Scheduling;

namespace TurniejePro.Tests;

/// <summary>
/// Rozstawienie drużyn w terminarzu (KAN-302): kolejność zgłoszeń, losowanie i ręczne rozstawienie wyznaczają pary
/// pierwszej rundy drabinki.
/// </summary>
public class ScheduleSeedingTests
{
    private static readonly DateTimeOffset FirstRegistration = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GenerateScheduleAsync_WithoutSeeding_UsesRegistrationOrderNoMatterHowTheRepositoryReturnsTeams()
    {
        var (tournament, teams) = CreateCup(4);
        var (t1, t2, t3, t4) = (teams[0], teams[1], teams[2], teams[3]);
        // Repozytorium zwraca drużyny w innej kolejności niż zgłoszenia
        var service = CreateService(tournament, [t3, t1, t4, t2], out _);

        var matches = await service.GenerateScheduleAsync(tournament.Id);

        Assert.Equal([(t1.Id, t4.Id), (t2.Id, t3.Id)], FirstRound(matches));
    }

    [Fact]
    public async Task GenerateScheduleAsync_RegistrationSeeding_PairsSeedOneWithFourAndTwoWithThree()
    {
        var (tournament, teams) = CreateCup(4);
        var service = CreateService(tournament, teams, out _);

        var matches = await service.GenerateScheduleAsync(tournament.Id, new ScheduleSeeding(SeedingMode.Registration));

        Assert.Equal([(teams[0].Id, teams[3].Id), (teams[1].Id, teams[2].Id)], FirstRound(matches));
    }

    [Fact]
    public async Task GenerateScheduleAsync_ManualSeeding_UsesTheOrderGivenByTheOrganizer()
    {
        var (tournament, teams) = CreateCup(4);
        var (t1, t2, t3, t4) = (teams[0], teams[1], teams[2], teams[3]);
        var service = CreateService(tournament, teams, out _);

        var matches = await service.GenerateScheduleAsync(
            tournament.Id,
            new ScheduleSeeding(SeedingMode.Manual, [t3.Id, t1.Id, t4.Id, t2.Id]));

        // Rozstawienie 1: t3, 2: t1, 3: t4, 4: t2, więc pary 1-4 i 2-3
        Assert.Equal([(t3.Id, t2.Id), (t1.Id, t4.Id)], FirstRound(matches));
    }

    [Fact]
    public async Task GenerateScheduleAsync_RandomSeeding_UsesTheDrawnOrder()
    {
        var (tournament, teams) = CreateCup(4);
        var (t1, t2, t3, t4) = (teams[0], teams[1], teams[2], teams[3]);
        var service = CreateService(tournament, teams, out _);

        var matches = await service.GenerateScheduleAsync(tournament.Id, new ScheduleSeeding(SeedingMode.Random, RandomIndex: _ => 0));

        // Losowanie "zawsze zero" daje z [t1,t2,t3,t4] kolejność [t2,t3,t4,t1], więc pary t2-t1 i t3-t4
        Assert.Equal([(t2.Id, t1.Id), (t3.Id, t4.Id)], FirstRound(matches));
    }

    [Fact]
    public async Task GenerateScheduleAsync_RandomSeedingWithFiveTeams_GivesByesToTheTopDrawnSeeds()
    {
        var (tournament, teams) = CreateCup(5);
        var service = CreateService(tournament, teams, out _);

        var matches = await service.GenerateScheduleAsync(tournament.Id, new ScheduleSeeding(SeedingMode.Random, RandomIndex: _ => 0));

        // Fisher-Yates z zerami: [t1..t5] -> [t2,t3,t4,t5,t1]. Wolne losy dostają rozstawieni 1-3: t2, t3 i t4.
        var byeTeams = matches.Where(match => match is { Round: 1, IsBye: true }).Select(match => match.HomeTeamId!.Value);
        Assert.Equal(new[] { teams[1].Id, teams[2].Id, teams[3].Id }.Order(), byeTeams.Order());
    }

    [Fact]
    public async Task GenerateScheduleAsync_ManualSeedingInLeague_BuildsRoundsFromTheGivenOrder()
    {
        var (tournament, teams) = CreateLeague(4);
        var order = teams.Select(team => team.Id).Reverse().ToList();
        var service = CreateService(tournament, teams, out _);

        var matches = await service.GenerateScheduleAsync(tournament.Id, new ScheduleSeeding(SeedingMode.Manual, order));

        var expectedFirstRound = RoundRobinScheduler.Generate(order)[0].Pairings.Select(pairing => (pairing.HomeTeamId, pairing.AwayTeamId));
        Assert.Equal(expectedFirstRound, FirstRound(matches));
    }

    [Fact]
    public async Task GenerateScheduleAsync_ManualSeedingMissingATeam_ThrowsBusinessRuleExceptionAndSavesNothing()
    {
        var (tournament, teams) = CreateCup(4);
        var service = CreateService(tournament, teams, out var unitOfWork);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.GenerateScheduleAsync(tournament.Id, new ScheduleSeeding(SeedingMode.Manual, [teams[0].Id, teams[1].Id, teams[2].Id])));

        Assert.Empty(unitOfWork.MatchesList);
    }

    [Fact]
    public async Task GenerateScheduleAsync_ManualSeedingWithoutOrder_ThrowsBusinessRuleException()
    {
        var (tournament, teams) = CreateCup(4);
        var service = CreateService(tournament, teams, out _);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GenerateScheduleAsync(tournament.Id, new ScheduleSeeding(SeedingMode.Manual)));
    }

    [Fact]
    public async Task GenerateScheduleAsync_NullSeeding_ThrowsArgumentNullException()
    {
        var (tournament, teams) = CreateCup(4);
        var service = CreateService(tournament, teams, out _);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.GenerateScheduleAsync(tournament.Id, null!));
    }

    private static (Tournament Tournament, List<Team> Teams) CreateCup(int teamCount) => Create(TournamentFormat.Cup, teamCount);

    private static (Tournament Tournament, List<Team> Teams) CreateLeague(int teamCount) => Create(TournamentFormat.League, teamCount);

    /// <summary>Turniej i zaakceptowane drużyny zgłoszone co godzinę, od najwcześniejszej do najpóźniejszej.</summary>
    private static (Tournament Tournament, List<Team> Teams) Create(TournamentFormat format, int teamCount)
    {
        var tournament = new Tournament("Turniej", "Piłka nożna", format, DateTimeOffset.UtcNow.AddDays(7), 16, null);
        var teams = Enumerable.Range(0, teamCount)
            .Select(index =>
            {
                var team = new Team($"Drużyna {index + 1}", tournament.Id) { RegisteredAt = FirstRegistration.AddHours(index) };
                team.Accept();

                return team;
            })
            .ToList();

        return (tournament, teams);
    }

    private static ScheduleService CreateService(Tournament tournament, List<Team> teamsInRepositoryOrder, out ScheduleServiceTests.FakeUnitOfWork unitOfWork)
    {
        unitOfWork = new ScheduleServiceTests.FakeUnitOfWork(tournament, teamsInRepositoryOrder);

        return new ScheduleService(unitOfWork);
    }

    /// <summary>Pary (gospodarz, gość) pierwszej rundy według pozycji, dla turniejów bez wolnych losów.</summary>
    private static List<(Guid Home, Guid Away)> FirstRound(IReadOnlyList<Match> matches) =>
        matches
            .Where(match => match.Round == 1)
            .OrderBy(match => match.Position)
            .Select(match => (match.HomeTeamId!.Value, match.AwayTeamId!.Value))
            .ToList();
}
