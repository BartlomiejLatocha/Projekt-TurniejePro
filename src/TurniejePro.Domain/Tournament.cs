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
    /// Ręczne rozpoczęcie turnieju (przycisk „Rozpocznij turniej”). Wymaga wygenerowanego terminarza, bo bez meczów
    /// turnieju nie da się rozegrać, a po starcie nie można już akceptować zgłoszeń. Zwykle ręczny start nie jest
    /// potrzebny: wygenerowanie terminarza rozpoczyna turniej samo. Wymaga wczytanych <see cref="Matches"/>.
    /// </summary>
    public void StartWithExistingSchedule()
    {
        if (Matches.Count == 0)
        {
            throw new BusinessRuleException("Turniej rozpoczyna się po wygenerowaniu terminarza. Najpierw wygeneruj terminarz.");
        }

        Start();
    }

    /// <summary>
    /// W trakcie → Zapisy po usunięciu terminarza: organizator może znów rozpatrywać zgłoszenia i wygenerować
    /// terminarz od nowa. W szkicu i w zapisach nic nie zmienia. Zakończonego turnieju nie można cofnąć.
    /// </summary>
    public void ReturnToRegistration()
    {
        if (Status == TournamentStatus.Finished)
        {
            throw new BusinessRuleException("Zakończonego turnieju nie można cofnąć do zapisów.");
        }

        if (Status == TournamentStatus.InProgress)
        {
            Status = TournamentStatus.RegistrationOpen;
        }
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

    /// <summary>
    /// Zmienia dane turnieju. Co wolno zmienić, zależy od statusu:
    /// Szkic – wszystko; Zapisy – nazwę, termin rozpoczęcia i limit drużyn; W trakcie – tylko nazwę; Zakończony – nic.
    /// Administrator (<paramref name="asAdmin"/>) pomija ograniczenia statusu, ale nie reguły spójności danych:
    /// formatu nie zmieni nikt, gdy istnieje terminarz, a limit nie spadnie poniżej liczby zgłoszeń.
    /// Pole przesłane bez zmian nie jest traktowane jako zmiana, więc klient może wysyłać cały formularz.
    /// Formatu przy istniejącym terminarzu pilnuje <see cref="Matches"/>, więc turniej musi mieć wczytane mecze.
    /// </summary>
    public void UpdateDetails(
        string name,
        string discipline,
        TournamentFormat format,
        DateTimeOffset startsAt,
        int maxTeams,
        ScoringRules? scoringRules = null,
        bool asAdmin = false)
    {
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

        var changed = new List<string>();
        if (!string.Equals(Name, name, StringComparison.Ordinal))
        {
            changed.Add(FieldName);
        }

        if (!string.Equals(Discipline, discipline, StringComparison.OrdinalIgnoreCase))
        {
            changed.Add(FieldDiscipline);
        }

        if (Format != format)
        {
            changed.Add(FieldFormat);
        }

        // Tolerancja minuty: formularz może odesłać ten sam termin z inną precyzją (sekundy, milisekundy)
        var startsAtChanged = (startsAt - StartsAt).Duration() >= TimeSpan.FromMinutes(1);
        if (startsAtChanged)
        {
            changed.Add(FieldStartsAt);
        }

        if (MaxTeams != maxTeams)
        {
            changed.Add(FieldMaxTeams);
        }

        if (scoringRules is not null && scoringRules != ScoringRules)
        {
            changed.Add(FieldScoringRules);
        }

        EnsureCanChange(changed, asAdmin);

        if (changed.Contains(FieldFormat) && Matches.Count > 0)
        {
            throw new BusinessRuleException("Nie można zmienić formatu turnieju, który ma terminarz. Najpierw usuń terminarz.");
        }

        if (startsAtChanged && startsAt <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("Termin rozpoczęcia musi być w przyszłości.", nameof(startsAt));
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

    /// <summary>
    /// Zmienia punktację. Organizator może to zrobić tylko w szkicu (drużyny zapisują się na konkretne zasady),
    /// administrator w każdym statusie (tabela przeliczy się z meczów według nowych zasad).
    /// </summary>
    public void UpdateScoringRules(ScoringRules scoringRules, bool asAdmin = false)
    {
        ArgumentNullException.ThrowIfNull(scoringRules);

        if (scoringRules != ScoringRules)
        {
            EnsureCanChange([FieldScoringRules], asAdmin);
        }

        ScoringRules = scoringRules;
    }

    // Nazwy pól w komunikatach dla użytkownika
    private const string FieldName = "nazwa";
    private const string FieldDiscipline = "dyscyplina";
    private const string FieldFormat = "format";
    private const string FieldStartsAt = "termin rozpoczęcia";
    private const string FieldMaxTeams = "limit drużyn";
    private const string FieldScoringRules = "punktacja";

    /// <summary>
    /// Pola, które organizator może zmienić w danym statusie. Szkic: wszystkie.
    /// </summary>
    private static string[]? EditableFieldsIn(TournamentStatus status) => status switch
    {
        TournamentStatus.Draft => null,
        TournamentStatus.RegistrationOpen => [FieldName, FieldStartsAt, FieldMaxTeams],
        TournamentStatus.InProgress => [FieldName],
        _ => []
    };

    private void EnsureCanChange(List<string> changed, bool asAdmin)
    {
        if (asAdmin || changed.Count == 0 || EditableFieldsIn(Status) is not { } editable)
        {
            return;
        }

        var blocked = changed.Where(field => !editable.Contains(field)).ToList();
        if (blocked.Count == 0)
        {
            return;
        }

        var statusName = Status == TournamentStatus.RegistrationOpen ? "Zapisy" : "W trakcie";

        var allowed = editable.Length == 0
            ? "Zakończonego turnieju nie można edytować."
            : $"W statusie „{statusName}” można zmienić tylko: {string.Join(", ", editable)}.";

        throw new BusinessRuleException($"{allowed} Zablokowane zmiany: {string.Join(", ", blocked)}.");
    }
}
