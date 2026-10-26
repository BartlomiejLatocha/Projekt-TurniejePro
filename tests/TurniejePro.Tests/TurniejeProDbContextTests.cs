using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TurniejePro.Data;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;
using Xunit;

namespace TurniejePro.Tests;

public class TurniejeProDbContextTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TurniejeProDbContext> _options;

    public TurniejeProDbContextTests()
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
    public async Task CanInsertAndRetrieveTournamentWithRelatedEntities()
    {
        // Arrange
        var tournament = new Tournament("Turniej Zimowy", "Siatkówka", TournamentFormat.League, DateTimeOffset.UtcNow, 8);
        var team = new Team("Sparta", tournament.Id, Guid.NewGuid(), "sparta@turniej.pl");
        var player = team.AddPlayer("Piotr Zieliński", 8);
        var match = new Match(tournament.Id, 1, team.Id, null);
        var token = new CollectibleToken("ZIMA2026", "Zimowy Medal", tournament.Id, "Token za udział");

        await using (var context = new TurniejeProDbContext(_options))
        {
            context.Tournaments.Add(tournament);
            context.Teams.Add(team);
            context.Matches.Add(match);
            context.CollectibleTokens.Add(token);
            await context.SaveChangesAsync();
        }

        // Act & Assert
        await using (var context = new TurniejeProDbContext(_options))
        {
            var savedTournament = await context.Tournaments
                .Include(t => t.Teams)
                    .ThenInclude(tm => tm.Players)
                .Include(t => t.Matches)
                .Include(t => t.Tokens)
                .FirstOrDefaultAsync(t => t.Id == tournament.Id);

            Assert.NotNull(savedTournament);
            Assert.Equal("Turniej Zimowy", savedTournament.Name);
            Assert.Single(savedTournament.Teams);
            Assert.Single(savedTournament.Teams.First().Players);
            Assert.Equal("Piotr Zieliński", savedTournament.Teams.First().Players.First().FullName);
            Assert.Single(savedTournament.Matches);
            Assert.Single(savedTournament.Tokens);
            Assert.Equal("ZIMA2026", savedTournament.Tokens.First().Code);
        }
    }

    [Fact]
    public async Task DeletingTournament_ShouldCascadeDeleteTeamsMatchesAndTokens()
    {
        // Arrange
        var tournament = new Tournament("Turniej do usunięcia", "Koszykówka", TournamentFormat.Cup, DateTimeOffset.UtcNow, 4);
        var team = new Team("Delfiny", tournament.Id);
        team.AddPlayer("Marek Nowak", 12);
        var match = new Match(tournament.Id, 1, team.Id, null);
        var token = new CollectibleToken("DELF2026", "Delfin", tournament.Id);

        await using (var context = new TurniejeProDbContext(_options))
        {
            context.Tournaments.Add(tournament);
            context.Teams.Add(team);
            context.Matches.Add(match);
            context.CollectibleTokens.Add(token);
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new TurniejeProDbContext(_options))
        {
            var savedTournament = await context.Tournaments.FindAsync(tournament.Id);
            Assert.NotNull(savedTournament);
            context.Tournaments.Remove(savedTournament);
            await context.SaveChangesAsync();
        }

        // Assert
        await using (var context = new TurniejeProDbContext(_options))
        {
            Assert.Empty(await context.Tournaments.ToListAsync());
            Assert.Empty(await context.Teams.ToListAsync());
            Assert.Empty(await context.Players.ToListAsync());
            Assert.Empty(await context.Matches.ToListAsync());
            Assert.Empty(await context.CollectibleTokens.ToListAsync());
        }
    }

    [Fact]
    public async Task DuplicateTokenCodeInSameTournament_ShouldThrowDbUpdateException()
    {
        // Arrange
        var tournament = new Tournament("Turniej Tokenowy");
        var token1 = new CollectibleToken("UNIKALNY", "Token 1", tournament.Id);
        var token2 = new CollectibleToken("UNIKALNY", "Token 2", tournament.Id);

        await using var context = new TurniejeProDbContext(_options);
        context.Tournaments.Add(tournament);
        context.CollectibleTokens.AddRange(token1, token2);

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
