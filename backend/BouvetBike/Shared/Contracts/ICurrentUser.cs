namespace BouvetBike.Shared.Contracts;

/// <summary>The user making the current request. Lets application code check identity without depending on HTTP.</summary>
public interface ICurrentUser
{
    /// <summary>Null for anonymous (guest) requests.</summary>
    Guid? Id { get; }

    bool IsAuthenticated => Id is not null;

    bool IsInRole(string role);
}
