using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Scheduling;

namespace TurniejePro.Tests;

/// <summary>
/// Awans zwycięzcy ma jedną implementację (KAN-281): rozegranie całej drabinki przez <see cref="MatchResultService"/>
/// (encje Match) daje dokładnie ten sam układ co przez <see cref="KnockoutBracketAdvancer"/> (model drabinki).
/// </summary>
public class MatchResultAdvancementConsistencyTests
{
    public static TheoryData<int> TeamCounts
    {
        get
        {
            var data = new TheoryData<int>();

            foreach (var count in Enumerable.Range(2, 33).Concat([64]))
            {
                data.Add(count);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public async Task PlayingOutTheBracketThroughMatchResultService_GivesTheSameTeamsAsTheAdvancer(int teamCount)
    {
        var tournament = new Tournament("Puchar", "Piłka nożna", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddDays(1), Math.Max(teamCount, 2), null);
        tournament.Start(); // wyniki wpisujemy tylko w rozpoczętym turnieju (KAN-290)
        var teams = Enumerable.Range(0, teamCount)
            .Select(index =>
            {
                var team = new Team($"Drużyna {index + 1}", tournament.Id);
                team.Accept();

                return team;
            })
            .ToList();
        var bracket = KnockoutBracketGenerator.Generate(teams.Select(team => team.Id).ToList());
        var matches = CreateMatches(tournament.Id, bracket);
        var service = new MatchResultService(new MatchResultServiceTests.FakeUnitOfWork(tournament, teams, matches));

        // Zwycięzcy na przemian z domu i z wyjazdu, żeby awansowały też drużyny z dalszych miejsc drabinki
        var expected = bracket;
        foreach (var round in bracket)
        {
            foreach (var position in Enumerable.Range(1, round.Matches.Count))
            {
                var match = matches.Single(m => m.Round == round.Number && m.Position == position);
                if (match.IsBye)
                {
                    continue;
                }

                var homeWins = (round.Number + position) % 2 == 0;
                await service.SetResultAsync(tournament.Id, match.Id, homeWins ? 1 : 0, homeWins ? 0 : 1);

                var expectedMatch = expected[round.Number - 1].Matches[position - 1];
                expected = KnockoutBracketAdvancer.AdvanceWinner(expected, round.Number, position, homeWins ? expectedMatch.HomeTeamId!.Value : expectedMatch.AwayTeamId!.Value);
            }
        }

        var differences = expected
            .SelectMany(round => round.Matches.Select(expectedMatch => (round.Number, expectedMatch)))
            .Where(item =>
            {
                var actual = matches.Single(m => m.Round == item.Number && m.Position == item.expectedMatch.Position);

                return actual.HomeTeamId != item.expectedMatch.HomeTeamId || actual.AwayTeamId != item.expectedMatch.AwayTeamId;
            })
            .Select(item => $"runda {item.Number}, pozycja {item.expectedMatch.Position}")
            .ToList();
        Assert.Empty(differences);

        var finalMatch = matches.Single(m => m.Round == bracket.Count);
        var championFromEntities = finalMatch.HomeScore > finalMatch.AwayScore ? finalMatch.HomeTeamId : finalMatch.AwayTeamId;
        Assert.Equal(KnockoutBracketAdvancer.GetChampion(expected), championFromEntities);
    }

    /// <summary>Mecze jak w <see cref="ScheduleService"/>: runda, pozycja, drużyny i oznaczenie wolnego losu.</summary>
    private static List<Match> CreateMatches(Guid tournamentId, IReadOnlyList<BracketRound> bracket)
    {
        var matches = new List<Match>();

        foreach (var round in bracket)
        {
            foreach (var bracketMatch in round.Matches)
            {
                var match = new Match(tournamentId, round.Number, bracketMatch.Position, bracketMatch.HomeTeamId, bracketMatch.AwayTeamId);

                if (bracketMatch.IsBye)
                {
                    match.MarkAsBye();
                }

                matches.Add(match);
            }
        }

        return matches;
    }
}
