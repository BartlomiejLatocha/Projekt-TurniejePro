namespace TurniejePro.Domain.Exceptions;

/// <summary>
/// Operacja jest sprzeczna z bieżącym stanem danych (np. terminarz już istnieje, mecz następnej rundy już rozegrany).
/// API zwraca <c>409 Conflict</c>.
/// </summary>
public class ConflictException : DomainException
{
    public ConflictException()
    {
    }

    public ConflictException(string? message)
        : base(message)
    {
    }

    public ConflictException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
