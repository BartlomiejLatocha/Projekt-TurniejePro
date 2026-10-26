namespace TurniejePro.Domain;

/// <summary>
/// Encja reprezentująca token kolekcjonerski dla kibiców turnieju (KAN-16).
/// </summary>
public class CollectibleToken
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public Tournament? Tournament { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Konstruktor dla EF Core.
    /// </summary>
    protected CollectibleToken()
    {
    }

    /// <summary>
    /// Tworzy nowy token kolekcjonerski powiązany z danym turniejem.
    /// </summary>
    public CollectibleToken(string code, string name, Guid tournamentId, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Kod tokenu nie może być pusty.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nazwa tokenu nie może być pusta.", nameof(name));
        }

        Id = Guid.NewGuid();
        Code = code.Trim().ToUpperInvariant();
        Name = name;
        TournamentId = tournamentId;
        Description = description;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
