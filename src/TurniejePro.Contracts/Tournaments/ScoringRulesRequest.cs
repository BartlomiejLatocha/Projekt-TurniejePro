namespace TurniejePro.Contracts.Tournaments;

/// <summary>
/// Punktacja turnieju: punkty za wygraną, remis i porażkę oraz informacja, czy remisy są dozwolone.
/// </summary>
/// <param name="PointsForWin">Punkty za wygraną (0 lub więcej).</param>
/// <param name="PointsForDraw">Punkty za remis (0 lub więcej).</param>
/// <param name="PointsForLoss">Punkty za porażkę (0 lub więcej).</param>
/// <param name="AllowDraws">Czy remisy są dozwolone.</param>
public sealed record ScoringRulesRequest(
    int PointsForWin,
    int PointsForDraw,
    int PointsForLoss,
    bool AllowDraws);
