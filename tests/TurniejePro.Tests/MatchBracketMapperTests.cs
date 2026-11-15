using TurniejePro.Domain;
using TurniejePro.Domain.Scheduling;

namespace TurniejePro.Tests;

public class MatchBracketMapperTests
{
    private static readonly Guid TournamentId = Guid.NewGuid();

    [Fact]
    public void ToBracket_NullMatches_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => MatchBracketMapper.ToBracket(null!));
    }

    [Fact]
    public void ToBracket_GroupsMatchesIntoRoundsInOrderOfNumberAndPosition()
    {
        var (a, b, c, d) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var final = new Match(TournamentId, 2, 1);
        var second = new Match(TournamentId, 1, 2, c, d);
        var first = new Match(TournamentId, 1, 1, a, b);

        var bracket = MatchBracketMapper.ToBracket([final, second, first]);

        Assert.Equal([1, 2], bracket.Select(round => round.Number));
        Assert.Equal([1, 2], bracket[0].Matches.Select(match => match.Position));
        Assert.Equal([(a, b), (c, d)], bracket[0].Matches.Select(match => (match.HomeTeamId!.Value, match.AwayTeamId!.Value)));
        Assert.Equal([1], bracket[1].Matches.Select(match => match.Position));
    }

    [Fact]
    public void ToBracket_FinishedMatch_WinnerIsTheTeamWithMoreGoals()
    {
        var (home, away) = (Guid.NewGuid(), Guid.NewGuid());
        var homeWin = Finished(1, home, away, 2, 1);
        var awayWin = Finished(2, home, away, 0, 3);

        var matches = MatchBracketMapper.ToBracket([homeWin, awayWin])[0].Matches;

        Assert.Equal(home, matches[0].WinnerId);
        Assert.Equal(away, matches[1].WinnerId);
    }

    [Fact]
    public void ToBracket_ByeMatch_WinnerIsTheHomeTeamAndFlagIsKept()
    {
        var team = Guid.NewGuid();
        var bye = new Match(TournamentId, 1, 1, team);
        bye.MarkAsBye();

        var match = MatchBracketMapper.ToBracket([bye])[0].Matches[0];

        Assert.True(match.IsBye);
        Assert.Equal(team, match.WinnerId);
        Assert.Null(match.AwayTeamId);
    }

    [Fact]
    public void ToBracket_MatchWithoutDecisiveResult_HasNoWinner()
    {
        var (home, away) = (Guid.NewGuid(), Guid.NewGuid());
        var scheduled = new Match(TournamentId, 1, 1, home, away);
        var inProgress = new Match(TournamentId, 1, 2, home, away);
        inProgress.StartMatch();
        var draw = Finished(3, home, away, 1, 1);
        var cancelledAfterResult = Finished(4, home, away, 2, 0);
        cancelledAfterResult.Cancel();

        var matches = MatchBracketMapper.ToBracket([scheduled, inProgress, draw, cancelledAfterResult])[0].Matches;

        Assert.All(matches, match => Assert.Null(match.WinnerId));
    }

    [Fact]
    public void ApplyTeams_CopiesTeamsFromTheBracketIntoMatchesOfTheSameRoundAndPosition()
    {
        var (a, b, winner) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var first = new Match(TournamentId, 1, 1, a, b);
        var final = new Match(TournamentId, 2, 1);
        var matches = new[] { first, final };
        var bracket = new List<BracketRound>
        {
            new(1, [new BracketMatch(1, a, b, false, null)]),
            new(2, [new BracketMatch(1, winner, null, false, null)])
        };

        MatchBracketMapper.ApplyTeams(bracket, matches);

        Assert.Equal(winner, final.HomeTeamId);
        Assert.Null(final.AwayTeamId);
        Assert.Equal((a, b), (first.HomeTeamId, first.AwayTeamId));
    }

    [Fact]
    public void ApplyTeams_MatchWithoutCounterpartInTheBracket_StaysUntouched()
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var orphan = new Match(TournamentId, 3, 7, a, b);

        MatchBracketMapper.ApplyTeams([new BracketRound(1, [new BracketMatch(1, null, null, false, null)])], [orphan]);

        Assert.Equal((a, b), (orphan.HomeTeamId, orphan.AwayTeamId));
    }

    [Fact]
    public void ApplyTeams_DoesNotChangeScoresOrStatus()
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var match = Finished(1, a, b, 2, 1);

        MatchBracketMapper.ApplyTeams([new BracketRound(1, [new BracketMatch(1, a, b, false, a)])], [match]);

        Assert.Equal((2, 1), (match.HomeScore, match.AwayScore));
        Assert.Equal(TurniejePro.Domain.Enums.MatchStatus.Finished, match.Status);
    }

    [Fact]
    public void ApplyTeams_NullArguments_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => MatchBracketMapper.ApplyTeams(null!, []));
        Assert.Throws<ArgumentNullException>(() => MatchBracketMapper.ApplyTeams([], null!));
    }

    private static Match Finished(int position, Guid home, Guid away, int homeScore, int awayScore)
    {
        var match = new Match(TournamentId, 1, position, home, away);
        match.SetResult(homeScore, awayScore);

        return match;
    }
}
