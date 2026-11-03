using System.ComponentModel.DataAnnotations;
using TurniejePro.Contracts.Validation;

namespace TurniejePro.Contracts.Tournaments;

public sealed record UpdateTournamentRequest(
    [Required, StringLength(100, MinimumLength = 3)] string Name,
    [Required, StringLength(50)] string Discipline,
    [EnumDataType(typeof(TournamentFormat))] TournamentFormat Format,
    [FutureDate] DateTimeOffset StartsAt,
    [Range(2, 128)] int MaxTeams);
