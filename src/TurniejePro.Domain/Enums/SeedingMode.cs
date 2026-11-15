namespace TurniejePro.Domain.Enums;

/// <summary>
/// Sposób rozstawienia drużyn przy generowaniu terminarza (kolejność wejściowa drabinki lub ligi).
/// </summary>
public enum SeedingMode
{
    /// <summary>Stała kolejność: według czasu zgłoszenia drużyny (najwcześniej zgłoszona jest rozstawiona najwyżej).</summary>
    Registration = 1,

    /// <summary>Losowanie kolejności.</summary>
    Random = 2,

    /// <summary>Ręczne rozstawienie: organizator podaje kolejność wszystkich zaakceptowanych drużyn.</summary>
    Manual = 3
}
