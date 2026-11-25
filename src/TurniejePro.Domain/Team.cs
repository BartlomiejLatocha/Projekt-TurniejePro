using TurniejePro.Domain.Enums;
using TurniejePro.Domain.Exceptions;

namespace TurniejePro.Domain;

public class Team
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TournamentId { get; set; }
    public Tournament? Tournament { get; set; }
    public Guid? CaptainId { get; set; }
    public string? ContactEmail { get; set; }

    /// <summary>Numer grupy w fazie grupowej (od 1), null poza fazą grupową.</summary>
    public int? GroupNumber { get; set; }
    public TeamRegistrationStatus Status { get; private set; } = TeamRegistrationStatus.Pending;
    public DateTimeOffset RegisteredAt { get; set; }

    /// <summary>Typ pliku logo (image/png albo image/jpeg), null gdy brak logo.</summary>
    public string? LogoContentType { get; private set; }

    /// <summary>Dane logo drużyny, null gdy brak logo.</summary>
    public byte[]? Logo { get; private set; }

    public bool HasLogo => Logo is not null;

    /// <summary>Maksymalny rozmiar logo: 1 MB.</summary>
    public const int MaxLogoBytes = 1024 * 1024;

    public void SetLogo(string contentType, byte[] data)
    {
        if (contentType is not ("image/png" or "image/jpeg"))
        {
            throw new ArgumentException("Logo musi być plikiem PNG lub JPG.", nameof(contentType));
        }

        if (data is null || data.Length == 0)
        {
            throw new ArgumentException("Plik logo nie może być pusty.", nameof(data));
        }

        if (data.Length > MaxLogoBytes)
        {
            throw new ArgumentException("Logo może mieć najwyżej 1 MB.", nameof(data));
        }

        LogoContentType = contentType;
        Logo = data;
    }

    public ICollection<Player> Players { get; set; } = new List<Player>();

    /// <summary>Najmniejsza liczba zawodników w drużynie.</summary>
    public const int MinPlayers = 1;

    /// <summary>Największa liczba zawodników w drużynie.</summary>
    public const int MaxPlayers = 30;

    /// <summary>Sprawdza, czy liczba zawodników mieści się w dozwolonym zakresie (<see cref="MinPlayers"/>–<see cref="MaxPlayers"/>).</summary>
    public static void ValidatePlayerCount(int count)
    {
        if (count < MinPlayers)
        {
            throw new BusinessRuleException($"Drużyna musi mieć co najmniej {MinPlayers} zawodnika.");
        }

        if (count > MaxPlayers)
        {
            throw new BusinessRuleException($"Drużyna może mieć najwyżej {MaxPlayers} zawodników.");
        }
    }

    /// <summary>
    /// Zastępuje cały skład drużyny. Liczba zawodników i numery na koszulkach są sprawdzane przed zmianą składu.
    /// </summary>
    public void ReplacePlayers(IReadOnlyList<(string FullName, int? JerseyNumber)> players)
    {
        ValidatePlayerCount(players.Count);

        Players.Clear();
        foreach (var (fullName, jerseyNumber) in players)
        {
            AddPlayer(fullName, jerseyNumber);
        }
    }

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
        Status = TeamRegistrationStatus.Pending;
        RegisteredAt = DateTimeOffset.UtcNow;
    }

    public void Accept()
    {
        Status = TeamRegistrationStatus.Accepted;
    }

    public void Reject()
    {
        Status = TeamRegistrationStatus.Rejected;
    }

    public void ResetToPending()
    {
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
            throw new BusinessRuleException($"Zawodnik z numerem {jerseyNumber.Value} już istnieje w tej drużynie.");
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
