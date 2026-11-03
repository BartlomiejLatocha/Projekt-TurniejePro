using TurniejePro.Contracts.Tournaments;

namespace TurniejePro.Tests;

/// <summary>
/// Testy jednostkowe reguł walidacji z <see cref="CreateTournamentRequest"/> i <see cref="UpdateTournamentRequest"/>
/// (KAN-70). Reguły obu kontraktów są takie same, więc każdy przypadek jest sprawdzany dla obu.
/// Każdy test psuje dokładnie jedno pole poprawnego żądania i sprawdza, że błąd dotyczy tylko tego pola.
/// </summary>
public class TournamentRequestValidationTests
{
    public enum RequestKind
    {
        Create,
        Update
    }

    public static TheoryData<RequestKind, string?> EmptyOrWhitespaceText => ForBothKinds<string?>(null, string.Empty, "   ");

    public static TheoryData<RequestKind, int> InvalidNameLengths => ForBothKinds(2, 101, 500);

    public static TheoryData<RequestKind, int> ValidNameLengths => ForBothKinds(3, 4, 99, 100);

    public static TheoryData<RequestKind, int> InvalidDisciplineLengths => ForBothKinds(51, 500);

    public static TheoryData<RequestKind, int> ValidDisciplineLengths => ForBothKinds(1, 2, 50);

    public static TheoryData<RequestKind, int> InvalidMaxTeams => ForBothKinds(int.MinValue, -1, 0, 1, 129, int.MaxValue);

    public static TheoryData<RequestKind, int> ValidMaxTeams => ForBothKinds(2, 3, 127, 128);

    public static TheoryData<RequestKind, int> UndefinedFormats => ForBothKinds(0, 3, 99, -1, int.MaxValue);

    public static TheoryData<RequestKind, TournamentFormat> DefinedFormats =>
        ForBothKinds(TournamentFormat.League, TournamentFormat.Cup);

    /// <summary>Przesunięcie terminu względem chwili wywołania testu, w minutach.</summary>
    public static TheoryData<RequestKind, int> PastStartOffsetsInMinutes => ForBothKinds(0, -1, -60, -1440, -525600);

    public static TheoryData<RequestKind, int> FutureStartOffsetsInMinutes => ForBothKinds(2, 60, 1440, 525600);

    [Theory]
    [InlineData(RequestKind.Create)]
    [InlineData(RequestKind.Update)]
    public void ValidRequest_HasNoErrors(RequestKind kind)
    {
        var errors = MvcValidation.Validate(Build(kind));

        Assert.Empty(errors);
    }

    [Theory]
    [MemberData(nameof(EmptyOrWhitespaceText))]
    public void Name_EmptyOrWhitespace_ReportsNameError(RequestKind kind, string? name)
    {
        var errors = MvcValidation.Validate(Build(kind, name: name));

        AssertOnlyFieldHasError(errors, "Name");
    }

    [Theory]
    [MemberData(nameof(InvalidNameLengths))]
    public void Name_LengthOutsideRange_ReportsNameError(RequestKind kind, int length)
    {
        var errors = MvcValidation.Validate(Build(kind, name: new string('a', length)));

        AssertOnlyFieldHasError(errors, "Name");
    }

    [Theory]
    [MemberData(nameof(ValidNameLengths))]
    public void Name_LengthWithinRange_IsValid(RequestKind kind, int length)
    {
        var errors = MvcValidation.Validate(Build(kind, name: new string('a', length)));

        Assert.Empty(errors);
    }

    [Theory]
    [MemberData(nameof(EmptyOrWhitespaceText))]
    public void Discipline_EmptyOrWhitespace_ReportsDisciplineError(RequestKind kind, string? discipline)
    {
        var errors = MvcValidation.Validate(Build(kind, discipline: discipline));

        AssertOnlyFieldHasError(errors, "Discipline");
    }

    [Theory]
    [MemberData(nameof(InvalidDisciplineLengths))]
    public void Discipline_TooLong_ReportsDisciplineError(RequestKind kind, int length)
    {
        var errors = MvcValidation.Validate(Build(kind, discipline: new string('a', length)));

        AssertOnlyFieldHasError(errors, "Discipline");
    }

    [Theory]
    [MemberData(nameof(ValidDisciplineLengths))]
    public void Discipline_LengthWithinRange_IsValid(RequestKind kind, int length)
    {
        var errors = MvcValidation.Validate(Build(kind, discipline: new string('a', length)));

        Assert.Empty(errors);
    }

