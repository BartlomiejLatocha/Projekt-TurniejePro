namespace TurniejePro.Domain.Exceptions;

/// <summary>
/// Szukany obiekt (turniej, drużyna, mecz) nie istnieje. API zwraca <c>404 Not Found</c>.
/// </summary>
public class NotFoundException : DomainException
{
    public NotFoundException()
    {
    }

    public NotFoundException(string? message)
        : base(message)
    {
    }

    public NotFoundException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
