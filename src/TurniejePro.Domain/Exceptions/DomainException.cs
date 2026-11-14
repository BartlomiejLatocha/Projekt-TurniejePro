namespace TurniejePro.Domain.Exceptions;

/// <summary>
/// Bazowy wyjątek logiki domenowej. Komunikat jest przeznaczony dla użytkownika (po polsku), bo API zwraca go
/// w polu <c>detail</c> odpowiedzi ProblemDetails. Kod HTTP wynika z typu pochodnego:
/// <see cref="NotFoundException"/> (404), <see cref="ConflictException"/> (409), <see cref="BusinessRuleException"/> (400).
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException()
    {
    }

    protected DomainException(string? message)
        : base(message)
    {
    }

    protected DomainException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
