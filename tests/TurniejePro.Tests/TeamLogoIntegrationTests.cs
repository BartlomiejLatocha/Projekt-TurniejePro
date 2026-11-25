using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TurniejePro.Contracts.Teams;
using TurniejePro.Domain;
using Xunit;

namespace TurniejePro.Tests;

public class TeamLogoIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private readonly HttpClient _client;
    private readonly TestTokens _testTokens;

    public TeamLogoIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _testTokens = new TestTokens(factory.Services);
    }

    [Fact]
    public async Task PUT_Logo_AsOrganizer_SavesLogoAndGETReturnsIt()
    {
        var teamId = await CreateTeamAsync();

        var upload = await UploadAsync(teamId, PngHeader, "logo.png", "image/png");
        var download = await _client.GetAsync($"/api/v1/teams/{teamId}/logo");

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var team = await upload.Content.ReadFromJsonAsync<TeamResponse>();
        Assert.True(team!.HasLogo);

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PngHeader, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task PUT_Logo_LargerThanOneMegabyte_Returns400()
    {
        var teamId = await CreateTeamAsync();
        var data = new byte[Team.MaxLogoBytes + 1];
        PngHeader.CopyTo(data, 0);

        var response = await UploadAsync(teamId, data, "logo.png", "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PUT_Logo_UnsupportedFormat_Returns400()
    {
        var teamId = await CreateTeamAsync();

        var response = await UploadAsync(teamId, "to nie jest obraz"u8.ToArray(), "logo.txt", "text/plain");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PUT_Logo_Anonymous_Returns401()
    {
        var teamId = await CreateTeamAsync();
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await UploadAsync(teamId, PngHeader, "logo.png", "image/png");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_Logo_WhenNoneUploaded_Returns404()
    {
        var teamId = await CreateTeamAsync();

        var response = await _client.GetAsync($"/api/v1/teams/{teamId}/logo");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<HttpResponseMessage> UploadAsync(Guid teamId, byte[] data, string fileName, string contentType)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(data);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);

        return await _client.PutAsync($"/api/v1/teams/{teamId}/logo", form);
    }

    private async Task<Guid> CreateTeamAsync()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _testTokens.ForRoles("Organizer"));

        var tournamentResponse = await _client.PostAsJsonAsync("/api/v1/tournaments", new
        {
            name = $"Logo {Guid.NewGuid():N}",
            discipline = "football",
            format = "League",
            startsAt = DateTimeOffset.UtcNow.AddDays(10),
            maxTeams = 4
        });
        var tournament = await tournamentResponse.Content.ReadFromJsonAsync<TournamentIdOnly>();

        var teamResponse = await _client.PostAsJsonAsync("/api/v1/teams", new
        {
            tournamentId = tournament!.Id,
            name = $"Druzyna {Guid.NewGuid():N}",
            contactEmail = (string?)null,
            players = new[] { new { fullName = "Jan Testowy", jerseyNumber = 1 } }
        });
        var team = await teamResponse.Content.ReadFromJsonAsync<TeamResponse>();
        return team!.Id;
    }

    private sealed record TournamentIdOnly(Guid Id);
}
