using TurniejePro.Contracts.Teams;
using TurniejePro.Contracts.Tournaments;

namespace TurniejePro.Contracts.Profile;

/// <summary>
/// Drużyna zalogowanego użytkownika jako kapitana (widok „Moje drużyny”) razem z podstawowymi danymi turnieju,
/// żeby UI nie musiało pobierać każdego turnieju osobno.
/// </summary>
/// <param name="Id">Identyfikator drużyny.</param>
/// <param name="Name">Nazwa drużyny.</param>
/// <param name="Status">Stan zgłoszenia: oczekuje, zaakceptowane albo odrzucone.</param>
/// <param name="RegisteredAt">Chwila zgłoszenia (UTC).</param>
/// <param name="PlayersCount">Liczba zawodników.</param>
/// <param name="HasLogo">Czy drużyna ma logo (<c>GET /api/v1/teams/{id}/logo</c>).</param>
/// <param name="TournamentId">Turniej, do którego zgłoszono drużynę.</param>
/// <param name="TournamentName">Nazwa turnieju.</param>
/// <param name="TournamentDiscipline">Kod dyscypliny turnieju.</param>
/// <param name="TournamentFormat">System rozgrywek turnieju.</param>
/// <param name="TournamentStatus">Etap turnieju.</param>
/// <param name="TournamentStartsAt">Termin rozpoczęcia turnieju (UTC).</param>
public sealed record MyTeamResponse(
    Guid Id,
    string Name,
    TeamRegistrationStatus Status,
    DateTimeOffset RegisteredAt,
    int PlayersCount,
    bool HasLogo,
    Guid TournamentId,
    string TournamentName,
    string TournamentDiscipline,
    TournamentFormat TournamentFormat,
    TournamentStatus TournamentStatus,
    DateTimeOffset TournamentStartsAt);
