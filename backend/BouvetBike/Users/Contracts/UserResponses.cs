namespace BouvetBike.Users.Contracts;

/// <summary>The logged-in user, for the frontend to decide what to show.</summary>
public sealed record CurrentUserResponse(Guid Id, string Name, string Email, string? Phone, IReadOnlyList<string> Roles)
{
    /// <summary>Admin or mechanic: may use the staff area.</summary>
    public bool IsStaff => Roles.Any(Users.Roles.Staff.Contains);
}

/// <summary>An admin or mechanic in the staff list.</summary>
public sealed record StaffUserResponse(Guid Id, string Name, string Email, IReadOnlyList<string> Roles);
