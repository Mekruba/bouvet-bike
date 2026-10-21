using BouvetBike.Shared.Domain;

namespace BouvetBike.Shared.Contracts;

public sealed record MoneyResponse(decimal Amount, string Currency)
{
    public static MoneyResponse From(Money money) => new(money.Amount, money.Currency);
}
