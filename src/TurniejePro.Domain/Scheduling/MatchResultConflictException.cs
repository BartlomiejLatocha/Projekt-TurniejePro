using TurniejePro.Domain.Exceptions;

namespace TurniejePro.Domain.Scheduling;

/// <summary>
/// Wyjątek rzucany w przypadku konfliktu biznesowego podczas próby wprowadzenia wyniku meczu
/// (np. mecz anulowany, wolny los, brak drużyn, lub edycja po rozpoczęciu kolejnej rundy w pucharze).
/// </summary>
public class MatchResultConflictException : ConflictException
{
    public MatchResultConflictException()
    {
    }

    public MatchResultConflictException(string? message)
        : base(message)
    {
    }

    public MatchResultConflictException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
