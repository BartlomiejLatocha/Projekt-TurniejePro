using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TurniejePro.Data;
using TurniejePro.Data.Repositories;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using Xunit;

namespace TurniejePro.Tests;

public class RepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TurniejeProDbContext> _options;

    public RepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<TurniejeProDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new TurniejeProDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GenericRepository_AddAndGetById_ShouldPersistEntity()
    {
        // Arrange
        await using var context = new TurniejeProDbContext(_options);
        var repository = new Repository<Tournament>(context);
        var tournament = new Tournament("Turniej Testowy");

        // Act
        await repository.AddAsync(tournament);
        await context.SaveChangesAsync();

        var retrieved = await repository.GetByIdAsync(tournament.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("Turniej Testowy", retrieved.Name);
    }

    [Fact]
    public async Task GenericRepository_GetAll_ShouldReturnAllEntities()
    {
        // Arrange
        await using var context = new TurniejeProDbContext(_options);
        var repository = new Repository<Tournament>(context);
        await repository.AddAsync(new Tournament("T1"));
        await repository.AddAsync(new Tournament("T2"));
        await context.SaveChangesAsync();

        // Act
        var all = await repository.GetAllAsync();

        // Assert
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GenericRepository_Update_ShouldModifyEntity()
    {
        // Arrange
        await using var context = new TurniejeProDbContext(_options);
        var repository = new Repository<Tournament>(context);
        var tournament = new Tournament("Przed Zmianą");
        await repository.AddAsync(tournament);
        await context.SaveChangesAsync();

        // Act
        tournament.Name = "Po Zmianie";
        repository.Update(tournament);
        await context.SaveChangesAsync();

        var updated = await repository.GetByIdAsync(tournament.Id);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("Po Zmianie", updated.Name);
    }

    [Fact]
    public async Task GenericRepository_Delete_ShouldRemoveEntity()
    {
        // Arrange
        await using var context = new TurniejeProDbContext(_options);
        var repository = new Repository<Tournament>(context);
        var tournament = new Tournament("Do Usunięcia");
        await repository.AddAsync(tournament);
        await context.SaveChangesAsync();

        // Act
        repository.Delete(tournament);
        await context.SaveChangesAsync();

        var deleted = await repository.GetByIdAsync(tournament.Id);

        // Assert
        Assert.Null(deleted);
    }

    [Fact]
    public async Task TournamentRepository_GetWithDetailsAsync_ShouldIncludeRelatedData()
    {
        // Arrange
        await using var context = new TurniejeProDbContext(_options);
        var repository = new TournamentRepository(context);

        var tournament = new Tournament("Wielki Turniej", "Siatkówka", TournamentFormat.League, DateTimeOffset.UtcNow, 8);
        var team = new Team("Sokoły", tournament.Id);
        team.AddPlayer("Jan Nowak", 5);
        var match = new Match(tournament.Id, 1, team.Id, null);
        var token = new CollectibleToken("SOKOL", "Sokół", tournament.Id);

        context.Tournaments.Add(tournament);
        context.Teams.Add(team);
        context.Matches.Add(match);
        context.CollectibleTokens.Add(token);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetWithDetailsAsync(tournament.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Teams);
        Assert.Single(result.Teams.First().Players);
        Assert.Single(result.Matches);
        Assert.Single(result.Tokens);
    }

    [Fact]
    public async Task TeamRepository_GetByTournamentIdAsync_ShouldReturnTeamsForSpecificTournament()
    {
        // Arrange
        await using var context = new TurniejeProDbContext(_options);
        var repository = new TeamRepository(context);

        var tournament1 = new Tournament("Turniej 1");
        var tournament2 = new Tournament("Turniej 2");

        var team1 = new Team("Team T1", tournament1.Id);
        team1.AddPlayer("Gracz 1", 1);
        var team2 = new Team("Team T2", tournament2.Id);

        context.Tournaments.AddRange(tournament1, tournament2);
        context.Teams.AddRange(team1, team2);
        await context.SaveChangesAsync();

        // Act
        var teamsForT1 = await repository.GetByTournamentIdAsync(tournament1.Id);

        // Assert
        Assert.Single(teamsForT1);
        Assert.Equal("Team T1", teamsForT1[0].Name);
        Assert.Single(teamsForT1[0].Players);
    }

    [Fact]
    public async Task UnitOfWork_ShouldPerformTransactionalOperationsAcrossRepositories()
    {
        // Arrange
        await using var context = new TurniejeProDbContext(_options);
        var uow = new UnitOfWork(context);

        var tournament = new Tournament("Turniej UoW");
        var team = new Team("Drużyna UoW", tournament.Id);
        var match = new Match(tournament.Id, 1);
        var token = new CollectibleToken("UOW1", "Token UoW", tournament.Id);

        // Act
        await uow.Tournaments.AddAsync(tournament);
        await uow.Teams.AddAsync(team);
        await uow.Matches.AddAsync(match);
        await uow.Tokens.AddAsync(token);
        var affectedRows = await uow.SaveChangesAsync();

        // Assert
        Assert.True(affectedRows > 0);

        var savedTournament = await uow.Tournaments.GetByIdAsync(tournament.Id);
        var savedTeam = await uow.Teams.GetByIdAsync(team.Id);
        var savedMatch = await uow.Matches.GetByIdAsync(match.Id);
        var savedToken = await uow.Tokens.GetByIdAsync(token.Id);

        Assert.NotNull(savedTournament);
        Assert.NotNull(savedTeam);
        Assert.NotNull(savedMatch);
        Assert.NotNull(savedToken);
    }
}
