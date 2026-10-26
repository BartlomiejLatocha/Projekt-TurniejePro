namespace TurniejePro.Domain;

/// <summary>
/// Encja reprezentująca drużynę zgłoszoną do turnieju.
/// </summary>
public class Team
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TournamentId { get; set; }
    public Tournament? Tournament { get; set; }
    public Guid? CaptainId { get; set; }
    public string? ContactEmail { get; set; }
    public bool IsAccepted { get; private set; }
    public DateTimeOffset RegisteredAt { get; set; }

    public ICollection<Player> Players { get; set; } = new List<Player>();

    /// <summary>
    /// Konstruktor dla EF Core.
    /// </summary>
    protected Team()
    {
    }

    /// <summary>
    /// Tworzy nowe zgłoszenie drużyny do turnieju.
    /// </summary>
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
        RegisteredAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Akceptuje udział drużyny w turnieju (akcja organizatora).
    /// </summary>
    public void Accept() => IsAccepted = true;

    /// <summary>
    /// Odrzuca zgłoszenie drużyny.
    /// </summary>
    public void Reject() => IsAccepted = false;

    /// <summary>
    /// Dodaje zawodnika do składu drużyny.
    /// </summary>
    public Player AddPlayer(string fullName, int? jerseyNumber = null)
    {
        var player = new Player(fullName, Id, jerseyNumber);
        Players.Add(player);
        return player;
    }
}
