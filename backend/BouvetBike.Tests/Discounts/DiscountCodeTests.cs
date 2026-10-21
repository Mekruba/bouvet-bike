using BouvetBike.Discounts.Domain;
using BouvetBike.Shared.Domain;
using static BouvetBike.Tests.TestData;

namespace BouvetBike.Tests.Discounts;

public class DiscountCodeTests
{
    private readonly TimeProvider _clock = Clock();

    [Fact]
    public void Code_IsNormalizedToUpperCase() =>
        Assert.Equal("SPRING-25", DiscountCode.Create(" spring-25 ", DiscountType.Percentage, 25).Code);

    [Theory]
    [InlineData(DiscountType.Percentage, 0)]
    [InlineData(DiscountType.Percentage, 101)]
    [InlineData(DiscountType.FixedAmount, 0)]
    public void RejectsInvalidValue(DiscountType type, decimal value) =>
        Assert.Throws<DomainException>(() => DiscountCode.Create("CODE", type, value));

    [Fact]
    public void Redeem_CountsUses_AndRejectsWhenUsedUp()
    {
        var code = DiscountCode.Create("ONCE", DiscountType.Percentage, 10, maxUses: 1);

        code.Redeem(_clock);

        Assert.Equal(1, code.TimesUsed);
        Assert.Throws<DomainException>(() => code.Redeem(_clock));
    }

    [Fact]
    public void Redeem_RejectsOutsideValidity()
    {
        var expired = DiscountCode.Create("OLD", DiscountType.Percentage, 10, validTo: Today.AddDays(-1));
        var future = DiscountCode.Create("SOON", DiscountType.Percentage, 10, validFrom: Today.AddDays(1));

        Assert.Throws<DomainException>(() => expired.Redeem(_clock));
        Assert.Throws<DomainException>(() => future.Redeem(_clock));
    }

    [Fact]
    public void Redeem_RejectsInactiveCode()
    {
        var code = DiscountCode.Create("OFF", DiscountType.Percentage, 10);
        code.Deactivate();

        Assert.Throws<DomainException>(() => code.Redeem(_clock));
    }
}
