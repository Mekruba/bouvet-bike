using BouvetBike.Orders.Domain;

namespace BouvetBike.Tests.Orders;

public class OrderReferenceTests
{
    [Fact]
    public void RoundTrips_WithoutPrefix_AndInLowerCase()
    {
        var reference = OrderReference.New();

        var parsed = OrderReference.Parse(reference.Value[OrderReference.Prefix.Length..].ToLowerInvariant());

        Assert.Equal(reference, parsed);
    }

    [Theory]
    [InlineData("BB-0000000")]
    [InlineData("BB-ABCDEFGO")] // O is not in the alphabet
    [InlineData("BB-ABCDEFG")]
    [InlineData("")]
    public void RejectsInvalid(string value) =>
        Assert.False(OrderReference.TryParse(value, out _));
}
