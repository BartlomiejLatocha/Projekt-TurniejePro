using TurniejePro.Domain.Enums;

namespace TurniejePro.Domain;

public sealed record ScoringRules
{
    public static ScoringRules Default => new(3, 1, 0, true);
    public static ScoringRules DefaultLeague => new(3, 1, 0, true);
    public static ScoringRules DefaultCup => new(3, 1, 0, false);

    public int PointsForWin { get; }
    public int PointsForDraw { get; }
    public int PointsForLoss { get; }
    public bool AllowDraws { get; }

    public ScoringRules(int pointsForWin, int pointsForDraw, int pointsForLoss, bool allowDraws = true)
    {
        if (pointsForWin < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pointsForWin), "Punkty za wygraną nie mogą być ujemne.");
        }

        if (pointsForDraw < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pointsForDraw), "Punkty za remis nie mogą być ujemne.");
        }

        if (pointsForLoss < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pointsForLoss), "Punkty za porażkę nie mogą być ujemne.");
        }

        PointsForWin = pointsForWin;
        PointsForDraw = pointsForDraw;
        PointsForLoss = pointsForLoss;
        AllowDraws = allowDraws;
    }

    public static ScoringRules DefaultFor(TournamentFormat format) =>
        new(3, 1, 0, format != TournamentFormat.Cup);
}
