using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Data;

namespace TurniejePro.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Ustawienie klucza JWT i opcji w konfiguracji testowej (dla środowisk CI bez User Secrets)
        builder.UseSetting("Jwt:Key", "TestSecretKey_MustBeAtLeast32CharactersLong_ForJwtValidation!");
        builder.UseSetting("Jwt:Issuer", "TurniejeProTest");
        builder.UseSetting("Jwt:Audience", "TurniejeProTest");
        builder.UseSetting("Jwt:ExpiresMinutes", "60");

        builder.ConfigureServices(services =>
        {
            // Usunięcie domyślnej rejestracji DbContext
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<TurniejeProDbContext>));

            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Utworzenie i otwarcie stałego połączenia do bazy w pamięci RAM
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<TurniejeProDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
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
