using BouvetBike.Shared.Domain;

namespace BouvetBike.Tests.Shared;

public class ValueObjectTests
{
    [Fact]
    public void Email_IsTrimmedAndLowerCased() =>
        Assert.Equal("kari@example.com", Email.Create("  Kari@Example.COM ").Value);

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("kari@")]
    [InlineData("Kari <kari@example.com>")]
    public void Email_RejectsInvalid(string value) =>
        Assert.Throws<DomainException>(() => Email.Create(value));

    [Theory]
    [InlineData("912 34 567", "+4791234567")]
    [InlineData("91234567", "+4791234567")]
    [InlineData("+47 912 34 567", "+4791234567")]
    [InlineData("0047 91234567", "+4791234567")]
    [InlineData("+46 70-123 45 67", "+46701234567")]
    public void PhoneNumber_IsNormalizedToE164(string input, string expected) =>
        Assert.Equal(expected, PhoneNumber.Create(input).Value);

    [Theory]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData("912345678")]
    [InlineData("call me")]
    [InlineData("+47 912 34 567 ext 2")]
    public void PhoneNumber_RejectsInvalid(string value) =>
        Assert.Throws<DomainException>(() => PhoneNumber.Create(value));

    [Fact]
    public void Money_CannotBeNegative() =>
        Assert.Throws<DomainException>(() => new Money(-1));

    [Fact]
    public void Money_CannotMixCurrencies() =>
        Assert.Throws<DomainException>(() => new Money(1, "NOK") + new Money(1, "EUR"));
}
