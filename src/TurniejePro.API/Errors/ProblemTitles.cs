using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TurniejePro.Api.Errors;

/// <summary>
/// Polskie tytuły odpowiedzi ProblemDetails. Tytuł jest krótki i zależy od rodzaju błędu, a szczegóły dla
/// użytkownika są w polu <c>detail</c>.
/// </summary>
public static class ProblemTitles
{
    public const string BadRequest = "Nieprawidłowe żądanie";
    public const string Validation = "Błąd walidacji danych";
    public const string InvalidData = "Nieprawidłowe dane";
    public const string BusinessRule = "Operacja niedozwolona";
    public const string Unauthorized = "Brak uwierzytelnienia";
    public const string Forbidden = "Brak uprawnień";
    public const string NotFound = "Nie znaleziono zasobu";
    public const string Conflict = "Konflikt z bieżącym stanem danych";
    public const string ServerError = "Wystąpił nieoczekiwany błąd serwera";
    public const string Error = "Wystąpił błąd";

    // Domyślne angielskie tytuły ASP.NET Core, które podmieniamy na polskie. Tytułów nadanych w kontrolerach nie ruszamy.
    private static readonly HashSet<string> FrameworkTitles = new(StringComparer.Ordinal)
    {
        "One or more validation errors occurred.",
        "An error occurred while processing your request.",
        "Bad Request",
        "Unauthorized",
        "Forbidden",
        "Not Found",
        "Method Not Allowed",
        "Not Acceptable",
        "Conflict",
        "Unsupported Media Type",
        "Unprocessable Entity",
        "Internal Server Error"
    };

    /// <summary>
    /// Ustawia polski tytuł, gdy odpowiedź nie ma tytułu albo ma domyślny angielski tytuł frameworka.
    /// </summary>
    public static void ApplyPolishTitle(ProblemDetails problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        if (problem.Title is not null && !FrameworkTitles.Contains(problem.Title))
        {
            return;
        }

        problem.Title = problem is HttpValidationProblemDetails or ValidationProblemDetails
            ? Validation
            : ForStatus(problem.Status);
    }

    /// <summary>
    /// Ogólny polski tytuł dla kodu odpowiedzi.
    /// </summary>
    public static string ForStatus(int? status) => status switch
    {
        StatusCodes.Status400BadRequest => BadRequest,
        StatusCodes.Status401Unauthorized => Unauthorized,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => NotFound,
        StatusCodes.Status409Conflict => Conflict,
        >= StatusCodes.Status500InternalServerError => ServerError,
        _ => Error
    };
}
