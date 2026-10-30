using TurniejePro.Domain.Enums;

namespace TurniejePro.Domain;

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

    protected Tournament()
    {
    }

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
        Discipline = string.IsNullOrWhiteSpace(discipline) ? "Ogólna" : discipline;
        Format = format;
        StartsAt = startsAt;
        MaxTeams = maxTeams;
        OrganizerId = organizerId;
        IsStarted = false;
    }

    public void Start()
    {
        if (IsStarted)
        {
            throw new InvalidOperationException("Turniej został już rozpoczęty.");
        }

        IsStarted = true;
    }

    public bool CanRegisterTeam() => !IsStarted && Teams.Count < MaxTeams;

    public void UpdateDetails(
        string name,
        string discipline,
        TournamentFormat format,
        DateTimeOffset startsAt,
        int maxTeams)
    {
        if (IsStarted)
        {
            throw new InvalidOperationException("Nie można modyfikować rozpoczętego turnieju.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nazwa turnieju nie może być pusta.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(discipline))
        {
            throw new ArgumentException("Dyscyplina nie może być pusta.", nameof(discipline));
        }

        if (maxTeams < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTeams), "Turniej musi dopuszczać co najmniej 2 drużyny.");
        }

        if (maxTeams < Teams.Count)
        {
            throw new InvalidOperationException("Limit drużyn nie może być mniejszy niż liczba już zarejestrowanych drużyn.");
        }

        Name = name;
        Discipline = discipline;
        Format = format;
        StartsAt = startsAt;
        MaxTeams = maxTeams;
    }
}
