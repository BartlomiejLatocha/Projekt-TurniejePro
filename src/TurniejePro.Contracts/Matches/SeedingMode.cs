using System.Text.Json.Serialization;

namespace TurniejePro.Contracts.Matches;

/// <summary>
/// Sposób rozstawienia drużyn przy generowaniu terminarza.
/// </summary>
// Serializacja jako tekst ("Registration", "Random", "Manual"), zgodnie z ustaleniami kontraktów.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SeedingMode
{
    /// <summary>Stała kolejność: według czasu zgłoszenia drużyny. To ustawienie domyślne.</summary>
    Registration = 1,

    /// <summary>Losowanie kolejności.</summary>
    Random = 2,

    /// <summary>Ręczne rozstawienie: organizator podaje kolejność w polu <c>teamOrder</c>.</summary>
    Manual = 3
}
