namespace TurniejePro.Domain.Enums;

/// <summary>
/// Status cyklu życia turnieju.
/// </summary>
public enum TournamentStatus
{
    /// <summary>Szkic turnieju przed otwarciem zapisów.</summary>
    Draft = 1,

    /// <summary>Zapisy drużyn są otwarte.</summary>
    RegistrationOpen = 2,

    /// <summary>Turniej jest w trakcie rozgrywania.</summary>
    InProgress = 3,

    /// <summary>Turniej został zakończony.</summary>
    Finished = 4
}