    [Theory]
    [MemberData(nameof(InvalidMaxTeams))]
    public void MaxTeams_OutsideRange_ReportsMaxTeamsError(RequestKind kind, int maxTeams)
    {
        var errors = MvcValidation.Validate(Build(kind, maxTeams: maxTeams));

        AssertOnlyFieldHasError(errors, "MaxTeams");
    }

    [Theory]
    [MemberData(nameof(ValidMaxTeams))]
    public void MaxTeams_WithinRange_IsValid(RequestKind kind, int maxTeams)
    {
        var errors = MvcValidation.Validate(Build(kind, maxTeams: maxTeams));

        Assert.Empty(errors);
    }

    [Theory]
    [MemberData(nameof(UndefinedFormats))]
    public void Format_ValueOutsideEnum_ReportsFormatError(RequestKind kind, int format)
    {
        var errors = MvcValidation.Validate(Build(kind, format: (TournamentFormat)format));

        AssertOnlyFieldHasError(errors, "Format");
    }

    [Theory]
    [MemberData(nameof(DefinedFormats))]
    public void Format_ValueFromEnum_IsValid(RequestKind kind, TournamentFormat format)
    {
        var errors = MvcValidation.Validate(Build(kind, format: format));

        Assert.Empty(errors);
    }

    [Theory]
    [MemberData(nameof(PastStartOffsetsInMinutes))]
    public void StartsAt_NotInFuture_ReportsStartsAtError(RequestKind kind, int minutesFromNow)
    {
        var errors = MvcValidation.Validate(Build(kind, startsAt: DateTimeOffset.UtcNow.AddMinutes(minutesFromNow)));

        AssertOnlyFieldHasError(errors, "StartsAt");
    }

    [Theory]
    [InlineData(RequestKind.Create)]
    [InlineData(RequestKind.Update)]
    public void StartsAt_DefaultValue_ReportsStartsAtError(RequestKind kind)
    {
        // Tak wygląda żądanie, w którym klient pominął pole startsAt
        var errors = MvcValidation.Validate(Build(kind, startsAt: default(DateTimeOffset)));

        AssertOnlyFieldHasError(errors, "StartsAt");
    }

    [Theory]
    [MemberData(nameof(FutureStartOffsetsInMinutes))]
    public void StartsAt_InFuture_IsValid(RequestKind kind, int minutesFromNow)
    {
        var errors = MvcValidation.Validate(Build(kind, startsAt: DateTimeOffset.UtcNow.AddMinutes(minutesFromNow)));

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(RequestKind.Create)]
    [InlineData(RequestKind.Update)]
    public void SeveralInvalidFields_ReportsEachFieldOnce(RequestKind kind)
    {
        var errors = MvcValidation.Validate(Build(
            kind,
            name: "AB",
            discipline: string.Empty,
            maxTeams: 1,
            format: (TournamentFormat)99,
            startsAt: default(DateTimeOffset)));

        Assert.Equal(["Discipline", "Format", "MaxTeams", "Name", "StartsAt"], errors.Keys.Order());
    }

    private static void AssertOnlyFieldHasError(IReadOnlyDictionary<string, string[]> errors, string field)
    {
        Assert.Equal([field], errors.Keys);
        Assert.NotEmpty(errors[field]);
    }

    private static object Build(
        RequestKind kind,
        string? name = "Liga Szkolna",
        string? discipline = "Piłka nożna",
        int maxTeams = 8,
        TournamentFormat format = TournamentFormat.League,
        DateTimeOffset? startsAt = null)
    {
        var start = startsAt ?? DateTimeOffset.UtcNow.AddDays(7);

        return kind switch
        {
            RequestKind.Create => new CreateTournamentRequest(name!, discipline!, format, start, maxTeams),
            RequestKind.Update => new UpdateTournamentRequest(name!, discipline!, format, start, maxTeams),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Nieznany rodzaj żądania.")
        };
    }

    private static TheoryData<RequestKind, T> ForBothKinds<T>(params T[] values)
    {
        var data = new TheoryData<RequestKind, T>();

        foreach (var kind in new[] { RequestKind.Create, RequestKind.Update })
        {
            foreach (var value in values)
            {
                data.Add(kind, value);
            }
        }

        return data;
    }
}
