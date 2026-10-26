using TurniejePro.Domain.Enums;

namespace TurniejePro.Domain;

/// <summary>
/// Encja reprezentująca turniej sportowy.
/// </summary>
public class Tournament
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Discipline { get; set; } = string.Empty;
    public TournamentFormat Format { get; set; } = TournamentFormat.Cup;
    public DateTimeOffset StartsAt { get; set; }
    public int MaxTeams { get; set; } = 16;
    public bool IsStarted { get; private set; }
    public Guid? OrganizerId { get; set; }

    public ICollection<Team> Teams { get; set; } = new List<Team>();
    public ICollection<Match> Matches { get; set; } = new List<Match>();
    public ICollection<CollectibleToken> Tokens { get; set; } = new List<CollectibleToken>();

    /// <summary>
    /// Konstruktor bezparametrowy dla Entity Framework Core.
    /// </summary>
    protected Tournament()
    {
    }

    /// <summary>
    /// Uproszczony konstruktor pomocniczy.
    /// </summary>
    public Tournament(string name)
    {
        Id = Guid.NewGuid();
        Name = name;
        Discipline = "Ogólna";
        Format = TournamentFormat.Cup;
        StartsAt = DateTimeOffset.UtcNow;
        MaxTeams = 16;
        IsStarted = false;
    }

    /// <summary>
    /// Pełny konstruktor domenowy turnieju.
    /// </summary>
    public Tournament(
        string name,
        string discipline,
        TournamentFormat format,
        DateTimeOffset startsAt,
        int maxTeams,
        Guid? organizerId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nazwa turnieju nie może być pusta.", nameof(name));
        }

        if (maxTeams < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTeams), "Turniej musi dopuszczać co najmniej 2 drużyny.");
        }

        Id = Guid.NewGuid();
        Name = name;
        Discipline = discipline;
        Format = format;
        StartsAt = startsAt;
        MaxTeams = maxTeams;
        OrganizerId = organizerId;
        IsStarted = false;
    }

    /// <summary>
    /// Rozpoczyna turniej. Po rozpoczęciu rejestracja drużyn jest zablokowana.
    /// </summary>
    public void Start()
    {
        if (IsStarted)
        {
            throw new InvalidOperationException("Turniej został już rozpoczęty.");
        }

        IsStarted = true;
    }

    /// <summary>
    /// Sprawdza, czy można jeszcze zarejestrować nową drużynę w turnieju.
    /// </summary>
    public bool CanRegisterTeam() => !IsStarted && Teams.Count < MaxTeams;
}
