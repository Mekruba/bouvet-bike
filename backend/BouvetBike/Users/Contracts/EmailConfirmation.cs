using System.ComponentModel.DataAnnotations;

namespace BouvetBike.Users.Contracts;

/// <param name="EmailConfirmationLink">
/// The link the confirmation email would contain (<c>/confirm-email?userId=…&amp;token=…</c>). Returned only
/// because the app has no email delivery (out of scope); with real email this field goes away and the link is
/// sent to the inbox, which is what proves the customer owns the address.
/// </param>
public sealed record RegistrationResponse(CurrentUserResponse User, string EmailConfirmationLink);

public sealed record ConfirmEmailRequest([Required] string Token);

/// <summary>Asks for a new confirmation link, e.g. when the first one was lost or expired.</summary>
public sealed record EmailConfirmationRequest([Required, EmailAddress] string Email);

/// <param name="EmailConfirmationLink">Stand-in for email delivery, as in <see cref="RegistrationResponse"/>. Null when there's no unconfirmed account with that email.</param>
public sealed record EmailConfirmationRequestResponse(string? EmailConfirmationLink);

public enum LoginFailure
{
    /// <summary>Wrong email or password, or locked out (deliberately not told apart).</summary>
    InvalidCredentials,

    /// <summary>The password was right, but the email hasn't been confirmed yet.</summary>
    EmailNotConfirmed,
}

public sealed record LoginResult(CurrentUserResponse? User, LoginFailure? Failure)
{
    public static LoginResult Success(CurrentUserResponse user) => new(user, null);

    public static LoginResult Failed(LoginFailure failure) => new(null, failure);
}
