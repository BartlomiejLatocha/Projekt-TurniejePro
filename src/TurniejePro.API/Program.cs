using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TurniejePro.Api.Auth;
using TurniejePro.Data;
using TurniejePro.Data.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDataServices(builder.Configuration);
builder.Services.AddControllers();

builder.Services.AddJwtAuthentication(builder.Configuration);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info.Title = "TurniejePro API";
        document.Info.Version = "v1";
        document.Info.Description = "API do organizacji turniejów sportowych.";
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<TurniejeProDbContext>();
        dbContext.Database.Migrate();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        await RoleSeeder.SeedRolesAsync(roleManager);
    }

    app.MapOpenApi().AllowAnonymous();                                  // /openapi/v1.json
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "TurniejePro API v1"));  // /swagger
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// --- SEEDING KONTA TESTOWEGO ORGANIZATORA (DEV ONLY) ---
using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); // Dostosuj typ użytkownika jeśli inny
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

    const string organizerRole = Roles.Organizer;
    const string organizerEmail = "organizer@example.com";
    const string organizerPassword = "Password123!";

    // 1. Upewnij się, że rola istnieje
    if (!await roleManager.RoleExistsAsync(organizerRole))
    {
        await roleManager.CreateAsync(new IdentityRole<Guid>(organizerRole));
    }

    // 2. Utwórz użytkownika, jeśli nie istnieje
    var user = await userManager.FindByEmailAsync(organizerEmail);
    if (user is null)
    {
        user = new ApplicationUser
        {
            UserName = organizerEmail,
            Email = organizerEmail,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, organizerPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, organizerRole);
        }
    }
}

app.Run();

public partial class Program { }
