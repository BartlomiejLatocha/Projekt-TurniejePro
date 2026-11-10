using System.Net.Http.Headers;
using System.Net.Http.Json;
using TurniejePro.Contracts.Matches;

namespace TurniejePro.Blazor.Services;

/// <summary>
/// Klient HTTP do wpisywania wyników meczów w TurniejePro.Api.
/// </summary>
public sealed class MatchApiClient(HttpClient http)
{
    public async Task<MatchResultOutcome> SetResultAsync(
        Guid tournamentId,
        Guid matchId,
        int homeScore,
        int awayScore,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, $"api/v1/tournaments/{tournamentId}/matches/{matchId}/result")
        {
            Content = JsonContent.Create(new UpdateMatchResultRequest(homeScore, awayScore))
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return MatchResultOutcome.Failure(await ApiError.ReadMessageAsync(response, cancellationToken));
            }

            var match = await response.Content.ReadFromJsonAsync<MatchResponse>(cancellationToken);
            return match is null
                ? MatchResultOutcome.Failure("Serwer zwrócił pustą odpowiedź.")
                : MatchResultOutcome.Success(match);
        }
        catch (HttpRequestException)
        {
            return MatchResultOutcome.Failure("Nie udało się połączyć z serwerem API.");
        }
    }
}

/// <summary>
/// Wynik zapisu wyniku meczu: zaktualizowany mecz albo komunikat o błędzie.
/// </summary>
public sealed record MatchResultOutcome(MatchResponse? Match, string? ErrorMessage)
{
    public static MatchResultOutcome Success(MatchResponse match) => new(match, null);

    public static MatchResultOutcome Failure(string message) => new(null, message);
}
