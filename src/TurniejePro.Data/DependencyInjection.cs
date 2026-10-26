using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TurniejePro.Data.Identity;
using TurniejePro.Data.Repositories;
using TurniejePro.Domain.Repositories;

namespace TurniejePro.Data;

/// <summary>
/// Metody rozszerzające do rejestracji usług warstwy danych i Identity w kontenerze DI.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddDataServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=turniejepro.db";

        services.AddDbContext<TurniejeProDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<TurniejeProDbContext>();

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ITournamentRepository, TournamentRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
