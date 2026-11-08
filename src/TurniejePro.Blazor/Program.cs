using TurniejePro.Blazor.Components;
using TurniejePro.Blazor.Services;
using TurniejePro.Domain.Scheduling;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

void ConfigureApiBaseAddress(HttpClient client)
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("Brak konfiguracji Api:BaseUrl."));
}

builder.Services.AddHttpClient<AuthApiClient>(ConfigureApiBaseAddress);
builder.Services.AddHttpClient<TeamApiClient>(ConfigureApiBaseAddress);
builder.Services.AddHttpClient<TournamentApiClient>(ConfigureApiBaseAddress);
builder.Services.AddScoped<AuthSession>();
builder.Services.AddScoped<ScheduleService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
