namespace TurniejePro.Domain.Enums;

/// <summary>
/// Status meczu w turnieju.
/// </summary>
public enum MatchStatus
{
    /// <summary>Zaplanowany.</summary>
    Scheduled = 1,

    /// <summary>W trakcie rozgrywania.</summary>
    InProgress = 2,

    /// <summary>Zakończony z wynikiem.</summary>
    Finished = 3,

    /// <summary>Odwołany.</summary>
    Cancelled = 4
}
