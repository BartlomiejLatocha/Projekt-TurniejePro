using System.Net.Http.Headers;
using System.Net.Http.Json;
using TurniejePro.Contracts.Common;
using TurniejePro.Contracts.Matches;
using TurniejePro.Contracts.Tournaments;

namespace TurniejePro.Blazor.Services;

/// <summary>
/// Klient HTTP do turniejów w TurniejePro.Api (lista publiczna i tworzenie przez organizatora).
/// </summary>
public sealed class TournamentApiClient(HttpClient http)
{
    private const int PageSize = 100;

    /// <summary>
    /// Tworzy turniej. Wymaga tokenu organizatora.
    /// </summary>
    public async Task<TournamentCreationResult> CreateTournamentAsync(
        CreateTournamentRequest request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/tournaments")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return TournamentCreationResult.Failure(await ApiError.ReadMessageAsync(response, cancellationToken));
            }

            var tournament = await response.Content.ReadFromJsonAsync<TournamentDetailsResponse>(cancellationToken);
            return tournament is null
                ? TournamentCreationResult.Failure("Serwer zwrócił pustą odpowiedź.")
                : TournamentCreationResult.Success(tournament);
        }
        catch (HttpRequestException)
        {
            return TournamentCreationResult.Failure("Nie udało się połączyć z serwerem API.");
        }
    }

    /// <summary>
    /// Zwraca terminarz turnieju (publiczny endpoint). Przy błędzie zwraca komunikat z API albo informację o połączeniu.
    /// </summary>
    public async Task<ScheduleLoadResult> GetScheduleAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync($"api/v1/tournaments/{tournamentId}/schedule", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ScheduleLoadResult.Failure(await ApiError.ReadMessageAsync(response, cancellationToken));
            }

            var schedule = await response.Content.ReadFromJsonAsync<ScheduleResponse>(cancellationToken);
            return schedule is null
                ? ScheduleLoadResult.Failure("Serwer zwrócił pustą odpowiedź.")
                : ScheduleLoadResult.Success(schedule);
        }
        catch (HttpRequestException)
        {
            return ScheduleLoadResult.Failure("Nie udało się połączyć z serwerem API.");
        }
    }

    /// <summary>
    /// Zwraca szczegóły turnieju (w tym liczbę zgłoszonych drużyn). Przy błędzie zwraca null.
    /// </summary>
    public async Task<TournamentDetailsResponse?> GetTournamentAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await http.GetFromJsonAsync<TournamentDetailsResponse>($"api/v1/tournaments/{tournamentId}", cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>
    /// Zwraca wybraną stronę turniejów z opcjonalnym wyszukiwaniem. Przy błędzie połączenia zwraca null.
    /// </summary>
    public async Task<PagedResponse<TournamentResponse>?> GetTournamentPageAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = $"api/v1/tournaments?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            query += $"&search={Uri.EscapeDataString(search)}";
        }

        try
        {
            return await http.GetFromJsonAsync<PagedResponse<TournamentResponse>>(query, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

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

/// <summary>
/// Wynik tworzenia turnieju: utworzony turniej albo komunikat o błędzie.
/// </summary>
public sealed record TournamentCreationResult(TournamentDetailsResponse? Tournament, string? ErrorMessage)
{
    public static TournamentCreationResult Success(TournamentDetailsResponse tournament) => new(tournament, null);

    public static TournamentCreationResult Failure(string message) => new(null, message);
}

/// <summary>
/// Wynik pobrania terminarza: terminarz albo komunikat dla użytkownika.
/// </summary>
public sealed record ScheduleLoadResult(ScheduleResponse? Schedule, string? ErrorMessage)
{
    public static ScheduleLoadResult Success(ScheduleResponse schedule) => new(schedule, null);

    public static ScheduleLoadResult Failure(string message) => new(null, message);
}
