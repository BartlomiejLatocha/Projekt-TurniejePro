using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Data;
using TurniejePro.Domain;
using TurniejePro.Domain.Repositories;
using Xunit;

namespace TurniejePro.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddDataServices_RegistersDbContextAndRepositories()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Data Source=test.db"
            })
            .Build();

        // Act
        services.AddDataServices(configuration);
        using var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetService<TurniejeProDbContext>());
        Assert.NotNull(provider.GetService<IUnitOfWork>());
        Assert.NotNull(provider.GetService<ITournamentRepository>());
        Assert.NotNull(provider.GetService<ITeamRepository>());
        Assert.NotNull(provider.GetService<IRepository<Tournament>>());
    }

    [Fact]
    public void AddDataServices_WithoutConnectionString_UsesDefaultFallback()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        services.AddDataServices(configuration);
        using var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetService<TurniejeProDbContext>());
    }
}
