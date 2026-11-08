namespace TurniejePro.Contracts.Matches;

public record UpdateMatchResultRequest(
    int HomeScore,
    int AwayScore);
