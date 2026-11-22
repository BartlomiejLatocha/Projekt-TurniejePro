using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Exceptions;

namespace TurniejePro.Domain;

public class Tournament
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Discipline { get; set; } = string.Empty;
    public TournamentFormat Format { get; set; } = TournamentFormat.Cup;
    public DateTimeOffset StartsAt { get; set; }
    public int MaxTeams { get; set; } = 16;
    public TournamentStatus Status { get; private set; } = TournamentStatus.RegistrationOpen;
    public bool IsStarted => Status is TournamentStatus.InProgress or TournamentStatus.Finished;
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
        Status = TournamentStatus.RegistrationOpen;
    }

    public Tournament(
        string name,
        string discipline,
        TournamentFormat format,
        DateTimeOffset startsAt,
        int maxTeams,
        Guid? organizerId = null,
        TournamentStatus status = TournamentStatus.RegistrationOpen)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nazwa turnieju nie może być pusta.", nameof(name));
        }

        if (maxTeams < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTeams), "Turniej musi dopuszczać co najmniej 2 drużyny.");
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "Nieprawidłowy status turnieju.");
        }

        Id = Guid.NewGuid();
        Name = name;
        Discipline = string.IsNullOrWhiteSpace(discipline) ? "Ogólna" : discipline;
        Format = format;
        StartsAt = startsAt;
        MaxTeams = maxTeams;
        OrganizerId = organizerId;
        Status = status;
    }

    public void OpenRegistration()
    {
        if (Status != TournamentStatus.Draft)
        {
            throw new BusinessRuleException("Zapisy można otworzyć tylko dla turnieju w statusie szkicu.");
        }

        Status = TournamentStatus.RegistrationOpen;
    }

    public void Start()
    {
        if (IsStarted)
        {
            throw new BusinessRuleException("Turniej został już rozpoczęty.");
        }

        if (Status != TournamentStatus.RegistrationOpen)
        {
            throw new BusinessRuleException("Nie można rozpocząć turnieju, który nie ma otwartych zapisów.");
        }

        Status = TournamentStatus.InProgress;
    }

    public void Finish()
    {
        if (Status != TournamentStatus.InProgress)
        {
            throw new BusinessRuleException("Można zakończyć tylko turniej będący w trakcie.");
        }

        Status = TournamentStatus.Finished;
    }

    public bool CanRegisterTeam() => Status == TournamentStatus.RegistrationOpen && Teams.Count < MaxTeams;

    /// <summary>
    /// Zwraca powód, dla którego turniej nie przyjmuje zgłoszeń w podanej chwili, albo null, gdy zgłoszenia są otwarte.
    /// Zgłoszenia zamyka rozpoczęcie turnieju, status szkicu, upływ terminu (<see cref="StartsAt"/>) i osiągnięcie limitu drużyn.
    /// Gdy zachodzi kilka powodów naraz, zwracany jest pierwszy z tej listy.
    /// </summary>
    public RegistrationBlockReason? GetRegistrationBlockReason(DateTimeOffset now)
    {
        if (IsStarted)
        {
            return RegistrationBlockReason.TournamentStarted;
        }

        if (Status != TournamentStatus.RegistrationOpen)
        {
            return RegistrationBlockReason.RegistrationNotOpen;
        }

        if (now >= StartsAt)
        {
            return RegistrationBlockReason.DeadlinePassed;
        }

        if (!CanRegisterTeam())
        {
            return RegistrationBlockReason.LimitReached;
        }

        return null;
    }

    public void UpdateDetails(
        string name,
        string discipline,
        TournamentFormat format,
        DateTimeOffset startsAt,
        int maxTeams)
    {
        if (IsStarted)
        {
            throw new BusinessRuleException("Nie można modyfikować rozpoczętego turnieju.");
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
            throw new BusinessRuleException("Limit drużyn nie może być mniejszy niż liczba już zarejestrowanych drużyn.");
        }

        Name = name;
        Discipline = discipline;
        Format = format;
        StartsAt = startsAt;
        MaxTeams = maxTeams;
    }
}
