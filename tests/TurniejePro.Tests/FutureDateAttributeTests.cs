using TurniejePro.Contracts.Validation;

namespace TurniejePro.Tests;

public class FutureDateAttributeTests
{
    private readonly FutureDateAttribute _attribute = new();

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(525600)]
    public void IsValid_DateInFuture_ReturnsTrue(int minutesFromNow)
    {
        Assert.True(_attribute.IsValid(DateTimeOffset.UtcNow.AddMinutes(minutesFromNow)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-525600)]
    public void IsValid_DateNotInFuture_ReturnsFalse(int minutesFromNow)
    {
        Assert.False(_attribute.IsValid(DateTimeOffset.UtcNow.AddMinutes(minutesFromNow)));
    }

    [Fact]
    public void IsValid_DefaultDate_ReturnsFalse()
    {
        Assert.False(_attribute.IsValid(default(DateTimeOffset)));
    }

    [Fact]
    public void IsValid_Null_ReturnsTrue()
    {
        // Obecność wartości pilnuje [Required], a nie ten atrybut
        Assert.True(_attribute.IsValid(null));
    }

    [Fact]
    public void IsValid_UnsupportedType_ReturnsFalse()
    {
        Assert.False(_attribute.IsValid("2030-01-01"));
    }

    [Fact]
    public void FormatErrorMessage_ReturnsPolishMessage()
    {
        Assert.Contains("przyszłości", _attribute.FormatErrorMessage("StartsAt"), StringComparison.Ordinal);
    }
}
