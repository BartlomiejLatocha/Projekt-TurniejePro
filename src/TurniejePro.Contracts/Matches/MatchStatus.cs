using System.Text.Json.Serialization;

namespace TurniejePro.Contracts.Matches;

/// <summary>
/// Status meczu.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MatchStatus
{
    /// <summary>Mecz zaplanowany.</summary>
    Scheduled = 1,

    /// <summary>Mecz w toku.</summary>
    InProgress = 2,

    /// <summary>Mecz zakończony (ma wynik lub jest wolnym losem).</summary>
    Finished = 3,

    /// <summary>Mecz anulowany.</summary>
    Cancelled = 4
}
