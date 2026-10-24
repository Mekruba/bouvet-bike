namespace BouvetBike.Shared.Contracts;

/// <summary>The user making the current request. Lets application code check identity without depending on HTTP.</summary>
public interface ICurrentUser
{
    /// <summary>Null for anonymous (guest) requests.</summary>
    Guid? Id { get; }

    /// <summary>True when the request comes from a logged-in user.</summary>
    bool IsAuthenticated => Id is not null;

    /// <summary>Whether the user has the role (see <c>Roles</c>); false for anonymous requests.</summary>
    bool IsInRole(string role);
}
