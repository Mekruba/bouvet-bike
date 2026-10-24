namespace BouvetBike.Users.Contracts;

/// <summary>
/// Things that happen to customer accounts, for other modules to react to. Users calls every registered
/// implementation, so it never depends on the modules that listen (Orders already depends on Users).
/// </summary>
public interface ICustomerAccountEvents
{
    /// <summary>
    /// A customer confirmed their email, so the address is proven to be theirs. Called in the same request,
    /// before the response.
    /// </summary>
    Task CustomerEmailConfirmedAsync(Guid customerId, string email, CancellationToken ct);
}
