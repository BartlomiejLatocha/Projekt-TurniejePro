namespace TurniejePro.Contracts.Teams;

public sealed record TeamResponse(
    Guid Id,
    Guid TournamentId,
    string Name,
    Guid? CaptainId,
    string? ContactEmail,
    TeamRegistrationStatus Status,
    DateTimeOffset RegisteredAt,
    IReadOnlyList<PlayerResponse> Players);
