using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace TurniejePro.Tests;

/// <summary>
/// Uruchamia walidację DataAnnotations tak, jak robi to MVC przy przyjęciu żądania, ale bez HTTP, bazy i autoryzacji.
/// Nie używamy <see cref="System.ComponentModel.DataAnnotations.Validator"/>: nie widzi on atrybutów postawionych
/// na parametrach konstruktora rekordu (a tak są zapisane wszystkie kontrakty <c>*Request</c>), więc każdy model
/// wyglądałby na poprawny.
/// </summary>
internal static class MvcValidation
{
    private static readonly ServiceProvider Provider = BuildProvider();

    /// <summary>Zwraca błędy walidacji: klucz to nazwa pola, wartość to komunikaty dla tego pola.</summary>
    public static IReadOnlyDictionary<string, string[]> Validate(object model)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext { RequestServices = Provider },
            new RouteData(),
            new ActionDescriptor());

        Provider.GetRequiredService<IObjectModelValidator>()
            .Validate(actionContext, validationState: null, prefix: string.Empty, model);

        return actionContext.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(entry => entry.Key, entry => entry.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
    }

    /// <summary>
    /// Waliduje pustą instancję typu (jej pola mogą być niepoprawne, to bez znaczenia). Zwraca wyjątek, jeśli MVC
    /// odrzuca sam typ, np. rekord z atrybutami walidacji na właściwościach zamiast na parametrach konstruktora
    /// (w API kończy się to błędem 500); w przeciwnym razie null.
    /// </summary>
    public static InvalidOperationException? TryValidateEmptyInstance(Type type)
    {
        try
        {
            Validate(RuntimeHelpers.GetUninitializedObject(type));

            return null;
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore().AddDataAnnotations();

        return services.BuildServiceProvider();
    }
}
