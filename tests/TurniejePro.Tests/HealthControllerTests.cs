using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TurniejePro.Api.Controllers;
using Xunit;

namespace TurniejePro.Tests;

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
