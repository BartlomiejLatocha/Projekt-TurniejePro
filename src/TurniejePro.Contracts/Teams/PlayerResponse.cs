namespace TurniejePro.Contracts.Teams;

public sealed record PlayerResponse(
    Guid Id,
    string FullName,
    int? JerseyNumber);
