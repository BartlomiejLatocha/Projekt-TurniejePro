using TurniejePro.Contracts.Tournaments;

namespace TurniejePro.Tests;

/// <summary>
/// Pilnuje, żeby żaden kontrakt <c>*Request</c> z <c>TurniejePro.Contracts</c> nie psuł przyjmowania żądań.
/// Atrybuty walidacji z prefiksem <c>property:</c> na rekordzie kończyły żądanie błędem 500 (wykryte w KAN-67).
/// </summary>
public class RequestContractsTests
{
    [Fact]
    public void RequestContracts_AreDiscovered()
    {
        var requestTypes = GetRequestTypes();

        Assert.Contains(typeof(CreateTournamentRequest), requestTypes);
        Assert.Contains(typeof(UpdateTournamentRequest), requestTypes);
    }

    [Fact]
    public void AllRequestContracts_AreAcceptedAsMvcModels()
    {
        var failures = GetRequestTypes()
            .Select(type => (type, error: MvcValidation.TryValidateEmptyInstance(type)))
            .Where(result => result.error is not null)
            .Select(result => $"{result.type.Name}: {result.error!.Message}")
            .ToList();

        Assert.Empty(failures);
    }

    private static List<Type> GetRequestTypes() =>
        typeof(CreateTournamentRequest).Assembly
            .GetExportedTypes()
            .Where(type => type.Name.EndsWith("Request", StringComparison.Ordinal))
            .ToList();
}
