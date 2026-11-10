namespace TurniejePro.Contracts.Standings;

/// <summary>
/// Tabela ligowa turnieju (tylko zaakceptowane drużyny).
/// </summary>
/// <param name="TournamentId">Identyfikator turnieju.</param>
/// <param name="Rows">Wiersze tabeli w kolejności miejsc.</param>
public sealed record StandingsResponse(
    Guid TournamentId,
    IReadOnlyList<StandingsRowResponse> Rows);
