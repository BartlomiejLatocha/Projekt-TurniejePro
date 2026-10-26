using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Data.Repositories;
using TurniejePro.Domain.Repositories;

namespace TurniejePro.Data;

/// <summary>
/// Metody rozszerzające do rejestracji usług warstwy danych w kontenerze DI.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddDataServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=turniejepro.db";

        services.AddDbContext<TurniejeProDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ITournamentRepository, TournamentRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
