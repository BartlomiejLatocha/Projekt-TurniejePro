using System.Net.Http.Headers;
using System.Net.Http.Json;
using TurniejePro.Contracts.Teams;

namespace TurniejePro.Blazor.Services;

/// <summary>
/// Klient HTTP do zgłaszania drużyn w TurniejePro.Api.
/// </summary>
public sealed class TeamApiClient(HttpClient http)
{
    public async Task<TeamRegistrationResult> RegisterTeamAsync(
        CreateTeamRequest request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/teams")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return TeamRegistrationResult.Failure(await ApiError.ReadMessageAsync(response, cancellationToken));
            }

            var team = await response.Content.ReadFromJsonAsync<TeamResponse>(cancellationToken);
            return team is null
                ? TeamRegistrationResult.Failure("Serwer zwrócił pustą odpowiedź.")
                : TeamRegistrationResult.Success(team);
        }
        catch (HttpRequestException)
        {
            return TeamRegistrationResult.Failure("Nie udało się połączyć z serwerem API.");
        }
    }
}

/// <summary>
/// Wynik zgłoszenia drużyny: zgłoszona drużyna albo komunikat o błędzie.
/// </summary>
public sealed record TeamRegistrationResult(TeamResponse? Team, string? ErrorMessage)
{
    public static TeamRegistrationResult Success(TeamResponse team) => new(team, null);

    public static TeamRegistrationResult Failure(string message) => new(null, message);
}
