namespace TurniejePro.Domain.Scheduling;

/// <summary>
/// Awansuje zwycięzców drabinki pucharowej do kolejnych rund. Drabinka jest niemutowalna: każda operacja
/// zwraca nową drabinkę, a przekazana zostaje bez zmian.
/// </summary>
public static class KnockoutBracketAdvancer
{
    /// <summary>
    /// Zapisuje zwycięzcę meczu i wpisuje go do następnej rundy: z meczu na pozycji nieparzystej jako gospodarza,
    /// z parzystej jako gościa. Zwycięzca finału zostaje mistrzem (<see cref="GetChampion"/>).
    /// Ponowne wskazanie tego samego zwycięzcy nic nie zmienia. Zwycięzcę można poprawić, dopóki następny mecz
    /// nie został rozstrzygnięty.
    /// </summary>
    /// <param name="rounds">Drabinka (wynik <see cref="KnockoutBracketGenerator.Generate"/> lub poprzedniego awansu).</param>
    /// <param name="roundNumber">Numer rundy meczu (od 1).</param>
    /// <param name="position">Pozycja meczu w rundzie (od 1).</param>
    /// <param name="winnerId">Zwycięzca: jedna z dwóch drużyn meczu.</param>
    public static IReadOnlyList<BracketRound> AdvanceWinner(IReadOnlyList<BracketRound> rounds, int roundNumber, int position, Guid winnerId)
    {
        ArgumentNullException.ThrowIfNull(rounds);

        var match = FindMatch(rounds, roundNumber, position);

        if (match.IsBye)
        {
            throw new InvalidOperationException("Mecz z wolnym losem nie ma wyniku: drużyna przechodzi dalej bez gry.");
        }

        if (match.HomeTeamId is null || match.AwayTeamId is null)
        {
            throw new InvalidOperationException("Mecz nie ma jeszcze obu drużyn, więc nie można wskazać zwycięzcy.");
        }

        if (winnerId != match.HomeTeamId && winnerId != match.AwayTeamId)
        {
            throw new ArgumentException("Zwycięzcą może być tylko jedna z drużyn tego meczu.", nameof(winnerId));
        }

        if (match.WinnerId == winnerId)
        {
            return rounds;
        }

        var isFinal = roundNumber == rounds[^1].Number;
        var updated = ReplaceMatch(rounds, roundNumber, position, current => current with { WinnerId = winnerId });

        if (isFinal)
        {
            return updated;
        }

        var nextPosition = (position + 1) / 2;
        var nextMatch = FindMatch(rounds, roundNumber + 1, nextPosition);

        if (nextMatch.WinnerId is not null)
        {
            throw new InvalidOperationException("Nie można zmienić zwycięzcy, bo następny mecz został już rozstrzygnięty.");
        }

        return ReplaceMatch(
            updated,
            roundNumber + 1,
            nextPosition,
            current => position % 2 == 1 ? current with { HomeTeamId = winnerId } : current with { AwayTeamId = winnerId });
    }

    /// <summary>
    /// Zwraca mistrza, czyli zwycięzcę finału, albo null, gdy finał nie został jeszcze rozstrzygnięty.
    /// </summary>
    public static Guid? GetChampion(IReadOnlyList<BracketRound> rounds)
    {
        ArgumentNullException.ThrowIfNull(rounds);

        return rounds[^1].Matches[0].WinnerId;
    }

    private static BracketMatch FindMatch(IReadOnlyList<BracketRound> rounds, int roundNumber, int position)
    {
        var round = rounds.FirstOrDefault(candidate => candidate.Number == roundNumber)
            ?? throw new ArgumentOutOfRangeException(nameof(roundNumber), roundNumber, "Drabinka nie ma rundy o tym numerze.");

        return round.Matches.FirstOrDefault(candidate => candidate.Position == position)
            ?? throw new ArgumentOutOfRangeException(nameof(position), position, "Runda nie ma meczu na tej pozycji.");
    }

    private static List<BracketRound> ReplaceMatch(
        IReadOnlyList<BracketRound> rounds,
        int roundNumber,
        int position,
        Func<BracketMatch, BracketMatch> change) =>
        rounds
            .Select(round => round.Number != roundNumber
                ? round
                : round with
                {
                    Matches = round.Matches.Select(match => match.Position == position ? change(match) : match).ToList()
                })
            .ToList();
}
