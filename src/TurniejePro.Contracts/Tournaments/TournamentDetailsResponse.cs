namespace TurniejePro.Contracts.Tournaments;

public sealed record TournamentDetailsResponse(
    Guid Id,
    string Name,
    string Discipline,
    TournamentFormat Format,
    DateTimeOffset StartsAt,
    int MaxTeams,
    bool IsStarted,
    Guid? OrganizerId,
    int TeamsCount);
