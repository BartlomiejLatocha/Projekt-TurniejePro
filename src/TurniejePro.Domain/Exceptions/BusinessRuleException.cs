namespace TurniejePro.Domain.Exceptions;

/// <summary>
/// Żądanie łamie regułę biznesową (np. remis w pucharze, zmiana rozpoczętego turnieju, za mało drużyn do terminarza).
/// API zwraca <c>400 Bad Request</c>.
/// </summary>
public class BusinessRuleException : DomainException
{
    public BusinessRuleException()
    {
    }

    public BusinessRuleException(string? message)
        : base(message)
    {
    }

    public BusinessRuleException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
