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

    /// <summary>
    /// Daty rund z żądania generowania terminarza: brak <c>firstRoundDate</c> oznacza terminarz bez dat,
    /// a brak odstępu – co tydzień.
    /// </summary>
    public static RoundDates? ToRoundDates(this GenerateScheduleRequest? request) =>
        request?.FirstRoundDate is { } first
            ? new RoundDates(first, request.DaysBetweenRounds ?? RoundDates.DefaultDaysBetweenRounds)
            : null;

    /// <summary>
    /// Jedna runda terminarza. Data rundy to wspólna data jej meczów (najwcześniejsza, gdyby się różniły),
    /// a null, gdy żaden mecz nie ma daty.
    /// </summary>
    public static ScheduleRoundResponse ToRoundResponse(this IEnumerable<Match> roundMatches, int round)
    {
        var matches = roundMatches.OrderBy(m => m.Position).ToList();

        return new ScheduleRoundResponse(
            Round: round,
            Matches: matches.Select(m => m.ToResponse()).ToList(),
            Date: matches.Where(m => m.ScheduledAt is not null).Min(m => m.ScheduledAt));
    }

    public static MatchResponse ToResponse(this Match match)
    {
        return new MatchResponse(
            match.Id,
            match.Round,
            match.Position,
            match.HomeTeam is not null ? new MatchTeamResponse(match.HomeTeam.Id, match.HomeTeam.Name, match.HomeTeam.HasLogo) : null,
            match.AwayTeam is not null ? new MatchTeamResponse(match.AwayTeam.Id, match.AwayTeam.Name, match.AwayTeam.HasLogo) : null,
            match.HomeScore,
            match.AwayScore,
            (ContractMatchStatus)match.Status,
            match.IsBye,
            match.ScheduledAt,
            match.IsGroupStage ? match.HomeTeam?.GroupNumber ?? match.AwayTeam?.GroupNumber : null
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
            .Select(g => g.ToRoundResponse(g.Key))
            .ToList();

        return new ScheduleResponse(
            TournamentId: tournamentId,
            Format: format,
            Rounds: rounds
        );
    }
}
