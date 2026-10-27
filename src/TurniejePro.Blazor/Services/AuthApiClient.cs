using System.Net;
using System.Net.Http.Json;
using TurniejePro.Contracts.Auth;

namespace TurniejePro.Blazor.Services;

/// <summary>
/// Klient HTTP do endpointów rejestracji i logowania w TurniejePro.Api.
/// </summary>
public sealed class AuthApiClient(HttpClient http)
{
    public Task<AuthOutcome> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default) =>
        SendAsync("api/v1/auth/register", request, cancellationToken);

    public Task<AuthOutcome> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
        SendAsync("api/v1/auth/login", request, cancellationToken);

    private async Task<AuthOutcome> SendAsync<T>(string url, T request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.PostAsJsonAsync(url, request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
                return auth is null
                    ? AuthOutcome.Failure("Serwer zwrócił pustą odpowiedź.")
                    : AuthOutcome.Success(auth);
            }

            return AuthOutcome.Failure(await DescribeErrorAsync(response, cancellationToken));
        }
        catch (HttpRequestException)
        {
            return AuthOutcome.Failure("Nie udało się połączyć z serwerem API.");
        }
    }

    private static async Task<string> DescribeErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return "Nieprawidłowy adres e-mail lub hasło.";
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return "Konto o podanym adresie e-mail już istnieje.";
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemBody>(cancellationToken);
            return problem?.Errors is { Count: > 0 } errors
                ? string.Join(" ", errors.Values.SelectMany(messages => messages))
                : "Dane formularza są niepoprawne.";
        }

        return $"Błąd serwera ({(int)response.StatusCode}).";
    }

    // Tylko pole "errors" z ValidationProblemDetails jest potrzebne do wyświetlenia komunikatu.
    private sealed record ProblemBody(Dictionary<string, string[]>? Errors);
}

/// <summary>
/// Wynik wywołania API: albo odpowiedź z tokenem, albo komunikat dla użytkownika.
/// </summary>
public sealed record AuthOutcome(AuthResponse? Response, string? ErrorMessage)
{
    public static AuthOutcome Success(AuthResponse response) => new(response, null);

    public static AuthOutcome Failure(string message) => new(null, message);
}
