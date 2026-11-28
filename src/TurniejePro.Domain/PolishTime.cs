namespace TurniejePro.Domain;

/// <summary>
/// Czas polski (Europe/Warsaw) z uwzględnieniem zmiany czasu letniego i zimowego. Turnieje odbywają się w Polsce, więc
/// „co tydzień o 10:00” oznacza 10:00 czasu polskiego, a nie stałą godzinę UTC.
/// </summary>
public static class PolishTime
{
    /// <summary>Strefa Europe/Warsaw (dostępna w .NET na Windows i Linux); awaryjnie UTC.</summary>
    public static TimeZoneInfo Zone { get; } =
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Warsaw", out var zone) ? zone : TimeZoneInfo.Utc;

    /// <summary>Ta sama chwila wyrażona w czasie polskim.</summary>
    public static DateTimeOffset ToPolish(DateTimeOffset moment) => TimeZoneInfo.ConvertTime(moment, Zone);

    /// <summary>
    /// Dodaje dni w kalendarzu polskim: godzina na zegarze zostaje ta sama także przy zmianie czasu
    /// (sobota 10:00 + 7 dni = sobota 10:00, nawet gdy w międzyczasie zmienił się czas). Wynik jest w UTC.
    /// </summary>
    public static DateTimeOffset AddDays(DateTimeOffset moment, int days)
    {
        var wallClock = ToPolish(moment).DateTime.AddDays(days);
        var offset = Zone.GetUtcOffset(wallClock);
        return new DateTimeOffset(wallClock, offset).ToUniversalTime();
    }
}
