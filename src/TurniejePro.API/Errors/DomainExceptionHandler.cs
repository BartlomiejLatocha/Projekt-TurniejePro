using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TurniejePro.Domain.Exceptions;

namespace TurniejePro.Api.Errors;

/// <summary>
/// Zamienia wyjątki logiki domenowej na odpowiedzi ProblemDetails z polskim tytułem i komunikatem wyjątku w <c>detail</c>:
/// <see cref="NotFoundException"/> → 404, <see cref="ConflictException"/> → 409, <see cref="BusinessRuleException"/> → 400,
/// <see cref="ArgumentException"/> (walidacja danych w encjach) → 400.
/// Pozostałych wyjątków nie obsługuje: kończą się odpowiedzią 500 bez szczegółów, bo oznaczają błąd w kodzie.
/// </summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;

    public DomainExceptionHandler(IProblemDetailsService problemDetailsService)
    {
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (Map(exception) is not { } mapped)
        {
            return false;
        }

        httpContext.Response.StatusCode = mapped.Status;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = mapped.Status,
                Title = mapped.Title,
                Detail = UserMessage(exception)
            }
        });
    }

    /// <summary>
    /// Kod i tytuł odpowiedzi dla wyjątku albo null, gdy wyjątek nie jest błędem użytkownika.
    /// </summary>
    public static (int Status, string Title)? Map(Exception exception) => exception switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, ProblemTitles.NotFound),
        ConflictException => (StatusCodes.Status409Conflict, ProblemTitles.Conflict),
        BusinessRuleException => (StatusCodes.Status400BadRequest, ProblemTitles.BusinessRule),
        ArgumentException => (StatusCodes.Status400BadRequest, ProblemTitles.InvalidData),
        _ => null
    };

    /// <summary>
    /// Komunikat bez technicznych dopisków .NET, np. „(Parameter 'name')” i „Actual value was …” z ArgumentException.
    /// </summary>
    public static string UserMessage(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var message = exception.Message;
        var technicalSuffix = message.IndexOf(" (Parameter '", StringComparison.Ordinal);

        return technicalSuffix >= 0 ? message[..technicalSuffix] : message;
    }
}
