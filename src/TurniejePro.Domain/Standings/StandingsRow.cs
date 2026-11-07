namespace TurniejePro.Domain.Standings;

/// <summary>
/// Wiersz tabeli ligowej jednej drużyny.
/// </summary>
/// <param name="Place">Miejsce w tabeli (od 1). Drużyny, których nie rozstrzygają żadne zasady, mają to samo miejsce.</param>
/// <param name="TeamId">Drużyna.</param>
/// <param name="Played">Rozegrane mecze.</param>
/// <param name="Won">Wygrane.</param>
/// <param name="Drawn">Remisy.</param>
/// <param name="Lost">Porażki.</param>
/// <param name="GoalsFor">Zdobyte bramki (punkty).</param>
/// <param name="GoalsAgainst">Stracone bramki (punkty).</param>
/// <param name="Points">Punkty tabeli.</param>
public sealed record StandingsRow(
    int Place,
    Guid TeamId,
    int Played,
    int Won,
    int Drawn,
    int Lost,
    int GoalsFor,
    int GoalsAgainst,
    int Points)
{
    /// <summary>Bilans bramek: zdobyte minus stracone.</summary>
    public int GoalDifference => GoalsFor - GoalsAgainst;
}
