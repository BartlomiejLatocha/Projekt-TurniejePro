using System.ComponentModel.DataAnnotations;

namespace TurniejePro.Contracts.Validation;

/// <summary>
/// Wymaga daty późniejszej niż chwila walidacji. Odrzuca też wartość domyślną (0001-01-01), która powstaje,
/// gdy klient pominie pole w JSON.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class FutureDateAttribute : ValidationAttribute
{
    public FutureDateAttribute()
        : base("Termin musi być w przyszłości.")
    {
    }

    public override bool IsValid(object? value) => value switch
    {
        // Brak wartości pilnuje [Required], a nie ten atrybut
        null => true,
        DateTimeOffset date => date > DateTimeOffset.UtcNow,
        _ => false
    };
}
