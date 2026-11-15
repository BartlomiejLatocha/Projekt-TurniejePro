using TurniejePro.Contracts.Matches;
using TurniejePro.Contracts.Tournaments;
using TurniejePro.Domain;
using TurniejePro.Domain.Scheduling;
using ContractMatchStatus = TurniejePro.Contracts.Matches.MatchStatus;
using ContractSeedingMode = TurniejePro.Contracts.Matches.SeedingMode;
using ContractTournamentFormat = TurniejePro.Contracts.Tournaments.TournamentFormat;
using DomainSeedingMode = TurniejePro.Domain.Enums.SeedingMode;

namespace TurniejePro.Api.Mapping;

public static class MatchMappingExtensions
{
    /// <summary>
    /// Zamienia opcjonalne parametry generowania terminarza na rozstawienie z domeny. Brak body lub brak
    /// <c>seeding</c> oznacza stałą kolejność zgłoszeń.
    /// </summary>
    public static ScheduleSeeding ToSeeding(this GenerateScheduleRequest? request)
    {
        if (request?.Seeding is not { } seeding)
        {
            return ScheduleSeeding.Registration;
        }

        return seeding switch
        {
            ContractSeedingMode.Registration => ScheduleSeeding.Registration,
            ContractSeedingMode.Random => new ScheduleSeeding(DomainSeedingMode.Random),
            ContractSeedingMode.Manual => new ScheduleSeeding(DomainSeedingMode.Manual, request.TeamOrder),
            _ => throw new ArgumentOutOfRangeException(nameof(request), seeding, "Nieznany sposób rozstawienia.")
        };
    }

    public static MatchResponse ToResponse(this Match match)
    {
        return new MatchResponse(
            match.Id,
            match.Round,
            match.Position,
            match.HomeTeam is not null ? new MatchTeamResponse(match.HomeTeam.Id, match.HomeTeam.Name) : null,
            match.AwayTeam is not null ? new MatchTeamResponse(match.AwayTeam.Id, match.AwayTeam.Name) : null,
            match.HomeScore,
            match.AwayScore,
            (ContractMatchStatus)match.Status,
            match.IsBye,
            match.ScheduledAt
        );
    }

    public static ScheduleResponse ToScheduleResponse(
        this IReadOnlyList<Match> matches,
        Guid tournamentId,
        ContractTournamentFormat format)
    {
        var rounds = matches
            .GroupBy(m => m.Round)
            .OrderBy(g => g.Key)
            .Select(g => new ScheduleRoundResponse(
                Round: g.Key,
                Matches: g.OrderBy(m => m.Position).Select(m => m.ToResponse()).ToList()
            ))
            .ToList();

        return new ScheduleResponse(
            TournamentId: tournamentId,
            Format: format,
            Rounds: rounds
        );
    }
}
