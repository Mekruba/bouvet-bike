namespace BouvetBike.Shared.Domain;

public sealed record Money
{
    public const string DefaultCurrency = "NOK";

    public decimal Amount { get; private init; }
    public string Currency { get; private init; } = DefaultCurrency;

    private Money() { } // EF Core

    public Money(decimal amount, string currency = DefaultCurrency)
    {
        if (amount < 0)
        {
            throw new DomainException("Amount cannot be negative.");
        }

        if (currency is not { Length: 3 })
        {
            throw new DomainException("Currency must be a three-letter ISO code.");
        }

        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency.ToUpperInvariant();
    }

    public static Money Zero(string currency = DefaultCurrency) => new(0, currency);

    public static Money operator +(Money left, Money right) =>
        new(left.Amount + EnsureSameCurrency(left, right).Amount, left.Currency);

    public static Money operator -(Money left, Money right) =>
        new(left.Amount - EnsureSameCurrency(left, right).Amount, left.Currency);

    public override string ToString() => $"{Amount:0.00} {Currency}";

    private static Money EnsureSameCurrency(Money left, Money right) =>
        left.Currency == right.Currency
            ? right
            : throw new DomainException($"Cannot combine {left.Currency} and {right.Currency}.");
}
