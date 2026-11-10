namespace TurniejePro.Contracts.Standings;

/// <summary>
/// Wiersz tabeli ligowej.
/// </summary>
/// <param name="Place">Miejsce w tabeli (od 1). Drużyny bez rozstrzygnięcia mają to samo miejsce.</param>
/// <param name="TeamId">Identyfikator drużyny.</param>
/// <param name="TeamName">Nazwa drużyny.</param>
/// <param name="Played">Rozegrane mecze.</param>
/// <param name="Won">Wygrane.</param>
/// <param name="Drawn">Remisy.</param>
/// <param name="Lost">Porażki.</param>
/// <param name="GoalsFor">Zdobyte bramki (punkty).</param>
/// <param name="GoalsAgainst">Stracone bramki (punkty).</param>
/// <param name="GoalDifference">Bilans bramek.</param>
/// <param name="Points">Punkty tabeli.</param>
public sealed record StandingsRowResponse(
    int Place,
    Guid TeamId,
    string TeamName,
    int Played,
    int Won,
    int Drawn,
    int Lost,
    int GoalsFor,
    int GoalsAgainst,
    int GoalDifference,
    int Points);
