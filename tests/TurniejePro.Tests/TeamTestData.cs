using TurniejePro.Contracts.Teams;

namespace TurniejePro.Tests;

/// <summary>
/// Dane drużyn do testów. Drużyna musi mieć co najmniej jednego zawodnika (<c>Team.MinPlayers</c>).
/// </summary>
public static class TeamTestData
{
    public static List<CreatePlayerRequest> OnePlayer() => [new("Jan Testowy", 1)];
}
