using System.ComponentModel.DataAnnotations;

namespace TurniejePro.Contracts.Teams;

/// <summary>
/// Edycja drużyny: nazwa, kontakt i pełny skład zawodników. Liczba zawodników musi mieścić się w zakresie
/// <c>MinPlayers</c>–<c>MaxPlayers</c> (1–30); skład zastępuje dotychczasowy.
/// </summary>
public sealed record UpdateTeamRequest(
    [Required, StringLength(100, MinimumLength = 2)] string Name,
    [EmailAddress, StringLength(100)] string? ContactEmail,
    List<CreatePlayerRequest>? Players);
