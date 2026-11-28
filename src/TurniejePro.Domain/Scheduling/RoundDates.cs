namespace TurniejePro.Domain.Scheduling;

/// <summary>
/// Daty rund wyliczane przy generowaniu terminarza: runda n odbywa się
/// <see cref="FirstRoundDate"/> + (n − 1) × <see cref="DaysBetweenRounds"/> dni.
/// </summary>
public sealed record RoundDates
{
    /// <summary>Odstęp używany, gdy organizator go nie poda.</summary>
    public const int DefaultDaysBetweenRounds = 7;

    public const int MaxDaysBetweenRounds = 60;

    public RoundDates(DateTimeOffset firstRoundDate, int daysBetweenRounds = DefaultDaysBetweenRounds)
    {
        if (daysBetweenRounds is < 1 or > MaxDaysBetweenRounds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(daysBetweenRounds),
                $"Odstęp między rundami musi wynosić od 1 do {MaxDaysBetweenRounds} dni.");
        }

        FirstRoundDate = firstRoundDate;
        DaysBetweenRounds = daysBetweenRounds;
    }

    public DateTimeOffset FirstRoundDate { get; }

    public int DaysBetweenRounds { get; }

    /// <summary>
    /// Data rundy o numerze <paramref name="round"/> (od 1), w UTC. Dni dodajemy w czasie polskim, więc runda jest
    /// zawsze o tej samej godzinie na zegarze, także po zmianie czasu letniego i zimowego.
    /// </summary>
    public DateTimeOffset For(int round)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(round, 1);
        return PolishTime.AddDays(FirstRoundDate, (round - 1) * DaysBetweenRounds);
    }
}
