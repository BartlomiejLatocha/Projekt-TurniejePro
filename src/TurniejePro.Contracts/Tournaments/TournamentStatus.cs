namespace TurniejePro.Contracts.Tournaments;

/// <summary>
/// Etap turnieju w API. Wartości muszą odpowiadać TurniejePro.Domain.Enums.TournamentStatus.
/// </summary>
public enum TournamentStatus
{
    Draft = 0,
    RegistrationOpen = 1,
    InProgress = 2,
    Finished = 3
}
