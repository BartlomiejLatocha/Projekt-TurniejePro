using TurniejePro.Api.Auth;
using TurniejePro.Api.Errors;
using TurniejePro.Api.Startup;
using TurniejePro.Data;
using TurniejePro.Domain.Scheduling;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDataServices(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<MatchResultService>();

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddApiErrorHandling();

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

// Migracje i role w każdym środowisku; konto testowe organizatora tylko w Development (DevSeed w User Secrets)
await app.InitializeDatabaseAsync();

// Błędy jako ProblemDetails z polskimi tytułami; wyjątki domenowe → 400/404/409 (docs/api-contracts.md)
app.UseApiErrorHandling();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();                                  // /openapi/v1.json
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "TurniejePro API v1"));  // /swagger
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
