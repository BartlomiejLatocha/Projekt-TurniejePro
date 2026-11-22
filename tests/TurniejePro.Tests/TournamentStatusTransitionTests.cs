using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Api.Auth;
using TurniejePro.Contracts.Matches;
using TurniejePro.Contracts.Tournaments;
using TurniejePro.Data;
using TurniejePro.Domain;
using DomainFormat = TurniejePro.Domain.Enums.TournamentFormat;
using DomainStatus = TurniejePro.Domain.Enums.TournamentStatus;

namespace TurniejePro.Tests;

public class TournamentStatusTransitionTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Theory]
    [InlineData(DomainFormat.League)]
    [InlineData(DomainFormat.Cup)]
    public async Task SetResult_OnlyLastPlayableMatch_PersistsFinishedStatus(DomainFormat format)
    {
        var organizerId = Guid.NewGuid();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
            var token = tokens.CreateToken(organizerId, "organizer@example.com", [Roles.User]);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        }

        var tournamentId = await SeedTournamentAsync(format, organizerId);
        var path = $"/api/v1/tournaments/{tournamentId}";

        await AssertStatusAsync(client, path, TournamentStatus.Draft);
        using var registration = await client.PatchAsync($"{path}/open-registration", null);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        await AssertStatusAsync(client, path, TournamentStatus.RegistrationOpen);

        using var scheduleResponse = await client.PostAsync($"{path}/schedule", null);
        Assert.Equal(HttpStatusCode.Created, scheduleResponse.StatusCode);
        var schedule = await scheduleResponse.Content.ReadFromJsonAsync<ScheduleResponse>();
        Assert.NotNull(schedule);
        var playableMatches = schedule.Rounds.SelectMany(round => round.Matches).Where(match => !match.IsBye).ToArray();
        Assert.Equal(format == DomainFormat.Cup ? 2 : 3, playableMatches.Length);
        await AssertStatusAsync(client, path, TournamentStatus.InProgress);

        for (var index = 0; index < playableMatches.Length; index++)
        {
            using var result = await client.PutAsJsonAsync(
                $"{path}/matches/{playableMatches[index].Id}/result", new UpdateMatchResultRequest(2, 1));
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var expectedStatus = index == playableMatches.Length - 1
                ? TournamentStatus.Finished
                : TournamentStatus.InProgress;
            await AssertStatusAsync(client, path, expectedStatus);
        }

        using var reopen = await client.PatchAsync($"{path}/open-registration", null);
        Assert.Equal(HttpStatusCode.BadRequest, reopen.StatusCode);
        await AssertStatusAsync(client, path, TournamentStatus.Finished);
    }

    [Fact]
    public void CompleteIfAllMatchesFinished_WithoutPlayableMatches_KeepsInProgress()
    {
        var tournament = new Tournament("Turniej");
        tournament.Start();
        var bye = new Match(tournament.Id, 1, 1, Guid.NewGuid());
        bye.MarkAsBye();
        var cancelled = new Match(tournament.Id, 1, 2, Guid.NewGuid(), Guid.NewGuid());
        cancelled.Cancel();

        var completed = tournament.CompleteIfAllMatchesFinished([bye, cancelled]);

        Assert.False(completed);
        Assert.Equal(DomainStatus.InProgress, tournament.Status);
    }

    private async Task<Guid> SeedTournamentAsync(DomainFormat format, Guid organizerId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();
        var tournament = new Tournament("Turniej statusów", "football", format, DateTimeOffset.UtcNow.AddDays(7), 8, organizerId);
        db.Tournaments.Add(tournament);
        foreach (var name in new[] { "Orły", "Sokoły", "Jastrzębie" })
        {
            var team = new Team(name, tournament.Id);
            team.Accept();
            db.Teams.Add(team);
        }

        await db.SaveChangesAsync();
        return tournament.Id;
    }

    private static async Task AssertStatusAsync(HttpClient client, string path, TournamentStatus expected)
    {
        var tournament = await client.GetFromJsonAsync<TournamentDetailsResponse>(path);

        Assert.NotNull(tournament);
        Assert.Equal(expected, tournament.Status);
        Assert.Equal(expected is TournamentStatus.InProgress or TournamentStatus.Finished, tournament.IsStarted);
    }
}
