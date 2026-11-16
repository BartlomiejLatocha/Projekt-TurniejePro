using System.Text.Json.Serialization;

namespace TurniejePro.Contracts.Tournaments;

/// <summary>
/// System rozgrywek turnieju.
/// </summary>
// Serializacja jako tekst ("League", "Cup"), zgodnie z ustaleniami kontraktów.
// Atrybut działa po obu stronach (API i Blazor) bez dodatkowej konfiguracji.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TournamentFormat
{
    /// <summary>Liga: każdy z każdym (round-robin).</summary>
    League = 1,

    /// <summary>Puchar: drabinka z eliminacją.</summary>
    Cup = 2,

    /// <summary>Faza grupowa (każdy z każdym w grupach), potem drabinka z najlepszymi z każdej grupy.</summary>
    GroupsAndCup = 3
}
