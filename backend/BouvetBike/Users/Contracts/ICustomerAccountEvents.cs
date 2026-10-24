namespace BouvetBike.Users.Contracts;

/// <summary>
/// Things that happen to customer accounts, for other modules to react to. Users calls every registered
/// implementation, so it never depends on the modules that listen (Orders already depends on Users).
/// </summary>
public interface ICustomerAccountEvents
{
    /// <summary>A customer account was created. Called before the response, in the same request.</summary>
    Task CustomerRegisteredAsync(Guid customerId, string email, CancellationToken ct);
}
