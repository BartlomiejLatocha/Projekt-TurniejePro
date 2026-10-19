using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TurniejePro.Api.Controllers;
using TurniejePro.Domain;
using Xunit;

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

public class HealthControllerTests
{
    [Fact]
    public void GetShouldReturnOkWithHealthyStatus()
    {
        // Arrange
        var controller = new HealthController();

        // Act
        var result = controller.Get();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }
}
