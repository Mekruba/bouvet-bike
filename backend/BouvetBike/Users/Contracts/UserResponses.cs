namespace BouvetBike.Users.Contracts;

/// <summary>The logged-in user, for the frontend to decide what to show.</summary>
public sealed record CurrentUserResponse(Guid Id, string Name, string Email, string? Phone, IReadOnlyList<string> Roles)
{
    public bool IsStaff => Roles.Any(Users.Roles.Staff.Contains);
}

public sealed record StaffUserResponse(Guid Id, string Name, string Email, IReadOnlyList<string> Roles);
