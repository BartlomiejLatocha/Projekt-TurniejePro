using Xunit;
using TurniejePro.Domain;

namespace TurniejePro.Tests;

public class TournamentTests
{
    [Fact]
    public void TournamentShouldInitializeWithCorrectNameAndStatus()
    {
        // Arrange (Przygotowanie danych)
        var tournamentName = "Mistrzostwa Polski w eSporcie";

        // Act (Działanie - tworzymy obiekt z domeny)
        var tournament = new Tournament(tournamentName);

        // Assert (Sprawdzenie - weryfikujemy stan)
        Assert.Equal(tournamentName, tournament.Name);
        Assert.False(tournament.IsStarted); // Nowy turniej nie powinien być jeszcze rozpoczęty
    }
}
