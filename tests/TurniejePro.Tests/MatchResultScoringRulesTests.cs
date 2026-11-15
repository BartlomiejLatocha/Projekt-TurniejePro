using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Exceptions;
using TurniejePro.Domain.Scheduling;

namespace TurniejePro.Tests;

/// <summary>
/// Walidacja wyniku według zasad punktacji turnieju (KAN-300): remis jest odrzucany, gdy zasady go nie dopuszczają
/// (<see cref="ScoringRules.AllowDraws"/>), a w pucharze zawsze, bo drabinka potrzebuje zwycięzcy.
/// </summary>
public class MatchResultScoringRulesTests
{
    [Fact]
    public async Task SetResultAsync_DrawInLeagueWithoutDraws_ThrowsBusinessRuleExceptionAndKeepsTheMatchOpen()
    {
        var (service, tournament, match) = Arrange(TournamentFormat.League, new ScoringRules(3, 1, 0, allowDraws: false));

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => service.SetResultAsync(tournament.Id, match.Id, 2, 2));

        Assert.Contains("remis", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(match.HomeScore);
        Assert.Equal(MatchStatus.Scheduled, match.Status);
    }

    [Fact]
    public async Task SetResultAsync_DecisiveResultInLeagueWithoutDraws_IsAccepted()
    {
        var (service, tournament, match) = Arrange(TournamentFormat.League, new ScoringRules(3, 1, 0, allowDraws: false));

        var result = await service.SetResultAsync(tournament.Id, match.Id, 2, 1);

        Assert.Equal(MatchStatus.Finished, result.Status);
        Assert.Equal((2, 1), (result.HomeScore, result.AwayScore));
    }

    [Fact]
    public async Task SetResultAsync_DrawInLeagueWithDraws_IsAccepted()
    {
        var (service, tournament, match) = Arrange(TournamentFormat.League, new ScoringRules(3, 1, 0, allowDraws: true));

        var result = await service.SetResultAsync(tournament.Id, match.Id, 1, 1);

        Assert.Equal(MatchStatus.Finished, result.Status);
    }

    [Fact]
    public async Task SetResultAsync_DrawInLeagueWithDefaultRules_IsAccepted()
    {
        var (service, tournament, match) = Arrange(TournamentFormat.League, scoringRules: null);

        var result = await service.SetResultAsync(tournament.Id, match.Id, 0, 0);

        Assert.Equal(MatchStatus.Finished, result.Status);
    }

    [Fact]
    public async Task SetResultAsync_DrawInCupEvenIfRulesAllowDraws_ThrowsBusinessRuleException()
    {
        var (service, tournament, match) = Arrange(TournamentFormat.Cup, new ScoringRules(3, 1, 0, allowDraws: true));

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => service.SetResultAsync(tournament.Id, match.Id, 1, 1));

        Assert.Contains("pucharow", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(match.HomeScore);
    }

    [Fact]
    public async Task SetResultAsync_DrawInCupWithDefaultRules_ThrowsBusinessRuleException()
    {
        var (service, tournament, match) = Arrange(TournamentFormat.Cup, scoringRules: null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SetResultAsync(tournament.Id, match.Id, 0, 0));
    }

    private static (MatchResultService Service, Tournament Tournament, Match Match) Arrange(TournamentFormat format, ScoringRules? scoringRules)
    {
        var tournament = new Tournament("Turniej", "Piłka nożna", format, DateTimeOffset.UtcNow.AddDays(7), 8, organizerId: null, scoringRules);
        var home = new Team("Gospodarze", tournament.Id);
        var away = new Team("Goście", tournament.Id);
        home.Accept();
        away.Accept();
        var match = new Match(tournament.Id, 1, 1, home.Id, away.Id);

        var service = new MatchResultService(new MatchResultServiceTests.FakeUnitOfWork(tournament, [home, away], [match]));

        return (service, tournament, match);
    }
}
