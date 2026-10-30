using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TurniejePro.Data;
using TurniejePro.Tests.Controllers;

namespace TurniejePro.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:Key", "TestSecretKey_MustBeAtLeast32CharactersLong_ForJwtValidation!");
        builder.UseSetting("Jwt:Issuer", "TurniejeProTest");
        builder.UseSetting("Jwt:Audience", "TurniejeProTest");
        builder.UseSetting("Jwt:ExpiresMinutes", "60");

        builder.ConfigureServices(services =>
        {
            services.AddControllers()
                .AddApplicationPart(typeof(TestAuthorizationController).Assembly);

            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<TurniejeProDbContext>));

            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<TurniejeProDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();

            db.Database.EnsureCreated();

            if (!db.Tournaments.Any(t => t.Id == Guid.Empty))
            {
                // Bezpośrednie wstawienie SQL omija nadpisywanie Guid.Empty przez EF Core
                db.Database.ExecuteSqlRaw(@"
                    INSERT INTO Tournaments (Id, Name, Discipline, Format, StartsAt, MaxTeams, IsStarted)
                    VALUES ('00000000-0000-0000-0000-000000000000', 'Turniej Testowy', 'E-Sport', 0, '2026-01-01 00:00:00', 16, 0);
                ");
            }
        }

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection?.Dispose();
        }
    }
}
