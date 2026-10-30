using System.ComponentModel.DataAnnotations;

namespace TurniejePro.Contracts.Tournaments;

public sealed record UpdateTournamentRequest(
    [Required, StringLength(100, MinimumLength = 3)] string Name,
    [Required, StringLength(50)] string Discipline,
    TournamentFormat Format,
    DateTimeOffset StartsAt,
    [Range(2, 128)] int MaxTeams);
