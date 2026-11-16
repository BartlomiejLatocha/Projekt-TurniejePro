namespace TurniejePro.Domain.Enums;

/// <summary>
/// Format rozgrywek turnieju w logice domenowej.
/// </summary>
public enum TournamentFormat
{
    /// <summary>Każdy z każdym (liga).</summary>
    League = 1,

    /// <summary>Drabinka pucharowa.</summary>
    Cup = 2,

    /// <summary>Faza grupowa (każdy z każdym w grupach), potem drabinka z najlepszymi z każdej grupy.</summary>
    GroupsAndCup = 3
}
