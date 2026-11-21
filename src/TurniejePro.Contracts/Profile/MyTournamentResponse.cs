using TurniejePro.Contracts.Tournaments;

namespace TurniejePro.Contracts.Profile;

/// <summary>
/// Turniej zalogowanego użytkownika jako organizatora (widok „Moje turnieje”). Zawiera także szkice, których nie ma
/// sensu pokazywać innym, oraz liczniki zgłoszeń, żeby organizator od razu widział, czy ktoś czeka na decyzję.
/// </summary>
/// <param name="Id">Identyfikator turnieju.</param>
/// <param name="Name">Nazwa turnieju.</param>
/// <param name="Discipline">Kod dyscypliny (np. <c>football</c>, nazwa: <see cref="DisciplineCatalog"/>).</param>
/// <param name="Format">System rozgrywek.</param>
/// <param name="Status">Etap turnieju.</param>
/// <param name="StartsAt">Termin rozpoczęcia (UTC).</param>
/// <param name="MaxTeams">Limit drużyn.</param>
/// <param name="AcceptedTeams">Liczba zaakceptowanych drużyn.</param>
/// <param name="PendingTeams">Liczba zgłoszeń czekających na decyzję organizatora.</param>
public sealed record MyTournamentResponse(
    Guid Id,
    string Name,
    string Discipline,
    TournamentFormat Format,
    TournamentStatus Status,
    DateTimeOffset StartsAt,
    int MaxTeams,
    int AcceptedTeams,
    int PendingTeams);
