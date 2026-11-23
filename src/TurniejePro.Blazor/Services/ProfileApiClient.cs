using System.Net.Http.Headers;
using System.Net.Http.Json;
using TurniejePro.Contracts.Profile;

namespace TurniejePro.Blazor.Services;

/// <summary>
/// Klient HTTP do zasobów zalogowanego użytkownika (<c>api/v1/me</c>): turnieje organizatora i drużyny kapitana.
/// </summary>
public sealed class ProfileApiClient(HttpClient http)
{
    public Task<ProfileLoadResult<MyTournamentResponse>> GetMyTournamentsAsync(string accessToken, CancellationToken cancellationToken = default) =>
        GetListAsync<MyTournamentResponse>("api/v1/me/tournaments", accessToken, cancellationToken);

    public Task<ProfileLoadResult<MyTeamResponse>> GetMyTeamsAsync(string accessToken, CancellationToken cancellationToken = default) =>
        GetListAsync<MyTeamResponse>("api/v1/me/teams", accessToken, cancellationToken);

    private async Task<ProfileLoadResult<T>> GetListAsync<T>(string url, string accessToken, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ProfileLoadResult<T>(null, await ApiError.ReadMessageAsync(response, cancellationToken));
            }

            var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<T>>(cancellationToken);
            return items is null
                ? new ProfileLoadResult<T>(null, "Serwer zwrócił pustą odpowiedź.")
                : new ProfileLoadResult<T>(items, null);
        }
        catch (HttpRequestException)
        {
            return new ProfileLoadResult<T>(null, "Nie udało się połączyć z serwerem API.");
        }
    }
}

/// <summary>
/// Wynik pobrania listy: elementy albo komunikat o błędzie.
/// </summary>
public sealed record ProfileLoadResult<T>(IReadOnlyList<T>? Items, string? ErrorMessage);
