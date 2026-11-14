namespace TurniejePro.Api.Errors;

/// <summary>
/// Jednolita obsługa błędów API: każda odpowiedź błędu to ProblemDetails (<c>application/problem+json</c>)
/// z polskim tytułem.
/// </summary>
public static class ErrorHandlingExtensions
{
    public static IServiceCollection AddApiErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context => ProblemTitles.ApplyPolishTitle(context.ProblemDetails));

        services.AddExceptionHandler<DomainExceptionHandler>();

        return services;
    }

    /// <summary>
    /// Wyjątki domenowe zamieniane na 400/404/409, pozostałe na 500 bez szczegółów, a puste odpowiedzi błędów
    /// (np. 401 i 403 z uwierzytelniania) dostają treść ProblemDetails.
    /// </summary>
    public static WebApplication UseApiErrorHandling(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler();
        app.UseStatusCodePages();

        return app;
    }
}
