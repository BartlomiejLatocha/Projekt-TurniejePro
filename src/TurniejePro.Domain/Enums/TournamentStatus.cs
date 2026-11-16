namespace TurniejePro.Domain.Enums;

/// <summary>
/// Etap życia turnieju. Przejścia: Szkic → Zapisy → W trakcie → Zakończony.
/// </summary>
public enum TournamentStatus
{
    /// <summary>Szkic: turniej utworzony, zapisy jeszcze nie ruszyły.</summary>
    Draft = 0,

    /// <summary>Zapisy: drużyny mogą się zgłaszać.</summary>
    RegistrationOpen = 1,

    /// <summary>W trakcie: turniej rozpoczęty, format i punktacja są zablokowane.</summary>
    InProgress = 2,

    /// <summary>Zakończony: wszystkie mecze rozegrane.</summary>
    Finished = 3
}
