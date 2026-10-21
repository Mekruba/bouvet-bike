using System.ComponentModel.DataAnnotations;

namespace BouvetBike.Orders.Contracts;

/// <summary>Guest lookup: the order reference plus the email or phone number on the order.</summary>
public sealed record LookupOrderRequest(
    [Required, StringLength(16)] string Reference,
    [Required, StringLength(ContactInputLimits.EmailOrPhone)] string EmailOrPhone);
