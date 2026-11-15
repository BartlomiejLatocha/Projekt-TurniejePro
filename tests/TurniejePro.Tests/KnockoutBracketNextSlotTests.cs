using TurniejePro.Domain.Scheduling;

namespace TurniejePro.Tests;

/// <summary>
/// Reguła awansu zwycięzcy (KAN-281) istnieje w jednym miejscu: <see cref="KnockoutBracketAdvancer.GetNextSlot"/>.
/// </summary>
public class KnockoutBracketNextSlotTests
{
    [Theory]
    [InlineData(1, 1, 2, 1, true)]
    [InlineData(1, 2, 2, 1, false)]
    [InlineData(1, 3, 2, 2, true)]
    [InlineData(1, 4, 2, 2, false)]
    [InlineData(2, 1, 3, 1, true)]
    [InlineData(2, 2, 3, 1, false)]
    [InlineData(3, 5, 4, 3, true)]
    [InlineData(5, 8, 6, 4, false)]
    public void GetNextSlot_ReturnsNextRoundMatchAndSide(int round, int position, int nextRound, int nextPosition, bool isHome)
    {
        var slot = KnockoutBracketAdvancer.GetNextSlot(round, position);

        Assert.Equal(new BracketSlot(nextRound, nextPosition, isHome), slot);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -3)]
    public void GetNextSlot_RoundOrPositionBelowOne_ThrowsArgumentOutOfRangeException(int round, int position)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => KnockoutBracketAdvancer.GetNextSlot(round, position));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GetNextSlot_TwoNeighbouringMatchesFeedOneMatchAsHomeAndAway(int round)
    {
        for (var pair = 1; pair <= 32; pair++)
        {
            var first = KnockoutBracketAdvancer.GetNextSlot(round, (2 * pair) - 1);
            var second = KnockoutBracketAdvancer.GetNextSlot(round, 2 * pair);

            Assert.Equal((first.RoundNumber, first.Position), (second.RoundNumber, second.Position));
            Assert.Equal(pair, first.Position);
            Assert.True(first.IsHome);
            Assert.False(second.IsHome);
        }
    }
}
