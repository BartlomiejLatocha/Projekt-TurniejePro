using TurniejePro.Domain.Enums;

namespace TurniejePro.Domain;

public class Team
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TournamentId { get; set; }
    public Tournament? Tournament { get; set; }
    public Guid? CaptainId { get; set; }
    public string? ContactEmail { get; set; }
    public bool IsAccepted { get; private set; }
    public TeamRegistrationStatus Status { get; private set; } = TeamRegistrationStatus.Pending;
    public DateTimeOffset RegisteredAt { get; set; }

    public ICollection<Player> Players { get; set; } = new List<Player>();

    protected Team()
    {
    }

    public Team(string name, Guid tournamentId, Guid? captainId = null, string? contactEmail = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nazwa drużyny nie może być pusta.", nameof(name));
        }

        Id = Guid.NewGuid();
        Name = name;
        TournamentId = tournamentId;
        CaptainId = captainId;
        ContactEmail = contactEmail;
        IsAccepted = false;
        Status = TeamRegistrationStatus.Pending;
        RegisteredAt = DateTimeOffset.UtcNow;
    }

    public void Accept()
    {
        IsAccepted = true;
        Status = TeamRegistrationStatus.Accepted;
    }

    public void Reject()
    {
        IsAccepted = false;
        Status = TeamRegistrationStatus.Rejected;
    }

    public void ResetToPending()
    {
        IsAccepted = false;
        Status = TeamRegistrationStatus.Pending;
    }

    public Player AddPlayer(string fullName, int? jerseyNumber = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Imię i nazwisko zawodnika nie mogą być puste.", nameof(fullName));
        }

        if (jerseyNumber is < 1 or > 99)
        {
            throw new ArgumentOutOfRangeException(nameof(jerseyNumber), "Numer na koszulce musi być z zakresu 1-99.");
        }

        if (jerseyNumber.HasValue && Players.Any(p => p.JerseyNumber == jerseyNumber.Value))
        {
            throw new InvalidOperationException($"Zawodnik z numerem {jerseyNumber.Value} już istnieje w tej drużynie.");
        }

        var player = new Player(fullName, Id, jerseyNumber);
        Players.Add(player);
        return player;
    }

    public bool RemovePlayer(Guid playerId)
    {
        var player = Players.FirstOrDefault(p => p.Id == playerId);
        if (player is null)
        {
            return false;
        }

        return Players.Remove(player);
    }

    public void UpdateDetails(string name, string? contactEmail)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nazwa drużyny nie może być pusta.", nameof(name));
        }

        Name = name;
        ContactEmail = contactEmail;
    }
}
