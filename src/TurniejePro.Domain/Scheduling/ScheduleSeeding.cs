using TurniejePro.Domain.Enums;

namespace TurniejePro.Domain.Scheduling;

/// <summary>
/// Wybrany sposób rozstawienia drużyn przy generowaniu terminarza.
/// </summary>
/// <param name="Mode">Tryb rozstawienia.</param>
/// <param name="TeamOrder">Kolejność podana przez organizatora (tylko dla <see cref="SeedingMode.Manual"/>).</param>
/// <param name="RandomIndex">Źródło losowości (tylko dla <see cref="SeedingMode.Random"/>); domyślnie generator kryptograficzny,
/// podmieniane w testach.</param>
public sealed record ScheduleSeeding(SeedingMode Mode, IReadOnlyList<Guid>? TeamOrder = null, Func<int, int>? RandomIndex = null)
{
    /// <summary>Domyślne rozstawienie: stała kolejność zgłoszeń.</summary>
    public static ScheduleSeeding Registration { get; } = new(SeedingMode.Registration);
}
