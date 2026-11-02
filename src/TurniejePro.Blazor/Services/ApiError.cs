using System.Net;
using System.Net.Http.Json;

namespace TurniejePro.Blazor.Services;

/// <summary>
/// Zamienia odpowiedź błędu z API (ProblemDetails / ValidationProblemDetails) na komunikat dla użytkownika.
/// </summary>
public static class ApiError
{
    public static async Task<string> ReadMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return "Sesja wygasła lub nie jesteś zalogowany. Zaloguj się ponownie.";
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return "Brak uprawnień do wykonania tej operacji.";
        }

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemBody>(cancellationToken);
            if (problem is null)
            {
                return FallbackMessage(response);
            }

            if (!string.IsNullOrWhiteSpace(problem.Detail))
            {
                return problem.Detail;
            }

            if (problem.Errors is { Count: > 0 } errors)
            {
                return string.Join(" ", errors.Values.SelectMany(messages => messages));
            }

            return string.IsNullOrWhiteSpace(problem.Title) ? FallbackMessage(response) : problem.Title;
        }
        catch (System.Text.Json.JsonException)
        {
            return FallbackMessage(response);
        }
        catch (NotSupportedException)
        {
            return FallbackMessage(response);
        }
    }

    private static string FallbackMessage(HttpResponseMessage response) =>
        $"Błąd serwera ({(int)response.StatusCode}).";

    // Pola ProblemDetails, które są potrzebne do wyświetlenia komunikatu.
    private sealed record ProblemBody(string? Title, string? Detail, Dictionary<string, string[]>? Errors);
}
