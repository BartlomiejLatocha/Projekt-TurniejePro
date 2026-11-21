using System.ComponentModel.DataAnnotations;

namespace TurniejePro.Contracts.Tournaments;

/// <summary>
/// Pełne dane turnieju do zapisania. Pola, które się nie zmieniły, nie są traktowane jako zmiana, więc formularz może
/// wysyłać wszystko. Co wolno zmienić w danym statusie turnieju, sprawdza domena (docs/scheduling.md).
/// </summary>
/// <param name="StartsAt">Termin rozpoczęcia. Musi być w przyszłości tylko wtedy, gdy się zmienia: rozpoczęty turniej
/// może mieć termin w przeszłości, a mimo to da się zmienić np. jego nazwę.</param>
public sealed record UpdateTournamentRequest(
    [Required, StringLength(100, MinimumLength = 3)] string Name,
    [Required, StringLength(50)] string Discipline,
    [EnumDataType(typeof(TournamentFormat))] TournamentFormat Format,
    DateTimeOffset StartsAt,
    [Range(2, 128)] int MaxTeams);
