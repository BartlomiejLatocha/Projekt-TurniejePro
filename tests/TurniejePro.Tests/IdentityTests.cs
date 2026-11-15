using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TurniejePro.Data;
using TurniejePro.Data.Identity;
using Xunit;

namespace TurniejePro.Tests;

public class IdentityTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TurniejeProDbContext> _options;

    public IdentityTests()
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
    public void ApplicationUser_Initialization_SetsProperties()
    {
        // Arrange & Act
        var user = new ApplicationUser
        {
            UserName = "jan.kowalski",
            Email = "jan@turniejepro.pl",
            FirstName = "Jan",
            LastName = "Kowalski"
        };

        // Assert
        Assert.Equal("jan.kowalski", user.UserName);
        Assert.Equal("jan@turniejepro.pl", user.Email);
        Assert.Equal("Jan", user.FirstName);
        Assert.Equal("Kowalski", user.LastName);
        Assert.True(user.CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void UserRoles_ContainsRequiredRoleConstants()
    {
        // Assert
        Assert.Equal("Admin", UserRoles.Admin);
        Assert.Equal("User", UserRoles.User);

        Assert.Equal(2, UserRoles.All.Count);
        Assert.Contains(UserRoles.Admin, UserRoles.All);
        Assert.Contains(UserRoles.User, UserRoles.All);
    }

    [Fact]
    public async Task RoleSeeder_SeedsAllRolesSuccessfully()
    {
        // Arrange
        await using var context = new TurniejeProDbContext(_options);
        var roleStore = new RoleStore<IdentityRole<Guid>, TurniejeProDbContext, Guid>(context);
        var roleManager = new RoleManager<IdentityRole<Guid>>(
            roleStore,
            null!,
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole<Guid>>>.Instance);

        // Act
        await RoleSeeder.SeedRolesAsync(roleManager);

        // Assert
        var allRoles = await context.Roles.ToListAsync();
        Assert.Equal(2, allRoles.Count);
        Assert.All(UserRoles.All, role => Assert.Contains(allRoles, r => r.Name == role));

        // Act again (idempotency check - calling twice should not duplicate roles)
        await RoleSeeder.SeedRolesAsync(roleManager);
        var rolesAfterSecondRun = await context.Roles.ToListAsync();
        Assert.Equal(2, rolesAfterSecondRun.Count);
    }

    [Fact]
    public async Task RoleSeeder_NullRoleManager_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => RoleSeeder.SeedRolesAsync(null!));
    }

    [Fact]
    public async Task CanPersistUserAndRoleInDbContext()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "organizator1",
            Email = "org@turniejepro.pl",
            FirstName = "Adam",
            LastName = "Bednarski"
        };

        var role = new IdentityRole<Guid>(UserRoles.User)
        {
            Id = Guid.NewGuid(),
            NormalizedName = UserRoles.User.ToUpperInvariant()
        };

        var userRole = new IdentityUserRole<Guid>
        {
            UserId = user.Id,
            RoleId = role.Id
        };

        // Act
        await using (var context = new TurniejeProDbContext(_options))
        {
            context.Users.Add(user);
            context.Roles.Add(role);
            context.UserRoles.Add(userRole);
            await context.SaveChangesAsync();
        }

        // Assert
        await using (var context = new TurniejeProDbContext(_options))
        {
            var savedUser = await context.Users.FindAsync(user.Id);
            var savedRole = await context.Roles.FindAsync(role.Id);
            var savedUserRole = await context.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id);

            Assert.NotNull(savedUser);
            Assert.Equal("organizator1", savedUser.UserName);
            Assert.NotNull(savedRole);
            Assert.Equal(UserRoles.User, savedRole.Name);
            Assert.NotNull(savedUserRole);
        }
    }
}
