using TurniejePro.Domain.Exceptions;

namespace TurniejePro.Domain.Scheduling;

/// <summary>
/// Wyjątek rzucany przy próbie ponownego wygenerowania terminarza dla turnieju, który posiada już wygenerowany terminarz.
/// </summary>
public class ScheduleAlreadyExistsException : ConflictException
{
    public ScheduleAlreadyExistsException()
    {
    }

    public ScheduleAlreadyExistsException(string? message)
        : base(message)
    {
    }

    public ScheduleAlreadyExistsException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    public ScheduleAlreadyExistsException(Guid tournamentId)
        : base($"Terminarz dla turnieju o ID '{tournamentId}' został już wygenerowany.")
    {
    }
}
