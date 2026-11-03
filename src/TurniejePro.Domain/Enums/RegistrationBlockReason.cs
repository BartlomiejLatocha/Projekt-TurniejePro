namespace TurniejePro.Domain.Enums;

/// <summary>
/// Powód, dla którego turniej nie przyjmuje nowych zgłoszeń drużyn.
/// </summary>
public enum RegistrationBlockReason
{
    /// <summary>Turniej został już rozpoczęty.</summary>
    TournamentStarted = 1,

    /// <summary>Upłynął termin rozpoczęcia turnieju, do którego przyjmowane są zgłoszenia.</summary>
    DeadlinePassed = 2,

    /// <summary>Osiągnięto limit drużyn.</summary>
    LimitReached = 3
}
