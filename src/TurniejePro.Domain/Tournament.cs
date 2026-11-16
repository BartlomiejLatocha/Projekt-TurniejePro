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
    public TournamentStatus Status { get; private set; } = TournamentStatus.Draft;

    /// <summary>Turniej rozpoczęty (w trakcie albo zakończony). Blokuje zmiany formatu i punktacji.</summary>
    public bool IsStarted => Status is TournamentStatus.InProgress or TournamentStatus.Finished;

    public Guid? OrganizerId { get; set; }
    public ScoringRules ScoringRules { get; private set; } = ScoringRules.DefaultCup;

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
        Status = TournamentStatus.Draft;
        ScoringRules = ScoringRules.DefaultFor(Format);
    }

    public Tournament(
        string name,
        string discipline,
        TournamentFormat format,
        DateTimeOffset startsAt,
        int maxTeams,
        Guid? organizerId = null,
        ScoringRules? scoringRules = null)
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
        Status = TournamentStatus.Draft;
        ScoringRules = scoringRules ?? ScoringRules.DefaultFor(format);
    }

    /// <summary>Szkic → Zapisy: drużyny mogą się zgłaszać.</summary>
    public void OpenRegistration()
    {
        if (Status != TournamentStatus.Draft)
        {
            throw new BusinessRuleException("Zapisy można otworzyć tylko dla turnieju w szkicu.");
        }

        Status = TournamentStatus.RegistrationOpen;
    }

    /// <summary>Szkic lub Zapisy → W trakcie. Od tej chwili format i punktacja są zablokowane.</summary>
    public void Start()
    {
        if (IsStarted)
        {
            throw new BusinessRuleException("Turniej został już rozpoczęty.");
        }

        Status = TournamentStatus.InProgress;
    }

    /// <summary>
    /// W trakcie → Zakończony, gdy wszystkie mecze turnieju są rozegrane (bez wolnych losów i anulowanych).
    /// Zwraca true, jeśli turniej właśnie się zakończył.
    /// </summary>
    public bool CompleteIfAllMatchesFinished(IEnumerable<Match> matches)
    {
        if (Status != TournamentStatus.InProgress)
        {
            return false;
        }

        var relevant = matches.Where(m => !m.IsBye && m.Status != MatchStatus.Cancelled).ToList();
        if (relevant.Count == 0 || relevant.Any(m => m.Status != MatchStatus.Finished))
        {
            return false;
        }

        Status = TournamentStatus.Finished;
        return true;
    }

    /// <summary>
    /// Liczba zgłoszeń zajmujących miejsce w limicie drużyn: oczekujących i zaakceptowanych. Odrzucone zgłoszenie
    /// zwalnia miejsce, więc się nie liczy.
    /// </summary>
    public int ActiveTeamsCount => Teams.Count(team => team.Status != TeamRegistrationStatus.Rejected);

    // Zgłoszenia są otwarte w szkicu i w zapisach. Szkic nie blokuje zgłoszeń, żeby istniejące przepływy (np. testy
    // i szybkie turnieje) działały bez dodatkowego kroku. Blokują je dopiero: start i zakończenie turnieju.
    public bool CanRegisterTeam() =>
        Status is TournamentStatus.Draft or TournamentStatus.RegistrationOpen && ActiveTeamsCount < MaxTeams;

    /// <summary>
    /// Zwraca powód, dla którego turniej nie przyjmuje zgłoszeń w podanej chwili, albo null, gdy zgłoszenia są otwarte.
    /// Zgłoszenia zamyka rozpoczęcie turnieju, upływ jego terminu (<see cref="StartsAt"/>) i osiągnięcie limitu drużyn.
    /// Gdy zachodzi kilka powodów naraz, zwracany jest pierwszy z tej listy.
    /// </summary>
    public RegistrationBlockReason? GetRegistrationBlockReason(DateTimeOffset now)
    {
        if (IsStarted)
        {
            return RegistrationBlockReason.TournamentStarted;
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
        int maxTeams,
        ScoringRules? scoringRules = null)
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

        if (maxTeams < ActiveTeamsCount)
        {
            throw new BusinessRuleException("Limit drużyn nie może być mniejszy niż liczba już zarejestrowanych drużyn.");
        }

        if (scoringRules is not null)
        {
            ScoringRules = scoringRules;
        }
        else if (Format != format)
        {
            ScoringRules = ScoringRules.DefaultFor(format);
        }

        Name = name;
        Discipline = discipline;
        Format = format;
        StartsAt = startsAt;
        MaxTeams = maxTeams;
    }

    public void UpdateScoringRules(ScoringRules scoringRules)
    {
        ArgumentNullException.ThrowIfNull(scoringRules);

        if (IsStarted)
        {
            throw new BusinessRuleException("Nie można modyfikować rozpoczętego turnieju.");
        }

        ScoringRules = scoringRules;
    }
}
