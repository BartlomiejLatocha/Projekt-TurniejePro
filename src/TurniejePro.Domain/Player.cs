namespace TurniejePro.Domain;

public class Player
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Team? Team { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int? JerseyNumber { get; set; }

    protected Player()
    {
    }

    public Player(string fullName, Guid teamId, int? jerseyNumber = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Imię i nazwisko zawodnika nie mogą być puste.", nameof(fullName));
        }

        if (jerseyNumber is < 1 or > 99)
        {
            throw new ArgumentOutOfRangeException(nameof(jerseyNumber), "Numer na koszulce musi być z zakresu 1-99.");
        }

        Id = Guid.NewGuid();
        FullName = fullName;
        TeamId = teamId;
        JerseyNumber = jerseyNumber;
    }

    public void Update(string fullName, int? jerseyNumber = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Imię i nazwisko zawodnika nie mogą być puste.", nameof(fullName));
        }

        if (jerseyNumber is < 1 or > 99)
        {
            throw new ArgumentOutOfRangeException(nameof(jerseyNumber), "Numer na koszulce musi być z zakresu 1-99.");
        }

        FullName = fullName;
        JerseyNumber = jerseyNumber;
    }
}
