namespace TurniejePro.Domain;

/// <summary>
/// Encja reprezentująca zawodnika w drużynie.
/// </summary>
public class Player
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Team? Team { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int? JerseyNumber { get; set; }

    /// <summary>
    /// Konstruktor dla EF Core.
    /// </summary>
    protected Player()
    {
    }

    /// <summary>
    /// Tworzy nowego zawodnika.
    /// </summary>
    public Player(string fullName, Guid teamId, int? jerseyNumber = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Imię i nazwisko zawodnika nie mogą być puste.", nameof(fullName));
        }

        Id = Guid.NewGuid();
        FullName = fullName;
        TeamId = teamId;
        JerseyNumber = jerseyNumber;
    }
}
