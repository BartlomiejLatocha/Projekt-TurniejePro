using TurniejePro.Domain.Enums;

namespace TurniejePro.Domain.Scheduling;

/// <summary>
/// Przekłada mecze zapisane w bazie (encje <see cref="Match"/>) na drabinkę, na której działa
/// <see cref="KnockoutBracketAdvancer"/>, i przepisuje z niej drużyny z powrotem do meczów. Dzięki temu awans
/// zwycięzców ma jedną implementację.
/// </summary>
public static class MatchBracketMapper
{
    /// <summary>
    /// Buduje drabinkę z meczów: rundy rosnąco, mecze w rundzie według pozycji. Zwycięzca meczu zakończonego to drużyna
    /// z wyższym wynikiem, a przy wolnym losie gospodarz. Mecz bez wyniku lub zakończony remisem nie ma zwycięzcy.
    /// </summary>
    public static IReadOnlyList<BracketRound> ToBracket(IEnumerable<Match> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);

        return matches
            .GroupBy(match => match.Round)
            .OrderBy(round => round.Key)
            .Select(round => new BracketRound(
                round.Key,
                round.OrderBy(match => match.Position).Select(ToBracketMatch).ToList()))
            .ToList();
    }

    /// <summary>
    /// Przepisuje gospodarza i gościa z drabinki do meczów o tej samej rundzie i pozycji. Zmienia tylko te pola, które
    /// się różnią. Mecze bez odpowiednika w drabince zostają bez zmian.
    /// </summary>
    public static void ApplyTeams(IReadOnlyList<BracketRound> bracket, IEnumerable<Match> matches)
    {
        ArgumentNullException.ThrowIfNull(bracket);
        ArgumentNullException.ThrowIfNull(matches);

        foreach (var match in matches)
        {
            var source = bracket
                .FirstOrDefault(round => round.Number == match.Round)?.Matches
                .FirstOrDefault(candidate => candidate.Position == match.Position);

            if (source is null)
            {
                continue;
            }

            if (match.HomeTeamId != source.HomeTeamId)
            {
                match.HomeTeamId = source.HomeTeamId;
            }

            if (match.AwayTeamId != source.AwayTeamId)
            {
                match.AwayTeamId = source.AwayTeamId;
            }
        }
    }

    private static BracketMatch ToBracketMatch(Match match) =>
        new(match.Position, match.HomeTeamId, match.AwayTeamId, match.IsBye, WinnerOf(match));

    private static Guid? WinnerOf(Match match)
    {
        if (match.IsBye)
        {
            return match.HomeTeamId;
        }

        if (match.Status != MatchStatus.Finished || match.HomeScore is not { } homeScore || match.AwayScore is not { } awayScore || homeScore == awayScore)
        {
            return null;
        }

        return homeScore > awayScore ? match.HomeTeamId : match.AwayTeamId;
    }
}
