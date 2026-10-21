using BouvetBike.Discounts.Domain;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;

namespace BouvetBike.Discounts.Contracts;

/// <summary>Discount codes: checks for other modules (Orders), and management for admins. Codes match case-insensitively.</summary>
public interface IDiscountService
{
    /// <summary>Checks the code is usable now, without using it up. For price quotes.</summary>
    /// <exception cref="Shared.Domain.DomainException">Unknown, inactive, expired or used-up code.</exception>
    Task<AppliedDiscount> PreviewAsync(string code, CancellationToken ct);

    /// <summary>
    /// Checks the code and counts one use. The change is tracked on the shared DbContext,
    /// so it's saved in the same transaction as the order that uses it.
    /// </summary>
    /// <exception cref="Shared.Domain.DomainException">Unknown, inactive, expired or used-up code.</exception>
    Task<AppliedDiscount> RedeemAsync(string code, CancellationToken ct);

    /// <summary>All codes, active or not, sorted by code.</summary>
    Task<IReadOnlyList<DiscountCodeResponse>> GetAllAsync(CancellationToken ct);

    /// <exception cref="Shared.Application.NotFoundException">Unknown code.</exception>
    Task<DiscountCodeResponse> GetByIdAsync(Guid id, CancellationToken ct);

    /// <exception cref="Shared.Application.ConflictException">The code exists (case-insensitive).</exception>
    /// <exception cref="Shared.Domain.DomainException">Invalid terms, e.g. a percentage over 100.</exception>
    Task<DiscountCodeResponse> CreateAsync(CreateDiscountCodeRequest request, CancellationToken ct);

    /// <summary>Applies an RFC 6902 JSON Patch atomically.</summary>
    /// <exception cref="Shared.Application.NotFoundException">Unknown code.</exception>
    /// <exception cref="Shared.Application.ConflictException">A <c>test</c> operation failed.</exception>
    /// <exception cref="Shared.Application.UnprocessableException">Unknown or read-only path, or invalid resulting terms.</exception>
    Task<DiscountCodeResponse> PatchAsync(Guid id, JsonPatchDocument<DiscountCodePatch> patch, CancellationToken ct);
}
