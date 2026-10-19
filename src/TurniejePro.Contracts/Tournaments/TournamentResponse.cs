namespace TurniejePro.Contracts.Tournaments;

/// <summary>
/// Turniej zwracany przez API.
/// </summary>
/// <param name="Id">Identyfikator turnieju.</param>
/// <param name="Name">Nazwa turnieju.</param>
/// <param name="Discipline">Dyscyplina sportowa.</param>
/// <param name="Format">System rozgrywek.</param>
/// <param name="StartsAt">Termin rozpoczęcia (UTC).</param>
/// <param name="MaxTeams">Limit drużyn.</param>
public sealed record TournamentResponse(
    Guid Id,
    string Name,
    string Discipline,
    TournamentFormat Format,
    DateTimeOffset StartsAt,
    int MaxTeams);
