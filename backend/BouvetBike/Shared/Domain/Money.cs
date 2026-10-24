namespace BouvetBike.Shared.Domain;

/// <summary>A non-negative amount in one currency, rounded to two decimals. Amounts in different currencies can't be combined.</summary>
public sealed record Money
{
    public const string DefaultCurrency = "NOK";

    public decimal Amount { get; private init; }
    public string Currency { get; private init; } = DefaultCurrency;

    private Money() { } // EF Core

    /// <summary>Creates an amount, rounded half away from zero to two decimals.</summary>
    /// <param name="currency">A three-letter ISO 4217 code, stored in upper case.</param>
    /// <exception cref="DomainException">The amount is negative, or the currency isn't three letters.</exception>
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

    /// <summary>Nothing, e.g. the starting point for a sum or a 100 % discount.</summary>
    public static Money Zero(string currency = DefaultCurrency) => new(0, currency);

    /// <summary>The sum of two amounts in the same currency.</summary>
    /// <exception cref="DomainException">The currencies differ.</exception>
    public static Money operator +(Money left, Money right) =>
        new(left.Amount + EnsureSameCurrency(left, right).Amount, left.Currency);

    /// <summary>The difference of two amounts in the same currency.</summary>
    /// <exception cref="DomainException">The currencies differ, or the result would be negative.</exception>
    public static Money operator -(Money left, Money right) =>
        new(left.Amount - EnsureSameCurrency(left, right).Amount, left.Currency);

    /// <summary>E.g. "790.00 NOK".</summary>
    public override string ToString() => $"{Amount:0.00} {Currency}";

    /// <summary>Returns <paramref name="right"/> when both amounts share a currency.</summary>
    private static Money EnsureSameCurrency(Money left, Money right) =>
        left.Currency == right.Currency
            ? right
            : throw new DomainException($"Cannot combine {left.Currency} and {right.Currency}.");
}
