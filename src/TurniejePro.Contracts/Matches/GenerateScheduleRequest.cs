using System.ComponentModel.DataAnnotations;

namespace TurniejePro.Contracts.Matches;

/// <summary>
/// Opcjonalne parametry generowania terminarza. Całe body można pominąć: wtedy drużyny są rozstawione w stałej
/// kolejności zgłoszeń.
/// </summary>
/// <param name="Seeding">Sposób rozstawienia; brak oznacza kolejność zgłoszeń.</param>
/// <param name="TeamOrder">Identyfikatory wszystkich zaakceptowanych drużyn w kolejności rozstawienia (pierwsza jest
/// najwyżej rozstawiona). Wymagane przy rozstawieniu ręcznym, ignorowane przy pozostałych.</param>
public sealed record GenerateScheduleRequest(
    [EnumDataType(typeof(SeedingMode))] SeedingMode? Seeding,
    List<Guid>? TeamOrder);
