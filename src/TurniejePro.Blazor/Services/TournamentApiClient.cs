using System.Net.Http.Json;
using TurniejePro.Contracts.Common;
using TurniejePro.Contracts.Tournaments;

namespace TurniejePro.Blazor.Services;

/// <summary>
/// Klient HTTP do pobierania publicznej listy turniejów z TurniejePro.Api.
/// </summary>
public sealed class TournamentApiClient(HttpClient http)
{
    private const int PageSize = 100;

    /// <summary>
    /// Zwraca pierwszą stronę turniejów. Przy błędzie połączenia zwraca pustą listę.
    /// </summary>
    public async Task<IReadOnlyList<TournamentResponse>> GetTournamentsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await http.GetFromJsonAsync<PagedResponse<TournamentResponse>>(
                $"api/v1/tournaments?pageSize={PageSize}",
                cancellationToken);

            return page?.Items ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }
}
