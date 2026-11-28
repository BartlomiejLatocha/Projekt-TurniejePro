using System.ComponentModel.DataAnnotations;

namespace TurniejePro.Contracts.Matches;

/// <summary>
/// Opcjonalne parametry generowania terminarza. Całe body można pominąć: wtedy drużyny są rozstawione w stałej
/// kolejności zgłoszeń, a rundy nie mają dat.
/// </summary>
/// <param name="Seeding">Sposób rozstawienia; brak oznacza kolejność zgłoszeń.</param>
/// <param name="TeamOrder">Identyfikatory wszystkich zaakceptowanych drużyn w kolejności rozstawienia (pierwsza jest
/// najwyżej rozstawiona). Wymagane przy rozstawieniu ręcznym, ignorowane przy pozostałych.</param>
/// <param name="FirstRoundDate">Data i godzina 1. rundy (UTC). Brak oznacza terminarz bez dat. Nie może być wcześniejsza
/// niż termin rozpoczęcia turnieju (koniec zapisów).</param>
/// <param name="DaysBetweenRounds">Odstęp między rundami w dniach (1–60, domyślnie 7). Używany tylko razem
/// z <paramref name="FirstRoundDate"/>.</param>
public sealed record GenerateScheduleRequest(
    [EnumDataType(typeof(SeedingMode))] SeedingMode? Seeding,
    List<Guid>? TeamOrder,
    DateTimeOffset? FirstRoundDate = null,
    [Range(1, 60)] int? DaysBetweenRounds = null);
