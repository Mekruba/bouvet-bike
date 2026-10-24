using BouvetBike.Shared.Domain;

namespace BouvetBike.Shared.Contracts;

/// <summary>An amount of money in API responses, e.g. <c>{ "amount": 790, "currency": "NOK" }</c>.</summary>
public sealed record MoneyResponse(decimal Amount, string Currency)
{
    /// <summary>Maps the domain value object to its response shape.</summary>
    public static MoneyResponse From(Money money) => new(money.Amount, money.Currency);
}
