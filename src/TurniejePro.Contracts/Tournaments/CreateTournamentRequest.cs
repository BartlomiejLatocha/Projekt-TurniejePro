using System.ComponentModel.DataAnnotations;
using TurniejePro.Contracts.Validation;

namespace TurniejePro.Contracts.Tournaments;

/// <summary>
/// Dane potrzebne do utworzenia turnieju.
/// </summary>
/// <param name="Name">Nazwa turnieju (3-100 znaków).</param>
/// <param name="Discipline">Dyscyplina sportowa (maks. 50 znaków).</param>
/// <param name="Format">System rozgrywek (jedna z wartości <see cref="TournamentFormat"/>).</param>
/// <param name="StartsAt">Termin rozpoczęcia (UTC, w przyszłości).</param>
/// <param name="MaxTeams">Limit drużyn (2-128).</param>
public sealed record CreateTournamentRequest(
    [Required, StringLength(100, MinimumLength = 3)] string Name,
    [Required, StringLength(50)] string Discipline,
    [EnumDataType(typeof(TournamentFormat))] TournamentFormat Format,
    [FutureDate] DateTimeOffset StartsAt,
    [Range(2, 128)] int MaxTeams);
