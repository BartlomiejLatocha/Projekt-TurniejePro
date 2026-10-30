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
        await using var context = new TurniejeProDbContext(_options);
        var repository = new Repository<Tournament>(context);
        var tournament = new Tournament("Turniej Testowy");

        await repository.AddAsync(tournament);
        await context.SaveChangesAsync();

        var retrieved = await repository.GetByIdAsync(tournament.Id);

        Assert.NotNull(retrieved);
        Assert.Equal("Turniej Testowy", retrieved.Name);
    }

    [Fact]
    public async Task GenericRepository_GetAll_ShouldReturnAllEntities()
    {
        await using var context = new TurniejeProDbContext(_options);
        var repository = new Repository<Tournament>(context);
        await repository.AddAsync(new Tournament("T1"));
        await repository.AddAsync(new Tournament("T2"));
        await context.SaveChangesAsync();

        var all = await repository.GetAllAsync();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GenericRepository_Update_ShouldModifyEntity()
    {
        await using var context = new TurniejeProDbContext(_options);
        var repository = new Repository<Tournament>(context);
        var tournament = new Tournament("Przed Zmianą");
        await repository.AddAsync(tournament);
        await context.SaveChangesAsync();

        tournament.Name = "Po Zmianie";
        repository.Update(tournament);
        await context.SaveChangesAsync();

        var updated = await repository.GetByIdAsync(tournament.Id);

        Assert.NotNull(updated);
        Assert.Equal("Po Zmianie", updated.Name);
    }

    [Fact]
    public async Task GenericRepository_Delete_ShouldRemoveEntity()
    {
        await using var context = new TurniejeProDbContext(_options);
        var repository = new Repository<Tournament>(context);
        var tournament = new Tournament("Do Usunięcia");
        await repository.AddAsync(tournament);
        await context.SaveChangesAsync();

        repository.Delete(tournament);
        await context.SaveChangesAsync();

        var deleted = await repository.GetByIdAsync(tournament.Id);

        Assert.Null(deleted);
    }

    [Fact]
    public async Task TournamentRepository_GetWithDetailsAsync_ShouldIncludeRelatedData()
    {
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

        var result = await repository.GetWithDetailsAsync(tournament.Id);

        Assert.NotNull(result);
        Assert.Single(result.Teams);
        Assert.Single(result.Teams.First().Players);
        Assert.Single(result.Matches);
        Assert.Single(result.Tokens);
    }

    [Fact]
    public async Task TournamentRepository_GetByOrganizerIdAsync_ShouldReturnTournamentsForOrganizer()
    {
        await using var context = new TurniejeProDbContext(_options);
        var repository = new TournamentRepository(context);
        var organizerId = Guid.NewGuid();

        var t1 = new Tournament("T1", "Piłka", TournamentFormat.League, DateTimeOffset.UtcNow, 8, organizerId);
        var t2 = new Tournament("T2", "Koszykówka", TournamentFormat.Cup, DateTimeOffset.UtcNow, 16, organizerId);
        var other = new Tournament("Other", "Szachy", TournamentFormat.Cup, DateTimeOffset.UtcNow, 4, Guid.NewGuid());

        context.Tournaments.AddRange(t1, t2, other);
        await context.SaveChangesAsync();

        var results = await repository.GetByOrganizerIdAsync(organizerId);

        Assert.Equal(2, results.Count);
        Assert.All(results, t => Assert.Equal(organizerId, t.OrganizerId));
    }

    [Fact]
    public async Task TournamentRepository_GetPagedAsync_ShouldPaginateAndFilterBySearchTerm()
    {
        await using var context = new TurniejeProDbContext(_options);
        var repository = new TournamentRepository(context);

        for (int i = 1; i <= 5; i++)
        {
            context.Tournaments.Add(new Tournament($"Mistrzostwa {i}", "Esport", TournamentFormat.Cup, DateTimeOffset.UtcNow.AddHours(i), 8));
        }
        context.Tournaments.Add(new Tournament("Turniej Szachowy", "Szachy", TournamentFormat.League, DateTimeOffset.UtcNow, 8));
        await context.SaveChangesAsync();

        var (items, totalCount) = await repository.GetPagedAsync(1, 3, "esport");

        Assert.Equal(5, totalCount);
        Assert.Equal(3, items.Count);
        Assert.All(items, t => Assert.Equal("Esport", t.Discipline));
    }

    [Fact]
    public async Task TournamentRepository_ExistsAsync_ShouldReturnCorrectStatus()
    {
        await using var context = new TurniejeProDbContext(_options);
        var repository = new TournamentRepository(context);
        var tournament = new Tournament("Istniejący");

        context.Tournaments.Add(tournament);
        await context.SaveChangesAsync();

        var exists = await repository.ExistsAsync(tournament.Id);
        var notExists = await repository.ExistsAsync(Guid.NewGuid());

        Assert.True(exists);
        Assert.False(notExists);
    }

    [Fact]
    public async Task TeamRepository_GetByTournamentIdAsync_ShouldReturnTeamsForSpecificTournament()
    {
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

        var teamsForT1 = await repository.GetByTournamentIdAsync(tournament1.Id);

        Assert.Single(teamsForT1);
        Assert.Equal("Team T1", teamsForT1[0].Name);
        Assert.Single(teamsForT1[0].Players);
    }

    [Fact]
    public async Task UnitOfWork_ShouldPerformTransactionalOperationsAcrossRepositories()
    {
        await using var context = new TurniejeProDbContext(_options);
        var uow = new UnitOfWork(context);

        var tournament = new Tournament("Turniej UoW");
        var team = new Team("Drużyna UoW", tournament.Id);
        var match = new Match(tournament.Id, 1);
        var token = new CollectibleToken("UOW1", "Token UoW", tournament.Id);

        await uow.Tournaments.AddAsync(tournament);
        await uow.Teams.AddAsync(team);
        await uow.Matches.AddAsync(match);
        await uow.Tokens.AddAsync(token);
        var affectedRows = await uow.SaveChangesAsync();

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
